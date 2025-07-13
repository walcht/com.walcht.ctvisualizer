Shader "UnityCTVisualizer/axis_and_ticks_shader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+1" }
        LOD 100
        ZTest Less
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            v2f vert (float4 vertex : POSITION)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(vertex);
                return o;
            }

            fixed4 frag (v2f frag) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}
