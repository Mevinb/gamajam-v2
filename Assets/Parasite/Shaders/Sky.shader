// PARASITE — dusk skybox: HDRI cubemap with exposure/tint/rotation,
// a graded zenith and a below-horizon haze so the puresky's fake ground never shows behind the skyline.
Shader "Parasite/Sky"
{
    Properties
    {
        [NoScaleOffset] _Tex ("HDRI Cubemap", Cube) = "grey" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Exposure ("Exposure", Range(0, 8)) = 1
        _Rotation ("Rotation (deg)", Range(0, 360)) = 0
        _DriftSpeed ("HDRI Rotation Rate (deg/s)", Range(0, 1)) = 0
        _ZenithColor ("Zenith Grade", Color) = (0.55, 0.6, 0.85, 1)
        _ZenithAmount ("Zenith Grade Amount", Range(0, 1)) = 0.35
        _HorizonColor ("Horizon / Ground Haze", Color) = (0.45, 0.42, 0.5, 1)
        _HorizonHeight ("Horizon Haze Height", Range(0.001, 0.5)) = 0.06
        _Saturation ("Saturation", Range(0, 2)) = 1.1
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"

            TEXTURECUBE(_Tex); SAMPLER(sampler_Tex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Exposure;
                float _Rotation;
                float _DriftSpeed;
                half4 _ZenithColor;
                half _ZenithAmount;
                half4 _HorizonColor;
                half _HorizonHeight;
                half _Saturation;
            CBUFFER_END
            float4 _Tex_HDR;

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            float3 RotateY(float3 v, float deg)
            {
                float a = deg * PI / 180.0, s = sin(a), c = cos(a);
                return float3(c * v.x - s * v.z, v.y, s * v.x + c * v.z);
            }

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 sd = RotateY(d, _Rotation + _Time.y * _DriftSpeed);
                half4 tex = SAMPLE_TEXTURECUBE_LOD(_Tex, sampler_Tex, sd, 0);
                half3 c = DecodeHDREnvironment(tex, _Tex_HDR) * _Tint.rgb * _Exposure;
                half lum = dot(c, half3(0.2126, 0.7152, 0.0722));
                c = max(0, lerp(lum.xxx, c, _Saturation));
                half up = saturate(d.y);
                c *= lerp(half3(1, 1, 1), _ZenithColor.rgb * 1.6, _ZenithAmount * up * up);
                half below = saturate(-d.y / _HorizonHeight);
                half nearH = 1.0 - saturate(abs(d.y) / _HorizonHeight);
                c = lerp(c, _HorizonColor.rgb * (lum * 0.5 + 0.5), saturate(below + nearH * 0.25));
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
