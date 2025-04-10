Shader "UnityCTVisualizer/dvr_incore_baseline"
{
    Properties
	{
        // without HideInInspector, significant performance penalties occur when viewing
        // a material with this shader in Unity Editor
	    [HideInInspector] _BrickCache("Brick Cache", 3D) = "" {}
        [HideInInspector] _TFColors("Transfer Function Colors Texture", 2D) = "" {}
		_AlphaCutoff("Opacity Cutoff", Range(0.0, 1.0)) = 0.95
        // should be an int, but Unity crashes when calling Material.SetInteger()
        // see: https://discussions.unity.com/t/crash-when-calling-material-setinteger-with-int-shaderlab-properties/891920
        _MaxIterations("Maximum number of samples to take along longest path (cube diagonal)", float) = 512
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

            sampler3D _BrickCache;
            sampler2D _TFColors;
            float _AlphaCutoff;
            float _MaxIterations;

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
                int num_iterations = (int)clamp(seg_len / step_size, 1, (int)_MaxIterations);
                float3 delta_step = ray.dir * step_size;
                float4 accm_color = float4(0.0f, 0.0f, 0.0f, 0.0f);
                float3 accm_ray = ray.origin;
                // TODO:    improve sampling loop (maybe use unroll with outside check?)
                for (int iter = 0; iter < num_iterations; ++iter)
                {
                    float sampled_density = tex3Dlod(_BrickCache, float4(accm_ray, 0.0f)).r;
                    float4 src = tex2Dlod(_TFColors, float4(sampled_density, 0.0f, 0.0f, 0.0f));
                    // move to next sample point
                    accm_ray += delta_step;
                    // blending
                    src.rgb *= src.a;
                    accm_color = (1.0f - accm_color.a) * src + accm_color;
                    // early-ray-termination optimization technique
                    if (accm_color.a > _AlphaCutoff) break;
                }
                return accm_color;
            }
			ENDCG
		}
	}
}