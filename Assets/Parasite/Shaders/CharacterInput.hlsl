#ifndef PARASITE_CHARACTER_INPUT_INCLUDED
#define PARASITE_CHARACTER_INPUT_INCLUDED

// Surface input for Parasite/Character: URP Lit inputs (same CBUFFER members so the stock URP pass files compile)
// plus character extras:
//   _MaskMap  (cloth)  R = tint group (0 none, 1/3 A, 2/3 B, 1 C), G = metallic, B = smoothness
//   _SkinSurfaceMap   filtered linear R smoothness, G local AO, B optical thickness
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
half _SkinSurfaceEnabled;
half _IrisUVScale;
float4 _EyeAtlasCentres;
half _HairForwardCutoff;
half _HairAnisotropy;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

TEXTURE2D(_HairFlowMap); SAMPLER(sampler_HairFlowMap);
TEXTURE2D(_MaskMap);
TEXTURE2D(_VeinMap);        SAMPLER(sampler_VeinMap);
TEXTURE2D(_SkinSurfaceMap); SAMPLER(sampler_SkinSurfaceMap);
SAMPLER(sampler_point_clamp_MaskMap);

half4 SampleCharacterMask(float2 uv)
{
    return SAMPLE_TEXTURE2D(_MaskMap, sampler_point_clamp_MaskMap, uv);
}

inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    float2 albedoUV = uv;
    // Opt-in MPFB two-island eye atlas correction. Only the iris-centred field
    // changes; exterior sclera UVs, geometry and every other material keep uv0.
    if (_CharacterMode > 0.5h && _CharacterMode < 1.5h && _IrisUVScale > 1.001h)
    {
        float2 centre = uv.x + uv.y > 1.0 ? _EyeAtlasCentres.zw : _EyeAtlasCentres.xy;
        float2 delta = uv - centre;
        float fade = smoothstep(0.15, 0.27, length(delta));
        albedoUV = centre + delta * (1.0 + (_IrisUVScale - 1.0) * (1.0 - fade));
    }
    half4 albedoAlpha = SampleAlbedoAlpha(albedoUV, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
#if defined(PARASITE_CHARACTER_FORWARD)
    // Exported hair atlases reserve the upper three quarters for scalp cards.
    // Restore opaque core coverage while the bottom-row brows/lashes stay soft.
    if (_CharacterMode > 1.5h && _CharacterMode < 2.5h && uv.y >= 0.25)
        albedoAlpha.a = saturate(albedoAlpha.a / 0.4h);
#endif
    half cutoff = _Cutoff;
#if defined(PARASITE_CHARACTER_FORWARD)
    // Only forward Hair blends fine fibre coverage. Shadow/depth passes retain
    // the original0.4 cutoff and cannot become broad solid brow/lash shadows.
    if (_CharacterMode > 1.5h && _CharacterMode < 2.5h) cutoff = _HairForwardCutoff;
#endif
    outSurfaceData.alpha = Alpha(albedoAlpha.a, _BaseColor, cutoff);

    half3 albedo = albedoAlpha.rgb * _BaseColor.rgb;
    half metallic = _Metallic;
    half smoothness = _Smoothness;
    half occlusion = 1.0h;
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
        if (_SkinSurfaceEnabled > 0.5h)
        {
            half3 surface = SAMPLE_TEXTURE2D(_SkinSurfaceMap, sampler_SkinSurfaceMap, uv).rgb;
            smoothness *= surface.r;
            occlusion = lerp(1.0h, surface.g, _OcclusionStrength);
        }
    }

    outSurfaceData.albedo = AlphaModulate(albedo, outSurfaceData.alpha);
    outSurfaceData.metallic = metallic;
    outSurfaceData.specular = _SpecColor.rgb;
    outSurfaceData.smoothness = smoothness;
    outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    // Isolated pilot only: 8-bit128/128 neutral is not exactly zero XY.
    // Preserve exact stock flat brow/lash normals in normal-only controls too.
    if (_CharacterMode > 1.5h && _CharacterMode < 2.5h && uv.y < 0.25)
        outSurfaceData.normalTS = half3(0, 0, 1);
    outSurfaceData.occlusion = occlusion;
    outSurfaceData.emission = emission;
    // Eye wet film uses URP's energy-conserving coat BRDF in the same material slot.
    outSurfaceData.clearCoatMask = _CharacterMode > 0.5h && _CharacterMode < 1.5h ? _ClearCoatMask : 0.0h;
    outSurfaceData.clearCoatSmoothness = _ClearCoatSmoothness;
}

#endif
