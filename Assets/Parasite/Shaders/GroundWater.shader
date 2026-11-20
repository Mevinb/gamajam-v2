// Shallow ground water. Coverage attenuates the entire lit result, including environment/specular light,
// so transparent texels cannot leave a rectangular reflection around the actual puddle.
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
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #define _SPECULAR_SETUP 1
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Smoothness;
                half _Coverage;
                half _EdgeSoftness;
                half _RippleStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half fogFactor : TEXCOORD2;
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
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p = input.uv * 2.0 - 1.0;
                // World-anchored low-frequency lobes vary each shore without consuming the city builder's RNG.
                float shoreNoise = sin(input.positionWS.x * 2.7 + input.positionWS.z * 1.3)
                                 * sin(input.positionWS.z * 3.1 - input.positionWS.x * 0.9);
                float shore = 0.87 - length(p) + shoreNoise * 0.065;
                float feather = max(_EdgeSoftness, fwidth(shore) * 1.5);
                half coverage = smoothstep(0.0, feather, shore)
                              * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _Coverage;
                clip(coverage - 0.001h);

                // Almost-flat water keeps reflections legible; small fixed ripples avoid a perfect mirror tile.
                half2 ripple = half2(sin(input.positionWS.x * 8.7 + input.positionWS.z * 5.3),
                                     sin(input.positionWS.z * 9.1 - input.positionWS.x * 4.1)) * _RippleStrength;
                half3 normalWS = normalize(half3(ripple.x, 1.0h, ripple.y));
                InputData lighting = (InputData)0;
                lighting.positionWS = input.positionWS;
                lighting.normalWS = normalWS;
                lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                lighting.bakedGI = SampleSH(normalWS);
                lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                lighting.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = _BaseColor.rgb;
                surface.specular = half3(0.02h, 0.02h, 0.02h); // Water's dielectric reflectance at normal incidence.
                surface.smoothness = _Smoothness;
                surface.normalTS = half3(0, 0, 1);
                surface.occlusion = 1;
                surface.alpha = 1;
                half3 lit = UniversalFragmentPBR(lighting, surface).rgb;
                // Premultiply AFTER lighting/fog so every lobe vanishes at the feathered shore.
                return half4(MixFog(lit, input.fogFactor) * coverage, coverage);
            }
            ENDHLSL
        }
    }
}
