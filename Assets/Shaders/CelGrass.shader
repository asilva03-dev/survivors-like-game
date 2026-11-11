// Custom/CelGrass
// Ground decorations (2026-11-10): grass tufts (and later flowers) baked into world-space tile meshes by
// GroundDecorations. Cel lighting like Custom/CelLit (moon banded with shadows, point lights banded, smooth ambient),
// colour from the mesh's vertex colours, every surface lit as if facing up (flat, even tufts).
// - Bending: uv0.x = how much a vertex may move (0 at the root, 1 at the tip); uv1 = the tuft's root (world X, Z).
//   Tufts within _BendRadius of _TD_PlayerPos (a global set by GroundDecorations) lean away from the player by up to
//   _BendStrength, and every tuft sways with a slow wind (_WindStrength, _WindSpeed).
// - Only a forward pass (no depth/normals/shadow passes): drawn on the Decoration layer by a Render Objects feature after
//   the outline pass and before the darkness pass, so tufts get no outlines but still fade into the dark. Double-sided.
Shader "Custom/CelGrass"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1,1,1,1)
        [Header(Cel)]
        _Bands ("Bands", Range(1, 4)) = 1
        _Softness ("Band Edge Softness", Range(0.001, 0.5)) = 0.005
        _Threshold ("Lit Threshold", Range(0, 1)) = 0.5
        _ShadowFloor ("Shadow Band Brightness", Range(0, 1)) = 0.3
        _PointBands ("Point Light Bands", Range(1, 6)) = 3
        _PointMax ("Point Light Full Brightness", Range(0.05, 4)) = 2
        _PointSoftness ("Point Light Band Softness (0.5 = smooth)", Range(0.001, 0.5)) = 0.5
        [Header(Bending)]
        _BendRadius ("Bend Radius", Float) = 1.4
        _BendStrength ("Bend Strength", Float) = 0.6
        _WindStrength ("Wind Strength", Float) = 0.06
        _WindSpeed ("Wind Speed", Float) = 1.3
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Bands;
                half _Softness;
                half _Threshold;
                half _ShadowFloor;
                half _PointBands;
                half _PointMax;
                half _PointSoftness;
                float _BendRadius;
                float _BendStrength;
                float _WindStrength;
                float _WindSpeed;
            CBUFFER_END

            float4 _TD_PlayerPos;

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 root : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 color : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float weight = input.uv.x;

                // Lean away from the player, strongest at the root's centre.
                float2 away = input.root - _TD_PlayerPos.xz;
                float distance = length(away);
                float push = saturate(1.0 - distance / max(_BendRadius, 0.01)) * _BendStrength * weight;
                float2 direction = distance > 0.001 ? away / distance : float2(0, 1);
                positionWS.xz += direction * push;
                positionWS.y -= push * 0.6;

                // Slow wind sway, out of phase across the map.
                float phase = _Time.y * _WindSpeed + input.root.x * 0.35 + input.root.y * 0.23;
                positionWS.xz += float2(sin(phase), cos(phase * 0.8)) * _WindStrength * weight;

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.color = input.color.rgb * _BaseColor.rgb;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half Band(half x, half bands, half softness)
            {
                half t = saturate(x) * bands;
                half step = floor(t);
                half edge = smoothstep(0.5 - softness, 0.5 + softness, t - step);
                return saturate((step + edge) / bands);
            }

            half3 PointLight(Light light, float3 normalWS)
            {
                half ndl = dot(normalWS, light.direction) * 0.5 + 0.5;
                half3 colour = light.color * (light.distanceAttenuation * light.shadowAttenuation * ndl);
                half brightness = max(colour.r, max(colour.g, colour.b));
                if (brightness <= 0.0001)
                {
                    return 0;
                }
                half banded = Band(brightness / _PointMax, _PointBands, _PointSoftness) * _PointMax;
                return colour * (banded / brightness);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = float3(0, 1, 0);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                half4 shadowMask = half4(1, 1, 1, 1);
                Light mainLight = GetMainLight(shadowCoord, input.positionWS, shadowMask);

                half halfLambert = dot(normalWS, mainLight.direction) * 0.5 + 0.5;
                half lit = halfLambert * mainLight.shadowAttenuation;
                half shifted = saturate(lit - _Threshold + 0.5 / max(_Bands, 1));
                half band = Band(shifted, _Bands, _Softness);
                half3 lighting = mainLight.color * mainLight.distanceAttenuation * lerp(_ShadowFloor, 1.0, band);

                #if defined(_ADDITIONAL_LIGHTS)
                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
                        lighting += PointLight(light, normalWS);
                    LIGHT_LOOP_END
                #endif

                half3 colour = input.color * (lighting + SampleSH(normalWS));
                colour = MixFog(colour, input.fogFactor);
                return half4(colour, 1);
            }
            ENDHLSL
        }
    }
}
