#ifndef PARASITE_CHARACTER_INPUT_INCLUDED
#define PARASITE_CHARACTER_INPUT_INCLUDED

// Surface input for Parasite/Character: URP Lit inputs (same CBUFFER members so the stock URP pass files compile)
// plus character extras:
//   _MaskMap  (cloth)  R = tint group (0 none, 1/3 A, 2/3 B, 1 C), G = metallic, B = smoothness
//   _TintA/B/C         per-instance garment colours (linear multipliers, HDR allowed)
//   _VeinMap  (skin)   R = vein mask in body UV space, lit by _VeinColor (possession veins)
//   _EmissionColor     uniform emission (HostGlow pulse, glowing eyes)
//   _CharacterMode     0 skin, 1 eyes, 2 hair, 3 cloth

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float4 _BaseMap_TexelSize;
float4 _DetailAlbedoMap_ST;
half4 _BaseColor;
half4 _SpecColor;
half4 _EmissionColor;
half _Cutoff;
half _Smoothness;
half _Metallic;
half _BumpScale;
half _Parallax;
half _OcclusionStrength;
half _ClearCoatMask;
half _ClearCoatSmoothness;
half _DetailAlbedoMapScale;
half _DetailNormalMapScale;
half _Surface;
half4 _TintA;
half4 _TintB;
half4 _TintC;
half4 _VeinColor;
half _CharacterMode;
half _Wrap;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

TEXTURE2D(_MaskMap);
TEXTURE2D(_VeinMap);        SAMPLER(sampler_VeinMap);
SAMPLER(sampler_point_clamp_MaskMap);

half4 SampleCharacterMask(float2 uv)
{
    return SAMPLE_TEXTURE2D(_MaskMap, sampler_point_clamp_MaskMap, uv);
}

inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    outSurfaceData.alpha = Alpha(albedoAlpha.a, _BaseColor, _Cutoff);

    half3 albedo = albedoAlpha.rgb * _BaseColor.rgb;
    half metallic = _Metallic;
    half smoothness = _Smoothness;
    half3 emission = _EmissionColor.rgb;

    UNITY_BRANCH
    if (_CharacterMode > 2.5)
    {
        // cloth atlas: garment tint groups + packed metallic/smoothness
        half4 m = SampleCharacterMask(uv);
        half g = round(m.r * 3.0h);
        half3 tint = g < 0.5h ? half3(1, 1, 1) : (g < 1.5h ? _TintA.rgb : (g < 2.5h ? _TintB.rgb : _TintC.rgb));
        albedo *= tint;
        metallic = m.g * _Metallic;
        smoothness = m.b * _Smoothness;
    }
    else if (_CharacterMode < 0.5h)
    {
        // skin: veins light up while possessed (vein map is in the shared MakeHuman body UV layout)
        half vein = SAMPLE_TEXTURE2D(_VeinMap, sampler_VeinMap, uv).r;
        emission += vein * _VeinColor.rgb;
        // subtle wrap: lift the darkest skin a little (cheap stand-in for subsurface scattering)
        albedo = lerp(albedo, albedo * half3(1.04h, 0.98h, 0.96h), _Wrap);
    }

    outSurfaceData.albedo = AlphaModulate(albedo, outSurfaceData.alpha);
    outSurfaceData.metallic = metallic;
    outSurfaceData.specular = half3(0.0, 0.0, 0.0);
    outSurfaceData.smoothness = smoothness;
    outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    outSurfaceData.occlusion = half(1.0);
    outSurfaceData.emission = emission;
    outSurfaceData.clearCoatMask = half(0.0);
    outSurfaceData.clearCoatSmoothness = half(0.0);
}

#endif
