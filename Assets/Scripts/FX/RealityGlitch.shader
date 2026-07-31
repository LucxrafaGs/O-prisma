Shader "Prisma/RealityGlitch"
{
    Properties
    {
        _MainTex ("Screen", 2D) = "white" {}
        _Intensity ("Intensity", Range(0, 1)) = 1
        _TimeSeed ("Time Seed", Float) = 0
        _Mode ("Mode", Float) = 0
        _BubbleCenter ("Bubble Center", Vector) = (0.5, 0.5, 0, 0)
        _BubbleRadius ("Bubble Radius", Range(0.05, 0.8)) = 0.28
        _Purple ("Purple", Color) = (0.45, 0.05, 0.7, 1)
        _Black ("Black", Color) = (0.02, 0.0, 0.04, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "RealityGlitch"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float _Intensity;
            float _TimeSeed;
            float _Mode;
            float4 _BubbleCenter;
            float _BubbleRadius;
            float4 _Purple;
            float4 _Black;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float2 Pixelate(float2 uv, float cells)
            {
                return floor(uv * cells) / cells;
            }

            float4 SampleScreen(float2 uv)
            {
                uv = saturate(uv);
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
            }

            // 0 — Faixas digitais / pixel tear
            float4 ModeTear(float2 uv, float t, float intensity)
            {
                float band = floor(uv.y * 28.0 + t * 6.0);
                float jitter = (Hash21(float2(band, floor(t * 12.0))) - 0.5) * 0.18 * intensity;
                float2 u = uv;
                u.x += jitter;
                if (Hash21(float2(band, 3.1)) > 0.82)
                    u = Pixelate(u, lerp(180.0, 48.0, intensity));

                float4 c = SampleScreen(u);
                float tear = step(0.88, Hash21(float2(band, t)));
                c.rgb = lerp(c.rgb, _Purple.rgb, tear * 0.65 * intensity);
                c.rgb = lerp(c.rgb, _Black.rgb, tear * step(0.5, Hash21(float2(band, 9.0))) * intensity);
                return c;
            }

            // 1 — Aberração cromática + estático roxo
            float4 ModeChromatic(float2 uv, float t, float intensity)
            {
                float2 dir = float2(sin(t * 7.0), cos(t * 5.0)) * 0.012 * intensity;
                float r = SampleScreen(uv + dir).r;
                float g = SampleScreen(uv).g;
                float b = SampleScreen(uv - dir).b;
                float4 c = float4(r, g, b, 1);
                float staticN = Hash21(Pixelate(uv, 120.0) + floor(t * 20.0));
                c.rgb = lerp(c.rgb, _Purple.rgb, staticN * 0.35 * intensity);
                c.rgb *= 1.0 - staticN * 0.25 * intensity;
                return c;
            }

            // 2 — Bolha de vidro / matéria desmontando
            float4 ModeBubble(float2 uv, float t, float intensity)
            {
                float2 center = _BubbleCenter.xy;
                float2 d = uv - center;
                // Corrige aspect aproximado
                d.x *= _ScreenParams.x / max(_ScreenParams.y, 1.0);
                float dist = length(d);
                float radius = _BubbleRadius * lerp(0.85, 1.15, 0.5 + 0.5 * sin(t * 3.0));
                float inside = saturate(1.0 - dist / max(radius, 1e-4));
                float edge = smoothstep(0.0, 0.25, inside) * smoothstep(1.0, 0.7, inside);

                float2 dir = dist > 1e-4 ? normalize(d) : float2(0, 1);
                float warp = inside * inside * 0.22 * intensity;
                float2 u = uv - dir * warp;
                // Refração tipo vidro
                u += dir * sin(dist * 40.0 - t * 10.0) * 0.012 * inside * intensity;

                float4 c = SampleScreen(u);
                float fresnel = pow(edge, 1.5);
                c.rgb = lerp(c.rgb, c.rgb * float3(0.85, 0.7, 1.15) + _Purple.rgb * 0.25, fresnel * intensity);
                c.rgb = lerp(c.rgb, _Black.rgb, pow(inside, 3.0) * 0.15 * intensity);
                // Anel de ruptura
                float ring = smoothstep(0.08, 0.0, abs(dist - radius));
                c.rgb = lerp(c.rgb, _Purple.rgb, ring * intensity);
                return c;
            }

            // 3 — Blocos corrompidos pretos/roxos
            float4 ModeBlocks(float2 uv, float t, float intensity)
            {
                float2 block = floor(uv * float2(24.0, 16.0));
                float h = Hash21(block + floor(t * 8.0));
                float2 u = uv;
                if (h > 0.72)
                {
                    float2 shift = (Hash21(block + 2.3) - 0.5) * 0.2 * intensity;
                    u += shift;
                    u = Pixelate(u, 64.0);
                }

                float4 c = SampleScreen(u);
                if (h > 0.9)
                    c.rgb = _Black.rgb;
                else if (h > 0.82)
                    c.rgb = lerp(c.rgb, _Purple.rgb, 0.85 * intensity);
                else if (h > 0.75)
                    c.rgb = c.bgr; // canal swap
                return c;
            }

            // 4 — Ondulação total + scanlines + invert flash
            float4 ModeWave(float2 uv, float t, float intensity)
            {
                float2 u = uv;
                u.x += sin(uv.y * 50.0 + t * 12.0) * 0.03 * intensity;
                u.y += cos(uv.x * 35.0 - t * 9.0) * 0.02 * intensity;
                float4 c = SampleScreen(u);
                float scan = sin((uv.y + t * 0.15) * 800.0) * 0.5 + 0.5;
                c.rgb *= 1.0 - scan * 0.25 * intensity;
                float flash = step(0.92, frac(t * 1.7));
                c.rgb = lerp(c.rgb, 1.0 - c.rgb, flash * 0.55 * intensity);
                c.rgb = lerp(c.rgb, _Purple.rgb, flash * 0.25 * intensity);
                return c;
            }

            // 5 — Vortex / buraco na realidade (bonus)
            float4 ModeVortex(float2 uv, float t, float intensity)
            {
                float2 center = _BubbleCenter.xy;
                float2 d = uv - center;
                d.x *= _ScreenParams.x / max(_ScreenParams.y, 1.0);
                float dist = length(d);
                float angle = atan2(d.y, d.x) + (0.8 - dist) * 3.5 * intensity * sin(t * 2.0);
                float2 dir = float2(cos(angle), sin(angle));
                float2 u = center + dir * dist;
                u.x /= (_ScreenParams.x / max(_ScreenParams.y, 1.0));
                u = lerp(uv, u + center - float2(0.5, 0.5) * 0.0, intensity);
                // rebuild u properly
                float2 warped = center;
                warped.x += dir.x * dist / (_ScreenParams.x / max(_ScreenParams.y, 1.0));
                warped.y += dir.y * dist;
                float4 c = SampleScreen(lerp(uv, warped, intensity));
                float hole = saturate(1.0 - dist / 0.35);
                c.rgb = lerp(c.rgb, _Black.rgb, pow(hole, 2.0) * intensity);
                c.rgb = lerp(c.rgb, _Purple.rgb, pow(hole, 4.0) * 0.5 * intensity);
                return c;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float t = _TimeSeed + _Time.y;
                float intensity = saturate(_Intensity);
                int mode = (int)_Mode;
                float4 color;

                if (mode == 1)
                    color = ModeChromatic(input.uv, t, intensity);
                else if (mode == 2)
                    color = ModeBubble(input.uv, t, intensity);
                else if (mode == 3)
                    color = ModeBlocks(input.uv, t, intensity);
                else if (mode == 4)
                    color = ModeWave(input.uv, t, intensity);
                else if (mode == 5)
                    color = ModeVortex(input.uv, t, intensity);
                else
                    color = ModeTear(input.uv, t, intensity);

                // Vinheta de corrupção nas bordas
                float2 v = input.uv * 2.0 - 1.0;
                float vig = saturate(length(v) - 0.7);
                color.rgb = lerp(color.rgb, _Black.rgb, vig * 0.35 * intensity);
                return color;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
