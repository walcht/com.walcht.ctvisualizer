Shader "UnityCTVisualizer/ic_dvr_shader"
{
    Properties
	{
        // without HideInInspector, significant performance penalties occur when viewing
        // a material with this shader in the Unity Editor
	    [HideInInspector] _BrickCache("Brick Cache", 3D) = "" {}
        [HideInInspector] _TFColors("Transfer Function Colors Texture", 2D) = "" {}
		_AlphaCutoff("Opacity Cutoff", Range(0.0, 1.0)) = 0.95
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
            #include "Include/common.cginc"

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

                float initial_epsilon = _InitialStepSize * 0.1f;
                float4 accm_color = float4(0.0f, 0.0f, 0.0f, 0.0f);

                // start from epsilon to avoid out-of-volume rendering artifacts due to
                // floating point precision
                [loop]
                for (float t = initial_epsilon; t < ray.t_out; t += _InitialStepSize)
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