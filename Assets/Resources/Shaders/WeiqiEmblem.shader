// Two glossy Go stones chasing each other in a yin-yang swirl (title emblem), or a single stone icon (_Mode 1 black, 2 white).
Shader "Weiqi/Emblem"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Mode ("Mode (0 emblem, 1 black stone, 2 white stone)", Float) = 0
        _Accent ("Accent", Color) = (0.95, 0.75, 0.35, 1)
        _Glow ("Glow", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        Cull Off Lighting Off ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "WeiqiCommon.cginc"

            float _Mode, _Glow;
            fixed4 _Accent;

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float2 rot(float2 p, float a) { float s = sin(a), c = cos(a); return float2(c * p.x - s * p.y, s * p.x + c * p.y); }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2.0;
                float aa = fwidth(p.x) * 1.5 + 1e-4;
                float R = 0.80;
                float rr = length(p);

                // outer glow
                // fall off to exactly zero before the quad edge (rr = 1) so no square outline shows
                float glow = exp(-max(rr - R, 0.0) * 9.0) * (1.0 - smoothstep(R, 0.99, rr)) * _Glow * (0.7 + 0.3 * sin(_Time.y * 1.7));
                float3 glowCol = _Accent.rgb * glow;

                float cover = 1.0 - smoothstep(R - aa, R + aa, rr);

                float3 stoneCol;
                if (_Mode > 0.5)
                {
                    float2 lp = p / R;
                    float3 n = float3(lp, sqrt(max(0.0, 1.0 - dot(lp, lp))));
                    n = normalize(float3(n.xy * 0.95, n.z + 0.12));
                    stoneCol = wq_shadeStone(n, _Mode > 1.5 ? 1.0 : 0.0, lp);
                }
                else
                {
                    float2 q = rot(p / R, _Time.y * 0.45);
                    float d1 = length(q - float2(0.0, 0.5));
                    float d2 = length(q + float2(0.0, 0.5));
                    float e = aa / R;
                    float side = smoothstep(-e, e, q.x);                 // 1 = right half (black)
                    float up = 1.0 - smoothstep(0.5 - e, 0.5 + e, d1);   // upper lobe black
                    float lo = 1.0 - smoothstep(0.5 - e, 0.5 + e, d2);   // lower lobe white
                    float black = side;
                    black = lerp(black, 1.0, up);
                    black = lerp(black, 0.0, lo);
                    float dotU = 1.0 - smoothstep(0.13 - e, 0.13 + e, d1);
                    float dotL = 1.0 - smoothstep(0.13 - e, 0.13 + e, d2);
                    black = lerp(black, 0.0, dotU);
                    black = lerp(black, 1.0, dotL);

                    float2 lp = p / R;
                    float3 n = float3(lp, sqrt(max(0.0, 1.0 - dot(lp, lp))));
                    n = normalize(float3(n.xy * 0.95, n.z + 0.12));
                    float3 b = wq_shadeStone(n, 0.0, lp);
                    float3 w = wq_shadeStone(n, 1.0, lp);
                    stoneCol = lerp(w, b, black);
                    // thin gold ring
                    float ring = 1.0 - smoothstep(0.0, 0.03, abs(rr / R - 0.985));
                    stoneCol = lerp(stoneCol, _Accent.rgb, ring * 0.9);
                }

                float3 col = lerp(glowCol, stoneCol, cover);
                float alpha = saturate(cover + glow) * i.color.a;
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
}
