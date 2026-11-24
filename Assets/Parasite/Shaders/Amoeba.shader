// PARASITE — translucent amoeba jelly (hand-written URP shader).
// Refractive body (camera opaque texture seen through the jelly, absorbed towards deep green where it is thick),
// streaming cytoplasm noise, subsurface scattering from every light (Forward+ cluster loop), dual-tier Fresnel rim,
// wet environment reflection + dual-lobe specular, organic breathing, reaching pseudopods along the motion direction,
// a flattened ground-contact patch, an impact jiggle and the _InfectPulse ripple used while possessing.
// Passes: UniversalForward (premultiplied blend, drawn after other transparents) + ShadowCaster.
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
        _AbsorbColor ("Absorption Tint", Color) = (0.18, 0.75, 0.24, 1)
        _Refraction ("Refraction", Range(0, 0.2)) = 0.06
        _RefractWeight ("Refraction Weight", Range(0, 1)) = 0.75
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
        _MoveDir ("Move Dir (object xyz, w = reach)", Vector) = (0, 0, 1, 0)
        _Impact ("Impact Jiggle", Range(0, 1)) = 0
        _Flatten ("Ground Contact Flatten", Range(0, 1)) = 0
        _PodAmount ("Idle Pseudopods", Range(0, 0.3)) = 0.08
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+50" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
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
            half4 _AbsorbColor;
            half _Refraction;
            half _RefractWeight;
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
            float4 _MoveDir;
            half _Impact;
            half _Flatten;
            half _PodAmount;
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
            float3 d = normalize(positionOS + 1e-5);
            // infection ripple: rings travelling up the body
            float ripple = sin(positionOS.y * 26.0 - _Time.y * 14.0) * _InfectPulse * 0.035;
            // organic multi-frequency breathing
            float breathe = (sin((_Time.y * _PulseRate + _PulsePhase) * 0.5) * 0.012) +
                            (sin((_Time.y * _PulseRate * 0.8 + _PulsePhase) * 0.3) * 0.006);
            // impact jiggle: a fast decaying standing wave over the surface
            float jiggle = sin(positionOS.y * 14.0 + _Time.y * 38.0) * sin(positionOS.x * 11.0 - _Time.y * 31.0) * _Impact * 0.06;
            // pseudopods: one reaching ahead with the motion, three slow idle ones wandering around the equator
            float3 md = normalize(_MoveDir.xyz + 1e-5);
            float reach = pow(saturate(dot(d, md)), 5.0) * _MoveDir.w;
            float pods = 0;
            UNITY_UNROLL
            for (int i = 0; i < 3; i++)
            {
                float a = t * (0.21 + i * 0.07) + i * 2.094;
                float3 pd = normalize(float3(cos(a), -0.15 + 0.25 * sin(a * 1.7), sin(a)));
                pods += pow(saturate(dot(d, pd)), 14.0) * (0.55 + 0.45 * sin(t * 0.9 + i * 1.3));
            }
            float3 p = positionOS + normalOS * (n * _WobbleAmp * (1.0 + _InfectPulse + _Impact * 2.0) + ripple + breathe + jiggle + reach + pods * _PodAmount);
            // flattened contact patch: the bottom spreads on the floor instead of balancing on a point
            float floorY = -0.42;
            float below = floorY - p.y;
            if (below > 0)
            {
                p.y = floorY - below * (1.0 - _Flatten) * 0.25 - below * 0.1;
                p.xz *= 1.0 + below * 0.9 * _Flatten;
            }
            return p;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardJelly"
            Tags { "LightMode" = "UniversalForward" }

            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

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
                // bent normal: displaced position direction mixed with the mesh normal (cheap, stays smooth)
                o.normalWS = TransformObjectToWorldNormal(normalize(lerp(input.normalOS, normalize(posOS + float3(0, 0.08, 0)), 0.5)));
                o.positionOS = input.positionOS.xyz;
                o.fogFactor = ComputeFogFactor(vpi.positionCS.z);
                return o;
            }

            float Hash3(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            float Noise3(float3 x)
            {
                float3 i = floor(x), f = frac(x); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash3(i), Hash3(i + float3(1, 0, 0)), f.x), lerp(Hash3(i + float3(0, 1, 0)), Hash3(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash3(i + float3(0, 0, 1)), Hash3(i + float3(1, 0, 1)), f.x), lerp(Hash3(i + float3(0, 1, 1)), Hash3(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            half3 JellyLight(Light l, half3 N, half3 V, half gloss)
            {
                half3 L = l.direction;
                // lights inside / touching the jelly (its own glow) would blow it out: fade them out up close
                half nearFade = saturate(2.5h - l.distanceAttenuation);
                half3 col = l.color * (min(l.distanceAttenuation, 2.5h) * l.shadowAttenuation * nearFade);
                half wrap = saturate((dot(N, L) + 0.6h) / 1.6h);
                half3 H = normalize(L + V);
                half nh = saturate(dot(N, H));
                half spec = pow(nh, gloss) * 1.8h + pow(nh, max(4.0h, gloss * 0.22h)) * 0.4h;
                half sss = pow(saturate(dot(V, -normalize(L + N * 0.4h))), 2.6h) * 1.1h;
                return col * (wrap * _BaseColor.rgb * 0.55h + _SSSColor.rgb * sss * 0.8h + spec);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 N = normalize(input.normalWS);
                half3 V = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half NdotV = saturate(dot(N, V));
                float t = _Time.y;
                half act = lerp(0.35h, 1.0h, _Activity);
                float pulseTime = t * _PulseRate + _PulsePhase;
                half pulse = 0.5h + 0.35h * sin(pulseTime) + 0.15h * sin(pulseTime * 1.618 + 1.2);

                // thickness proxy: thick through the middle, thin at the silhouette
                half thick = pow(NdotV, 0.65h);
                // streaming cytoplasm: two octaves of slowly advecting noise in object space
                float3 q = input.positionOS * 5.0 + float3(0, _WobbleTime * 0.35, _WobbleTime * 0.2);
                half cyto = Noise3(q) * 0.65h + Noise3(q * 2.3 + 7.1) * 0.35h;
                half cells = smoothstep(0.45h, 0.85h, cyto);

                // refraction of the scene behind (camera opaque texture), absorbed towards deep green
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = N;
                inputData.viewDirectionWS = V;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                half3 nV = mul((float3x3)UNITY_MATRIX_V, N);
                float2 ruv = inputData.normalizedScreenSpaceUV - nV.xy * _Refraction * (0.4 + thick) * (1.0 + cyto * 0.3);
                half3 behind = SampleSceneColor(saturate(ruv));
                half3 absorb = lerp(half3(1, 1, 1), _AbsorbColor.rgb, saturate(thick * 1.1h + 0.15h));
                behind *= absorb;

                // lights: main + every additional light (street lamps, headlights, the amoeba's own glow)
                Light ml = GetMainLight(TransformWorldToShadowCoord(input.positionWS), input.positionWS, half4(1, 1, 1, 1));
                half3 lit = JellyLight(ml, N, V, _Gloss);
                #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();
                #if USE_CLUSTER_LIGHT_LOOP
                UNITY_LOOP for (uint li = 0; li < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); li++)
                    lit += JellyLight(GetAdditionalLight(li, input.positionWS, half4(1, 1, 1, 1)), N, V, _Gloss);
                #endif
                LIGHT_LOOP_BEGIN(lightCount)
                    lit += JellyLight(GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1)), N, V, _Gloss);
                LIGHT_LOOP_END
                #endif

                // Fresnel: soft inner halo + sharp electric outer edge, chromatic shift at grazing angles
                half rimBase = 1.0h - NdotV;
                half fresSoft = pow(rimBase, _RimPower);
                half fresSharp = pow(rimBase, _RimPower * 2.2h) * 1.6h;
                half fresnel = fresSoft + fresSharp;
                half3 rimCol = lerp(_RimColor.rgb, half3(_RimColor.r * 0.4h, _RimColor.g * 1.15h, _RimColor.b * 1.5h + 0.25h), fresSharp * 0.45h);

                // wet environment reflection
                half3 R = reflect(-V, N);
                half envF = 0.04h + 0.96h * pow(rimBase, 5.0h);
                half3 env = GlossyEnvironmentReflection(R, input.positionWS, 0.06h, 1.0h, inputData.normalizedScreenSpaceUV) * envF;

                // bioluminescent core + cellular glow
                half core = (pow(NdotV, 3.4h) * 2.0h + pow(NdotV, 1.5h) * 0.55h) * (0.65h + 0.35h * pulse);
                half band = saturate(sin(input.positionOS.y * 28.0 - t * 15.0) * 0.5 + 0.5);
                band = band * band * _InfectPulse;

                half3 ambient = SampleSH(N);
                half3 body = lerp(_DeepColor.rgb, _BaseColor.rgb, 0.35h + 0.65h * cyto);
                half3 surf = body * ambient * 0.45h + lit * 0.8h;
                surf += _CoreColor.rgb * core * (_Emission * 1.1h) * act * (0.75h + 0.5h * cells);
                surf += _SSSColor.rgb * cells * thick * 0.35h * act * _Emission;
                surf += _BaseColor.rgb * _Emission * act * 0.35h * (0.8h + 0.2h * pulse);
                surf += rimCol * fresnel * (0.8h + 0.8h * act);
                surf += _RimColor.rgb * (band * 1.5h + _InfectPulse * 0.6h);
                surf += env;

                // premultiplied composite: surface over refracted background, a little real framebuffer shows through
                half a = saturate(_Alpha * (0.55h + 0.45h * thick) + fresnel * 0.45h + _InfectPulse * 0.15h);
                half r = _RefractWeight;
                half3 col = surf * a + behind * (1.0h - a) * r;
                col = MixFog(col, input.fogFactor);
                return half4(col, a + (1.0h - a) * r);
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
