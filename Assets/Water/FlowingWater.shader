// Stylised flowing water for the pipe (URP, transparent, one pass, mobile friendly).
// The pattern scrolls toward -Z by _WaterFlowOffset (world units), which FlowingWater.cs advances
// with the game speed, so the water moves exactly as fast as the pipe and the obstacles.
// _WaterPlayerPos (xyz = board position, w = 0..1 how much the board is touching the water)
// draws the wake and the foam around the board.
Shader "SlideSurfer/FlowingWater"
{
    Properties
    {
        [Header(Colour)]
        _DeepColor ("Deep Color", Color) = (0.0, 0.32, 0.55, 0.9)
        _ShallowColor ("Shallow Color", Color) = (0.12, 0.82, 0.86, 0.7)
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _SkyColor ("Reflected Sky Color", Color) = (0.75, 0.93, 1, 1)

        [Header(Surface)]
        _RippleScale ("Ripple Scale", Float) = 1.1
        _RippleStrength ("Ripple Strength", Range(0, 1)) = 0.5
        _WaveHeight ("Wave Height", Float) = 0.05
        _Gloss ("Gloss", Range(8, 512)) = 160
        _Sparkle ("Sparkle", Range(0, 2)) = 1

        [Header(Caustics)]
        _CausticScale ("Caustic Scale", Float) = 0.6
        _CausticStrength ("Caustic Strength", Range(0, 1)) = 0.22

        [Header(Foam)]
        _StreakScale ("Streak Length", Float) = 0.12
        _StreakAmount ("Streak Amount", Range(0, 1)) = 0.35
        _EdgeFoam ("Edge Foam Width", Range(0, 0.5)) = 0.07
        _WakeAmount ("Wake Amount", Range(0, 1)) = 1
        _WakeLength ("Wake Length", Float) = 9
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
            // cheaper variant for the lowest quality level (set by GraphicsQuality.cs)
            #pragma multi_compile _ _WATER_LITE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _ShallowColor;
                half4 _FoamColor;
                half4 _SkyColor;
                float _RippleScale;
                float _RippleStrength;
                float _WaveHeight;
                float _Gloss;
                float _Sparkle;
                float _CausticScale;
                float _CausticStrength;
                float _StreakScale;
                float _StreakAmount;
                float _EdgeFoam;
                float _WakeAmount;
                float _WakeLength;
            CBUFFER_END

            // globals, set every frame by FlowingWater.cs
            float _WaterFlowOffset;
            float4 _WaterPlayerPos;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;      // x: 0..1 across the pipe
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            // ---------------------------------------------------------------- noise

            // Integer hash (pcg2d) of a whole-number cell coordinate. The old frac(p * 123.34) hash was so sensitive
            // to rounding that phone compilers, which reorder float maths, gave the shared corner of two neighbouring
            // cells two different values - the water broke into hard-edged rectangles. Integer maths is exact everywhere.
            uint2 Pcg2d(float2 cell)
            {
                // round, not truncate: a cell of 3.9999 must still be cell 4
                uint2 v = uint2(int2(floor(cell + 0.5)));
                v = v * 1664525u + 1013904223u;
                v.x += v.y * 1664525u;
                v.y += v.x * 1664525u;
                v = v ^ (v >> 16u);
                v.x += v.y * 1664525u;
                v.y += v.x * 1664525u;
                v = v ^ (v >> 16u);
                return v;
            }

            // 0..1 from the top 24 bits, which a float holds exactly
            float Hash21(float2 cell)
            {
                return float(Pcg2d(cell).x >> 8u) * (1.0 / 16777216.0);
            }

            float2 Hash22(float2 cell)
            {
                return float2(Pcg2d(cell) >> 8u) * (1.0 / 16777216.0);
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

            // two octaves (three cost too much per pixel on phones); rescaled to keep the old three-octave range
            float Fbm(float2 p)
            {
                float v = 0.5 * ValueNoise(p) + 0.25 * ValueNoise(p * 2.03 + 17.1);
                return v * 1.1667;
            }

            // distance to the nearest cell border of a wobbling voronoi: thin bright lines = caustics
            float CausticLines(float2 p, float t)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float d1 = 8.0;
                float d2 = 8.0;
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 g = float2(x, y);
                        float2 o = Hash22(i + g);
                        o = 0.5 + 0.45 * sin(t + 6.2831 * o);
                        float d = length(g + o - f);
                        if (d < d1) { d2 = d1; d1 = d; }
                        else if (d < d2) { d2 = d; }
                    }
                }
                return d2 - d1;
            }

            // flow space: x across, y along the pipe, shifted by the distance the water has travelled
            float2 FlowCoord(float3 positionWS)
            {
                return float2(positionWS.x, positionWS.z + _WaterFlowOffset);
            }

            // ---------------------------------------------------------------- vertex

            Varyings vert(Attributes input)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                // travelling swells, pinned to zero at the edges so the water stays glued to the walls
                float2 f = FlowCoord(positionWS);
                float edge = saturate(min(input.uv.x, 1.0 - input.uv.x) * 6.0);
                float wave = sin(f.y * 0.55 + f.x * 0.9 + _Time.y * 1.3) * 0.55
                           + sin(f.y * 1.7 - f.x * 1.4 + _Time.y * 2.1) * 0.3
                           + sin(f.y * 3.1 + _Time.y * 3.0) * 0.15;
                // only ever lift the surface, so the swells never dip below the pipe and let it poke through
                positionWS += normalWS * (wave * 0.5 + 0.5) * _WaveHeight * edge;

                o.positionWS = positionWS;
                o.normalWS = normalWS;
                o.uv = input.uv;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            // ---------------------------------------------------------------- fragment

            // foam the board leaves in the water: a ring under the board and a V-shaped wake trailing toward -Z
            float WakeFoam(float3 positionWS, float2 flow)
            {
                float3 d = positionWS - _WaterPlayerPos.xyz;
                float touching = _WaterPlayerPos.w;
                if (touching <= 0.0 || abs(d.x) > 6.0 || d.z > 2.0 || d.z < -_WakeLength * 1.5) return 0.0;

                // bubbly breakup that travels with the water, so the foam churns instead of sitting still
                float bubbles = Fbm(flow * float2(2.2, 1.4) + float2(0.0, _Time.y * 1.5));

                // white water boiling right around the board
                float ring = 1.0 - smoothstep(0.3, 0.95, length(float2(d.x * 1.4, d.z * 0.75)));
                ring *= smoothstep(0.3, 0.6, bubbles);

                // two ragged foam lines opening up behind the board, broken turbulence between them
                float behind = -d.z;
                float wake = 0.0;
                if (behind > 0.0)
                {
                    float spread = 0.35 + behind * 0.3 + (bubbles - 0.5) * 0.25;
                    float fade = exp(-behind * 3.0 / _WakeLength);
                    float lineDist = abs(abs(d.x) - spread);
                    float lines = (1.0 - smoothstep(0.04, 0.18 + behind * 0.02, lineDist)) * fade;
                    lines *= smoothstep(0.2, 0.5, bubbles);
                    float churn = (1.0 - smoothstep(0.0, spread * 0.85, abs(d.x))) * exp(-behind * 0.7);
                    churn *= smoothstep(0.5, 0.72, bubbles) * 0.8;
                    wake = lines + churn;
                }
                return saturate(ring + wake) * touching * _WakeAmount;
            }

            half4 frag(Varyings i, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                float2 f = FlowCoord(i.positionWS);

                // ripples stretched along the flow, plus a slower churn on top
                float2 rp = f * _RippleScale * float2(1.0, 0.45);
                float n1 = Fbm(rp);
                float n2 = Fbm(rp * 2.3 + float2(0.0, _Time.y * 0.6));

                float3 n = normalize(i.normalWS * (frontFace ? 1.0 : -1.0));
                float across = min(i.uv.x, 1.0 - i.uv.x) * 2.0;
                half4 col = lerp(_ShallowColor, _DeepColor, smoothstep(0.05, 0.85, across));
                col.rgb *= 0.88 + 0.24 * n1;

            #if defined(_WATER_LITE)
                // lite: slope straight from the two noises already sampled, no caustics, glints or streaks
                float3 normalWS = normalize(n - float3(n1 - 0.44, 0.0, n2 - 0.44) * (_RippleStrength * 0.8));
                float foam = 0.0;
            #else
                // bend the normal with the slope of one cheap noise layer (three samples instead of two full fbm's)
                float2 sp = rp * 1.7;
                float e = 0.12;
                float s0 = ValueNoise(sp);
                float dx = ValueNoise(sp + float2(e, 0)) - s0;
                float dz = ValueNoise(sp + float2(0, e)) - s0;
                float3 normalWS = normalize(n - float3(dx, 0.0, dz) * (_RippleStrength * 5.0));

                // dancing caustic net, soft and broken up by the ripples, strongest in the shallow parts
                float2 cp = f * _CausticScale * float2(1.0, 0.55) + float2(n1, n2) * 1.2;
                float caustic = CausticLines(cp, _Time.y * 1.2);
                caustic = pow(1.0 - smoothstep(0.0, 0.09, caustic), 3.0) * smoothstep(0.35, 0.65, n2);
                col.rgb += caustic * _CausticStrength * (0.5 + 0.5 * (1.0 - across));

                // thin foam streaks running with the current (one stretched noise layer is enough)
                float streak = ValueNoise(float2(f.x * 3.0, f.y * _StreakScale)) * 0.875;
                float foam = smoothstep(0.64, 0.74, streak + n2 * 0.12) * _StreakAmount;
            #endif

                // foam lace along both walls and the board's wake
                float edgeDist = min(i.uv.x, 1.0 - i.uv.x);
                float lace = smoothstep(_EdgeFoam, 0.0, edgeDist) * smoothstep(0.3, 0.6, n2 + 0.2);
                lace = max(lace, smoothstep(_EdgeFoam * 0.35, 0.0, edgeDist));
                float wake = WakeFoam(i.positionWS, f);
                foam = saturate(foam + lace + wake);

                Light mainLight = GetMainLight();
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                float3 halfDir = normalize(mainLight.direction + viewDir);
                float nh = saturate(dot(normalWS, halfDir));
                float spec = pow(nh, _Gloss) * 2.0;
            #if defined(_WATER_LITE)
                float glint = 0.0;
            #else
                // tiny sun glints that pop on and off as the ripples move
                float2 gc = f * 9.0;
                float glintDot = 1.0 - smoothstep(0.05, 0.22, length(frac(gc) - 0.5));
                float glint = step(0.97, Hash21(floor(gc) + floor(_Time.y * 5.0))) * glintDot * pow(nh, 8.0) * _Sparkle;
            #endif
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDir)), 4.0);
                float diffuse = 0.65 + 0.35 * saturate(dot(normalWS, mainLight.direction));

                half3 rgb = col.rgb * diffuse * mainLight.color;
                rgb = lerp(rgb, _SkyColor.rgb, fresnel * 0.5);
                rgb = lerp(rgb, _FoamColor.rgb * (0.85 + 0.15 * diffuse), foam);
                rgb += (spec + glint) * mainLight.color;

                half alpha = saturate(lerp(col.a, _FoamColor.a, foam) + fresnel * 0.3 + spec + glint);

                rgb = MixFog(rgb, i.fogFactor);
                // keep the HDR value sane: in half precision a stray overflow becomes Inf/NaN and bloom spreads it
                return half4(clamp(rgb, 0.0, 8.0), alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
