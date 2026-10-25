Shader "TeamOdd/Time Freeze Overlay"
{
    // 시간 정지된 오브젝트의 메시 위에 한 번 더 그려 파란 반투명 막을 씌우는 셰이더
    // TimeFreezeOverlayFeature가 멈춘 오브젝트의 렌더러마다 DrawRenderer로 그린다.
    // 막은 오브젝트 발밑에서 머리끝까지 높이를 따라 쓸어 올라가며 덮이고, 풀릴 때는 위에서부터 걷힌다.
    // - 진행도(_TimeFreezeOverlayProgress)와 오브젝트 높이 범위(_TimeFreezeOverlayBounds)는 오브젝트마다 전역 값으로 넘긴다.
    // - 막의 경계선 근처는 밝은 색으로 띠를 그려 막이 씌워지는 지점을 보여준다.
    // - 가장자리(시선과 표면이 비스듬한 곳)일수록 진하게 해서 실루엣이 살아 보이게 한다.
    Properties
    {
        _BaseColor ("Color", Color) = (0.225, 0.45, 1, 0.275)
        _RimColor ("Rim Color", Color) = (0.425, 0.65, 1, 0.775)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _EdgeColor ("Edge Color", Color) = (0.675, 0.8, 1, 0.925)
        _EdgeWidth ("Edge Width (m)", Float) = 0.15
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }

        Pass
        {
            Name "TimeFreezeOverlay"
            Cull Back
            ZWrite Off
            ZTest LEqual
            // 원래 메시와 같은 깊이라 깜빡이지 않도록 살짝 앞으로 당김
            Offset -1, -1
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _RimColor;
                float _RimPower;
                half4 _EdgeColor;
                float _EdgeWidth;
            CBUFFER_END

            float _TimeFreezeOverlayProgress;
            // x: 오브젝트 맨 아래 월드 높이, y: 맨 위 월드 높이
            float4 _TimeFreezeOverlayBounds;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 진행도 0이면 경계선이 맨 아래보다 띠 폭만큼 아래, 1이면 맨 위보다 띠 폭만큼 위라서 막이 전혀 없거나 다 덮인다.
                float edgeWidth = max(_EdgeWidth, 0.001);
                float sweepHeight = lerp(_TimeFreezeOverlayBounds.x - edgeWidth, _TimeFreezeOverlayBounds.y + edgeWidth, _TimeFreezeOverlayProgress);
                float covered = sweepHeight - input.positionWS.y;
                clip(covered);

                float3 normal = normalize(input.normalWS);
                float3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(1.0 - saturate(dot(normal, view)), _RimPower);

                half3 color = lerp(_BaseColor.rgb, _RimColor.rgb, rim);
                half alpha = lerp(_BaseColor.a, _RimColor.a, rim);

                // 경계선에 가까울수록 밝은 띠
                half edge = 1.0 - saturate(covered / edgeWidth);
                color = lerp(color, _EdgeColor.rgb, edge);
                alpha = max(alpha, _EdgeColor.a * edge);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
