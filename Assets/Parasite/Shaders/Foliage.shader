// PARASITE — wind-animated tree shader (bark + alpha-clipped leaf-cluster cards), hand-written URP.
// Vertex colour (from Tools/blender/environment/trees.py): R = branch sway weight, G = branch phase,
// B = leaf flutter weight. Whole-tree bending grows with height above the object pivot.
// Leaves: crown-spherical vertex normals (no back-face flip), back-light translucency, LOD cross-fade.
Shader "Parasite/Foliage"
{
    Properties
    {
        _BaseMap ("Albedo (A = coverage)", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0, 2)) = 1
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.4
        _Smoothness ("Smoothness", Range(0, 1)) = 0.3
        _Translucency ("Translucency", Range(0, 2)) = 0.7
        [HDR] _TransColor ("Translucency Colour", Color) = (0.9, 1.0, 0.45, 1)
        _AmbientBoost ("Ambient Boost", Range(0, 2)) = 1
        _WindStrength ("Wind Strength", Range(0, 2)) = 0.6
        _WindSpeed ("Wind Speed", Range(0, 4)) = 1.1
        _BranchAmp ("Branch Sway (m)", Range(0, 0.5)) = 0.12
        _FlutterAmp ("Leaf Flutter (m)", Range(0, 0.2)) = 0.04
        _WindDir ("Wind Direction (xz)", Vector) = (0.8, 0, 0.6, 0)
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip (leaves)", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _BumpScale;
            half _Cutoff;
            half _Smoothness;
            half _Translucency;
            half4 _TransColor;
            half _AmbientBoost;
            float _WindStrength;
            float _WindSpeed;
            float _BranchAmp;
            float _FlutterAmp;
            float4 _WindDir;
        CBUFFER_END

        // global gust multiplier written by the runtime (0 = calm default of the material)
        float _ParasiteWindGust;

        float3 WindOffset(float3 positionWS, float3 normalWS, float4 col)
        {
            float3 pivot = UNITY_MATRIX_M._m03_m13_m23;
            float t = _Time.y * _WindSpeed;
            float2 dir = normalize(_WindDir.xz + 1e-4);
            float objPhase = dot(pivot.xz, float2(0.131, 0.077));
            float gust = 0.55 + 0.45 * sin(_Time.y * 0.37 + objPhase * 0.5) * sin(_Time.y * 0.23 + 1.3);
            float strength = _WindStrength * (1.0 + _ParasiteWindGust) * gust;
            float h = max(0.0, positionWS.y - pivot.y);
            // whole-tree bend (quadratic with height), slow + a faster harmonic
            float bend = (sin(t + objPhase) * 0.6 + sin(t * 1.83 + objPhase * 1.7) * 0.25 + 0.55) * strength * h * h * 0.0016;
            float3 o = float3(dir.x, 0, dir.y) * bend;
            // branch sway: perpendicular + along-wind, phase per branch
            float ph = col.g * 6.2831 + objPhase;
            float bs = (sin(t * 2.3 + ph) * 0.7 + sin(t * 3.7 + ph * 1.3) * 0.3) * col.r * col.r * _BranchAmp * strength;
            o += float3(dir.x * 0.6 - dir.y * 0.8, 0.25, dir.y * 0.6 + dir.x * 0.8) * bs;
            // leaf flutter along the card normal
            float fl = sin(_Time.y * 9.0 + col.g * 41.0 + dot(positionWS, float3(1.7, 2.3, 1.1))) * col.b * _FlutterAmp * (0.4 + strength);
            o += normalWS * fl;
            return o;
        }

        half Coverage(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                half3 vertexSH : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexNormalInputs ni = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                float3 pw = TransformObjectToWorld(v.positionOS.xyz);
                pw += WindOffset(pw, ni.normalWS, v.color);
                o.positionWS = pw;
                o.positionCS = TransformWorldToHClip(pw);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.normalWS = ni.normalWS;
                o.tangentWS = half4(ni.tangentWS, v.tangentOS.w * GetOddNegativeScale());
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                o.vertexSH = SampleSH(ni.normalWS);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                #if defined(_ALPHATEST_ON)
                    clip(albedo.a - _Cutoff);
                #endif
                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(i.positionCS);
                #endif
                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                half3 n0 = normalize(i.normalWS);
                half3 t0 = normalize(i.tangentWS.xyz);
                half3 b0 = cross(n0, t0) * i.tangentWS.w;
                half3 N = normalize(mul(nTS, half3x3(t0, b0, n0)));

                InputData id = (InputData)0;
                id.positionWS = i.positionWS;
                id.positionCS = i.positionCS;
                id.normalWS = N;
                id.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                id.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                id.fogCoord = i.fogFactor;
                id.bakedGI = i.vertexSH * _AmbientBoost;
                id.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                id.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo.rgb;
                s.metallic = 0;
                s.specular = half3(0, 0, 0);
                s.smoothness = _Smoothness;
                s.normalTS = nTS;
                s.occlusion = 1;
                s.alpha = 1;
                half4 c = UniversalFragmentPBR(id, s);

                #if defined(_ALPHATEST_ON)
                    // back-lit translucency (main light through the canopy, attenuated by its shadow)
                    Light ml = GetMainLight(id.shadowCoord, id.positionWS, id.shadowMask);
                    half back = pow(saturate(dot(id.viewDirectionWS, -ml.direction) * 0.5 + 0.5), 4.0);
                    half wrap = saturate(dot(n0, -ml.direction) * 0.5 + 0.5);
                    c.rgb += albedo.rgb * _TransColor.rgb * ml.color * (ml.shadowAttenuation * 0.75 + 0.25) * (back * 1.6 + wrap * 0.25) * _Translucency;
                #endif
                c.rgb = MixFog(c.rgb, i.fogFactor);
                return half4(c.rgb, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 pw = TransformObjectToWorld(v.positionOS.xyz);
                float3 nw = TransformObjectToWorldNormal(v.normalOS);
                pw += WindOffset(pw, nw, v.color);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 ld = normalize(_LightPosition - pw);
            #else
                float3 ld = _LightDirection;
            #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(pw, nw, ld));
            #if UNITY_REVERSED_Z
                cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
            #else
                cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                o.positionCS = cs;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                    clip(Coverage(i.uv) - _Cutoff);
                #endif
                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(i.positionCS);
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 pw = TransformObjectToWorld(v.positionOS.xyz);
                pw += WindOffset(pw, TransformObjectToWorldNormal(v.normalOS), v.color);
                o.positionCS = TransformWorldToHClip(pw);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half Frag(Varyings i) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                    clip(Coverage(i.uv) - _Cutoff);
                #endif
                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(i.positionCS);
                #endif
                return i.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half3 normalWS : TEXCOORD1; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 pw = TransformObjectToWorld(v.positionOS.xyz);
                float3 nw = TransformObjectToWorldNormal(v.normalOS);
                pw += WindOffset(pw, nw, v.color);
                o.positionCS = TransformWorldToHClip(pw);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.normalWS = nw;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                    clip(Coverage(i.uv) - _Cutoff);
                #endif
                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(i.positionCS);
                #endif
                return half4(NormalizeNormalPerPixel(i.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
