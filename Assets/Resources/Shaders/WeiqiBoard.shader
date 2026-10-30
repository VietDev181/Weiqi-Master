// Draws the whole Go board in one pass: lacquered kaya wood, grid, star points, glossy stones
// with drop shadows, hover ghost, last-move ring, territory marks and dead-stone crosses.
// Board state is read from a small point-filtered texture (one texel per intersection):
//   R = stone  (0 empty, 100 black, 200 white)
//   G = marker (0 none, 50 last move, 100 black territory, 150 white territory, 200 dead)
//   B = placement animation 0..255, A = stone opacity (fades captured stones out)
Shader "Weiqi/Board"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _State ("State", 2D) = "black" {}
        _N ("Size", Float) = 9
        _Hover ("Hover (x, y, colour, 1 valid / 0.5 invalid)", Vector) = (-1, -1, 0, 0)
        _Reveal ("Reveal", Range(0, 1)) = 1
        _Pulse ("Pulse", Range(0, 1)) = 0
        _Wood1 ("Wood light", Color) = (0.90, 0.71, 0.41, 1)
        _Wood2 ("Wood dark", Color) = (0.72, 0.50, 0.24, 1)
        _Zoom ("Zoom", Float) = 1.12
        _Pad ("Padding (cells)", Float) = 0.9
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        Cull Off
        Lighting Off
        ZWrite Off
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

            sampler2D _State;
            float _N, _Reveal, _Pulse, _Zoom, _Pad;
            float4 _Hover;
            fixed4 _Wood1, _Wood2;

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

            float4 cellState(float2 c)
            {
                float2 t = (c + 0.5) / _N;
                return tex2Dlod(_State, float4(t, 0, 0)) * 255.0;
            }

            float eq(float a, float b) { return step(abs(a - b), 0.1); }

            float isStar(float2 r)
            {
                float n = _N;
                if (n < 10.0)
                    return max(eq(r.x, 4) * eq(r.y, 4), max(eq(r.x, 2), eq(r.x, 6)) * max(eq(r.y, 2), eq(r.y, 6)));
                if (n < 16.0)
                    return max(eq(r.x, 6) * eq(r.y, 6), max(eq(r.x, 3), eq(r.x, 9)) * max(eq(r.y, 3), eq(r.y, 9)));
                return max(max(eq(r.x, 3), eq(r.x, 9)), eq(r.x, 15)) * max(max(eq(r.y, 3), eq(r.y, 9)), eq(r.y, 15));
            }

            // Returns rgb + coverage of a stone seen at local coordinates lp (-1..1), kind 0 black / 1 white.
            float4 stoneLayer(float2 lp, float kind, float aa)
            {
                float rr = length(lp);
                float cover = saturate((1.0 - rr) / aa);
                float3 n = float3(lp, sqrt(max(0.0, 1.0 - rr * rr)));
                n = normalize(float3(n.xy * 0.95, n.z + 0.12));
                return float4(wq_shadeStone(n, kind, lp), cover);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float reveal = smoothstep(0.0, 1.0, _Reveal);
                float2 bu = (uv - 0.5) * _Zoom;                 // centred board space, board spans -0.5..0.5
                bu /= lerp(0.88, 1.0, reveal);
                bu += 0.5;

                // ---- board silhouette + soft drop shadow
                float sd = wq_sdRoundBox(bu - 0.5, float2(0.5, 0.5), 0.022);
                float aaS = fwidth(sd) * 1.2 + 1e-5;
                float mask = 1.0 - smoothstep(-aaS, aaS, sd);
                float sds = wq_sdRoundBox(bu - 0.5 - float2(0.006, -0.018), float2(0.5, 0.5), 0.03);
                float shadow = (1.0 - smoothstep(-0.015, 0.075, sds)) * 0.62;

                // ---- lacquered wood
                float2 grainUV = bu * float2(2.2, 1.0);
                float warp = wq_fbm(grainUV * float2(1.5, 5.0)) * 2.4;
                float rings = 0.5 + 0.5 * sin((bu.y * 46.0 + warp * 3.0 + wq_fbm(bu * float2(3, 18)) * 4.0));
                float wood_t = saturate(rings * 0.55 + wq_fbm(bu * float2(1.2, 9.0)) * 0.55);
                float3 wood = lerp(_Wood2.rgb, _Wood1.rgb, smoothstep(0.15, 0.85, wood_t));
                float fiber = wq_noise(bu * float2(7.0, 520.0));
                wood *= 0.93 + 0.14 * fiber;
                float knot = wq_fbm(bu * 5.0 + 3.1);
                wood *= 0.96 + 0.06 * knot;

                float vign = length(bu - 0.5);
                wood *= 1.0 - 0.34 * pow(saturate(vign * 1.25), 2.6);
                float band = exp(-pow((bu.x * 0.8 + bu.y * 0.6 - frac(_Time.y * 0.035) * 2.6 + 0.5) * 4.0, 2.0));
                wood += band * float3(1.0, 0.92, 0.75) * 0.075;                   // slow lacquer sheen
                float edgeIn = smoothstep(0.0, 0.03, -sd);
                wood *= lerp(0.62, 1.0, edgeIn);                                   // bevel darkening
                wood += (1.0 - smoothstep(0.0, 0.008, -sd)) * float3(1.0, 0.9, 0.7) * 0.16 * step(0.0, 0.5 - bu.y + 0.5);

                // ---- grid
                float span = _N - 1.0 + 2.0 * _Pad;
                float2 p = bu * span - _Pad;                  // cell units, lines on integers 0..N-1
                float2 fw = fwidth(p) + 1e-5;
                float2 r = round(p);
                float inX = step(-0.01, r.x) * step(r.x, _N - 0.99);
                float inY = step(-0.01, r.y) * step(r.y, _N - 0.99);
                float spanX = step(-0.03, p.x) * step(p.x, _N - 0.97);
                float spanY = step(-0.03, p.y) * step(p.y, _N - 0.97);
                float wV = (eq(r.x, 0) + eq(r.x, _N - 1)) * 0.022 + 0.026;
                float wH = (eq(r.y, 0) + eq(r.y, _N - 1)) * 0.022 + 0.026;
                float lv = (1.0 - smoothstep(wV - fw.x, wV + fw.x, abs(p.x - r.x))) * inX * spanY;
                float lh = (1.0 - smoothstep(wH - fw.y, wH + fw.y, abs(p.y - r.y))) * inY * spanX;
                float grid = max(lv, lh);
                float3 ink = float3(0.10, 0.065, 0.035);
                wood = lerp(wood, ink, grid * 0.88);
                float star = isStar(r) * inX * inY * (1.0 - smoothstep(0.085 - fw.x, 0.085 + fw.x, length(p - r)));
                wood = lerp(wood, ink, star);

                // ---- stones
                float2 c = clamp(round(p), 0.0, _N - 1.0);
                float stoneShadow = 0.0;
                [unroll]
                for (int dy = -1; dy <= 1; dy++)
                {
                    [unroll]
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        float2 cc = c + float2(dx, dy);
                        if (cc.x < 0 || cc.y < 0 || cc.x > _N - 1 || cc.y > _N - 1) continue;
                        float4 st = cellState(cc);
                        float code = floor(st.r / 100.0 + 0.5);
                        if (code < 0.5) continue;
                        float anim = saturate(st.b / 255.0);
                        float life = st.a / 255.0;
                        float lift = (1.0 - anim * anim) * 0.35;
                        float2 off = float2(0.07 + lift * 0.5, -0.10 - lift);
                        float d = length(p - cc - off);
                        float blur = 0.14 + lift * 0.4;
                        stoneShadow += (1.0 - smoothstep(0.34 - blur, 0.50 + blur, d)) * life * (0.55 - lift * 0.5);
                    }
                }
                wood *= 1.0 - saturate(stoneShadow) * 0.62;

                float4 st0 = cellState(c);
                float code0 = floor(st0.r / 100.0 + 0.5);
                float mark = floor(st0.g / 50.0 + 0.5);
                float anim0 = saturate(st0.b / 255.0);
                float life0 = st0.a / 255.0;
                float2 d0 = p - c;
                float aaC = fwidth(d0.x) * 1.6 + 1e-5;
                float3 outCol = wood;

                if (code0 > 0.5)
                {
                    float ease = 1.0 - pow(1.0 - anim0, 3.0);
                    float R = 0.475 * (1.0 + (1.0 - ease) * 0.32) * lerp(0.55, 1.0, life0);
                    float2 lp = d0 / R;
                    float4 sl = stoneLayer(lp, code0 > 1.5 ? 1.0 : 0.0, aaC / R);
                    float a = sl.a * saturate(anim0 * 4.0 + 0.05) * life0;
                    float3 col = sl.rgb;

                    // dead marker: dim stone + vermilion crs
                    if (mark > 3.5)
                    {
                        col = lerp(col, wood, 0.45);
                        float2 q = abs(lp);
                        float crs = min(abs(lp.x - lp.y), abs(lp.x + lp.y)) * 0.7071;
                        float ca = (1.0 - smoothstep(0.07, 0.07 + aaC * 2, crs)) * step(max(q.x, q.y), 0.55);
                        col = lerp(col, float3(0.86, 0.17, 0.12), ca);
                        a = max(a, ca * 0.9 * a);
                    }
                    // last move ring
                    if (abs(mark - 1.0) < 0.5)
                    {
                        float rr = length(lp);
                        float ring = 1.0 - smoothstep(0.06, 0.06 + aaC * 3, abs(rr - 0.42));
                        float pulse = 0.65 + 0.35 * sin(_Time.y * 4.0);
                        col = lerp(col, float3(0.95, 0.30, 0.20), ring * pulse);
                    }
                    outCol = lerp(outCol, col, a);
                }
                else
                {
                    // territory square
                    if (abs(mark - 2.0) < 0.5 || abs(mark - 3.0) < 0.5)
                    {
                        float2 q = abs(d0);
                        float sq = 1.0 - smoothstep(0.17 - aaC, 0.17 + aaC, max(q.x, q.y));
                        float3 tc = mark < 2.5 ? float3(0.05, 0.05, 0.06) : float3(0.97, 0.96, 0.93);
                        outCol = lerp(outCol, tc, sq * 0.9);
                    }
                    // hover ghost
                    if (_Hover.x >= 0 && abs(c.x - _Hover.x) < 0.1 && abs(c.y - _Hover.y) < 0.1)
                    {
                        float R = 0.46;
                        float2 lp = d0 / R;
                        if (_Hover.w > 0.75)
                        {
                            float4 sl = stoneLayer(lp, _Hover.z > 1.5 ? 1.0 : 0.0, aaC / R);
                            float pulse = 0.5 + 0.1 * sin(_Time.y * 5.0);
                            outCol = lerp(outCol, sl.rgb, sl.a * pulse);
                            float halo = (1.0 - smoothstep(0.5, 0.78, length(lp))) * 0.16;
                            outCol = lerp(outCol, float3(1.0, 0.85, 0.45), halo);
                        }
                        else if (_Hover.w > 0.25)
                        {
                            float crs = min(abs(lp.x - lp.y), abs(lp.x + lp.y)) * 0.7071;
                            float ca = (1.0 - smoothstep(0.06, 0.06 + aaC * 2, crs)) * step(max(abs(lp.x), abs(lp.y)), 0.5);
                            outCol = lerp(outCol, float3(0.8, 0.15, 0.1), ca * 0.75);
                        }
                    }
                }

                // victory glow
                float glow = _Pulse * (0.5 + 0.5 * sin(_Time.y * 2.6 - length(bu - 0.5) * 9.0));
                outCol += float3(1.0, 0.78, 0.30) * glow * 0.14 * mask;

                float3 rgb = lerp(float3(0, 0, 0), outCol, mask);
                float alpha = saturate(mask + (1.0 - mask) * shadow) * reveal * i.color.a;
                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }
}
