// Stylised flowing water for the pipe (URP, transparent, one pass, mobile friendly).
// The pattern scrolls toward -Z by _WaterFlowOffset (world units), which FlowingWater.cs advances
// with the game speed, so the water moves exactly as fast as the pipe and the obstacles.
Shader "SlideSurfer/FlowingWater"
{
    Properties
    {
        _DeepColor ("Deep Color", Color) = (0.03, 0.33, 0.58, 0.85)
        _ShallowColor ("Shallow Color", Color) = (0.20, 0.78, 0.88, 0.6)
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _RippleScale ("Ripple Scale", Float) = 1.2
        _RippleStrength ("Ripple Strength", Range(0, 1)) = 0.45
        _StreakScale ("Streak Length", Float) = 0.15
        _StreakAmount ("Streak Amount", Range(0, 1)) = 0.5
        _EdgeFoam ("Edge Foam Width", Range(0, 0.5)) = 0.1
        _WaveHeight ("Wave Height", Float) = 0.03
        _Gloss ("Gloss", Range(4, 256)) = 80
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _ShallowColor;
                half4 _FoamColor;
                float _RippleScale;
                float _RippleStrength;
                float _StreakScale;
                float _StreakAmount;
                float _EdgeFoam;
                float _WaveHeight;
                float _Gloss;
            CBUFFER_END

            // global, set every frame by FlowingWater.cs
            float _WaterFlowOffset;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;      // x: 0..1 across the pipe, y: unused
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int k = 0; k < 3; k++)
                {
                    v += a * ValueNoise(p);
                    p = p * 2.03 + 17.1;
                    a *= 0.5;
                }
                return v;
            }

            // flow space: x across, y along the pipe, shifted by the distance the water has travelled
            float2 FlowCoord(float3 positionWS)
            {
                return float2(positionWS.x, positionWS.z + _WaterFlowOffset);
            }

            Varyings vert(Attributes input)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                // small travelling swell, pinned to zero at the edges so the water stays glued to the walls
                float2 f = FlowCoord(positionWS);
                float edge = saturate(min(input.uv.x, 1.0 - input.uv.x) * 6.0);
                float wave = sin(f.y * 0.9 + f.x * 1.3 + _Time.y * 1.7) * 0.6 + sin(f.y * 2.1 - f.x * 0.7 + _Time.y * 2.3) * 0.4;
                positionWS += normalWS * wave * _WaveHeight * edge;

                o.positionWS = positionWS;
                o.normalWS = normalWS;
                o.uv = input.uv;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                float2 f = FlowCoord(i.positionWS);

                // ripples stretched along the flow, plus a slower churn on top
                float2 rp = f * _RippleScale * float2(1.0, 0.45);
                float n1 = Fbm(rp);
                float n2 = Fbm(rp * 2.3 + float2(0.0, _Time.y * 0.6));

                // bend the normal with the ripple slope for the highlights
                float e = 0.15;
                float dx = Fbm(rp + float2(e, 0)) - n1;
                float dz = Fbm(rp + float2(0, e)) - n1;
                float3 n = normalize(i.normalWS * (frontFace ? 1.0 : -1.0));
                float3 normalWS = normalize(n - float3(dx, 0.0, dz) * (_RippleStrength * 8.0));

                // deeper colour in the middle of the pipe, lighter where the water climbs the walls
                float across = min(i.uv.x, 1.0 - i.uv.x) * 2.0;
                half4 col = lerp(_ShallowColor, _DeepColor, smoothstep(0.0, 0.8, across));
                col.rgb *= 0.85 + 0.3 * n1;

                // long foam streaks running with the current and foam along both edges
                float streak = Fbm(float2(f.x * 2.5, f.y * _StreakScale));
                float foam = smoothstep(0.6, 0.72, streak + n2 * 0.15) * _StreakAmount;
                float edgeFoam = smoothstep(_EdgeFoam, 0.0, min(i.uv.x, 1.0 - i.uv.x)) * (0.55 + 0.45 * n2);
                foam = saturate(foam + edgeFoam);

                Light mainLight = GetMainLight();
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                float3 halfDir = normalize(mainLight.direction + viewDir);
                float spec = pow(saturate(dot(normalWS, halfDir)), _Gloss);
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDir)), 4.0);
                float diffuse = 0.6 + 0.4 * saturate(dot(normalWS, mainLight.direction));

                half3 rgb = col.rgb * diffuse * mainLight.color;
                rgb = lerp(rgb, _FoamColor.rgb, foam);
                rgb += spec * mainLight.color * 0.9 + fresnel * 0.2;
                half alpha = saturate(lerp(col.a, _FoamColor.a, foam) + fresnel * 0.25 + spec);

                rgb = MixFog(rgb, i.fogFactor);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
