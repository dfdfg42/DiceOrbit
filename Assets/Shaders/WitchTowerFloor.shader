Shader "DiceOrbit/Witch Tower/Backdrop Floor"
{
    Properties
    {
        _BaseColor("Color", Color) = (0.45, 0.4, 0.55, 1)
        _Pigment("Paper grain", Range(0,0.2)) = 0.045
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry-10" "RenderPipeline"="UniversalPipeline" }
        // No depth-only or depth-normal pass: URP's prepass must not cut through 2D billboards.
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "WitchTowerPaper.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Pigment;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                return half4(_BaseColor.rgb * (1.0 + TowerPigment(input.positionWS) * _Pigment), 1);
            }
            ENDHLSL
        }
    }
}
