Shader "DiceOrbit/Witch Tower/Inlaid Monster Zone"
{
    Properties
    {
        [PerRendererData] _MainTex("Zone silhouette", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _StoneColor("Floor stone", Color) = (.47,.44,.55,1)
        _SeamColor("Engraved seam", Color) = (.30,.26,.36,1)
        _InlayColor("Muted brass", Color) = (.68,.59,.46,1)
        _OwnerMix("Owner color in stone", Range(0,1)) = .22
        _OuterRadius("Outer radius in world units", Float) = 16.35
        _InnerRadius("Center opening radius", Float) = 2.65
        _EdgeInset("Half of sector seam gap", Float) = .10
        _SeamWidth("Seam width in world units", Float) = .075
        _Pigment("Stone grain", Range(0,.2)) = .06
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "DisableBatching"="True" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "WitchTowerPaper.hlsl"
            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; };
            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
                float2 platePosition : TEXCOORD1;
                float3 worldPosition : TEXCOORD4;
            };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _StoneColor, _SeamColor, _InlayColor;
                float _OwnerMix, _OuterRadius, _InnerRadius, _EdgeInset, _SeamWidth, _Pigment;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                // Uniform plate scale expands the drawing bounds; distances below remain world-sized.
                o.platePosition = input.positionOS.xy * length(TransformObjectToWorldDir(float3(1,0,0), false));
                o.worldPosition = TransformObjectToWorld(input.positionOS);
                return o;
            }
            float RoundedIntersection(float a, float b)
            {
                float h = max(.28 - abs(a - b), 0) / .28;
                return max(a, b) + h * h * .07;
            }
            float SectorDistance(float2 p)
            {
                // The painted sprite's old inset clipped tile corners. Use an annular quarter with
                // narrow radial seams and a radius enclosing the actual 20-tile mesh footprint.
                float r = length(p);
                float sides = max(_EdgeInset - p.x, _EdgeInset - p.y);
                return RoundedIntersection(RoundedIntersection(r - _OuterRadius, sides), _InnerRadius - r);
            }
            float FineLine(float distance, float width)
            {
                float aa = max(fwidth(distance), .008);
                return 1 - smoothstep(width, width + aa, abs(distance));
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float distance = SectorDistance(i.platePosition);
                float aa = max(fwidth(distance), .008);
                float coverage = 1 - smoothstep(-aa, aa, distance);
                float interior = 1 - smoothstep(-aa, aa, distance + _SeamWidth);
                float seam = saturate(coverage - interior);
                float inlay = FineLine(distance + .245, .0225);
                half3 owner = i.color.rgb;
                half3 stone = lerp(_StoneColor.rgb, owner, _OwnerMix);
                stone *= 1 + TowerPigment(i.worldPosition) * _Pigment;
                float radius = length(i.platePosition);
                // Quiet circular engraving just inside the tile track, shared across the four sectors.
                float arc = FineLine(radius - 10.0, .025) * .48;
                float secondaryArc = FineLine(radius - 9.82, .016) * .22;
                half3 color = lerp(stone, _SeamColor.rgb, saturate(seam + secondaryArc));
                half3 brass = lerp(_InlayColor.rgb, stone, .38);
                color = lerp(color, brass, saturate(inlay * .75 + arc));
                // A short inset accent retains the owner's hue without flooding the entire floor.
                float angle = atan2(i.platePosition.y, i.platePosition.x);
                float accent = FineLine(radius - 10.18, .055) * (1 - smoothstep(.31,.35,abs(angle - .785398)));
                color = lerp(color, lerp(stone,owner,.35),accent*.65);
                return half4(color, coverage * i.color.a);
            }
            ENDHLSL
        }
    }
}
