// Shallow ground water: puddle decals (texture outline) and the street-wide wet film (_WETFILM: world-space puddle
// noise + curb gutters + a thin damp sheen). Coverage attenuates the entire lit result, including reflections, so
// transparent texels cannot leave a rectangle around the water. Reflections: a short screen-space ray march against
// the camera depth/opaque textures (real neon, windows and lamps in the water), falling back to reflection probes.
// Rain ripples are driven by the global _ParasiteRain (0..1, set by RainSystem at runtime).
Shader "Parasite/GroundWater"
{
    Properties
    {
        _BaseMap ("Puddle Outline (Alpha)", 2D) = "white" {}
        _BaseColor ("Water Bed Tint", Color) = (0.025, 0.03, 0.035, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.94
        _Coverage ("Coverage", Range(0, 1)) = 0.92
        _EdgeSoftness ("Shore Feather", Range(0.01, 0.3)) = 0.08
        _RippleStrength ("Reflection Ripple", Range(0, 0.05)) = 0.008
        _DampCoverage ("Film: Damp Coverage", Range(0, 1)) = 0.3
        _PuddleThreshold ("Film: Puddle Threshold", Range(0, 1)) = 0.62
        _PuddleScale ("Film: Puddle Scale (1/m)", Range(0.01, 1)) = 0.11
        _Gutter ("Film: Gutter Water", Range(0, 1)) = 0.75
        _SSRStrength ("SSR Strength", Range(0, 1)) = 1
        _ReflectionBoost ("Reflection Boost", Range(0, 3)) = 1.25
        _BedOpacity ("Bed Opacity (shallow)", Range(0, 1)) = 0.7
        _BedOpacityDeep ("Bed Opacity (deep)", Range(0, 1)) = 0.85
        [Toggle(_WETFILM)] _WetFilm ("Wet Film (procedural coverage)", Float) = 0
        [Toggle(_VERTEX_COVERAGE)] _VertexCoverage ("Vertex Colour Coverage (slime decals)", Float) = 0
        [HDR] _GlowColor ("Glow (emissive slime)", Color) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "GroundWater"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _WETFILM
            #pragma shader_feature_local _VERTEX_COVERAGE
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _PARASITE_SSR_OFF
            #define _SPECULAR_SETUP 1
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Smoothness;
                half _Coverage;
                half _EdgeSoftness;
                half _RippleStrength;
                half _DampCoverage;
                half _PuddleThreshold;
                half _PuddleScale;
                half _Gutter;
                half _SSRStrength;
                half _ReflectionBoost;
                half4 _GlowColor;
                half _BedOpacity;
                half _BedOpacityDeep;
            CBUFFER_END
            float _ParasiteRain;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                #if defined(_VERTEX_COVERAGE)
                half4 color : COLOR;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                #if defined(_VERTEX_COVERAGE)
                half4 color : COLOR;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                #if defined(_VERTEX_COVERAGE)
                output.color = input.color;
                #endif
                return output;
            }

            float Hash12(float2 p) { float3 p3 = frac(float3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return frac((p3.x + p3.y) * p3.z); }
            float2 Hash22(float2 p) { float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973)); p3 += dot(p3, p3.yzx + 33.33); return frac((p3.xx + p3.yz) * p3.zy); }
            float VNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash12(i), Hash12(i + float2(1, 0)), u.x), lerp(Hash12(i + float2(0, 1)), Hash12(i + float2(1, 1)), u.x), u.y);
            }

            // expanding rain-drop rings, two staggered cell layers -> xz normal offset
            float2 Ripples(float2 xz, float t)
            {
                float2 acc = 0;
                UNITY_UNROLL
                for (int layer = 0; layer < 2; layer++)
                {
                    float2 g = xz * 2.3 + layer * float2(0.37, 0.71);
                    float2 cell = floor(g);
                    float2 f = frac(g) - 0.5;
                    float h = Hash12(cell + layer * 17.0);
                    float life = frac(t * 1.25 + h);
                    float2 c = (Hash22(cell + layer * 5.0) - 0.5) * 0.5;
                    float2 dv = f - c;
                    float d = length(dv) + 1e-4;
                    float r = life * 0.48;
                    float band = saturate(1.0 - abs(d - r) * 14.0);
                    float wave = sin((d - r) * 70.0) * band * (1.0 - life) * (1.0 - life);
                    acc += dv / d * wave;
                }
                return acc;
            }

            half3 TraceReflection(float3 posWS, float3 R, half3 fallback, out half hit)
            {
                hit = 0;
                #if defined(_PARASITE_SSR_OFF)
                    return fallback;
                #else
                float3 p = posWS + R * 0.05;
                float stepLen = 0.18;
                float3 prev = p;
                UNITY_LOOP
                for (int k = 0; k < 22; k++)
                {
                    prev = p;
                    p += R * stepLen;
                    stepLen *= 1.22;
                    float4 cs = TransformWorldToHClip(p);
                    if (cs.w <= 0.0) break;
                    float4 sp = ComputeScreenPos(cs);
                    float2 uv = sp.xy / sp.w;
                    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) break;
                    float raw = SampleSceneDepth(uv);
                    float sceneZ = LinearEyeDepth(raw, _ZBufferParams);
                    float diff = cs.w - sceneZ;
                    if (diff > 0.0)
                    {
                        if (diff > stepLen * 3.0 + 0.4) break;           // passed behind a thin object: no reliable hit
                        float3 a = prev, b = p;
                        UNITY_UNROLL
                        for (int j = 0; j < 4; j++)
                        {
                            float3 m = (a + b) * 0.5;
                            float4 mcs = TransformWorldToHClip(m);
                            float4 msp = ComputeScreenPos(mcs);
                            float2 muv = msp.xy / msp.w;
                            if (mcs.w > LinearEyeDepth(SampleSceneDepth(muv), _ZBufferParams)) b = m; else a = m;
                        }
                        float4 hcs = TransformWorldToHClip(b);
                        float4 hsp = ComputeScreenPos(hcs);
                        float2 huv = hsp.xy / hsp.w;
                        float2 edge = min(huv, 1.0 - huv);
                        hit = saturate(min(edge.x, edge.y) * 9.0) * saturate(1.2 - k / 22.0) * _SSRStrength;
                        return lerp(fallback, SampleSceneColor(huv), hit);
                    }
                }
                return fallback;
                #endif
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 pw = input.positionWS;
                half coverage;
                #if defined(_WETFILM)
                    float n = VNoise(pw.xz * _PuddleScale) * 0.62 + VNoise(pw.xz * _PuddleScale * 3.7 + 11.3) * 0.38;
                    half puddle = smoothstep(_PuddleThreshold, _PuddleThreshold + 0.06, n);
                    half gutter = smoothstep(0.16, 0.0, min(input.uv.y, 1.0 - input.uv.y)) * _Gutter;
                    half damp = _DampCoverage * (0.75 + 0.5 * VNoise(pw.xz * 0.9 + 3.1));
                    coverage = max(max(damp, puddle * _Coverage), gutter * _Coverage);
                #else
                    float2 p = input.uv * 2.0 - 1.0;
                    // World-anchored low-frequency lobes vary each shore without consuming the city builder's RNG.
                    float shoreNoise = sin(pw.x * 2.7 + pw.z * 1.3) * sin(pw.z * 3.1 - pw.x * 0.9);
                    float shore = 0.87 - length(p) + shoreNoise * 0.065;
                    float feather = max(_EdgeSoftness, fwidth(shore) * 1.5);
                    coverage = smoothstep(0.0, feather, shore) * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _Coverage;
                #endif
                half3 bed = _BaseColor.rgb;
                #if defined(_VERTEX_COVERAGE)
                    coverage *= input.color.a;
                    bed *= input.color.rgb;
                #endif
                clip(coverage - 0.002h);
                half deep = saturate((coverage - 0.45) * 2.0);

                // almost-flat water + fixed micro ripples + animated rain rings in standing water
                half2 ripple = half2(sin(pw.x * 8.7 + pw.z * 5.3), sin(pw.z * 9.1 - pw.x * 4.1)) * _RippleStrength;
                ripple += Ripples(pw.xz, _Time.y) * (_ParasiteRain * 0.11 * (0.3 + 0.7 * deep));
                half3 N = normalize(half3(ripple.x, 1.0h, ripple.y));

                InputData inputData = (InputData)0;
                inputData.positionWS = pw;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = N;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(pw);
                inputData.shadowCoord = TransformWorldToShadowCoord(pw);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);
                half3 V = inputData.viewDirectionWS;

                half smooth = lerp(_Smoothness * 0.86, _Smoothness, deep);
                BRDFData brdf;
                half alphaOne = 1;
                InitializeBRDFData(bed, 0, half3(0.02h, 0.02h, 0.02h), smooth, alphaOne, brdf);

                // direct light: sun + every street lamp (Forward+ cluster loop) -> long wet highlights
                Light ml = GetMainLight(inputData.shadowCoord, pw, inputData.shadowMask);
                half3 lit = LightingPhysicallyBased(brdf, ml, N, V);
                #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();
                #if USE_CLUSTER_LIGHT_LOOP
                UNITY_LOOP for (uint li = 0; li < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); li++)
                {
                    Light l = GetAdditionalLight(li, pw, inputData.shadowMask);
                    lit += LightingPhysicallyBased(brdf, l, N, V);
                }
                #endif
                LIGHT_LOOP_BEGIN(lightCount)
                    Light l = GetAdditionalLight(lightIndex, pw, inputData.shadowMask);
                    lit += LightingPhysicallyBased(brdf, l, N, V);
                LIGHT_LOOP_END
                #endif

                // bed diffuse from ambient + reflections (screen-space where possible, probes otherwise)
                half NoV = saturate(dot(N, V));
                half fresnel = 0.02h + 0.98h * pow(1.0h - NoV, 5.0h);
                half3 R = reflect(-V, N);
                half3 probe = GlossyEnvironmentReflection(R, pw, brdf.perceptualRoughness, 1.0h, inputData.normalizedScreenSpaceUV);
                half hit;
                half3 refl = TraceReflection(pw, R, probe, hit);
                lit += bed * SampleSH(N);
                lit += _GlowColor.rgb * (0.4h + 0.6h * deep);
                lit += refl * lerp(fresnel, 1.0h, 0.04h) * _ReflectionBoost * lerp(0.75h, 1.0h, deep);

                // Premultiply AFTER lighting/fog so every lobe vanishes at the feathered shore. Shallow water only partly
                // hides (darkens) the ground below it; reflections and highlights are added on top.
                half opacity = lerp(_BedOpacity, _BedOpacityDeep, deep);
                return half4(MixFog(lit, input.fogFactor) * coverage, coverage * opacity);
            }
            ENDHLSL
        }
    }
}
