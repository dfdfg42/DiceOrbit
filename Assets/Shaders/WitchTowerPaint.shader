Shader "DiceOrbit/Witch Tower/Painted Props"
{
    Properties
    {
        _BaseColor("Paint color", Color) = (0.6,0.5,0.7,1)
        _EmissionColor("Glow color", Color) = (0,0,0,0)
        _Pigment("Pigment variation", Range(0,0.3)) = 0.09
        _WoodGrain("Wood grain", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "WitchTowerPaper.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EmissionColor;
                half _Pigment;
                half _WoodGrain;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
            };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half3 normal = normalize(i.normalWS);
                Light main = GetMainLight();
                half ndl = dot(normal, main.direction);
                // Soft gouache bands, with violet shadows and warm upper faces.
                half lightBand = smoothstep(-0.12, 0.16, ndl);
                half highlight = smoothstep(0.60, 0.85, ndl);
                half3 shade = lerp(half3(0.63,0.61,0.78), half3(0.94,0.90,0.96), lightBand);
                shade += half3(0.08,0.07,0.04) * highlight;
                float pigment = TowerPigment(i.positionWS);
                float wood = sin(i.positionWS.y * 21.0 + TowerNoise(i.positionWS * 1.2) * 4.0);
                float grainVisibility = 1.0 - smoothstep(0.04,0.2,length(fwidth(i.positionWS)));
                half3 color = _BaseColor.rgb * shade;
                color *= 1.0 + pigment * _Pigment + wood * 0.025 * _WoodGrain * grainVisibility;
                color += _EmissionColor.rgb * 0.22;
                return half4(color,1);
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
            Cull Back
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            // Opaque props cast their actual silhouettes on the lit masonry; the ink hull does not.
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }
}
