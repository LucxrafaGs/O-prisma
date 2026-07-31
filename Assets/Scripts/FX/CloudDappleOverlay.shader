Shader "Prisma/CloudDappleOverlay"
{
    Properties
    {
        _MainTex ("Noise", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 0.95, 0.75, 1)
        _ShadowColor ("Shadow Color", Color) = (0.08, 0.12, 0.22, 1)
        _Intensity ("Intensity", Range(0, 1.5)) = 0.65
        _Contrast ("Contrast", Range(0.2, 3)) = 1.35
        _Scroll ("Scroll", Vector) = (0, 0, 0, 0)
        _ShaftStrength ("Shaft Strength", Range(0, 2)) = 0
        _ShaftAngle ("Shaft Angle", Float) = 0.55
        _Mode ("Mode", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+80"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "CloudDapple"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _Tint;
            float4 _ShadowColor;
            float _Intensity;
            float _Contrast;
            float4 _Scroll;
            float _ShaftStrength;
            float _ShaftAngle;
            float _Mode;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 worldXY : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.worldXY = world.xy;
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
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

            float Fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    v += a * Noise(p);
                    p = p * 2.05 + 17.1;
                    a *= 0.5;
                }
                return v;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv * 3.2 + _Scroll.xy;
                float2 worldUv = input.worldXY * 0.085 + _Scroll.zw;

                float texSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv * 0.35 + _Scroll.xy * 0.2).r;
                float n1 = Fbm(worldUv * 1.15);
                float n2 = Fbm(worldUv * 2.4 + 9.7);
                float clouds = saturate(n1 * 0.72 + n2 * 0.28 + texSample * 0.15);

                // Manchas irregulares: valores altos = sol; baixos = sombra de nuvem.
                float dapple = saturate(pow(clouds, _Contrast));
                float sunMask = smoothstep(0.38, 0.72, dapple);
                float shadeMask = 1.0 - smoothstep(0.22, 0.58, dapple);

                // Raios diagonais no golden hour.
                float2 shaftUv = input.worldXY;
                float ca = cos(_ShaftAngle);
                float sa = sin(_ShaftAngle);
                float along = shaftUv.x * ca + shaftUv.y * sa;
                float across = -shaftUv.x * sa + shaftUv.y * ca;
                float shafts = sin(along * 0.55 + _Scroll.x * 4.0) * 0.5 + 0.5;
                shafts *= smoothstep(2.8, 0.15, abs(across * 0.35));
                shafts = pow(saturate(shafts), 2.2) * _ShaftStrength * sunMask;

                float3 sunColor = _Tint.rgb;
                float3 shadeColor = _ShadowColor.rgb;

                // Modo: 0 day, 1 sunset, 2 night
                float night = saturate((_Mode - 1.5) * 2.0);
                float sunset = saturate(1.0 - abs(_Mode - 1.0) * 2.0);

                float3 lit = lerp(shadeColor, sunColor, sunMask);
                lit = lerp(lit, lit * float3(1.15, 0.72, 0.35), sunset * 0.55);
                lit = lerp(lit, lerp(shadeColor, float3(0.45, 0.55, 0.85), sunMask * 0.65), night);

                float alpha = (shadeMask * 0.42 + sunMask * 0.18 + shafts * 0.55) * _Intensity;
                alpha *= lerp(1.0, 0.55, night);

                // Mistura: sombra escurece / sol aquece levemente.
                float3 rgb = lerp(shadeColor, sunColor, sunMask * 0.85 + shafts * 0.5);
                rgb = lerp(rgb, sunColor * float3(1.2, 0.7, 0.3), shafts);

                return float4(rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
