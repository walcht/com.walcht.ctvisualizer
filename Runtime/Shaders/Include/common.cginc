#define BOUNDING_BOX_LONGEST_SEGMENT 1.732050808f  // diagonal of a cube
#pragma once

/// <summary>
///   Parametric description of a ray: r = origin + t * direction
/// </summary>
struct Ray {
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
struct Box {
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
float slabs(float3 origin, float3 dir, Box b) {
    float3 inverseDir = 1.0f / dir;
    float3 t0 = (b.min - origin) * inverseDir;
    float3 t1 = (b.max - origin) * inverseDir;
    float3 tmax = max(t0, t1);
    float t_out = min(min(tmax.x, tmax.y), tmax.z);
    return t_out;
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
    float t_frust_out = 
      -(ray_origin_viewspace.z + _ProjectionParams.y) / ray_dir_viewspace.z;
    ray.t_out = min(ray.t_out, t_frust_out);

    return ray;
}

Ray flipRay(Ray ray) {
    Ray flipped_ray;
    flipped_ray.origin = ray.origin + ray.t_out * ray.dir;
    flipped_ray.dir = -ray.dir;
    flipped_ray.t_out = ray.t_out;
    return flipped_ray;
} 