// Custom/ShadowVeil
// The cloudy see-through veil around a shadow rat (2026-11-21, user: "a transparent cloudy effect over their model to
// sell them being masked in darkness"). Unlit, alpha-blended, drawn after opaques (no outline, not darkened by the
// darkness pass, like every transparent).
// - Shape: a soft blob (a smooth sphere mesh scaled around the rat). Alpha is strongest where the surface faces the
//   camera and fades to 0 at the silhouette (_EdgeFade), so there is no hard edge.
// - Clouds: 3D value noise in world space, scrolling up and drifting (_NoiseScale, _Speed), makes the density churn;
//   _Density sets how much the noise thins the veil. The vertices also wobble a little along their normals (_Wobble).
// - _Color.rgb = the veil's tint (dark violet), _Color.a = its overall opacity.
Shader "Custom/ShadowVeil"
{
    Properties
    {
        _Color ("Tint (A = opacity)", Color) = (0.12, 0.08, 0.2, 0.6)
        _EdgeFade ("Edge Fade", Range(0.2, 6)) = 1.6
        _NoiseScale ("Cloud Size (smaller = bigger clouds)", Float) = 2.5
        _Speed ("Cloud Speed", Float) = 0.5
        _Density ("Cloud Breakup (0 = solid, 1 = patchy)", Range(0, 1)) = 0.6
        _Wobble ("Wobble", Float) = 0.04
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "Veil"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _EdgeFade;
                float _NoiseScale;
                float _Speed;
                half _Density;
                float _Wobble;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            // Smooth 3D value noise, 0..1.
            float Noise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash(i + float3(0, 0, 0)), Hash(i + float3(1, 0, 0)), f.x),
                                 lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
                                 lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float wobble = Noise(positionWS * 3.0 + _Time.y * float3(0.3, 0.9, 0.2)) - 0.5;
                positionWS += normalWS * wobble * _Wobble * 2.0;
                output.positionWS = positionWS;
                output.normalWS = normalWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                half facing = saturate(dot(n, v));
                half edge = pow(facing, _EdgeFade);

                float3 p = input.positionWS * _NoiseScale + float3(0.2, 1.0, 0.35) * _Time.y * _Speed;
                float cloud = Noise(p) * 0.65 + Noise(p * 2.13 + 7.1) * 0.35;
                half density = lerp(1.0, saturate(cloud * 1.6 - 0.2), _Density);

                return half4(_Color.rgb, _Color.a * edge * density);
            }
            ENDHLSL
        }
    }
}
