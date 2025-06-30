#pragma once
#include "common.cginc"

#define INVALID_BRICK_ID                                0x80000000
#define MAPPED_PAGE_TABLE_ENTRY                         2
#define UNMAPPED_PAGE_TABLE_ENTRY                       1
#define HOMOGENEOUS_PAGE_TABLE_ENTRY                    0

#define MAX_ALLOWED_MAX_NBR_BRICK_REQUESTS_PER_RAY      4
#define MAX_ALLOWED_NBR_RESOLUTION_LVLS                 6

#define VISUALIZE_RANDOM_BRICK_REQUESTS_TEX             0

// this should have been an uint4 but Unity ... Just use a float4, you
// don't want to deal with the crap that will happen if you use any other type ...
uniform Texture3D<float4> _PageDir;

/// <summary>
///     dimensions (x, y, z) of the page directory in each resolution level.
/// </summary>
uniform float4 _PageDirDims[MAX_ALLOWED_NBR_RESOLUTION_LVLS];

// 4th component (i.e., w channel) has to be set to 0
uniform uint4 _PageDirBase[MAX_ALLOWED_NBR_RESOLUTION_LVLS];

uniform float3 _BrickCacheDims;

// the size of a voxel in the brick cache
// set to: 1.0f / _BrickCacheDims
// 4th component (i.e., w channel) has to be set to 0
uniform float4 _BrickCacheVoxelSize;

uniform int _BrickSize = 128;
uniform int3 _BrickCacheNbrBricks;

uniform sampler2D _BrickRequestsRandomTex;
uniform float4 _BrickRequestsRandomTex_ST;

uniform float _LODDistancesSquared[MAX_ALLOWED_NBR_RESOLUTION_LVLS];

uniform int _MaxResLvl = 3;

// index is the resolution level and (x, y, z) are the total number
// of voxels of the volumetric dataset along each of its dimensions.
// 4th component (i.e., w channel) has to be set to 0
uniform uint4 _VolumeDims[MAX_ALLOWED_NBR_RESOLUTION_LVLS];
uniform float3 _VolumeTexelSize;

// should be set to nbr_chunks_per_res_lvl[res_lvl] * _ChunkSize / _BrickSize
uniform uint4 nbr_bricks_per_res_lvl[MAX_ALLOWED_NBR_RESOLUTION_LVLS];
            
uniform int _MaxNbrBrickRequestsPerRay = 4;
uniform int _MaxNbrBrickRequests = 16;

uniform RWStructuredBuffer<uint> brick_requests : register(u1);

/// <summary>
///     Each entry holds a bitmask for 32 bricks. If a brick's associated bit is set,
///     then it has been used for this frame.
/// </summary>
uniform RWStructuredBuffer<uint> brick_cache_usage : register(u2);


v2f vert(appdata v)
{
    v2f output;

    // stereo-rendering related
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_OUTPUT(v2f, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    output.clipVertex = UnityObjectToClipPos(v.modelVertex);
    output.uv = TRANSFORM_TEX(v.uv, _BrickRequestsRandomTex);
    output.modelVertex = v.modelVertex.xyz;
    return output;
}


/// <summary>
///     Uses viewing parameters to determine the desired resolution level for the provided sample point.
///     This simply uses the distance to the camera to determine the desired resolution level for the provided sample point.
/// </summary>
int chooseDesiredResolutionLevel(float3 p)
{
    // distance-based approach - convert back to model space then view space
    float3 _p = UnityObjectToViewPos(p - float3(0.5f, 0.5f, 0.5f));
    float d_squared = dot(_p, _p);
    [unroll(MAX_ALLOWED_NBR_RESOLUTION_LVLS)]
    for (int i = 0; i < MAX_ALLOWED_NBR_RESOLUTION_LVLS; ++i)
    {
       if (d_squared < _LODDistancesSquared[i]) return i;
    }
    return _MaxResLvl;
}


/// <summary>
///     Computes a delta distance along a given ray position and direction so that p + dir * delta is
///     the exiting intersection of the provided node.
/// </summary>
float skipPageDirEntry(float3 p, float3 dir, int res_lvl, float min_skippable_distance, float epsilon)
{
    // a page directory entry is mapped to a spatial extent in the volumetric
    // dataset. This extent has to be skipped.
    Box aabb;
    float3 sides = (_BrickSize << res_lvl) * _VolumeTexelSize;
    aabb.min = float3(int3(p / sides) * sides);
    aabb.max = aabb.min + sides;
    return max(slabs(p, dir, aabb) + epsilon, min_skippable_distance);
}


int4 getPageDirAddrs(float3 p, int res_lvl)
{
    return _PageDirBase[res_lvl] + (float4(p, 0) * _VolumeDims[0]) / (_BrickSize << res_lvl);
}


uint getBrickID(float3 p, int res_lvl)
{
    uint3 d = (p * _VolumeDims[res_lvl]) / _BrickSize;
    return (d.z * (nbr_bricks_per_res_lvl[res_lvl].x * nbr_bricks_per_res_lvl[res_lvl].y)
        + d.y * (nbr_bricks_per_res_lvl[res_lvl].x) + d.x) | (res_lvl << 26);
}


/// <summary>
///     Adapts the sampling rate according to the provided resolution level. The sampling
///     rate is simply halved for each increase of the resolution level
///     (i.e., new_sampling_rate = initial_sampling_rate * 2^res_lvl).
/// </summary>
float adpatSamplingDistance(float initial_step_size, int res_lvl)
{
    return initial_step_size * (1 << res_lvl);
}


void reportBrickCacheSlotUsage(float4 page_dir_entry)
{
    uint brick_idx = _BrickCacheNbrBricks.x * _BrickCacheNbrBricks.y * round(page_dir_entry.z * _BrickCacheNbrBricks.z)
        + _BrickCacheNbrBricks.x * round(page_dir_entry.y * _BrickCacheNbrBricks.y)
        + round(page_dir_entry.x * _BrickCacheNbrBricks.x);
    // indicate that this brick cache slot was used in this frame
    brick_cache_usage[brick_idx / 32u] |= (1 << (brick_idx % 32u));
}
