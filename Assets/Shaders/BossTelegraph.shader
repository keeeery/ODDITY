Shader "TeamOdd/Boss Telegraph"
{
    // 보스 공격 범위(원/부채꼴/사각형)를 바닥면에 투영해 그리는 화면 공간 데칼 셰이더
    // AttackTelegraph의 상자(바닥 근처 높이만 덮음)를 그리면서, 장면 깊이(_CameraDepthTexture)로 그 픽셀에 보이는 표면 위치를 되살려
    // 상자 안에 있고 위를 향한 면에만, 그 표면의 수평 위치로 모양을 그린다. 그래서 단차 위에도 틈 없이 붙고 벽 옆면·구멍 아래에는 그리지 않는다.
    // 플레이어 몸(_TelegraphExclude 발바닥 위 원기둥)과, TelegraphMaskFeature가 스텐실 128로 표시한 바닥(Ground 레이어) 외 물체·보스·플레이어 몸 픽셀에는 그리지 않는다.
    // 모양, 크기, 진행도는 AttackTelegraph가 MaterialPropertyBlock으로 넘긴다.
    Properties
    {
        _BaseColor ("Color", Color) = (1,0.25,0.1,1)
        _BackAlpha ("Back Alpha", Range(0,1)) = 0.15
        _FillAlpha ("Fill Alpha", Range(0,1)) = 0.45
        _OutlineAlpha ("Outline Alpha", Range(0,1)) = 0.9
        _OutlineWidth ("Outline Width (m)", Float) = 0.15
        // 위를 향한 정도(법선 y)가 이보다 작은 면(벽 옆면 등)에는 그리지 않음
        _MinUpNormal ("Min Up Normal", Range(0,1)) = 0.6

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
            // 상자의 한쪽 면만 그려 같은 픽셀을 두 번 섞지 않음
            Cull Front
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            Stencil
            {
                Ref 128
                ReadMask 128
                Comp NotEqual
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _BackAlpha;
                float _FillAlpha;
                float _OutlineAlpha;
                float _OutlineWidth;
                float _MinUpNormal;
                float _Shape;
                float4 _Size;
                float _HalfAngle;
                float _Progress;
                float _Fade;
            CBUFFER_END

            // 플레이어 영역, xyz는 발바닥 중심, w는 반경 (AttackTelegraph가 전역으로 넘김)
            float4 _TelegraphExclude;
            float _TelegraphExcludeHeight;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            float3 SceneWorldPosition(float4 positionCS)
            {
                float2 screenUV = GetNormalizedScreenSpaceUV(positionCS);
                float sceneDepth = SampleSceneDepth(screenUV);
                #if !UNITY_REVERSED_Z
                    sceneDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1, sceneDepth);
                #endif
                return ComputeWorldSpacePosition(screenUV, sceneDepth, UNITY_MATRIX_I_VP);
            }

            bool IsInsidePlayer(float3 positionWS)
            {
                float radius = _TelegraphExclude.w;
                float height = positionWS.y - _TelegraphExclude.y;
                float2 offset = positionWS.xz - _TelegraphExclude.xz;
                // 발바닥과 같은 높이의 바닥은 남겨 발밑에도 예고가 보이게 함
                return radius > 0.0 && height > 0.05 && height < _TelegraphExcludeHeight && dot(offset, offset) < radius * radius;
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
                float3 sceneWS = SceneWorldPosition(input.positionCS);
                // 미분은 버리기(discard) 전에 구함, 이웃 픽셀 표면 위치의 차이로 면의 기울기를 앎
                float3 sceneNormal = normalize(cross(ddy(sceneWS), ddx(sceneWS)));
                float3 local = TransformWorldToObject(sceneWS);

                // 상자 밖(더 높은 벽 윗면, 구멍 아래), 위를 향하지 않은 면, 플레이어 몸에는 그리지 않음
                if (any(abs(local.xz) > 0.5) || local.y < 0.0 || local.y > 1.0) discard;
                if (abs(sceneNormal.y) < _MinUpNormal) discard;
                if (IsInsidePlayer(sceneWS)) discard;

                // 상자의 z가 forward이므로 uv의 y가 forward
                float2 uv = local.xz + 0.5;
                float2 p = (uv - 0.5) * _Size.xy;
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
                    fillCoord = uv.y;
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
