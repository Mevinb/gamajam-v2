Shader "Parasite/LakesideWater"
{
    Properties { _QuietTime("Quiet time", Float) = 0 _Intent("Intent", Range(0,1)) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                float _QuietTime;
                float _Intent;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.positionWS.xz;
                float horizon = saturate((p.y + 4) / 100);
                float3 deep = lerp(float3(.16,.23,.30), float3(.12,.18,.23), _Intent);
                float3 far = lerp(float3(.65,.44,.40), float3(.31,.24,.33), _Intent);
                float3 color = lerp(deep, far, pow(horizon, .65));
                float ripple = sin(p.y * 9 + sin(p.x * 2.3) + _QuietTime * .45);
                float quiet = sin(p.x * 1.8 + p.y * 3 - _QuietTime * .2);
                color += (ripple + quiet) * .012;
                float sunAxis = lerp(.6, 16, saturate(p.y / 70));
                float streak = exp(-abs(p.x - sunAxis) / (.12 + max(0,p.y) * .017));
                streak *= pow(saturate(ripple * .5 + .5), 9) * saturate(p.y / 4);
                float3 reflection = lerp(float3(1,.70,.32), float3(.40,.80,.55), _Intent);
                color += reflection * streak * .7;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
