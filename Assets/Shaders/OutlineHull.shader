// Custom/OutlineHull
// Inverted-hull outline: draw the mesh again, pushed out along its normals by _Width (object space,
// so it scales with the object) with front faces culled, in a flat color. Put it on a duplicate
// renderer of the mesh to get a solid border around it. For gap-free thick outlines use a mesh copy
// whose UV2 holds position-averaged (smoothed) normals; see the WoodDrop outline mesh asset.
Shader "Custom/OutlineHull"
{
    Properties
    {
        _Color ("Outline Color", Color) = (1,1,1,1)
        _Width ("Width (object space)", Float) = 0.04
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Width;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float3 smoothNormalOS : TEXCOORD2;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                // Smoothed normal (averaged per position, stored in UV2 by the outline mesh asset) so
                // hard edges do not split open; falls back to the vertex normal if UV2 is empty.
                float3 n = dot(input.smoothNormalOS, input.smoothNormalOS) > 0.0001
                    ? input.smoothNormalOS : input.normalOS;
                float3 pos = input.positionOS.xyz + normalize(n) * _Width;
                output.positionCS = TransformObjectToHClip(pos);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }
}
