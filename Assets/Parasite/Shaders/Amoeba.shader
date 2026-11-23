// PARASITE — translucent amoeba jelly (hand-written URP shader).
// Enhanced bioluminescent inner glow, dynamic subsurface scattering, dual-tier Fresnel rim,
// wet dual-lobe specular, organic breathing pulse, and an _InfectPulse ripple used
// while possessing. One UniversalForward pass + ShadowCaster.
Shader "Parasite/Amoeba"
{
    Properties
    {
        [HDR] _BaseColor ("Jelly Colour", Color) = (0.30, 0.95, 0.35, 1)
        _DeepColor ("Deep Colour", Color) = (0.02, 0.22, 0.06, 1)
        [HDR] _RimColor ("Rim Colour", Color) = (0.55, 1.4, 0.55, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.6
        [HDR] _SSSColor ("Subsurface Colour", Color) = (0.45, 1.2, 0.30, 1)
        [HDR] _CoreColor ("Inner Glow Colour", Color) = (0.8, 1.6, 0.5, 1)
        _Emission ("Emission", Range(0, 4)) = 0.35
        _Alpha ("Alpha (centre)", Range(0, 1)) = 0.62
        _Gloss ("Gloss", Range(4, 256)) = 72
        _WobbleAmp ("Wobble Amplitude", Range(0, 0.2)) = 0.045
        _WobbleFreq ("Wobble Frequency", Range(0.5, 12)) = 4.5
        _WobbleSpeed ("Wobble Speed", Range(0, 8)) = 2.2
        _PulseRate ("Pulse Rate", Range(0, 12)) = 3.0
        _PulsePhase ("Pulse Phase (script-driven)", Float) = 0
        _WobbleTime ("Wobble Time (script-driven)", Float) = 0
        _Activity ("Activity (0 dim .. 1 bright)", Range(0, 1)) = 1
        _InfectPulse ("Infect Pulse", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _DeepColor;
            half4 _RimColor;
            half _RimPower;
            half4 _SSSColor;
            half4 _CoreColor;
            half _Emission;
            half _Alpha;
            half _Gloss;
            float _WobbleAmp;
            float _WobbleFreq;
            float _WobbleSpeed;
            float _PulseRate;
            float _PulsePhase;
            float _WobbleTime;
            half _Activity;
            half _InfectPulse;
        CBUFFER_END

        // Cheap smooth pseudo-noise in [-1, 1]: two octaves of crossed sines.
        float WobbleNoise(float3 p, float t)
        {
            float n = sin(p.x * 1.00 + t * 1.30) * sin(p.y * 1.13 + t * 1.70) * sin(p.z * 0.91 + t * 1.10);
            n += 0.5 * sin(p.x * 2.31 - t * 2.10 + p.z * 1.7) * sin(p.y * 2.07 + t * 1.90 - p.x * 0.6);
            return n * 0.8;
        }

        float3 WobbleObject(float3 positionOS, float3 normalOS)
        {
            // scripts may drive time themselves (_WobbleSpeed = 0, _WobbleTime accumulated) to vary speed smoothly
            float t = _Time.y * _WobbleSpeed + _WobbleTime;
            float n = WobbleNoise(positionOS * _WobbleFreq, t);
            // infection ripple: rings travelling up the body
            float ripple = sin(positionOS.y * 26.0 - _Time.y * 14.0) * _InfectPulse * 0.035;
            // organic multi-frequency breathing
            float breathe = (sin((_Time.y * _PulseRate + _PulsePhase) * 0.5) * 0.012) +
                            (sin((_Time.y * _PulseRate * 0.8 + _PulsePhase) * 0.3) * 0.006);
            return positionOS + normalOS * (n * _WobbleAmp * (1.0 + _InfectPulse) + ripple + breathe);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardJelly"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                half   fogFactor  : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 posOS = WobbleObject(input.positionOS.xyz, input.normalOS);
                VertexPositionInputs vpi = GetVertexPositionInputs(posOS);
                o.positionCS = vpi.positionCS;
                o.positionWS = vpi.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.positionOS = input.positionOS.xyz;
                o.fogFactor = ComputeFogFactor(vpi.positionCS.z);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 N = normalize(input.normalWS);
                half3 V = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light mainLight = GetMainLight();
                half3 L = mainLight.direction;
                half3 lightCol = mainLight.color * mainLight.distanceAttenuation;

                half NdotV = saturate(dot(N, V));

                // Enhanced organic breathing pulse with harmonic complexity
                float t = _Time.y;
                float pulseTime = t * _PulseRate + _PulsePhase;
                half pulse1 = sin(pulseTime);
                half pulse2 = sin(pulseTime * 1.618f + 1.2f);
                half pulse = 0.5h + 0.35h * pulse1 + 0.15h * pulse2;
                half act = lerp(0.35h, 1.0h, _Activity);

                // Subtle cellular micro-shimmer across the cytoplasm
                half microShimmer = sin(input.positionOS.x * 14.0 + input.positionOS.y * 16.0 + t * 3.5) * 0.08h;

                // Multi-tier Fresnel rim: soft luminous inner halo + sharp electric outer edge
                half rimBase = 1.0h - NdotV;
                half fresSoft = pow(rimBase, _RimPower);
                half fresSharp = pow(rimBase, _RimPower * 2.2h) * 1.6h;
                half fresnel = fresSoft + fresSharp;
                // Chromatic rim shift: extreme grazing edge shifts towards electric cyan/aurora
                half3 rimShiftColor = lerp(_RimColor.rgb, half3(_RimColor.r * 0.4h, _RimColor.g * 1.15h, _RimColor.b * 1.5h + 0.25h), fresSharp * 0.45h);

                // Dynamic subsurface scattering (SSS): forward and wrap transmission through translucent jelly
                half wrap = saturate((dot(N, L) + 0.65h) / 1.65h);
                half3 sssFwdDir = normalize(L + N * 0.4h);
                half sssForward = pow(saturate(dot(V, -sssFwdDir)), 2.8h) * 1.1h;
                half3 sssBackDir = normalize(-L + N * 0.25h);
                half sssBack = saturate(dot(V, sssBackDir)) * wrap * 0.5h;
                half sss = sssForward + sssBack + wrap * 0.2h;

                // Wet dual-lobe specular highlight (sharp primary highlight + soft broad sheen)
                half3 H = normalize(L + V);
                half NdotH = saturate(dot(N, H));
                half specSharp = pow(NdotH, _Gloss) * 1.8h;
                half specBroad = pow(NdotH, max(4.0h, _Gloss * 0.22h)) * 0.45h;
                half spec = specSharp + specBroad;

                // Enhanced bioluminescent inner glow: layered deep core + radiant cellular halo
                half coreSharp = pow(NdotV, 3.4h);
                half coreHalo = pow(NdotV, 1.5h) * 0.55h;
                half core = (coreSharp * 2.0h + coreHalo) * (0.65h + 0.35h * pulse + microShimmer);

                // Infection ripple bands travelling through the jelly
                half band = saturate(sin(input.positionOS.y * 28.0 - t * 15.0) * 0.5 + 0.5);
                band = band * band * _InfectPulse;

                // Additional lights support (streetlights, headlights, amoeba's own light)
                half3 additionalLighting = half3(0, 0, 0);
                #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
                {
                    Light addLight = GetAdditionalLight(lightIndex, input.positionWS);
                    half3 addL = addLight.direction;
                    half3 addCol = addLight.color * addLight.distanceAttenuation;
                    half addWrap = saturate((dot(N, addL) + 0.5h) / 1.5h);
                    half3 addH = normalize(addL + V);
                    half addSpec = pow(saturate(dot(N, addH)), _Gloss) * 1.2h;
                    half3 addSssDir = normalize(addL + N * 0.4h);
                    half addSss = pow(saturate(dot(V, -addSssDir)), 2.5h) * 0.6h;
                    additionalLighting += addCol * (addWrap * _BaseColor.rgb * 0.6h + addSpec + _SSSColor.rgb * addSss * 0.5h);
                }
                #endif

                half3 ambient = SampleSH(N);
                half3 body = lerp(_DeepColor.rgb, _BaseColor.rgb, wrap * 0.7h + 0.3h);
                half3 col = body * (ambient * 0.85h + lightCol * wrap * 0.9h);
                col += _SSSColor.rgb * lightCol * sss * 0.85h;
                col += rimShiftColor * fresnel * (0.9h + 0.8h * act);
                col += lightCol * spec;
                col += _CoreColor.rgb * core * (_Emission * 2.2h) * act;
                col += _BaseColor.rgb * _Emission * act * 0.4h * (0.8h + 0.2h * pulse);
                col += _RimColor.rgb * (band * 1.5h + _InfectPulse * 0.6h);
                col += additionalLighting;

                half alpha = saturate(_Alpha + fresnel * 0.48h + specSharp * 0.5h + _InfectPulse * 0.18h);
                col = MixFog(col, input.fogFactor);
                return half4(col, alpha);
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
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 posOS = WobbleObject(input.positionOS.xyz, input.normalOS);
                float3 positionWS = TransformObjectToWorld(posOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                o.positionCS = positionCS;
                return o;
            }

            half4 ShadowFrag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack Off
}
