// Barber-pole water slide (URP, lit, no textures). Diagonal stripes in two colours wrap around the slide
// and slowly crawl along it, like a barber's pole. Drawn in the pipe's own space (object position times its
// scale, so in metres that travel with the moving pipe):
//   channel and outer sides -> glossy fibreglass with the twisting stripes
//   flat top rim            -> the same stripes, lightly tinted toward the rim colour
// The stripe period divides 4 m, so the two pipe pieces (96 m and 88 m long) line up at the joint.
Shader "SlideSurfer/PipeWall"
{
    Properties
    {
        [Header(Stripes)]
        _ColorA ("Color A (yellow)", Color) = (1, 0.78, 0.12, 1)
        _ColorB ("Color B (terracotta)", Color) = (0.78, 0.33, 0.2, 1)
        _StripePeriod ("Stripe Period (m, divides 4)", Float) = 2
        _Twist ("Twist (how diagonal)", Float) = 1
        _StripeSpeed ("Crawl Speed (periods/sec)", Float) = 0.35
        _EdgeSoftness ("Edge Softness", Range(0.5, 4)) = 1.5

        [Header(Rim)]
        _RimColor ("Rim Color", Color) = (1, 0.95, 0.84, 1)
        _RimHeight ("Rim Starts Above (m, object)", Float) = 1.45
        _RimTint ("Rim Tint (0 = stripes, 1 = solid)", Range(0, 1)) = 0.15

        [Header(Lighting)]
        _Gloss ("Gloss", Range(4, 256)) = 96
        _SpecStrength ("Specular", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _ColorA;
            half4 _ColorB;
            float _StripePeriod;
            float _Twist;
            float _StripeSpeed;
            float _EdgeSoftness;
            half4 _RimColor;
            float _RimHeight;
            float _RimTint;
            float _Gloss;
            float _SpecStrength;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionM : TEXCOORD2;   // object space in metres
                float3 normalM : TEXCOORD3;     // object-space normal, corrected for the non-uniform scale
                float fogFactor : TEXCOORD4;
            };

            float3 ObjectScale()
            {
                return float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), length(UNITY_MATRIX_M._m02_m12_m22));
            }

            Varyings vert(Attributes input)
            {
                Varyings o;
                float3 scale = ObjectScale();
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.positionM = input.positionOS.xyz * scale;
                o.normalM = normalize(input.normalOS / scale);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            // Barber-pole stripes. 'around' grows as you go round the slide's cross-section (across the floor,
            // up one wall, over the side), so adding it to the distance along the slide tilts the stripes into a spiral.
            half3 BarberPole(float3 p)
            {
                float around = p.x + p.y * sign(p.x);
                float s = (p.z + around * _Twist) / _StripePeriod - _Time.y * _StripeSpeed;
                // distance to the nearest stripe edge, antialiased with the screen-space derivative
                float d = abs(frac(s) - 0.5);
                float w = fwidth(s) * _EdgeSoftness;
                float t = smoothstep(0.25 - w, 0.25 + w, d);
                return lerp(_ColorB.rgb, _ColorA.rgb, t);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 p = i.positionM;
                float3 n = normalize(i.normalM);

                half3 albedo;
                float gloss = _Gloss;
                albedo = BarberPole(p);
                if (n.y > 0.85 && p.y > _RimHeight)
                {
                    // the flat top keeps the stripes but is tinted toward the rim colour (white rim = 1)
                    albedo = lerp(albedo, _RimColor.rgb, _RimTint);
                }

                float3 normalWS = normalize(i.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light light = GetMainLight(shadowCoord);
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));

                float ndl = saturate(dot(normalWS, light.direction));
                float atten = light.shadowAttenuation * light.distanceAttenuation;
                half3 ambient = SampleSH(normalWS);
                half3 diffuse = albedo * (ambient + light.color * ndl * atten);
                // glossy fibreglass: sharp highlight plus a soft sheen at grazing angles
                float spec = pow(saturate(dot(normalWS, normalize(light.direction + viewDir))), gloss) * _SpecStrength * atten;
                float sheen = pow(1.0 - saturate(dot(normalWS, viewDir)), 5.0) * 0.15;

                half3 rgb = diffuse + spec * light.color + sheen;
                rgb = MixFog(rgb, i.fogFactor);
                // keep the HDR value sane: in half precision a stray overflow becomes Inf/NaN and bloom spreads it
                return half4(clamp(rgb, 0.0, 8.0), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes input)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - positionWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                o.positionCS = positionCS;
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            half frag(Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return half4(normalize(i.normalWS), 0.0); }
            ENDHLSL
        }
    }

    FallBack Off
}
