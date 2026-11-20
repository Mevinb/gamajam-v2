// PARASITE — glowing nucleus / organelles inside the amoeba. Opaque, unlit, emissive, self-pulsing.
// Drawn in the opaque queue so the translucent "Parasite/Amoeba" shell blends over it.
Shader "Parasite/AmoebaNucleus"
{
    Properties
    {
        [HDR] _Color ("Glow Colour", Color) = (0.75, 1.8, 0.45, 1)
        _EdgeColor ("Edge Colour", Color) = (0.05, 0.35, 0.08, 1)
        _Intensity ("Intensity", Range(0, 8)) = 1.6
        _PulseRate ("Pulse Rate", Range(0, 12)) = 3.0
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.45
        _Phase ("Phase", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" "RenderType" = "Opaque" }
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half4 _EdgeColor;
            half _Intensity;
            float _PulseRate;
            half _PulseAmount;
            float _Phase;
        CBUFFER_END

        // Shared by every pass so depth prepass / depth priming (ZTest Equal) matches exactly.
        float3 NucleusPositionWS(float3 positionOS)
        {
            float t = _Time.y * _PulseRate + _Phase;
            float3 p = positionOS * (1.0 + sin(t) * 0.06 + sin(t * 2.3 + positionOS.y * 9.0) * 0.03);
            return TransformObjectToWorld(p);
        }

        struct DepthAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS   : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct DepthVaryings
        {
            float4 positionCS : SV_POSITION;
            float3 normalWS   : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        DepthVaryings DepthVert(DepthAttributes input)
        {
            DepthVaryings o = (DepthVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.positionCS = TransformWorldToHClip(NucleusPositionWS(input.positionOS.xyz));
            o.normalWS = TransformObjectToWorldNormal(input.normalOS);
            return o;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardNucleus"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                half   fogFactor  : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = NucleusPositionWS(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 N = normalize(input.normalWS);
                half3 V = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half facing = saturate(dot(N, V));
                half pulse = 1.0h + _PulseAmount * sin(_Time.y * _PulseRate + _Phase);
                half3 col = lerp(_EdgeColor.rgb, _Color.rgb * _Intensity * pulse, pow(facing, 1.5h));
                col = MixFog(col, input.fogFactor);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            half DepthFrag(DepthVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment NormalsFrag
            #pragma multi_compile_instancing

            half4 NormalsFrag(DepthVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
