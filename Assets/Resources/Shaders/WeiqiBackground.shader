// Animated ink-and-gold backdrop: drifting ink clouds, a faint tilted Go grid and floating golden dust.
Shader "Weiqi/Background"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _ColorA ("Top colour", Color) = (0.07, 0.09, 0.16, 1)
        _ColorB ("Bottom colour", Color) = (0.02, 0.03, 0.06, 1)
        _Accent ("Accent", Color) = (0.95, 0.75, 0.35, 1)
        _Aspect ("Aspect", Float) = 1.777
        _Mood ("Mood (0 menu, 1 game)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Background" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "WeiqiCommon.cginc"

            fixed4 _ColorA, _ColorB, _Accent;
            float _Aspect, _Mood;

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

            float dust(float2 p, float scale, float speed, float seed)
            {
                float2 q = p * scale;
                q.y -= _Time.y * speed;
                float2 id = floor(q);
                float2 f = frac(q) - 0.5;
                float2 h = wq_hash22(id + seed);
                float2 pos = (h - 0.5) * 0.7;
                pos.x += sin(_Time.y * (0.4 + h.y) + h.x * 6.28) * 0.12;
                float d = length(f - pos);
                float size = 0.015 + h.x * 0.03;
                float tw = 0.5 + 0.5 * sin(_Time.y * (0.8 + h.y * 2.0) + h.x * 20.0);
                return smoothstep(size * 2.2, 0.0, d) * tw * step(0.55, h.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 p = (uv - 0.5) * float2(_Aspect, 1.0);

                float3 col = lerp(_ColorB.rgb, _ColorA.rgb, smoothstep(0.0, 1.0, uv.y));

                // ink clouds
                float t = _Time.y * 0.03;
                float2 w = float2(wq_fbm(p * 1.4 + t), wq_fbm(p * 1.4 - t + 5.2));
                float clouds = wq_fbm(p * 1.6 + w * 1.8 + float2(t, -t * 0.7));
                col += (clouds - 0.45) * lerp(float3(0.16, 0.18, 0.28), float3(0.10, 0.2, 0.18), _Mood);

                // tilted Go grid fading towards the edges
                float2 g = rot(p, 0.22 + sin(_Time.y * 0.05) * 0.03) * 2.6;
                float2 gf = abs(frac(g + 0.5) - 0.5);
                float2 gw = fwidth(g) * 1.2 + 1e-4;
                float2 gl = 1.0 - smoothstep(0.0, gw * 1.5 + 0.004, gf);
                float grid = max(gl.x, gl.y);
                float gridFade = smoothstep(1.1, 0.1, length(p));
                col += _Accent.rgb * grid * 0.045 * gridFade;

                // glowing lantern halo
                float halo = exp(-length(p - float2(0.0, 0.1)) * 2.2);
                col += _Accent.rgb * halo * lerp(0.10, 0.05, _Mood);

                // dust layers
                float d = dust(p, 6.0, 0.02, 1.0) + dust(p, 11.0, 0.035, 7.0) * 0.8 + dust(p, 18.0, 0.05, 13.0) * 0.6;
                col += _Accent.rgb * d * 0.8;

                // vignette
                col *= 1.0 - 0.75 * pow(saturate(length(p) * 0.85), 2.4);
                // film grain
                col += (wq_hash21(uv * 931.7 + frac(_Time.y)) - 0.5) * 0.012;

                return fixed4(saturate(col), i.color.a);
            }
            ENDCG
        }
    }
}
