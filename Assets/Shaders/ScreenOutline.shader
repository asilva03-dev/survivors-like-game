// ScreenOutline (2026-11-10, with the cel shading)
// Full-screen pass run by ScreenOutlineFeature before transparents (and before the darkness pass): draws a line where
// the depth jumps (something standing in front of what is behind it) or the surface direction turns sharply
// (silhouettes, wall and cliff edges). Reads the camera depth and normals textures (URP renders them for SSAO too).
// - _Thickness: line width in pixels. _DepthThreshold: depth jump, relative to the distance, that counts as an edge.
//   _NormalThreshold: how different two surface directions must be. _OutlineColor: fixed line colour, alpha = strength.
// - _ObjectColor (2026-11-10, user: black lines too harsh): 0 = the fixed colour, 1 = the colour of the nearest thing at
//   the edge (the object in front) times _Darken, so each line is a darker shade of its own object. Blends in between.
// - Lines fade out between _FadeStart and _FadeEnd units from the camera.
Shader "TD/ScreenOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.03, 0.03, 0.05, 1)
        _Thickness ("Thickness (pixels)", Range(0.5, 4)) = 1.2
        _DepthThreshold ("Depth Threshold (relative)", Range(0.001, 0.5)) = 0.04
        _NormalThreshold ("Normal Threshold", Range(0.05, 2)) = 0.9
        _ObjectColor ("Use Object Colour (0 = fixed colour)", Range(0, 1)) = 1
        _Darken ("Object Colour Darken", Range(0, 1)) = 0.55
        _FadeStart ("Fade Start", Float) = 60
        _FadeEnd ("Fade End", Float) = 90
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "ScreenOutline"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            half4 _OutlineColor;
            float _Thickness;
            float _DepthThreshold;
            float _NormalThreshold;
            float _ObjectColor;
            float _Darken;
            float _FadeStart;
            float _FadeEnd;

            float EyeDepth(float2 uv)
            {
                return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float2 o = _ScreenSize.zw * _Thickness * 0.5;
                float2 uvA = uv + float2(-o.x, -o.y);
                float2 uvB = uv + float2(o.x, o.y);
                float2 uvC = uv + float2(o.x, -o.y);
                float2 uvD = uv + float2(-o.x, o.y);

                // Roberts cross on linear depth, relative to the distance (so far and near edges match).
                float dA = EyeDepth(uvA);
                float dB = EyeDepth(uvB);
                float dC = EyeDepth(uvC);
                float dD = EyeDepth(uvD);
                float nearest = min(min(dA, dB), min(dC, dD));
                // The sample nearest the camera = the object in front at this edge; its colour tints the line.
                float2 uvNear = uvA;
                if (dB <= nearest) uvNear = uvB;
                if (dC <= nearest) uvNear = uvC;
                if (dD <= nearest) uvNear = uvD;
                float depthJump = (abs(dA - dB) + abs(dC - dD)) / max(nearest, 0.001);
                float depthEdge = smoothstep(_DepthThreshold, _DepthThreshold * 1.5, depthJump);

                // Roberts cross on the surface normals.
                float3 nA = SampleSceneNormals(uvA);
                float3 nB = SampleSceneNormals(uvB);
                float3 nC = SampleSceneNormals(uvC);
                float3 nD = SampleSceneNormals(uvD);
                float normalJump = length(nA - nB) + length(nC - nD);
                float normalEdge = smoothstep(_NormalThreshold, _NormalThreshold * 1.3, normalJump);

                float edge = max(depthEdge, normalEdge);
                edge *= 1.0 - smoothstep(_FadeStart, _FadeEnd, nearest);
                half3 objectColour = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvNear).rgb * _Darken;
                half3 lineColour = lerp(_OutlineColor.rgb, objectColour, _ObjectColor);
                color.rgb = lerp(color.rgb, lineColour, edge * _OutlineColor.a);
                return color;
            }
            ENDHLSL
        }
    }
}
