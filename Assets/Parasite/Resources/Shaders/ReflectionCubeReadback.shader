Shader "Hidden/Parasite/ReflectionCubeReadback"
{
    Properties
    {
        _ReflectionCube("Reflection cube", Cube) = "" {}
        _CubeHDR("HDR decode", Vector) = (1,1,0,0)
        _Mip("Mip", Float) = 0
        _SampleScale("Sample region", Float) = 1
        _Face("Face", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
            TEXTURECUBE(_ReflectionCube); SAMPLER(sampler_ReflectionCube);
            CBUFFER_START(UnityPerMaterial)
                float4 _CubeHDR;
                float _Mip;
                float _SampleScale;
                float _Face;
            CBUFFER_END
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(uint id:SV_VertexID)
            {
                Varyings output;
                output.positionCS=GetFullScreenTriangleVertexPosition(id);
                output.uv=GetFullScreenTriangleTexCoord(id);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 p=(input.uv-.5)*2*_SampleScale;
                float3 direction;
                if(_Face==0) direction=float3(1,-p.y,-p.x);
                else if(_Face==1) direction=float3(-1,-p.y,p.x);
                else if(_Face==2) direction=float3(p.x,1,p.y);
                else if(_Face==3) direction=float3(p.x,-1,-p.y);
                else if(_Face==4) direction=float3(p.x,-p.y,1);
                else direction=float3(-p.x,-p.y,-1);
                half4 encoded=SAMPLE_TEXTURECUBE_LOD(_ReflectionCube,sampler_ReflectionCube,normalize(direction),_Mip);
                return half4(DecodeHDREnvironment(encoded,_CubeHDR),encoded.a);
            }
            ENDHLSL
        }
    }
}
