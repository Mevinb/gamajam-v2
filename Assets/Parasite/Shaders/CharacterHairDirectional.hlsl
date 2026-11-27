// Portions derived from Unity URP17.3 ShaderLibrary/Lighting.hlsl.
// com.unity.render-pipelines.universal copyright © 2020 Unity Technologies ApS
// Licensed under the Unity Companion License for Unity-dependent projects.
// See CharacterHairDirectional.LICENSE.md; original package source and SHA are pinned in source-contract.json.
// Modifications: flow frame, bounded anisotropic surface BRDF and direct dispatch.
// Opt-in LakeGirl hair surface path. Other material defaults remain off. URP17.3.
// Single anisotropic dielectric microfacet *surface* lobe, not a hair-fiber BSDF.
// The light-loop body below is copied from installed UniversalFragmentPBR;
// only direct-light dispatch changes. GI, AO, shadows, cookies, light layers,
// debug masks, Forward+/cluster loops and vertex diffuse remain stock once.
#ifndef PARASITE_CHARACTER_HAIR_DIRECTIONAL_INCLUDED
#define PARASITE_CHARACTER_HAIR_DIRECTIONAL_INCLUDED

struct CharacterHairFlowResult
{
    float3 strandWS;
    float weight;
};

CharacterHairFlowResult CharacterHairFlow(Varyings input, float3 shadedNormalWS)
{
    // Return a fully initialized value at every exit. Invalid flow/frame paths
    // keep zero weight and therefore use the stock per-light response.
    CharacterHairFlowResult result = (CharacterHairFlowResult)0;
    result.strandWS = float3(1, 0, 0);
    float4 flow = SAMPLE_TEXTURE2D(_HairFlowMap, sampler_HairFlowMap, input.uv);
    float2 direction = flow.rg * 2.0 - 1.0;
    float n2 = dot(input.normalWS, input.normalWS);
    if (n2 < 1e-6) return result;
    float3 baseN = input.normalWS * rsqrt(n2);
    float3 baseT = input.tangentWS.xyz - baseN * dot(baseN, input.tangentWS.xyz);
    float t2 = dot(baseT, baseT);
    if (t2 < 1e-6 || dot(direction, direction) < 0.0625 || abs(input.tangentWS.w) < 0.5)
        return result;
    baseT *= rsqrt(t2);
    float3 baseB = input.tangentWS.w * cross(baseN, baseT);
    float3 strandWS = direction.x * baseT + direction.y * baseB;
    strandWS -= shadedNormalWS * dot(strandWS, shadedNormalWS);
    float s2 = dot(strandWS, strandWS);
    if (s2 < 1e-6) return result;
    result.strandWS = strandWS * rsqrt(s2);
    result.weight = saturate(flow.b) * saturate(flow.a);
    return result;
}

float3 CharacterHairFresnel(float cosine, float3 f0)
{
    float x = 1.0 - saturate(cosine);
    float x2 = x * x;
    return f0 + (1.0 - f0) * (x2 * x2 * x);
}

float3 CharacterHairSurfaceBRDF(BRDFData brdf, float3 n, float3 strand,
                                float3 l, float3 v)
{
    float nl = saturate(dot(n, l)), nv = saturate(dot(n, v));
    if (nl < 1e-4 || nv < 1e-4) return 0;
    float3 h = SafeNormalize(l + v);
    float3 across = cross(n, strand);
    // Preserve alphaAlong*alphaAcross = baseAlpha^2 before safety clamps.
    // A narrow along-flow slope distribution yields a broader highlight
    // perpendicular to strands. This is a surface proxy for the cap only.
    float aspect = sqrt(1.0 - 0.9 * clamp(_HairAnisotropy, 0.0, 0.8));
    float baseAlpha = max((float)brdf.roughness, 0.045);
    float along = max(baseAlpha * aspect, 0.045);
    float crosswise = min(baseAlpha / aspect, 1.0);
    float th = dot(strand, h), bh = dot(across, h), nh = saturate(dot(n, h));
    float q = (th * th) / (along * along) + (bh * bh) / (crosswise * crosswise) + nh * nh;
    // URP direct Lambert omits 1/pi. D here omits 1/pi too, keeping
    // the same light-intensity convention; divide total BRDF by pi for SI.
    float d = rcp(max(along * crosswise * q * q, 1e-6));
    float tv = dot(strand, v), bv = dot(across, v);
    float tl = dot(strand, l), bl = dot(across, l);
    float projectedV = sqrt(along * along * tv * tv + crosswise * crosswise * bv * bv + nv * nv);
    float projectedL = sqrt(along * along * tl * tl + crosswise * crosswise * bl * bl + nl * nl);
    // Height-correlated Smith GGX visibility = G2/(4*N.L*N.V).
    float visibility = 0.5 / max(nl * projectedV + nv * projectedL, 1e-6);
    float3 f0 = saturate((float3)brdf.specular);
    float3 f = CharacterHairFresnel(dot(v, h), f0);
    float3 specular = d * visibility * f;
    // Conservative dielectric substrate transmission approximation. Keep the
    // original diffuse coefficient/premultiplication, with incident+exit
    // Fresnel attenuation. This is not a full coupled energy-conserving BSDF.
    float3 diffuseTransmission = (1.0 - CharacterHairFresnel(nl, f0)) *
                                 (1.0 - CharacterHairFresnel(nv, f0));
    float3 diffuse = (float3)brdf.diffuse * diffuseTransmission / max(1.0 - (float)brdf.reflectivity, 1e-3);
#if REAL_IS_HALF
    specular = min(specular, 1000.0);
#endif
    return diffuse + specular;
}

half3 CharacterHairDirect(BRDFData brdf, BRDFData coat, Light light,
                         half3 n, half3 v, half coatMask, bool specularOff,
                         float3 strandWS, float weight)
{
#if defined(_SPECULARHIGHLIGHTS_OFF)
    return LightingPhysicallyBased(brdf, coat, light, n, v, coatMask, specularOff);
#else
    // Cull0 Hair retains stock backside diffuse. The surface microfacet proxy
    // has no valid outgoing hemisphere here, so do not blacken stock response.
    if (specularOff || weight <= 0.0 || dot(n, v) <= 1e-4)
        return LightingPhysicallyBased(brdf, coat, light, n, v, coatMask, specularOff);
    float nl = saturate(dot(n, light.direction));
    float3 proxy = CharacterHairSurfaceBRDF(brdf, n, strandWS, light.direction, v) *
                   (float3)light.color * (light.distanceAttenuation * light.shadowAttenuation * nl);
    // Valid cap texels avoid evaluating the stock direct specular at all.
    if (weight >= 1.0) return (half3)proxy;
    half3 stock = LightingPhysicallyBased(brdf, coat, light, n, v, coatMask, specularOff);
    // Convex replacement, never stock specular PLUS a second lobe.
    return (half3)lerp((float3)stock, proxy, saturate(weight));
#endif
}

half4 CharacterHairFragment(Varyings input, InputData inputData, SurfaceData surfaceData)

{
    CharacterHairFlowResult flow = CharacterHairFlow(input, inputData.normalWS);
    float3 strandWS = flow.strandWS;
    float flowWeight = flow.weight;
    // Keep one initialized URP GI/lighting path. Zero weight is already an
    // exact stock per-light fallback in CharacterHairDirect; an early nested
    // full-PBR return is redundant and is the reported Vulkan warning site.
    #if defined(_SPECULARHIGHLIGHTS_OFF)
    bool specularHighlightsOff = true;
    #else
    bool specularHighlightsOff = false;
    #endif
    BRDFData brdfData;

    // NOTE: can modify "surfaceData"...
    InitializeBRDFData(surfaceData, brdfData);

    #if defined(DEBUG_DISPLAY)
    half4 debugColor;

    if (CanDebugOverrideOutputColor(inputData, surfaceData, brdfData, debugColor))
    {
        return debugColor;
    }
    #endif

    // Clear-coat calculation...
    BRDFData brdfDataClearCoat = CreateClearCoatBRDFData(surfaceData, brdfData);
    half4 shadowMask = CalculateShadowMask(inputData);
    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData, surfaceData);
    uint meshRenderingLayers = GetMeshRenderingLayer();
    Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);

    // NOTE: We don't apply AO to the GI here because it's done in the lighting calculation below...
    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI);

    LightingData lightingData = CreateLightingData(inputData, surfaceData);

    lightingData.giColor = GlobalIllumination(brdfData, brdfDataClearCoat, surfaceData.clearCoatMask,
                                              inputData.bakedGI, aoFactor.indirectAmbientOcclusion, inputData.positionWS,
                                              inputData.normalWS, inputData.viewDirectionWS, inputData.normalizedScreenSpaceUV);
#ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
#endif
    {
        lightingData.mainLightColor = CharacterHairDirect(brdfData, brdfDataClearCoat,
                                                              mainLight,
                                                              inputData.normalWS, inputData.viewDirectionWS,
                                                              surfaceData.clearCoatMask, specularHighlightsOff, strandWS, flowWeight);
    }

    #if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();

    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK

        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);

#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
        {
            lightingData.additionalLightsColor += CharacterHairDirect(brdfData, brdfDataClearCoat, light,
                                                                          inputData.normalWS, inputData.viewDirectionWS,
                                                                          surfaceData.clearCoatMask, specularHighlightsOff, strandWS, flowWeight);
        }
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);

#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
        {
            lightingData.additionalLightsColor += CharacterHairDirect(brdfData, brdfDataClearCoat, light,
                                                                          inputData.normalWS, inputData.viewDirectionWS,
                                                                          surfaceData.clearCoatMask, specularHighlightsOff, strandWS, flowWeight);
        }
    LIGHT_LOOP_END
    #endif

    #if defined(_ADDITIONAL_LIGHTS_VERTEX)
    lightingData.vertexLightingColor += inputData.vertexLighting * brdfData.diffuse;
    #endif

#if REAL_IS_HALF
    // Clamp any half.inf+ to HALF_MAX
    return min(CalculateFinalColor(lightingData, surfaceData.alpha), HALF_MAX);
#else
    return CalculateFinalColor(lightingData, surfaceData.alpha);
#endif
}
#endif
