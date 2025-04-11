/*
    A software memory virtualization HLSL shader implementation
    based on the paper "Interactive Volume Exploration of Petascale
    Microscopy Data Streams Using a Visualization-Driven Virtual
    Memory Approach". The implementation makes use of a multi-level,
    multi-resolution page table hierarchy.
*/

Shader "UnityCTVisualizer/DVR_outofcore_page_table_only"
{
    Properties
	{
	    [HideInInspector] _BrickCache("Brick Cache", 3D) = "" {}
        [HideInInspector] _TFColors("Transfer Function Colors Texture", 2D) = "" {}
        [HideInInspector] _PageDir("Top level multi-resolution page directory", 3D) = "" {}
        _BrickRequestsRandomTex("Brick requests random (uniform) texture", 2D) = "" {}
		_AlphaCutoff("Opacity Cutoff", Range(0.0, 1.0)) = 0.95
        _MaxIterations("Maximum number of samples to take along longest path (cube diagonal) [type: int]", float) = 512
        _VolumeTexelSize("Size of one voxel (or texel) in the volumetric dataset", Vector) = (1, 1, 1)

        _BrickCacheDims("Brick cache dimensions [type: int]", Vector) = (1, 1, 1)
        _BrickCacheNbrBricks("Number of bricks along each dimension of the brick cache [type: int]", Vector) = (1, 1, 1)
        _BrickSize("Brick size [type: int]", Integer) = 128
        _MaxNbrBrickRequestsPerRay("Max number of brick requests per ray", Integer) = 4
        _MaxNbrBrickRequests("Max number of brick requests per frame", Integer) = 16

	}

	SubShader {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        LOD 500
        Cull Front
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
		    CGPROGRAM
            #pragma target 5.0
			#pragma vertex vert
			#pragma fragment frag

            #include "UnityCG.cginc"
            #include "Include/common.cginc"

            #define INVALID_BRICK_ID 0x80000000
            #define BRICK_CACHE_SLOT_USED 1

            #define MAX_ALLOWED_MAX_NBR_BRICK_REQUESTS_PER_RAY 12
            #define MAX_ALLOWED_NBR_RESOLUTION_LVLS 16

            #define MAPPED_PAGE_TABLE_ENTRY 2
            #define UNMAPPED_PAGE_TABLE_ENTRY 1
            #define EMPTY_PAGE_TABLE_ENTRY 0

            // Visualization Parameters
            sampler3D _BrickCache;
            // TODO: use one sampler for these 2D textures
            sampler2D _TFColors;
            sampler2D _BrickRequestsRandomTex;
            float _AlphaCutoff;
            float _MaxIterations;

            // this should have been an uint4 but Unity ... Just use a float4, you
            // don't want to deal with the crap that will happen if you use any other type ...
            Texture3D<float4> _PageDir;

            /// <summary>
            ///     dimensions (x, y, z) of the page directory in each resolution level.
            /// </summary>
            float4 _PageDirDims[MAX_ALLOWED_NBR_RESOLUTION_LVLS];

            uint4 _PageDirBase[MAX_ALLOWED_NBR_RESOLUTION_LVLS];

            float3 _BrickCacheDims;

            // the size of a voxel in the brick cache
            // set to: 1.0f / _BrickCacheDims
            float4 _BrickCacheVoxelSize;

            int _BrickSize = 128;
            int3 _BrickCacheNbrBricks;

            // index is the resolution level and (x, y, z) are the total number
            // of voxels of the volumetric dataset along each of its dimensions.
            uint4 _VolumeDims[MAX_ALLOWED_NBR_RESOLUTION_LVLS];
            float3 _VolumeTexelSize;

            // should be set to nbr_chunks_per_res_lvl[res_lvl] * _ChunkSize / _BrickSize
            uint4 nbr_bricks_per_res_lvl[MAX_ALLOWED_NBR_RESOLUTION_LVLS];
            
            int _MaxNbrBrickRequestsPerRay = 4;
            int _MaxNbrBrickRequests = 16;
            uniform RWStructuredBuffer<uint> brick_requests : register(u1);

            /// <summary>
            ///     Each entry holds a boolean whether the brick, identified by its index,
            ///     has been used in this frame. Negative value indicates that the brick
            ///     spot has not been used in this frame. Index is the brick ID within
            ///     the brick cache (follows the same order as residency octree node
            ///     children - see above)
            /// </summary>
            uniform RWStructuredBuffer<float> brick_cache_usage : register(u2);

            /// <summary>
            ///     Uses viewing parameters to determine the desired resolution level for the provided sample point.
            /// </summary>
            int chooseDesiredResolutionLevel(float3 p) {
                return 0;
            }

            /// <summary>
            ///     Computes a delta distance along a given ray position and direction so that p + dir * delta is
            ///     the exiting intersection of the provided node.
            /// </summary>
            float skip_page_directory_entry(float3 p, float3 dir, int res_lvl) {

                // a page directory entry is mapped to a spatial extent in the volumetric
                // dataset. This extent has to be skipped.
                Box aabb;
                float3 sides = (_BrickSize << res_lvl) * _VolumeTexelSize;
                aabb.min = float3((p * _PageDirDims[res_lvl]) * sides);
                aabb.max = aabb.min + sides;
                return slabs(p, dir, aabb);
            }

            uint getBrickID(float3 p, int res_lvl) {
                uint3 d = (p * _VolumeDims[res_lvl]) / _BrickSize;
                return (d.z * (nbr_bricks_per_res_lvl[res_lvl].x * nbr_bricks_per_res_lvl[res_lvl].y)
                    + d.y * (nbr_bricks_per_res_lvl[res_lvl].x) + d.x) | (res_lvl << 26);
            }

            int3 get_page_dir_offset(float3 p, int res_lvl) {
                return int3((p * _VolumeDims[0]) / (_BrickSize << res_lvl));
            }
            
            struct appdata {
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

            v2f vert(appdata v)
            {
                v2f output;

                // stereo-rendering related
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.clipVertex = UnityObjectToClipPos(v.modelVertex);
                output.uv = v.uv;
                output.modelVertex = v.modelVertex.xyz;
                return output;
            }

            fixed4 frag(v2f interpolated) : SV_Target
            {
                int nbr_requested_bricks = 0;
                uint requests[MAX_ALLOWED_MAX_NBR_BRICK_REQUESTS_PER_RAY];

                // initialize a ray in model space
                Ray ray = flipRay(getRayFromBackface(interpolated.modelVertex));

                float step_size = BOUNDING_BOX_LONGEST_SEGMENT / _MaxIterations;
                float epsilon = step_size / 100.0f;
                float4 accm_color = float4(0.0f, 0.0f, 0.0f, 0.0f);

                int prev_res_lvl = -1;
                int3 prev_page_dir_offset = int3(-1, -1, -1);
                float4 page_dir_entry;

                // start from epsilon to avoid out-of-volume rendering artifacts due to
                // floating point precision
                for (float t = epsilon; t < ray.t_out; ) {

                    float3 accm_ray = ray.origin + ray.dir * t;
                    int res_lvl = chooseDesiredResolutionLevel(accm_ray);

                    // sample current position
                    float sampled_density = 0.0f;
                    int3 page_dir_offset = get_page_dir_offset(accm_ray, res_lvl);

                    // exploit spatial coherency to avoid expensive texture lookups
                    if ((res_lvl != prev_res_lvl) || any(page_dir_offset != prev_page_dir_offset)) {

                        int4 page_dir_addrs = int4(_PageDirBase[res_lvl].xyz + page_dir_offset, 0);
                        page_dir_entry = _PageDir.Load(page_dir_addrs);
                        prev_page_dir_offset = page_dir_offset;
                        prev_res_lvl = res_lvl;

                    }

                    uint paging_flag = page_dir_entry.w;

                    if ((paging_flag != UNMAPPED_PAGE_TABLE_ENTRY) && (paging_flag != EMPTY_PAGE_TABLE_ENTRY)) {

                        float3 offset_within_brick = fmod(accm_ray * _VolumeDims[res_lvl], (float)_BrickSize)
                            * _BrickCacheVoxelSize.xyz;
                        float4 brick_pos = float4(page_dir_entry.xyz + offset_within_brick, 0);
                        sampled_density = tex3Dlod(_BrickCache, brick_pos).r;

                        // then report the brick cache usage for this frame
                        int brick_idx =
                            (_BrickCacheNbrBricks.x * _BrickCacheNbrBricks.y * round(page_dir_entry.z * _BrickCacheNbrBricks.z)) +
                            (_BrickCacheNbrBricks.x * round(page_dir_entry.y * _BrickCacheNbrBricks.y)) +
                            round(page_dir_entry.x * _BrickCacheNbrBricks.x);
                        // set to any value other than 0 to indicate that this brick cache slot was
                        // used in this frame
                        brick_cache_usage[brick_idx] = BRICK_CACHE_SLOT_USED;

                    }

                    if (paging_flag == UNMAPPED_PAGE_TABLE_ENTRY) {

                        // skip the unmapped and/or empty page directory entry
                        t += step_size; // skip_page_directory_entry(accm_ray, ray.dir, res_lvl) + epsilon;

                        // try to report brick request
                        if (nbr_requested_bricks < _MaxNbrBrickRequestsPerRay) {
                           requests[nbr_requested_bricks] = getBrickID(accm_ray, res_lvl);
                           ++nbr_requested_bricks;
                        }
                        
                        // continue because there is nothing to be sampled
                        continue;

                    }

                    // apply transfer function and composition
                    float4 src = tex2Dlod(_TFColors, float4(sampled_density, 0.0f, 0.0f, 0.0f));
                    src.rgb *= src.a;
                    accm_color += (1.0f - accm_color.a) * src;

                    // advance to nex sample
                    t += step_size;

                    // early-ray-termination optimization technique
                    if (accm_color.a > _AlphaCutoff) break;

                }  // END ray sampling loop

                if (nbr_requested_bricks > 0) {
                    // report all the saved brick requests along the ray - sampled random value belongs to [0.0, 1.0[
                    for (int k = 0; k < nbr_requested_bricks; ++k) {
                        brick_requests[k] = requests[k];
                    }
                }

                return accm_color;
            }
			ENDCG
		}
	}
}

