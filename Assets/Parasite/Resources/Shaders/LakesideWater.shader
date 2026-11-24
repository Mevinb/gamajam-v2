// Film-set water: animated world-space ripple normals, Fresnel sky reflections, a physical sun lobe and shore foam.
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
            #pragma target 3.5
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
                float clock = _QuietTime;
                float a = p.x * 3.6 + p.y * 6.4 - clock * .68;
                float b = p.x * -7.3 + p.y * 4.6 + clock * .43;
                float c = p.y * 18.0 + sin(p.x * 2.2) - clock * 1.1;
                float3 normal = normalize(float3(.018*cos(a)*3.6 - .007*cos(b)*7.3,
                    1, .018*cos(a)*6.4 + .007*cos(b)*4.6 + .0025*cos(c)*18));
                float3 view = SafeNormalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                float fresnel = .02 + .98 * pow(1-saturate(dot(view,normal)),5);
                float3 reflected = reflect(-view,normal);
                float elevation = saturate(reflected.y * 2.5);
                float3 horizon = lerp(float3(.72,.39,.27),float3(.32,.23,.37),_Intent);
                float3 sky = lerp(horizon, float3(.16,.27,.45),pow(elevation,.75));
                float3 deep = lerp(float3(.035,.12,.14),float3(.035,.065,.10),_Intent);
                float3 color = lerp(deep,sky,fresnel*.9+.13);
                // The distant disc is at (16,4.7,70) relative to this temporary scene's origin.
                float3 light = normalize(float3(16,4.7,70));
                float3 halfDirection = SafeNormalize(view+light);
                float highlight = pow(saturate(dot(normal,halfDirection)),550);
                float glitter = pow(saturate(sin(a*2.1+c)*.5+.5),5);
                float3 sun = lerp(float3(1.0,.66,.29),float3(.39,.84,.56),_Intent);
                color += sun * highlight * (2.5+glitter*2.0);
                float shoreline = .85+.22*sin(p.x*.8)+.16*sin(p.x*2.1);
                float shoreDistance = p.y-shoreline;
                float foam = exp(-abs(shoreDistance-.07*sin(clock*.4+p.x))*9);
                foam *= .25+.2*sin(p.x*16+clock*.35)*sin(p.x*7-p.y*8);
                color = lerp(color,float3(.6,.61,.53),saturate(foam)*.5);
                // Fine waves progressively filter out with distance, preventing a noisy horizon.
                color += .005 * sin(c) * saturate(1-length(p)/85);
                return half4(max(color,0),1);
            }
            ENDHLSL
        }
    }
}
