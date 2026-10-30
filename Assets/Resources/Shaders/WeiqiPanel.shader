// Rounded glass / lacquer panel used for every card and button.
// _Size must be set to the rect size in pixels (PanelSkin does this).
Shader "Weiqi/Panel"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _ColorTop ("Top", Color) = (0.20, 0.22, 0.30, 0.92)
        _ColorBottom ("Bottom", Color) = (0.10, 0.11, 0.17, 0.92)
        _Accent ("Accent", Color) = (0.95, 0.75, 0.35, 1)
        _Size ("Size px", Vector) = (200, 80, 0, 0)
        _Radius ("Radius px", Float) = 22
        _Hover ("Hover", Range(0, 1)) = 0
        _Press ("Press", Range(0, 1)) = 0
        _Selected ("Selected", Range(0, 1)) = 0
        _Glow ("Glow", Range(0, 1)) = 0
        _Disabled ("Disabled", Range(0, 1)) = 0
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

            fixed4 _ColorTop, _ColorBottom, _Accent;
            float4 _Size;
            float _Radius, _Hover, _Press, _Selected, _Glow, _Disabled;

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

            fixed4 frag(v2f i) : SV_Target
            {
                float2 sz = max(_Size.xy, 2.0);
                float2 p = (i.uv - 0.5) * sz;
                float r = min(_Radius, min(sz.x, sz.y) * 0.5);
                float d = wq_sdRoundBox(p, sz * 0.5 - 1.0, r);
                float cover = 1.0 - smoothstep(-0.8, 0.8, d);

                float vy = i.uv.y;
                float4 baseC = lerp(_ColorBottom, _ColorTop, vy);
                float act = saturate(_Hover * 0.6 + _Selected + _Glow);
                float3 col = baseC.rgb;
                col = lerp(col, col + _Accent.rgb * 0.22, act * 0.8);
                col = lerp(col, _Accent.rgb * 0.85, _Selected * 0.55);

                // inner glow along the rim
                float rim = exp(-max(-d, 0.0) / 14.0);
                col += _Accent.rgb * rim * (0.05 + 0.22 * act);

                // glossy top highlight
                float gloss = smoothstep(0.55, 1.0, vy) * smoothstep(-1.0, 0.0, d + 0.0) ;
                col += float3(1, 1, 1) * gloss * 0.07;

                // moving shine on hover
                float diag = (p.x + p.y * 0.6) / sz.x + 0.5;
                float shine = exp(-pow((diag - frac(_Time.y * 0.55)) * 7.0, 2.0)) * _Hover;
                col += _Accent.rgb * shine * 0.28;

                // border
                float border = 1.0 - smoothstep(0.0, 1.6, abs(d + 1.0));
                float3 borderC = lerp(float3(1, 1, 1) * 0.35, _Accent.rgb, saturate(act + 0.25));
                col = lerp(col, borderC, border * (0.28 + 0.55 * act));

                col *= 1.0 - 0.18 * _Press;
                col = lerp(col, dot(col, float3(0.3, 0.59, 0.11)).xxx * 0.7, _Disabled);

                float alpha = cover * lerp(baseC.a, 1.0, saturate(act * 0.5)) * i.color.a * lerp(1.0, 0.55, _Disabled);
                return fixed4(saturate(col), alpha);
            }
            ENDCG
        }
    }
}
