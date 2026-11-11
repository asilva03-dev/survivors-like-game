// Custom/CelLit
// Cel (toon) shading for URP (2026-11-10; the user picked "2-tone hard": defaults 1 band, softness 0.005, shadow
// band 0.3, 3 point-light bands up to 2): the light on a surface is cut into flat bands instead of a smooth
// gradient.
// - Main light (the moon): half-Lambert x shadow, cut into _Bands steps with _Softness edges; the darkest band keeps
//   _ShadowFloor of the light, so shadow sides read as a flat darker tone, not black.
// - Point/spot lights (campfire, player light): each light's brightness on the surface is cut into _PointBands steps
//   (up to _PointMax), which gives the light pools hard rings. Works with Forward+ (cluster light loop).
// - Ambient (the scene's low ambient light) is added smooth, so the night look stays.
// - Has ShadowCaster, DepthOnly and DepthNormals passes so shadows, the depth copy and the darkness pass work as with
//   the Lit shader. Texture + colour like URP Lit (_BaseMap, _BaseColor).
// - Ground textures (2026-11-10, user: the ground looked plain): with "World Space Ground Textures" on (_WORLD_UV),
//   _DetailMap is tiled from above in world space every _DetailTiling units and _MacroMap every _MacroTiling units. Both
//   are linear greyscale tints around 0.5 (0.5 = no change): colour x lerp(1, 2 x sample, strength). Off by default;
//   only the ground materials turn it on.
Shader "Custom/CelLit"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        [Header(Main light)]
        _Bands ("Bands", Range(1, 4)) = 1
        _Softness ("Band Edge Softness", Range(0.001, 0.5)) = 0.005
        _Threshold ("Lit Threshold (0.5 = terminator at 90 deg)", Range(0, 1)) = 0.5
        _ShadowFloor ("Shadow Band Brightness", Range(0, 1)) = 0.3
        [Header(Point lights)]
        _PointBands ("Point Light Bands", Range(1, 6)) = 3
        _PointMax ("Point Light Full Brightness", Range(0.05, 4)) = 2
        _PointSoftness ("Point Light Band Softness (0.5 = smooth)", Range(0.001, 0.5)) = 0.5
        [Header(Ground textures)]
        [Toggle(_WORLD_UV)] _WorldUV ("World Space Ground Textures", Float) = 0
        [NoScaleOffset] _DetailMap ("Detail (greyscale, 0.5 = none)", 2D) = "grey" {}
        _DetailTiling ("Detail Tile Size (units)", Float) = 4
        _DetailStrength ("Detail Strength", Range(0, 1)) = 0.5
        [NoScaleOffset] _MacroMap ("Patches (greyscale, 0.5 = none)", 2D) = "grey" {}
        _MacroTiling ("Patch Tile Size (units)", Float) = 48
        _MacroStrength ("Patch Strength", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Bands;
            half _Softness;
            half _Threshold;
            half _ShadowFloor;
            half _PointBands;
            half _PointMax;
            half _PointSoftness;
            float _DetailTiling;
            half _DetailStrength;
            float _MacroTiling;
            half _MacroStrength;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _WORLD_UV

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_DetailMap);
            SAMPLER(sampler_DetailMap);
            TEXTURE2D(_MacroMap);
            SAMPLER(sampler_MacroMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            // x in 0..1 cut into `bands` flat steps, each edge blurred by `softness`.
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
                UNITY_SETUP_INSTANCE_ID(input);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                #if defined(_WORLD_UV)
                    float2 groundUV = input.positionWS.xz;
                    half detail = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, groundUV / max(_DetailTiling, 0.01)).r;
                    half macro = SAMPLE_TEXTURE2D(_MacroMap, sampler_MacroMap, groundUV / max(_MacroTiling, 0.01)).r;
                    albedo.rgb *= lerp(1.0, detail * 2.0, _DetailStrength) * lerp(1.0, macro * 2.0, _MacroStrength);
                #endif
                float3 normalWS = normalize(input.normalWS);

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

                // Main light: half-Lambert x shadow, shifted so _Threshold sits at the band edge, then banded.
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

                half3 ambient = SampleSH(normalWS);
                half3 colour = albedo.rgb * (lighting + ambient);
                colour = MixFog(colour, input.fogFactor);
                return half4(colour, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                return ApplyShadowClamping(positionCS);
            }

            half4 frag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(input.positionOS.xyz);
            }

            half frag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(input.normalWS), 0);
            }
            ENDHLSL
        }
    }
}
