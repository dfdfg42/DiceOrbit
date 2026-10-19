Shader "DiceOrbit/Dice Surface"
{
    Properties
    {
        _MainTex ("Face artwork", 2D) = "white" {}
        _FaceRect ("Sprite UV rectangle", Vector) = (0,0,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _FaceRect;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float2 uv : TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = _FaceRect.xy + v.uv * _FaceRect.zw;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half4 art = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half3 color = lerp(half3(.19,.16,.28), art.rgb, art.a);
                half3 n = normalize(i.normalWS);
                half light = saturate(dot(n, normalize(half3(-.45,.75,-1))));
                color *= lerp(half3(.56,.51,.67), half3(1.04,1.02,.97), smoothstep(-.1, .9, light));
                half gloss = pow(saturate(dot(n, normalize(half3(-.3,.48,-1)))), 30);
                return half4(color + gloss * .075, 1);
            }
            ENDHLSL
        }
    }
}
