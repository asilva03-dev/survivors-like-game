// DarknessPastLight (slice 7 step 2)
// Full-screen pass run by DarknessPastLightFeature before transparents: darkens every opaque pixel by its flat
// distance from the base light's centre. Fully lit up to the radius, then a smooth fade over the fade width into
// _TD_DarkTint (a colour multiplier). The globals come from BaseLightVisual; with _TD_DarknessOn 0 it does nothing.
Shader "TD/DarknessPastLight"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "DarknessPastLight"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float4 _TD_LightCircle; // x, y = centre (world X, Z), z = radius, w = fade width
            float4 _TD_DarkTint;    // rgb = colour multiplier at full dark
            float _TD_DarknessOn;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (_TD_DarknessOn < 0.5)
                {
                    return color;
                }

                #if UNITY_REVERSED_Z
                    float depth = SampleSceneDepth(uv);
                #else
                    float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
                #endif
                float3 world = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float distanceToCentre = distance(world.xz, _TD_LightCircle.xy);
                float t = smoothstep(_TD_LightCircle.z, _TD_LightCircle.z + max(_TD_LightCircle.w, 0.001), distanceToCentre);
                color.rgb *= lerp(float3(1, 1, 1), _TD_DarkTint.rgb, t);
                return color;
            }
            ENDHLSL
        }
    }
}
