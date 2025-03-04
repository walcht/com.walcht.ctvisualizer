/*
    This shader implements the residency octree direct volume rendering
    technique from the paper: Residency Octree: A Hybrid Approach for Scalable
    Web-Based Multi-Volume Rendering. Support for 16bit data has been dropped
    due to the additional complexity that results in unnoticable quality
    improvements.

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
		_AlphaCutoff("Opacity Cutoff", Range(0.0, 1.0)) = 0.95
        _MaxIterations("Maximum number of samples to take along longest path (cube diagonal) [type: int]", float) = 512
        _MaxOctreeDepth("Maximum depth of the Residency Octree [type: int]") = 5
        _OctreeStartDepth("Depth at which to start the Residency Octree traversal [type: int]") = 0
        [HideInInspector] _BrickCacheTexSize("Brick Cache 3D texture dimensions", Vector) = (1, 1, 1)
	}

	SubShader {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        LOD 500
        Cull Front
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
		    CGPROGRAM
            #pragma multi_compile TEXTURE_RESHAPED_OFF TEXTURE_RESHAPED_ON
			#pragma vertex vert
			#pragma fragment frag

            #include "UnityCG.cginc"
            #include "Include/common.cginc"

            // Visualization Parameters
            sampler3D _BrickCache;
            sampler2D _TFColors;
            float _AlphaCutoff;
            float _MaxIterations;
            float _MaxOctreeDepth;
            float _OctreeStartDepth;
            float3 _BrickCacheTexSize;
            
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
            ///     Looking at these examples it seems impractical to set MAX_OCTREE_DEPTH to anythin above 7
            ///     The reasonable candidates seem to be: 6 and 5
            ///
            ///     It is also important to make sure that the smallest subdivision is significantly larger than the
            ///     sampling distance (i.e., distance between two successive sampling points). That way the smallest
            ///     region that can be skipped saves us a significant number of sampling points.
            ///
            ///     
            ///     Below is an illustration of the first octree node (i.e., the node that covers the whole volume).
            ///     Its 8 children are ordered as follows: from origin along the X axis then downwards along the Y
            ///     axis then repeate the same for the next "plane" along the Z axis.
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
                float center;       // 4 bytes
                float side_halfed;  // 4 bytes
                uint data;          // 4 bytes
            };
            uint ResidencyOctreeArraySize;
            StructuredBuffer<ResidencyNode> residency_octree: register(u1);


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
                return _MaxOctreeDepth;
            }

            /// <summary>
            ///     Returns whether the node is homogenous by compairing its min and max densities 
            /// </summary>
            bool isHomogenous(ResidencyNode node) {
                return (node.data & 0x000000FF) == ((node.data >> 8) & 0x000000FF);
            }

            /// <summary>
            ///     Computes a delta distance along a given ray position and direction so that p + dir * delta is
            ///     outside of node.
            /// </summary>
            // 0 => whole volume
            // 
            float skipNode(ResidencyNode node, float3 p, float3 dir) {
                Box aabb;
                // uint d = (node.data >> 16) & 0x0000000F;
                // float l = 1.0f / (1 << d);
                aabb.min = float3(node.center - node.side_halfed);
                aabb.max = float3(node.center + node.side_halfed);
                return slabs(p, dir, aabb);
            }

            bool isPartiallyMapped(ResidencyNode node, int res_lvl) {
                return ((node.data >> 16) & (1 << res_lvl)) == 1
            }

            void reportBrickRequest(ResidencyNode node, float3 accm_ray, int res_lvl) {
                
            }

            /// <summary>
            ///     TODO
            /// </summary>
            ResidencyNode getChildNode(ResidencyNode node, float3 p) {
                return residency_octree[0];
            }

            
            /// <summary>
            ///     Samples density from the brick cache 3D texture
            ///     slices corresponding to the bottom left block of the original volume:
            ///
            /// </summary>
            /// <remark>
            ///     Original volume's slices should be arranged along the Y-axis.
            /// </remark>
            float4 sampleDensity(float3 volumeCoord, float3 texArraySize)
            {
                float brickID = floor(volumeCoord.x * _NbrBlocksX) floor(volumeCoord.y * _NbrBlocksY);
                // is brick in cache?
                float s_00 = UNITY_SAMPLE_TEX2DARRAY_LOD(_BrickDensities, float3(, , arrayTexIndex), 0);
                float s_01 = UNITY_SAMPLE_TEX2DARRAY_LOD(_BrickDensities, float3(, , arrayTexIndex), 0);
                return lerp(s_00, s_01, t);
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
                // initialize a ray in model space
                Ray ray = flipRay(getRayFromBackface(interpolated.modelVertex));
                // distance of segment intersecting with AABB volume
                float seg_len = ray.t_out;  // because t_in == 0
                float step_size = BOUNDING_BOX_LONGEST_SEGMENT / _MaxIterations;
                float epsilon = step_size / 100.0f;
                int num_iterations = (int)clamp(seg_len / step_size, 1, (int)_MaxIterations);
                float4 accm_color = float4(0.0f, 0.0f, 0.0f, 0.0f);
                for (float t = 0; iter < ray.t_out; )
                {
                    float3 accm_ray = ray.origin + ray.dir * t;
                    int res_lvl = chooseDesiredResolutionLevel(accm_ray);
                    int traversal_depth = chooseTraversalDepth(/* step_size */);

                    // empty space skipping through residency octree traversal
                    ResidencyNode node = resolutionsresidency[0];  // root node
                    float sampled_density;
                    for (int i = 0; i <= traversal_depth; ++i) {
                        if (isHomogenous(node)) {
                            // EPSILON is added to avoid infinite loop where exit point lies on one of the node's faces
                            t += skipNode(node, accm_ray, ray.dir) + epsilon;
                            // the density has to be normalized in the same manner OpenGL/Vulkan normalizes an R8_UNORM
                            // texture. See: https://www.khronos.org/opengl/wiki/Normalized_Integer
                            sampled_density = (node.data & 0x000000FF) / 255.0f;
                            break;
                        }
                        // if this node is not partially mapped => skip it
                        if (!isPartiallyMapped(node, res_lvl)) {
                            reportBrickRequest(node, accm_ray, res_lvl);
                            t += skipNode(node, accm_ray, ray.dir) + epsilon;
                            break;
                        }
                        if (i < traversal_depth) {
                            node = getChildNode(node, accm_ray);
                            continue;
                        }
                        ideal_brick = getBrick(node, accm_ray, res_lvl);
                        if (brick.missing) {
                            reportBrickRequest();
                            getAlternativeBrick(node, );
                        }
                    }

                    float sampled_density = sampleDensity();

                    // blending
                    float4 src = tex2Dlod(_TFColors, float4(sampled_density, 0.0f, 0.0f, 0.0f));
                    src.rgb *= src.a;
                    accm_color += (1.0f - accm_color.a) * src;

                    // early-ray-termination optimization technique
                    if (accm_color.a > _AlphaCutoff) break;

                    // move to next sample point
                    t += step_size;
                }
                return accm_color;
            }
			ENDCG
		}
	}
}

