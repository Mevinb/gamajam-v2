// PARASITE — full-screen atmospheric perspective for the URP FullScreenPassRendererFeature (after transparents,
// before post-processing). Exponential height fog + distance haze with sun in-scattering, a warmer low layer lit
// by the city, and horizon haze over the sky so the skyline melts into the dusk instead of ending at a hard edge.
// Crepuscular rays: a short screen-space march towards the sun counts open sky between the towers.
Shader "Parasite/Atmosphere"
{
    Properties
    {
        _FogColor ("Haze Colour (high)", Color) = (0.42, 0.44, 0.56, 1)
        _FogColorLow ("Haze Colour (street level)", Color) = (0.52, 0.42, 0.40, 1)
        [HDR] _SunColor ("Sun In-scatter", Color) = (1.6, 0.85, 0.45, 1)
        _SunPower ("Sun Lobe Power", Range(1, 64)) = 7
        _SunStrength ("Sun Lobe Strength", Range(0, 2)) = 0.8
        _Density ("Height Fog Density", Range(0, 0.1)) = 0.012
        _HeightFalloff ("Height Falloff (1/m)", Range(0.001, 0.5)) = 0.045
        _BaseHeight ("Base Height", Float) = 0
        _DistanceDensity ("Distance Haze Density", Range(0, 0.02)) = 0.0016
        _StartDistance ("Start Distance", Float) = 12
        _MaxOpacity ("Max Opacity", Range(0, 1)) = 0.88
        _SkyHaze ("Sky Horizon Haze", Range(0, 1)) = 0.55
        _SkyHazeFalloff ("Sky Haze Falloff", Range(1, 40)) = 9
        _LowLayerHeight ("Low Layer Height (m)", Float) = 14
        _ShaftStrength ("Sun Shafts", Range(0, 2)) = 0.6
        _ShaftDecay ("Shaft Decay", Range(0.8, 1)) = 0.95
        _ShaftLength ("Shaft Length (screen)", Range(0.1, 1)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off
        Pass
        {
            Name "Atmosphere"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FogColor;
                half4 _FogColorLow;
                half4 _SunColor;
                half _SunPower;
                half _SunStrength;
                float _Density;
                float _HeightFalloff;
                float _BaseHeight;
                float _DistanceDensity;
                float _StartDistance;
                half _MaxOpacity;
                half _SkyHaze;
                half _SkyHazeFalloff;
                float _LowLayerHeight;
                half _ShaftStrength;
                half _ShaftDecay;
                half _ShaftLength;
            CBUFFER_END
            float _ParasiteShaftScale;   // graphics preset (0 = off, unset/negative = 1)
            // runtime overrides (0 = use material): global multiplier for density from presets / weather
            float _ParasiteFogScale;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float raw = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    bool sky = raw < 1e-6;
                #else
                    bool sky = raw > 1.0 - 1e-6;
                #endif
                float3 pw = ComputeWorldSpacePosition(uv, sky ? 0.5 : raw, UNITY_MATRIX_I_VP);
                float3 ro = _WorldSpaceCameraPos;
                float3 toP = pw - ro;
                float dist = length(toP);
                float3 rd = toP / max(dist, 1e-4);
                float3 L = normalize(_MainLightPosition.xyz);
                half sunLobe = pow(saturate(dot(rd, L)), _SunPower) * _SunStrength;
                float scale = _ParasiteFogScale > 0 ? _ParasiteFogScale : 1.0;

                half amount;
                half3 haze;
                if (sky)
                {
                    float up = rd.y;
                    amount = _SkyHaze * (up > 0 ? exp(-up * _SkyHazeFalloff) : 1.0);
                    haze = _FogColor.rgb + _SunColor.rgb * sunLobe * 0.6;
                }
                else
                {
                    float d = max(0.0, dist - _StartDistance);
                    float a = _Density * scale, b = _HeightFalloff;
                    float h = ro.y - _BaseHeight + rd.y * min(dist, _StartDistance);
                    float k = rd.y * b;
                    float heightTerm = abs(k) > 1e-4 ? (1.0 - exp(-d * k)) / k : d;
                    float od = a * exp(-h * b) * heightTerm + d * _DistanceDensity * scale;
                    amount = 1.0 - exp(-od);
                    // the haze near the streets glows warmer from the city lights; higher up it turns to dusk blue
                    half low = saturate(1.0 - (pw.y - _BaseHeight) / max(_LowLayerHeight, 0.1));
                    haze = lerp(_FogColor.rgb, _FogColorLow.rgb, low * low) + _SunColor.rgb * sunLobe;
                }
                amount = min(amount, _MaxOpacity);
                half3 outCol = lerp(col.rgb, haze, amount);

                // sun shafts between the buildings
                float shaftScale = _ParasiteShaftScale < 0 ? 0.0 : (_ParasiteShaftScale > 0 ? _ParasiteShaftScale : 1.0);
                float3 camFwd = -UNITY_MATRIX_V[2].xyz;
                half facingSun = saturate(dot(camFwd, L) * 1.6 - 0.2);
                if (_ShaftStrength * shaftScale * facingSun > 0.001)
                {
                    float4 sunCS = mul(UNITY_MATRIX_VP, float4(ro + L * 1000.0, 1.0));
                    // same uv convention as ComputeWorldSpacePosition (UNITY_UV_STARTS_AT_TOP flips y)
                    float2 sunUV = (sunCS.xy / max(sunCS.w, 1e-4)) * 0.5 + 0.5;
                    #if UNITY_UV_STARTS_AT_TOP
                        sunUV.y = 1.0 - sunUV.y;
                    #endif
                    float2 delta = (sunUV - uv) * (_ShaftLength / 24.0);
                    float2 suv = uv;
                    half illum = 0, decay = 1, total = 0;
                    UNITY_LOOP
                    for (int i = 0; i < 24; i++)
                    {
                        suv += delta;
                        if (suv.x < 0 || suv.x > 1 || suv.y < 0 || suv.y > 1) break;
                        float d = SampleSceneDepth(suv);
                        #if UNITY_REVERSED_Z
                            half open = d < 1e-6 ? 1 : 0;
                        #else
                            half open = d > 1.0 - 1e-6 ? 1 : 0;
                        #endif
                        illum += open * decay;
                        total += decay;
                        decay *= _ShaftDecay;
                    }
                    half shaft = total > 0 ? illum / total : 0;
                    float2 toSun = (uv - sunUV) * float2(_ScreenParams.x / _ScreenParams.y, 1.0);
                    half radial = saturate(1.0 - length(toSun) * 0.9);
                    half glow = shaft * radial * radial * facingSun * _ShaftStrength * shaftScale;
                    // only where geometry/sky is not already the sun disc: lift the air, not the opaque silhouettes' tops
                    outCol += _SunColor.rgb * glow * 0.35;
                }
                return half4(outCol, col.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
