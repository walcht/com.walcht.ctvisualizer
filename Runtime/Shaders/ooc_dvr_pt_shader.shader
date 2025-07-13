/*
    A software memory virtualization HLSL shader implementation
    based on the paper "Interactive Volume Exploration of Petascale
    Microscopy Data Streams Using a Visualization-Driven Virtual
    Memory Approach". The implementation makes use of a single-level
    (versus multi-level), multi-resolution page table hierarchy capable
    of scaling up to large volumetric datasets.
*/

Shader "UnityCTVisualizer/ooc_dvr_pt_shader"
{
    Properties
	{
	    [HideInInspector] _BrickCache("Brick Cache", 3D) = "" {}
        [HideInInspector] _TFColors("Transfer Function Colors Texture", 2D) = "" {}
        [HideInInspector] _PageDir("Top level multi-resolution page directory", 3D) = "" {}
		_AlphaCutoff("Opacity Cutoff", Range(0.0, 1.0)) = 0.95
        
        _InitialStepSize("Opacity Cutoff", Float) = 0.95
        _BrickRequestsRandomTex("Brick requests random (uniform) texture", 2D) = "white" {}
        _MaxResLvl("Max allowed resolution level (inclusive)", Integer) = 0
        _VolumeTexelSize("Size of one voxel (or texel) in the volumetric dataset", Vector) = (1, 1, 1)
        _BrickCacheDims("Brick cache dimensions [type: int]", Vector) = (1, 1, 1)
        _BrickCacheNbrBricks("Number of bricks along each dimension of the brick cache [type: int]", Vector) = (1, 1, 1)
        _BrickSize("Brick size [type: int]", Integer) = 128
        _MaxNbrBrickRequestsPerRay("Max number of brick requests per ray", Integer) = 4
        _MaxNbrBrickRequests("Max number of brick requests per frame", Integer) = 16
	}

	SubShader {
        Tags { "Queue" = "Transparent-1" "RenderType" = "Transparent" }
        LOD 500
		Cull Front
        ZTest Less
        ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
		    CGPROGRAM
            #pragma target 5.0
            #pragma vertex vert
            #pragma fragment frag
//          #define EXPLOIT_SPATIAL_COHERENCY_FOR_PTS_ON
            #include "Include/ooc_common.cginc"


            fixed4 frag(v2f interpolated) : SV_Target
            {
                int nbr_requested_bricks = 0;
                uint requests[MAX_ALLOWED_MAX_NBR_BRICK_REQUESTS_PER_RAY];

                // initialize a ray in model space
                Ray ray = flipRay(getRayFromBackface(interpolated.modelVertex));

                float initial_epsilon = _InitialStepSize * 0.1f;
                float4 accm_color = float4(0.0f, 0.0f, 0.0f, 0.0f);
#ifdef EXPLOIT_SPATIAL_COHERENCY_FOR_PTS_ON
                int4 prev_page_dir_addrs = int4(-1, -1, -1, 0);
#endif
                float4 page_dir_entry;

#if VISUALIZE_RANDOM_BRICK_REQUESTS_TEX
                return fixed4(tex2Dlod(_BrickRequestsRandomTex, float4(interpolated.uv, 0, 0)).r, 0.0f, 0.0f, 1.0f);
#endif

                // start from epsilon to avoid out-of-volume rendering artifacts due to
                // floating point precision. Ending with -epsilon to avoid invalid
                // brick requests due to floating point precision errors.
                [loop]
                for (float t = initial_epsilon; t < (ray.t_out - initial_epsilon); )
                {
                    float3 accm_ray = ray.origin + ray.dir * t;
                    int res_lvl = chooseDesiredResolutionLevel(accm_ray);

                    // adaptive ray sampling technique
                    float step_size = adpatSamplingDistance(_InitialStepSize, res_lvl);
                    float epsilon = step_size * 0.1f;

                    // sample current position
                    float sampled_density = 0.0f;
                    int4 page_dir_addrs = getPageDirAddrs(accm_ray, res_lvl);

#ifdef EXPLOIT_SPATIAL_COHERENCY_FOR_PTS_ON
                    // exploit spatial coherency to avoid expensive texture lookups
                    if (any(page_dir_addrs != prev_page_dir_addrs))
                    {
                        page_dir_entry = _PageDir.Load(page_dir_addrs);
                        prev_page_dir_addrs = page_dir_addrs;
                    }
#else
                    page_dir_entry = _PageDir.Load(page_dir_addrs);
#endif
                    uint paging_flag = asuint(page_dir_entry.w) & 0x000000FF;

                    if (paging_flag == MAPPED_PAGE_TABLE_ENTRY)
                    {
                        float4 offset_within_brick = fmod(float4(accm_ray, 0) * _VolumeDims[res_lvl], (float)_BrickSize) * _BrickCacheVoxelSize;
                        float4 brick_pos = page_dir_entry + offset_within_brick;
                        sampled_density = tex3Dlod(_BrickCache, brick_pos).r;

                        // then report the brick cache usage for this frame
                        reportBrickCacheSlotUsage(page_dir_entry);

                        // advance to next sample
                        t += step_size;
                    }

                    else if (paging_flag == HOMOGENEOUS_PAGE_TABLE_ENTRY)
                    {
                        // TODO:    number of sample points along the page entry have to computed and accounted for
                        //          in the homogeneous sampling value's alpha channel (i.e., more opaque the more sample points there are).
                        sampled_density = page_dir_entry.x;

                        // skip the homogeneous page directory entry
                        float a = skipPageDirEntry(accm_ray, ray.dir, res_lvl, step_size, epsilon);
                        t += a;
                    }

                    else  // if(paging_flag == UNMAPPED_PAGE_TABLE_ENTRY)
                    {
                        // skip the unmapped page directory entry
                        float a = skipPageDirEntry(accm_ray, ray.dir, res_lvl, step_size, epsilon);
                        t += a;

                        if (nbr_requested_bricks < _MaxNbrBrickRequestsPerRay)
                        {
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

                    // early-ray-termination optimization technique
                    if (accm_color.a > _AlphaCutoff) break;

                }  // END ray sampling loop

                if (nbr_requested_bricks > 0)
                {
                    // report all the saved brick requests along the ray - sampled random value belongs to [0.0, 1.0[
                    int r = (int)(tex2Dlod(_BrickRequestsRandomTex, float4(interpolated.uv, 0.0f, 0.0f)).r
                        * _MaxNbrBrickRequests / _MaxNbrBrickRequestsPerRay) * _MaxNbrBrickRequestsPerRay;

                    // report all the saved brick requests along the ray - sampled random value belongs to [0.0, 1.0[
                    for (int k = 0; k < nbr_requested_bricks; ++k)
                    {
                        brick_requests[r + k] = requests[k];
                    }
                }

                return accm_color;
            }
			ENDCG
		}
	}
}

