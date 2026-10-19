Shader "DiceOrbit/Witch Tower/Ink Silhouette"
{
    Properties
    {
        _InkColor("Ink", Color) = (0.18,0.14,0.25,1)
        _Width("World width", Range(0.005,0.1)) = 0.035
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+1" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Front
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _InkColor;
                float _Width;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                float3 position = TransformObjectToWorld(i.positionOS.xyz);
                float3 normal = TransformObjectToWorldNormal(i.normalOS);
                // Fixed in space, never animated: a subtle irregular pen edge without shimmer.
                float wobble = 0.93 + 0.07 * sin(dot(position,float3(3.1,2.3,4.7)));
                position += normal * _Width * wobble;
                o.positionCS = TransformWorldToHClip(position);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target { return _InkColor; }
            ENDHLSL
        }
    }
}
