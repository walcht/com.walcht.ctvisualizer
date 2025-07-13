Shader "UnityCTVisualizer/HistogramShader"
{
    Properties
    {
        _MainTex ("Densities frequency 1D-ish texture", 2D) = "white" {}
        _BinColor("Bins color", Color) = (1.0, 1.0, 1.0, 1.0)
        _BackgroundColor("Background color", Color) = (0.0, 0.0, 0.0, 1.0)
        _PlotColor("Alpha plot color", Color) = (0.0, 1.0, 0.0, 1.0)
        _LineWidth("Line width", float) = 0.05
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        LOD 100
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #define MAX_ALPHA_CONTROL_POINTS 16

            
            sampler2D _MainTex;
            float4 _MainTex_ST;

            float4 _BinColor;
            float4 _BackgroundColor;
            float4 _PlotColor;

            int _AlphaCount = 0;
            float _AlphaPositions[MAX_ALPHA_CONTROL_POINTS];
            float _AlphaValues[MAX_ALPHA_CONTROL_POINTS];

            float _LineWidth = 0.05;

            float4 _Scale;

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (float4 vertex : POSITION, float2 uv : TEXCOORD0)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(vertex);
                o.uv =  TRANSFORM_TEX(uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f frag) : SV_Target
            {
                // p is the current fragment position scaled so that line width remains constant
                float2 p = frag.uv * _Scale.xy;

                // determine closest distance (squared) to the segments defining the alpha curve
                float closest = 99999.0;

                [unroll(MAX_ALPHA_CONTROL_POINTS - 1)]
                for (int i = 0; i < _AlphaCount - 1; ++i)
                {
                    // the current segment s is defined by the parametric equation: s = v + t * (w - v)
                    float2 v = float2(_AlphaPositions[i], _AlphaValues[i]) * _Scale.xy;
                    float2 w = float2(_AlphaPositions[i + 1], _AlphaValues[i + 1]) * _Scale.xy;
                    float2 vw = w - v;

                    // project current fragment into this segment
                    float t = clamp(dot(p - v, vw) / dot(vw, vw), 0.0f, 1.0f);

                    // projection point on the segment
                    float2 proj = v + t * vw;

                    // closest distance squared is the length of the projection vector squared
                    closest = min(closest, dot(p - proj, p - proj));
                }

                // p is now unscaled
                p = frag.uv;

                // sample the frequency for current density
                float freq = tex2Dlod(_MainTex, float4(p.x, 0., 0., 0.)).r;

                float t0 = step(freq, p.y);
                float4 col = t0 * _BackgroundColor + (1 - t0) * _BinColor;

                float t1 = 1.0f - smoothstep(0.0f, _LineWidth, sqrt(closest));
                
                col = t1 * _PlotColor + (1.0f - t1) * col;

                return col;
            }
            ENDCG
        }
    }
}
