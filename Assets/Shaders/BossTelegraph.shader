Shader "TeamOdd/Boss Telegraph"
{
    // 바닥에 눕힌 Quad 하나에 보스 공격 범위(원/부채꼴/사각형)를 그리는 셰이더
    // 모양, 크기, 진행도는 AttackTelegraph가 MaterialPropertyBlock으로 넘긴다.
    Properties
    {
        _BaseColor ("Color", Color) = (1,0.25,0.1,1)
        _BackAlpha ("Back Alpha", Range(0,1)) = 0.15
        _FillAlpha ("Fill Alpha", Range(0,1)) = 0.45
        _OutlineAlpha ("Outline Alpha", Range(0,1)) = 0.9
        _OutlineWidth ("Outline Width (m)", Float) = 0.15

        // 0 원, 1 부채꼴, 2 사각형 (TelegraphShape.ShapeKind)
        [HideInInspector] _Shape ("Shape", Float) = 0
        // Quad의 월드 크기(m), x는 오른쪽, y는 forward
        [HideInInspector] _Size ("Size", Vector) = (1,1,0,0)
        // 부채꼴 절반 각도(라디안)
        [HideInInspector] _HalfAngle ("Half Angle", Float) = 3.14159265
        [HideInInspector] _Progress ("Progress", Range(0,1)) = 0
        [HideInInspector] _Fade ("Fade", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }

        Pass
        {
            Name "Telegraph"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            // 바닥과 같은 높이에서도 깜빡이지 않도록 깊이를 살짝 당김
            Offset -1, -1
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _BackAlpha;
                float _FillAlpha;
                float _OutlineAlpha;
                float _OutlineWidth;
                float _Shape;
                float4 _Size;
                float _HalfAngle;
                float _Progress;
                float _Fade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            // 안쪽이 음수인 거리 함수들, p는 중심 기준 미터 단위 좌표(y가 forward)
            float CircleDistance(float2 p, float radius)
            {
                return length(p) - radius;
            }

            // forward(y축)를 중심으로 좌우 halfAngle만큼 펼친 부채꼴
            float SectorDistance(float2 p, float radius, float halfAngle)
            {
                if (halfAngle >= PI - 0.0001) return CircleDistance(p, radius);

                float2 edge = float2(sin(halfAngle), cos(halfAngle));
                p.x = abs(p.x);
                float radial = length(p) - radius;
                float side = length(p - edge * clamp(dot(p, edge), 0.0, radius));
                return max(radial, side * sign(edge.y * p.x - edge.x * p.y));
            }

            float BoxDistance(float2 p, float2 halfSize)
            {
                float2 q = abs(p) - halfSize;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = (input.uv - 0.5) * _Size.xy;
                float shapeDistance;
                // 0에서 1로 차오르는 좌표, 원/부채꼴은 중심에서 바깥으로, 사각형은 시작점에서 끝으로
                float fillCoord;

                if (_Shape < 0.5)
                {
                    float radius = _Size.x * 0.5;
                    shapeDistance = CircleDistance(p, radius);
                    fillCoord = length(p) / radius;
                }
                else if (_Shape < 1.5)
                {
                    float radius = _Size.x * 0.5;
                    shapeDistance = SectorDistance(p, radius, _HalfAngle);
                    fillCoord = length(p) / radius;
                }
                else
                {
                    shapeDistance = BoxDistance(p, _Size.xy * 0.5);
                    fillCoord = input.uv.y;
                }

                float edgeAA = max(fwidth(shapeDistance), 0.0001);
                float inside = 1.0 - smoothstep(-edgeAA, edgeAA, shapeDistance);
                float outline = inside * smoothstep(-_OutlineWidth - edgeAA, -_OutlineWidth + edgeAA, shapeDistance);

                float fillAA = max(fwidth(fillCoord), 0.0001);
                float filled = 1.0 - smoothstep(_Progress - fillAA, _Progress + fillAA, fillCoord);

                float alpha = inside * lerp(_BackAlpha, _FillAlpha, filled);
                alpha = max(alpha, outline * _OutlineAlpha);
                alpha *= _BaseColor.a * _Fade;

                clip(alpha - 0.001);
                return half4(_BaseColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
