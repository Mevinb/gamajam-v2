// PARASITE — photo facade with interior-mapped lit windows. Same inputs as URP Lit (so CityAssets sets it up the
// same way, and the depth/shadow passes are Lit's own), plus: every lit window (emission map > 0) opens onto a
// small room behind the glass — the facade texture is a 6 x 6 grid of bays x floors and each cell gets a box room
// (back wall lit by the window's own emission colour, darker side walls, a floor and a glowing ceiling, plus a
// per-room hash for blinds / a furniture silhouette / a half-drawn curtain), so the windows show parallax depth.
Shader "Parasite/Facade"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _GlossMapScale("Smoothness Scale", Range(0.0, 1.0)) = 1.0
        _SmoothnessTextureChannel("Smoothness texture channel", Float) = 0
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _MetallicGlossMap("Metallic", 2D) = "white" {}
        _SpecColor("Specular", Color) = (0.2, 0.2, 0.2)
        _SpecGlossMap("Specular", 2D) = "white" {}
        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1.0
        _BumpScale("Scale", Float) = 1.0
        _BumpMap("Normal Map", 2D) = "bump" {}
        _Parallax("Scale", Range(0.005, 0.08)) = 0.005
        _ParallaxMap("Height Map", 2D) = "black" {}
        _OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
        _OcclusionMap("Occlusion", 2D) = "white" {}
        [HDR] _EmissionColor("Color", Color) = (0,0,0)
        _EmissionMap("Emission", 2D) = "white" {}
        _DetailMask("Detail Mask", 2D) = "white" {}
        _DetailAlbedoMapScale("Scale", Range(0.0, 2.0)) = 1.0
        _DetailAlbedoMap("Detail Albedo x2", 2D) = "linearGrey" {}
        _DetailNormalMapScale("Scale", Range(0.0, 2.0)) = 1.0
        [Normal] _DetailNormalMap("Normal Map", 2D) = "bump" {}
        _ClearCoatMask("_ClearCoatMask", Float) = 0.0
        _ClearCoatSmoothness("_ClearCoatSmoothness", Float) = 0.0
        _Surface("__surface", Float) = 0.0
        _Blend("__blend", Float) = 0.0
        _Cull("__cull", Float) = 2.0
        [ToggleUI] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _BlendModePreserveSpecular("_BlendModePreserveSpecular", Float) = 1.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0
        [ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1.0
        _QueueOffset("Queue offset", Float) = 0.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Lit" "IgnoreProjector" = "True" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull[_Cull]
            ZWrite[_ZWrite]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LitPassVertex
            #pragma fragment FacadeFragment

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local_fragment _EMISSION
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

            float RoomHash(float2 p) { float3 p3 = frac(float3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return frac((p3.x + p3.y) * p3.z); }

            // ray from the window plane into a unit room (x, y in 0..1 across the cell, z 0 .. depth behind the glass)
            half3 InteriorRoom(float2 cell, float2 cellId, half3 viewTS, half3 light)
            {
                float depth = 0.9;
                float3 ro = float3(cell, 0);
                float3 rd = normalize(float3(-viewTS.xy, max(viewTS.z, 0.05)));     // view ray pointing into the wall
                rd.xy *= float2(1.0, 0.85);                                           // cells are a little wider than tall
                float3 inv = 1.0 / max(abs(rd), 1e-4) * sign(rd + 1e-6);
                float tx = ((rd.x > 0 ? 1.0 : 0.0) - ro.x) * inv.x;
                float ty = ((rd.y > 0 ? 1.0 : 0.0) - ro.y) * inv.y;
                float tz = depth * inv.z;
                float t = min(min(tx, ty), tz);
                float3 hp = ro + rd * t;
                float h = RoomHash(cellId);
                float h2 = RoomHash(cellId + 17.31);
                half3 c;
                if (t == tz)                                                          // back wall
                {
                    half v = 0.75 + 0.25 * hp.y;
                    c = light * v * lerp(0.75, 1.05, h2);
                    // furniture silhouette (desk / sofa / shelf) against the lit back wall
                    float fx0 = 0.1 + h * 0.4, fw = 0.25 + h2 * 0.35, fh = 0.22 + h * 0.3;
                    if (hp.x > fx0 && hp.x < fx0 + fw && hp.y < fh) c *= 0.25;
                }
                else if (t == ty)                                                     // floor / ceiling
                {
                    half z = saturate(hp.z / depth);
                    c = rd.y > 0 ? light * (1.25 - 0.4 * z) : light * (0.32 + 0.22 * (1.0 - z));
                }
                else                                                                  // side walls
                {
                    half z = saturate(hp.z / depth);
                    c = light * (0.42 + 0.3 * (1.0 - z)) * (0.9 + 0.2 * hp.y);
                }
                // closed venetian blinds in some rooms, a half-drawn curtain in others
                if (h > 0.82) c *= 0.55 + 0.45 * step(0.5, frac(cell.y * 22.0));
                else if (h < 0.18 && cell.x > 0.62) c *= 0.35;
                return c;
            }

            void FacadeFragment(Varyings input, out half4 outColor : SV_Target0)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            #if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
                half3 viewDirTS = GetViewDirectionTangentSpace(input.tangentWS, input.normalWS, viewDirWS);
            #elif defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
                half3 viewDirTS = input.viewDirTS;
            #else
                half3 viewDirTS = half3(0, 0, 1);
            #endif
                float2 uvRoom = input.uv;
            #if defined(_PARALLAXMAP)
                ApplyPerPixelDisplacement(viewDirTS, input.uv);
            #endif

                SurfaceData surfaceData;
                InitializeStandardLitSurfaceData(input.uv, surfaceData);

            #if defined(_EMISSION)
                // lit window texels: replace the flat emission with the room behind the glass
                half3 em = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uvRoom).rgb;
                half lum = max(em.r, max(em.g, em.b));
                if (lum > 0.02)
                {
                    float2 g = uvRoom * 6.0;
                    float2 cellId = floor(g);
                    float2 cell = frac(g);
                    half3 light = em * _EmissionColor.rgb;
                    half3 room = InteriorRoom(cell, cellId, viewDirTS, light);
                    half edge = smoothstep(0.02, 0.12, lum);
                    surfaceData.emission = lerp(surfaceData.emission, room, edge);
                    surfaceData.albedo *= 1.0 - 0.85 * edge;                     // glass: the room, not the photo, shows
                    surfaceData.smoothness = lerp(surfaceData.smoothness, 0.9, edge);
                }
            #endif

                InputData inputData;
                InitializeInputData(input, surfaceData.normalTS, inputData);
                InitializeBakedGIData(input, inputData);
                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                outColor = half4(color.rgb, 1);
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
