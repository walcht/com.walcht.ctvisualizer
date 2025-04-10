/*
    This shader implements the residency octree direct volume rendering
    technique from the paper: Residency Octree: A Hybrid Approach for Scalable
    Web-Based Multi-Volume Rendering. Support for 16bit data has been dropped
    due to the significant additional complexity and performance costs without
    any noticable quality improvements.

    Note: writing this was quite challenging, so any notes on how to improve
    this are really, really appreciated.

    Assumptions:
        - Brick cache texture format SHOULD BE R8_UNORM
        - Number of resolution levels SHOULD BE less than or equal to 12
        - Maximal residency octree depth SHOULD BE less than or equal to 15
*/

Shader "UnityCTVisualizer/DVR_outofcore"
{
    Properties
	{
	    [HideInInspector] _BrickCache("Brick Cache", 3D) = "" {}
        [HideInInspector] _TFColors("Transfer Function Colors Texture", 2D) = "" {}
        [HideInInspector] _PageTable("Top level multi-resolution page table", 3D) = "" {}
        _BrickRequestsRandomTex("Brick requests random (uniform) texture", 2D) = "" {}
		_AlphaCutoff("Opacity Cutoff", Range(0.0, 1.0)) = 0.95
        _MaxIterations("Maximum number of samples to take along longest path (cube diagonal) [type: int]", float) = 512
        _MaxOctreeDepth("Maximum depth of the Residency Octree [type: int]", Integer) = 5
        _OctreeStartDepth("Depth at which to start the Residency Octree traversal [type: int]", Integer) = 0
        _VolumeDims("Original volume dimensions [type: int]", Vector) = (1, 1, 1)
        _PageTableDims("Page table dimensions [type: int]", Vector) = (1, 1, 1)
        _BrickCacheDims("Brick cache dimensions [type: int]", Vector) = (1, 1, 1)
        _BrickCacheNbrBricks("Number of bricks along each dimension of the brick cache [type: int]", Vector) = (1, 1, 1)
        _BrickSize("Brick size [type: int]", Integer) = 128
        _ChunkSize("Chunk size (i.e., smallest accessible volume subregion in persistent memory", Integer) = 128
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
            #define MAX_ALLOWED_MAX_NBR_BRICK_REQUESTS_PER_RAY 12
            #define BRICK_CACHE_SLOT_USED 1
            #define UNMAPPED_PAGE_TABLE_ENTRY 0

            // Visualization Parameters
            sampler3D _BrickCache;
            // TODO: use one sampler for these 2D textures
            sampler2D _TFColors;
            sampler2D _BrickRequestsRandomTex;
            sampler3D _PageTable;
            float _AlphaCutoff;
            float _MaxIterations;

            int _MaxOctreeDepth;
            int _OctreeStartDepth;
            float3 _VolumeDims;
            float3 _PageTableDims;
            float3 _BrickCacheDims;
            int _BrickSize;
            int _ChunkSize;
            int _MaxNbrBrickRequestsPerRay;
            int _MaxNbrBrickRequests;
            float3 _BrickCacheNbrBricks;
            float3 nbr_chunks_per_res_lvl[16];  // (16 - 1) is the maximum allowed resolution level
            

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
            ///
            ///     Residency octree node. Stores transfer-function independent metadata and per-volume
            ///     cache residency information.
            ///
            ///
            ///                         ROOT                    <-------- depth 0
            ///                          |
            ///        +----+----+----+--+--+----+----+----+
            ///        |    |    |    |     |    |    |    |
            ///       A0    A1  ...   |    ...  A5   A6   A7    <-------- depth 1
            ///                       |
            ///     +----+----+----+--+--+----+----+----+
            ///     |    |    |    |     |    |    |    |
            ///    B0   B1   B2   B3    B4   B5   B6   B7       <-------- depth 2
            ///
            ///
            ///     Each node has exactly 8 children nodes. The octree data structure is implemented as a full,
            ///     pointerless (i.e., an array in the shader because shaders don't support pointers).
            ///     See: https://geidav.wordpress.com/2014/08/18/advanced-octrees-2-node-representations/
            ///     
            ///     Size of the array representation is:
            ///         sum(8^i) over i from 0 to MAX_OCTREE_DEPTH == (8^(MAX_OCTREE_DEPTH+1) - 1) / (8 - 1)
            ///
            ///     The smallest volume subdivision dimension is (unit: voxels) (with VOLUME_DIMENSION = (x, y, z)): 
            ///         min(VOLUME_DIMENSION * 2^(-MAX_OCTREE_DEPTH))
            ///
            ///     Reagardless of the color depth, resolution residency bitmask is 32 bits - therefore
            ///     maximum levels of resolution is 32.
            ///
            /// </summary>
            /// <example>
            ///
            ///     For a volume of the size 6000^3 (8-bit 216 GBs raw uncompressed size):
            ///
            ///         For MAX_OCTREE_DEPTH = 10 we get the following properties of the array representation:
            ///             - number of elements: 1 227 133 513 (really bad)
            ///             - array size (4 bytes struct): 4.57 GB (really bad)
            ///             - smallest subdivision: 5.859375 voxels (really bad)
            ///
            ///         For MAX_OCTREE_DEPTH = 8 we get:
            ///             - number of elements: 19 173 961 (really bad)
            ///             - array size (4 bytes struct): 73.15 MB (really bad)
            ///             - smallest subdivision: 23.4375 voxels (really bad)
            ///
            ///         For MAX_OCTREE_DEPTH = 7 we get:
            ///             - number of elements: 2 396 745 (bad)
            ///             - array size (4 bytes struct): 9.15 MB (bad)
            ///             - smallest subdivision: 46.875 voxels (good)
            ///
            ///         For MAX_OCTREE_DEPTH = 6 we get:
            ///             - number of elements: 299 593 (ok)
            ///             - array size (4 bytes struct): 1.15 MB (good)
            ///             - smallest subdivision: 93.75 voxels (good)
            /// 
            ///         For MAX_OCTREE_DEPTH = 5 we get:
            ///             - number of elements: 37 449 (really good)
            ///             - array size (4 bytes struct): 76.8 KB (perfect)
            ///             - smallest subdivision: 187.5 voxels (ok-ish)
            ///
            ///     Looking at these examples it seems impractical to set MAX_OCTREE_DEPTH to anything above 7
            ///     The reasonable candidates seem to be: 6 and 5
            ///
            ///     It is also important to make sure that the smallest subdivision is significantly larger than the
            ///     sampling distance (i.e., distance between two successive sampling points). That way the smallest
            ///     region that can be skipped saves us a significant number of sampling points.
            ///
            ///     
            ///     Below is an illustration of the first octree node (i.e., the node that covers the whole volume).
            ///     Its 8 children are ordered as follows: from origin along the X axis then downwards along the Y
            ///     axis then repeat the same for the next "plane" along the Z axis.
            ///     So: C111, C110, C100, C101, C011, C010, C000, C001
            ///
            ///                    ORIGIN
            ///                      ↓ 
            ///                c111 .X-------------------------------+ c110 ⟶ X
            ///                   .' |             .'|            .' |
            ///                 .'   |           .'  |          .'   |
            ///               .'-------------- + -------------.'     |
            ///             .'|      |       .'|     |      .' |     |
            ///           .'  |      |     .'  |     |    .'   |     |
            ///         .'    |      | - .' -- | --- + -.' ----|---- |
            ///   c011 +-------------------------------+ c010  |   .'|
            ///      ↙ |      | .'   |  |      |     | |       | .'  |
            ///     Z  |      + -----|--|----- + ------|------ +'    |
            ///        |   .' |      |  |      |     | |     .'|     |
            ///        | .'   |      |  |      |     | |   .'  |     |
            ///        |'     | c100 +--|------|-------|-.'----------+ c101
            ///        |------------ ↓- + -------------|'      |   .'
            ///        |      | .'   Y  |      | .'    |       | .'
            ///        |      .'--------|----- + ------|-------.'
            ///        |    .'          |    .'        |     .'
            ///        |  .'            |  .'          |   .'
            ///        |.'              | '            | .'
            ///   c000 |________________|______________|'c001
            ///   
            ///
            ///     data field can be interpreted as follows:
            ///         [         16 bits        |   8 bits  |  8 bits  ]
            ///         [    res lvl bitmask     |    max    |    min   ]
            /// 
            /// </example>
            struct ResidencyNode {
                float center_x;     // 4 bytes
                float center_y;     // 4 bytes
                float center_z;     // 4 bytes
                float side_halved;  // 4 bytes
                uint data;          // 4 bytes
            };
            uniform StructuredBuffer<ResidencyNode> residency_octree;

            /// <summary>
            ///     Uses viewing parameters to determine the desired resolution level for the provided sample point.
            /// </summary>
            int chooseDesiredResolutionLevel(float3 p) {
                return 0;
            }

            /// <summary>
            ///     TODO
            /// </summary>
            int chooseTraversalDepth(/* float step_size */) {
                return 2;
            }

            /// <summary>
            ///     Returns whether the node is homogenous by compairing its min and max densities 
            /// </summary>
            bool isHomogenous(int node_idx) {
                return ((residency_octree[node_idx].data >> 0) & 0xFF)
                    == ((residency_octree[node_idx].data >> 8) & 0xFF);
            }

            /// <summary>
            ///     Computes a delta distance along a given ray position and direction so that p + dir * delta is
            ///     the exiting intersection of the provided node.
            /// </summary>
            float skipNode(int node_idx, float3 p, float3 dir) {
                Box aabb;
                aabb.min = float3(residency_octree[node_idx].center_x - residency_octree[node_idx].side_halved,
                    residency_octree[node_idx].center_y - residency_octree[node_idx].side_halved,
                    residency_octree[node_idx].center_z - residency_octree[node_idx].side_halved);
                aabb.max = float3(residency_octree[node_idx].center_x + residency_octree[node_idx].side_halved,
                    residency_octree[node_idx].center_y + residency_octree[node_idx].side_halved,
                    residency_octree[node_idx].center_z + residency_octree[node_idx].side_halved);
                return slabs(p, dir, aabb);
            }

            bool isPartiallyMapped(int node_idx, int res_lvl) {
                return ((residency_octree[node_idx].data >> (16 + res_lvl)) & 1) == 1;
            }

            uint getBrickID(float3 p, int res_lvl) {
                int3 nbr_bricks = nbr_chunks_per_res_lvl[res_lvl] * _ChunkSize / _BrickSize;
                int3 d = (p * _VolumeDims) / (float)(_BrickSize << res_lvl);
                return (d.z * (nbr_bricks.x * nbr_bricks.y) + d.y * (nbr_bricks.x) + d.x)
                    | (res_lvl << 26);
            }

            /// <summary>
            ///     TODO: verify this is absolutely correct
            /// </summary>
            int getChildNodeIndex(int parent_idx, float3 p) {
                int3 offset = int3(frac(p / (residency_octree[parent_idx].side_halved * 2)) / 0.5f);
                return 8 * parent_idx + 1 + (4 * offset.z + 2 * offset.y + offset.x);
            }

            bool tryGetBrick(float3 p, int res_lvl, out float3 brick_pos, out float3 brick_offset) {
                // the virtualized volume dimensions are NOT necessarily the same
                // as the page table dims!
                float3 t = (p * _VolumeDims) / (_PageTableDims * (_BrickSize << res_lvl));  // TODO: _SubPageTableDims[res_lvl]
                brick_pos = tex3Dlod(_PageTable, float4(t, 0.0f));

                int3 d = (p * _VolumeDims) / (_BrickSize << res_lvl);
                brick_offset = p - d * ((_BrickSize << res_lvl) / _VolumeDims);
                // if R channel of the brick is 0 => brick is NOT in brick cache
                if (brick_pos.x != UNMAPPED_PAGE_TABLE_ENTRY) {
                    // update the brick cache usage
                    float3 b =  _BrickSize / _BrickCacheDims;
                    int brick_idx = round(_BrickCacheNbrBricks.x * _BrickCacheNbrBricks.y * brick_pos.z / b.z
                        + _BrickCacheNbrBricks.x * brick_pos.y / b.y
                        + brick_pos.x / b.x);
                    // set to any value other than 0 to indicate that this brick cache slot was
                    // used in this frame
                    brick_cache_usage[brick_idx] = BRICK_CACHE_SLOT_USED;
                    return true;
                }
                return false;
            }

            /// <summary>
            ///     Tries to get an alternative brick that is resident in cache. The residency node
            ///     is checked for the existance of bricks in lower resolution level than
            ///     what the original brick is requested in.
            /// </summary>
            // bool tryGetAlternativeBrick(int node_idx, float3 p, int res_lvl, out float3 brick_pos) {
            //     int i = 1;
            //     while (!((residency_octree[node_idx].data >> (16 + res_lvl + i)) & 1) && i <= 12) ++i;
            //     // TODO: access page table at res lvl
            //     brick_pos = tex3Dlod(_PageTable, float4(p, 0.0f));
            //     return !(brick_pos.r == -1)
            // }
            
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
                // distance of segment intersecting with AABB volume
                float seg_len = ray.t_out;  // because t_in == 0
                float step_size = BOUNDING_BOX_LONGEST_SEGMENT / _MaxIterations;
                float epsilon = step_size / 10.0f;
                float4 accm_color = float4(0.0f, 0.0f, 0.0f, 0.0f);

                for (float t = 0; t < ray.t_out; ) {

                    float3 accm_ray = ray.origin + ray.dir * t;
                    int res_lvl = chooseDesiredResolutionLevel(accm_ray);
                    int traversal_depth = chooseTraversalDepth(/* step_size */);

                    // empty space skipping through residency octree traversal
                    int node_idx = 0;  // root node

                    for (int i = 0; i <= traversal_depth; ++i) {

                        if (isHomogenous(node_idx)) {
                            // EPSILON is added to avoid infinite loop where exit point lies on one of the node's faces
                            t += skipNode(node_idx, accm_ray, ray.dir) + epsilon;
                            // the density has to be normalized in the same manner OpenGL/Vulkan normalizes an R8_UNORM
                            // texture. See: https://www.khronos.org/opengl/wiki/Normalized_Integer
                            float sampled_density = (residency_octree[node_idx].data & 0xFF) / 255.0f;
                            // blending
                            float4 src = tex2Dlod(_TFColors, float4(sampled_density, 0.0f, 0.0f, 0.0f));
                            src.rgb *= src.a;
                            accm_color += (1.0f - accm_color.a) * src;
                            break;
                        }

                        // if this node is not partially mapped => request the corresponding brick & skip the node
                        if (!isPartiallyMapped(node_idx, res_lvl)) {
                            t += skipNode(node_idx, accm_ray, ray.dir) + epsilon;
                            // try to report brick request so that the node becomes partially mapped
                            if (nbr_requested_bricks < _MaxNbrBrickRequestsPerRay) {
                                requests[nbr_requested_bricks] = getBrickID(accm_ray, res_lvl);
                                ++nbr_requested_bricks;
                            }
                            break;
                        }


                        if (i < traversal_depth) {
                            node_idx = getChildNodeIndex(node_idx, accm_ray);
                            continue;
                        }
                        
                        // at this point maximal traversal depth is reached => node_idx is a leaf node
                        float3 brick_pos;
                        float3 brick_offset;
                        if (tryGetBrick(accm_ray, res_lvl, brick_pos, brick_offset)) {
                            t += step_size;
                            float sampled_density = tex3Dlod(_BrickCache, float4(brick_pos + brick_offset, 0.0f)).r;
                            // blending
                            float4 src = tex2Dlod(_TFColors, float4(sampled_density, 0.0f, 0.0f, 0.0f));
                            src.rgb *= src.a;
                            accm_color += (1.0f - accm_color.a) * src;
                        }
                        else {
                            t += step_size;
                            // try to report brick request
                            if (nbr_requested_bricks < _MaxNbrBrickRequestsPerRay) {
                                requests[nbr_requested_bricks] = getBrickID(accm_ray, res_lvl);
                                ++nbr_requested_bricks;
                            }
                            // tryGetAlternativeBrick(node_idx, accm_ray, res_lvl, brick_pos);
                            // TODO: doesn't it make more sense to skip entire brick here?
                        }

                    }  // END octree traversal loop

                    // early-ray-termination optimization technique
                    if (accm_color.a > _AlphaCutoff) break;

                }  // END ray sampling loop

                if (nbr_requested_bricks > 0) {
                    // report all the saved brick requests along the ray - sampled random value belongs to [0.0, 1.0[
                    int r = (int)(tex2Dlod(_BrickRequestsRandomTex, float4(interpolated.uv, 0.0f, 0.0f)).r
                        * _MaxNbrBrickRequests / _MaxNbrBrickRequestsPerRay) * _MaxNbrBrickRequestsPerRay;
                    for (int k = 0; k < nbr_requested_bricks; ++k) {
                        brick_requests[r+k] = requests[k];
                    }
                }

                /*
                float4 h = tex3Dlod(_PageTable, float4(interpolated.uv, 0.0f, 0.0f));
                if (h.r <= 0) {
                    return fixed4(1.0f, 0.0f, 0.0f, 1.0f);
                }
                return fixed4(h.x / _PageTableDims.x, h.y / _PageTableDims.y, h.z / _PageTableDims.z, 1.0f);
                */

                return accm_color;
            }
			ENDCG
		}
	}
}

