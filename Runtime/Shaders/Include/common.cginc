#define BOUNDING_BOX_LONGEST_SEGMENT 1.732050808f  // diagonal of a cube
#pragma once

#include "UnityCG.cginc"

uniform sampler3D _BrickCache;
uniform sampler2D _TFColors;

uniform float _AlphaCutoff = 254.0f / 255.0f;
uniform float _SamplingQualityFactor = 1.0f;


struct appdata
{
    float4 modelVertex: POSITION;
    float2 uv: TEXCOORD0;
    // enable single-pass instanced rendering
    UNITY_VERTEX_INPUT_INSTANCE_ID
};


struct v2f
{
    float4 clipVertex : SV_POSITION;
    float2 uv: TEXCOORD0;
    float3 modelVertex : TEXCOORD1;
    // stereo-rendering related
    UNITY_VERTEX_OUTPUT_STEREO 
};


inline float3 viewToObjectDir(in float3 dir)
{
    return normalize(mul((float3x3)unity_WorldToObject, mul((float3x3)UNITY_MATRIX_I_V, dir)));
}


inline float3 viewToObjectNorm(in float3 norm)
{
    return normalize(mul(mul((float3x3)UNITY_MATRIX_I_V, norm), (float3x3)unity_ObjectToWorld));
}


inline float3 viewToObjectPos(in float3 pos)
{
    return mul(unity_WorldToObject, mul(UNITY_MATRIX_I_V, float4(pos, 1.0))).xyz;
}


/// <summary>
///   Parametric description of a ray: r = origin + t * direction
/// </summary>
struct Ray
{
    // in object space shifted by (0.5, 0.5, 0.5) (i.e., reference is at some
    // edge vertex of the normalized bounding cube)
    float3 origin;
    float3 dir;
    // t_in is 0
    float t_out;
};


/// <summary>
///   Axis-Aligned Bounding Box (AABB) box. An AABB only needs coordinate of
///   its two corner points to fully describe it.
/// </summary>
struct Box
{
    float3 min;
    float3 max;
};


/// <summary>
///   Branch-free and efficient Axis-Aligned Bounding Box (AABB) intersection
///   algorithm. For an in-depth explanation, visit:
///   https://tavianator.com/2022/ray_box_boundary.html
/// </summary>
///
/// <remark>
///     SLABS is can result in noise - consider adding a small epsilon
///     and correctly setting the texture wrap mode.
/// </remark>
float slabs(float3 origin, float3 dir, Box b)
{
    float3 inverseDir = 1.0f / dir;
    float3 t0 = (b.min - origin) * inverseDir;
    float3 t1 = (b.max - origin) * inverseDir;
    float3 tmax = max(t0, t1);
    float t_out = min(min(tmax.x, tmax.y), tmax.z);
    return t_out;
}


/// <summary>
///     Computes the ray-plane intersection by returning the parametric distance
///     along the ray up-to the intersection point. All supplied vectors should
///     be in the same coordinate system.
/// </summary>
///
/// <remark>
///     Since this is expected to be used in the context of ray marching, there
///     is no reason to add checks for null denominator (i.e., ray coincides with
///     the camera's near plane).
/// </remark>
inline float intersectNearPlane(float3 rayOrigin, float3 rayDir, float3 planePoint, float3 planeNormal)
{
    return dot(planePoint - rayOrigin, planeNormal) / dot(rayDir, planeNormal);
}


///
///     Process only backward-facing triangles
///
///         .+--------+
///       .' |        |
///      +   |  x <---|--- fragment position in object space is the start of the
///      |   |        |    view ray towards the camera
///      |  ,+--------+
///      |.'        .'
///      +--------+'
///
///
Ray getRayFromBackface(float3 modelVertex) {
    Ray ray;
    // convert from model space coordinate system to 3d texture space
    ray.origin = modelVertex + float3(0.5f, 0.5f, 0.5f);

    // get normalized ray direction in object space and in view space
    // (i.e., camera space)
    float3 ray_dir_viewspace;
    float3 ray_origin_viewspace = UnityObjectToViewPos(modelVertex);
    if (unity_OrthoParams.w == 0) {
        // perspective camera. Ray direction depends on interpolated model
        // vertex position for this fragment
        ray_dir_viewspace = -normalize(ray_origin_viewspace);
        ray.dir = normalize(ObjSpaceViewDir(float4(modelVertex, 0.0f)));
    } else {
        // orthogonal camera mode. Every ray has the same direction
        ray_dir_viewspace = float3(0.0f, 0.0f, 1.0f);
        // unity_CameraToWorld's forward axis is NOT -Z
        float3 cameraWorldDir = 
          mul((float3x3)unity_CameraToWorld, -ray_dir_viewspace);
        ray.dir = normalize(mul(unity_WorldToObject, cameraWorldDir));
    }

    // initialize the axis-aligned bounding box (AABB)
    Box aabb;
    aabb.min = float3(0.0f, 0.0f, 0.0f);
    aabb.max = float3(1.0f, 1.0f, 1.0f);
    // intersect ray with the AABB and get parametric start and end values
    ray.t_out = slabs(ray.origin, ray.dir, aabb);

    // t_out corresponds to the volume's AABB exit point but the exit point may
    // be reached earlier if the camera is inside the volume.
    // t_frust_out corresponds to the frustrum exit
    //
    // x = 1 or -1 (-1 if projection is flipped)
    // y = near plane
    // z = far plane
    // w = 1/far plane
    // uniform vec4 _ProjectionParams;
    float t_frust_out = intersectNearPlane(modelVertex, ray.dir, viewToObjectPos(float3(0, 0, -_ProjectionParams.y)), viewToObjectNorm(float3(0, 0, 1)));

    ray.t_out = min(ray.t_out, t_frust_out);

    return ray;
}


Ray flipRay(Ray ray)
{
    Ray flipped_ray;
    flipped_ray.origin = ray.origin + ray.t_out * ray.dir;
    flipped_ray.dir = -ray.dir;
    flipped_ray.t_out = ray.t_out;
    return flipped_ray;
} 