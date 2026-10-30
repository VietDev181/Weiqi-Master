#ifndef WEIQI_COMMON_INCLUDED
#define WEIQI_COMMON_INCLUDED

float wq_hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float2 wq_hash22(float2 p)
{
    float n = wq_hash21(p);
    return float2(n, wq_hash21(p + n + 17.3));
}

float wq_noise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = wq_hash21(i);
    float b = wq_hash21(i + float2(1, 0));
    float c = wq_hash21(i + float2(0, 1));
    float d = wq_hash21(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float wq_fbm(float2 p)
{
    float v = 0.0;
    float a = 0.5;
    [unroll]
    for (int i = 0; i < 5; i++)
    {
        v += a * wq_noise(p);
        p = p * 2.03 + 11.7;
        a *= 0.5;
    }
    return v;
}

float wq_sdRoundBox(float2 p, float2 halfSize, float r)
{
    float2 q = abs(p) - halfSize + r;
    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
}

// Glossy sphere shading shared by stones / emblem / icons. n = surface normal, kind 0 = black slate, 1 = white shell.
float3 wq_shadeStone(float3 n, float kind, float2 lp)
{
    float3 L = normalize(float3(-0.42, 0.62, 0.66));
    float3 V = float3(0, 0, 1);
    float diff = saturate(dot(n, L));
    float3 R = reflect(-L, n);
    float spec = pow(saturate(dot(R, V)), kind < 0.5 ? 70.0 : 48.0);
    float fres = pow(1.0 - saturate(n.z), 3.0);
    float3 col;
    if (kind < 0.5)
    {
        col = float3(0.020, 0.022, 0.030) + diff * float3(0.10, 0.105, 0.12);
        // soft window-like reflection
        float2 hp = (lp - float2(-0.34, 0.40)) / float2(0.34, 0.20);
        float win = saturate(1.0 - dot(hp, hp));
        col += win * win * 0.30;
        col += spec * 0.95;
        col += fres * float3(0.05, 0.07, 0.11);
    }
    else
    {
        float grain = wq_noise(lp * 9.0) * 0.5 + wq_noise(lp * 26.0) * 0.25;
        float rings = 0.5 + 0.5 * sin(length(lp) * 34.0 + grain * 5.0);
        col = float3(0.93, 0.915, 0.88) * (0.60 + 0.48 * diff);
        col *= 1.0 - 0.025 * rings - 0.03 * grain;
        col = lerp(col, float3(0.68, 0.64, 0.58), fres * 0.55);
        col += spec * 0.75;
        col += float3(1.0, 0.97, 0.9) * pow(saturate(dot(n, L)), 8.0) * 0.07;
    }
    return col;
}

#endif
