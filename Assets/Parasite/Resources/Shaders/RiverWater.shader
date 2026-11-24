// Depth-writing river water: animated flow normals, Fresnel-filtered local HDR reflections and real lamp/sun lighting.
// Opaque water keeps the city atmosphere's depth reconstruction stable; reflections come from the local river probe.
Shader "Parasite/RiverWater"
{
    Properties
    {
        _BaseColor("Clean water scattering", Color) = (0.025, 0.105, 0.115, 1)
        _PollutedColor("Runoff scattering", Color) = (0.075, 0.09, 0.045, 1)
        _CleanWater("Floodgate isolated", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = 0.94
        _WaveStrength("Flow normal strength", Range(0,2)) = 0.7
        _FlowSpeed("Tidal flow speed", Range(0,3)) = 0.65
        _ShoreZ("Quay shoreline Z", Float) = -38.2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Back ZWrite On ZTest LEqual
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _PollutedColor;
            half _CleanWater;
            half _Smoothness;
            half _WaveStrength;
            float _FlowSpeed;
            float _ShoreZ;
        CBUFFER_END
        struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half fog : TEXCOORD1;
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings Vert(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.fog = ComputeFogFactor(output.positionCS.z);
            return output;
        }
        float Hash(float2 p) { float3 q = frac(float3(p.xyx) * 0.1031); q += dot(q,q.yzx+33.33); return frac((q.x+q.y)*q.z); }
        float Noise(float2 p)
        {
            float2 a=floor(p), b=frac(p); b=b*b*(3-2*b);
            return lerp(lerp(Hash(a),Hash(a+float2(1,0)),b.x),lerp(Hash(a+float2(0,1)),Hash(a+1),b.x),b.y);
        }
        half3 FlowNormal(float3 positionWS)
        {
            float2 p = positionWS.xz;
            float t = _Time.y * _FlowSpeed;
            float a = dot(p, float2(0.78,1.25)) - t;
            float b = dot(p, float2(-1.9,1.45)) + t * 0.73;
            float c = dot(p, float2(4.4,6.3)) - t * 1.9;
            // Screen derivatives filter fine wave slopes before they alias into glitter at grazing distance.
            float footprint = max(length(ddx(p)),length(ddy(p)));
            float fine = 1.0 / (1.0 + footprint * 8.0);
            float2 slope = cos(a) * float2(.028,.045) + cos(b) * float2(-.021,.016);
            slope += cos(c) * float2(.018,.025) * fine;
            return normalize(half3(-slope.x * _WaveStrength, 1, -slope.y * _WaveStrength));
        }
        ENDHLSL
        Pass
        {
            Name "RiverForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #define _SPECULAR_SETUP 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 pw = input.positionWS;
                half3 N = FlowNormal(pw);
                half3 V = GetWorldSpaceNormalizeViewDir(pw);
                // Only the plume near the floodgate outfall changes after isolation; the entire river does not
                // magically turn clean. Flow noise breaks up its edge without a translucent decal rectangle.
                float runoff = (1-_CleanWater) * exp(-abs(pw.x-215.0) * .075) * exp(-abs(pw.z-_ShoreZ) * .04);
                half3 scattering = lerp(_BaseColor.rgb, _PollutedColor.rgb,runoff*.7);
                float t = _Time.y * _FlowSpeed;
                scattering *= lerp(.82,1.08,Noise(pw.xz * .17 + float2(t * .05,0)));
                half NoV = saturate(dot(N,V));
                half fresnel = .02h + .98h * pow(1.0h-NoV,5.0h);
                half3 R = reflect(-V,N);
                half roughness = 1.0h - _Smoothness;
                // Long waves remain specular; subpixel slopes broaden the reflection rather than flickering.
                roughness = saturate(roughness + min(length(fwidth(N)) * .5h,.16h));
                half3 reflected = GlossyEnvironmentReflection(R,pw,sqrt(roughness),1.0h,GetNormalizedScreenSpaceUV(input.positionCS));

                BRDFData brdf;
                half alpha = 1;
                InitializeBRDFData(scattering * .35h,0,half3(.02h,.02h,.02h),_Smoothness,alpha,brdf);
                InputData inputData = (InputData)0;
                inputData.positionWS = pw;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                half4 shadowMask = half4(1,1,1,1);
                Light sun = GetMainLight(TransformWorldToShadowCoord(pw),pw,shadowMask);
                half3 direct = LightingPhysicallyBased(brdf,sun,N,V);
                #if defined(_ADDITIONAL_LIGHTS)
                    uint count = GetAdditionalLightsCount();
                    #if USE_CLUSTER_LIGHT_LOOP
                        UNITY_LOOP for(uint li=0;li<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);li++)
                            direct += LightingPhysicallyBased(brdf,GetAdditionalLight(li,pw,shadowMask),N,V);
                    #endif
                    LIGHT_LOOP_BEGIN(count)
                        direct += LightingPhysicallyBased(brdf,GetAdditionalLight(lightIndex,pw,shadowMask),N,V);
                    LIGHT_LOOP_END
                #endif
                half3 color = reflected * fresnel + scattering * SampleSH(N) * (1-fresnel) + direct;
                float nearQuay = exp(-abs(pw.z-_ShoreZ) * 3.3);
                float streak = Noise(float2(pw.x*1.7-t*.14,pw.z*4+t*.08));
                half foam = saturate((streak-.48)*2.6) * nearQuay;
                half3 foamColor = lerp(half3(.28,.31,.22),half3(.53,.59,.56),_CleanWater);
                color = lerp(color,foamColor * (SampleSH(N)+sun.color*.16),foam*.5h);
                return half4(MixFog(max(color,0),input.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Depth
            #pragma multi_compile_instancing
            half Depth(Varyings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Normals
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 Normals(Varyings input) : SV_Target
            {
                float3 normal = FlowNormal(input.positionWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(normal);
                    return half4(PackFloat2To888(saturate(oct*.5+.5)),0);
                #else
                    return half4(normal,0);
                #endif
            }
            ENDHLSL
        }
    }
}
