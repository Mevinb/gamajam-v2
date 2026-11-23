// PARASITE — fake volumetric light cone under street lamps / fixtures (additive, no depth write).
// Mesh: open cone, uv.y = 0 at the lamp .. 1 at the ground. Brightness falls off along the cone and towards
// silhouette edges (view-dependent), soft-fades where it meets geometry (depth texture) and near the camera,
// with slow drifting dust noise so it reads as hazy air rather than a plastic cone.
Shader "Parasite/LightCone"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (1.0, 0.72, 0.42, 1)
        _Intensity ("Intensity", Range(0, 4)) = 0.35
        _EdgePower ("Edge Softness", Range(0.5, 8)) = 2.2
        _LengthFalloff ("Length Falloff", Range(0.2, 6)) = 1.6
        _DepthFade ("Depth Fade (m)", Range(0.05, 4)) = 0.8
        _CameraFade ("Camera Fade (m)", Range(0.1, 10)) = 2.5
        _Dust ("Dust Noise", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+5" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Off
        Pass
        {
            Name "Cone"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _EdgePower;
                half _LengthFalloff;
                half _DepthFade;
                half _CameraFade;
                half _Dust;
            CBUFFER_END
            float _ParasiteConeScale;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 screen : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.screen = ComputeScreenPos(o.positionCS);
                return o;
            }

            float Hash(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            float Noise(float3 x)
            {
                float3 i = floor(x), f = frac(x); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x), lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x), lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            half4 Frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                half3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half3 N = normalize(i.normalWS) * (front ? 1 : -1);
                half facing = saturate(abs(dot(N, V)));
                half edge = pow(facing, _EdgePower);
                half along = pow(saturate(1.0 - i.uv.y), _LengthFalloff) * smoothstep(0.0, 0.08, i.uv.y);
                float2 suv = i.screen.xy / i.screen.w;
                float sceneZ = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float z = i.screen.w;
                half soft = saturate((sceneZ - z) / _DepthFade);
                half cam = saturate((z - 0.3) / _CameraFade);
                float n = Noise(i.positionWS * 1.7 + float3(0, -_Time.y * 0.25, _Time.y * 0.1)) * 0.6 + Noise(i.positionWS * 4.3 - _Time.y * 0.3) * 0.4;
                half dust = lerp(1.0, n * 1.6, _Dust);
                float scale = _ParasiteConeScale > 0 ? _ParasiteConeScale : 1.0;
                half a = edge * along * soft * cam * dust * _Intensity * scale;
                // half intensity for back faces (both sides of the cone are drawn)
                a *= front ? 1.0 : 0.6;
                return half4(_Color.rgb * a, 0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
