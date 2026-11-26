// Scan-derived URP ground with restrained, non-repeating world-space damp/dry variation.
// Same fine A/N/M maps, shadow/depth passes and metre UVs as the existing materials.
Shader "Parasite/RealisticSurface"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        _Smoothness("Smoothness", Range(0,1)) = 0.5
        _Metallic("Metallic", Range(0,1)) = 0
        _MetallicGlossMap("Metallic / Smoothness", 2D) = "white" {}
        _BumpScale("Normal Scale", Float) = 1
        [Normal] _BumpMap("Normal", 2D) = "bump" {}
        _OcclusionMap("Occlusion", 2D) = "white" {}
        _OcclusionStrength("Occlusion Strength", Range(0,1)) = 1
        // Reuse three unused Lit CBUFFER fields, so the stock depth/shadow passes and SRP batcher
        // share exactly the standard material layout. This shader has no detail/parallax keywords.
        _DetailAlbedoMapScale("Large Scale Albedo Variation", Range(0,0.25)) = 0.08
        _DetailNormalMapScale("Variation Length (metres)", Float) = 11.5
        _Parallax("Damp / Dry Smoothness Variation", Range(0,0.1)) = 0.025
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _Blend("Blend", Float) = 0
        [HideInInspector] _Cull("Cull", Float) = 2
        [HideInInspector] _Cutoff("Cutoff", Float) = 0.5
        [HideInInspector] _AlphaClip("Alpha Clip", Float) = 0
        [HideInInspector] _SrcBlend("Src Blend", Float) = 1
        [HideInInspector] _DstBlend("Dst Blend", Float) = 0
        [HideInInspector] _ZWrite("ZWrite", Float) = 1
        [HideInInspector] _SpecColor("Specular", Color) = (0.2,0.2,0.2,1)
        [HideInInspector] _EmissionColor("Emission", Color) = (0,0,0,0)
        [HideInInspector] _EmissionMap("Emission Map", 2D) = "white" {}
        [HideInInspector] _SpecularHighlights("Specular Highlights", Float) = 1
        [HideInInspector] _EnvironmentReflections("Environment Reflections", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "UniversalMaterialType"="Lit" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull] ZWrite [_ZWrite]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LitPassVertex
            #pragma fragment SurfaceFragment
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

            float Hash(float2 p)
            {
                float3 v = frac(float3(p.xyx) * 0.1031);
                v += dot(v, v.yzx + 33.33);
                return frac((v.x + v.y) * v.z);
            }
            float Field(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f*f*(3.0-2.0*f);
                return lerp(lerp(Hash(i), Hash(i+float2(1,0)), f.x),
                            lerp(Hash(i+float2(0,1)), Hash(i+float2(1,1)), f.x), f.y);
            }
            void SurfaceFragment(Varyings input, out half4 outColor : SV_Target0)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                SurfaceData s;
                InitializeStandardLitSurfaceData(input.uv, s);
                float2 p = input.positionWS.xz / max(_DetailNormalMapScale, 0.1);
                half macro = Field(p) * 0.72 + Field(p * 2.13 + 13.1) * 0.28;
                half variation = macro * 2.0 - 1.0;
                s.albedo *= 1.0 + variation * _DetailAlbedoMapScale;
                s.smoothness = saturate(s.smoothness + variation * _Parallax);
                InputData d;
                InitializeInputData(input, s.normalTS, d);
                InitializeBakedGIData(input, d);
                half4 c = UniversalFragmentPBR(d, s);
                c.rgb = MixFog(c.rgb, d.fogCoord);
                outColor = half4(c.rgb, 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
        UsePass "Universal Render Pipeline/Lit/Meta"
    }
    FallBack "Universal Render Pipeline/Lit"
}
