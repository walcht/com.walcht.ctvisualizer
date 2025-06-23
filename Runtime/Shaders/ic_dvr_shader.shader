Shader "UnityCTVisualizer/ic_dvr_shader"
{
    Properties
	{
        // without HideInInspector, significant performance penalties occur when viewing
        // a material with this shader in Unity Editor
	    [HideInInspector] _BrickCache("Brick Cache", 3D) = "" {}
        [HideInInspector] _TFColors("Transfer Function Colors Texture", 2D) = "" {}

		_AlphaCutoff("Opacity Cutoff", Range(0.0, 1.0)) = 0.95
        _SamplingQualityFactor("Sampling quality factor (multiplier) [type: float]", Range(0.1, 3.0)) = 1.00
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

            float _AlphaCutoff = 254.0f / 255.0f;
            float _SamplingQualityFactor = 1.0f;
            float _MaxVolumeDim = 1;


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

                float step_size =  1.0f / (_MaxVolumeDim * _SamplingQualityFactor);
                float epsilon = step_size / 2.0f;
                float4 accm_color = float4(0.0f, 0.0f, 0.0f, 0.0f);

                // start from epsilon to avoid out-of-volume rendering artifacts due to
                // floating point precision
                [loop]
                for (float t = epsilon; t < ray.t_out; t += step_size)
                {
                    float3 accm_ray = ray.origin + ray.dir * t;

                    float sampled_density = tex3Dlod(_BrickCache, float4(accm_ray, 0.0f)).r;
                    float4 src = tex2Dlod(_TFColors, float4(sampled_density, 0.0f, 0.0f, 0.0f));

                    // blending
                    src.rgb *= src.a;
                    accm_color += (1.0f - accm_color.a) * src;

                    // early-ray-termination optimization technique
                    if (accm_color.a > _AlphaCutoff) break;
 
                }
                return accm_color;
            }
			ENDCG
		}
	}
}