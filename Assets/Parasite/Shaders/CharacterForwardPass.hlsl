#ifndef PARASITE_CHARACTER_FORWARD_PASS_INCLUDED
#define PARASITE_CHARACTER_FORWARD_PASS_INCLUDED

// Preserve URP 17.3's vertex, GI, decal, fog and rendering-layer contracts.
// The stock fragment remains unused; this small entry point adds shadow-aware,
// main-light thin-skin transmission after the stock PBR response. This is a
// raster approximation, not a diffusion/SSS solver or hardware ray tracing.
#if defined(_PARASITE_HAIR_DIRECTIONAL)
    // Flow requires UV tangents even when the pilot normal map is disabled.
    #define REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR
#endif
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
#if defined(_PARASITE_HAIR_DIRECTIONAL)
    #include "CharacterHairDirectional.hlsl"
#endif

void CharacterLitPassFragment(Varyings input, out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    SurfaceData surfaceData;
    InitializeStandardLitSurfaceData(input.uv, surfaceData);
#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif
    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);
    SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));
#if defined(_DBUFFER)
    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
#endif
    InitializeBakedGIData(input, inputData);
    half4 color;
#if defined(_PARASITE_HAIR_DIRECTIONAL) && !defined(_SPECULAR_SETUP)
    if (_CharacterMode > 1.5h && _CharacterMode < 2.5h && input.uv.y >= 0.25 &&
        surfaceData.metallic <= 0.001h && surfaceData.clearCoatMask <= 0.001h)
        color = CharacterHairFragment(input, inputData, surfaceData);
    else
#endif
        color = UniversalFragmentPBR(inputData, surfaceData);
#if !defined(DEBUG_DISPLAY)
    // Debug output from UniversalFragmentPBR is authoritative. Do not add a
    // lighting term over material/normal/streaming debug colours.
    if (_CharacterMode < 0.5h && _SkinSurfaceEnabled > 0.5h)
    {
        half thickness = SAMPLE_TEXTURE2D(_SkinSurfaceMap, sampler_SkinSurfaceMap, input.uv).b;
        half thin = saturate(1.0h - thickness);
        half4 shadowMask = CalculateShadowMask(inputData);
        AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData, surfaceData);
        Light light = GetMainLight(inputData, shadowMask, aoFactor);
#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, GetMeshRenderingLayer()))
#endif
        {
            half back = saturate(dot(-light.direction, inputData.normalWS));
            half scatter = pow(saturate(dot(inputData.viewDirectionWS, -light.direction)), 4.0h);
            half amount = thin * thin * _Wrap * (back * 0.6h + scatter * 0.4h) * 0.25h;
            color.rgb += surfaceData.albedo * half3(1.0h, 0.30h, 0.16h) * light.color *
                         (amount * light.distanceAttenuation * light.shadowAttenuation);
        }
    }
#endif
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent(_Surface));
    outColor = color;
#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}
#endif
