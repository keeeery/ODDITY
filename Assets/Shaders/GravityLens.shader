Shader "TeamOdd/Gravity Lens"
{
    // 염동 구체(검은 구) 둘레의 중력렌즈, 사각형에 절차적으로 그린다(불투명 텍스처 필요).
    // 사각형은 셰이더가 늘 카메라를 보게 세우므로 오브젝트 회전과 상관없다. 크기는 오브젝트 크기를 따른다.
    // 블랙홀처럼 중심에 가까울수록 뒤 배경을 크게 휘어, 아인슈타인 반지름 근처에 배경이 고리처럼 감겨 보인다.
    // 반지름 값은 사각형 반 크기를 1로 본 비율이다. 검은 구 안쪽(_CoreRadius)은 그리지 않아 구가 그대로 보인다.
    Properties
    {
        _Strength ("Strength", Range(0, 2)) = 1
        _EinsteinRadius ("Einstein Radius", Range(0, 1)) = 0.45
        _CoreRadius ("Core Radius", Range(0, 1)) = 0.33
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+80" "IgnoreProjector"="True" }

        Pass
        {
            Name "GravityLens"
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Strength;
                float _EinsteinRadius;
                float _CoreRadius;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float4 centerScreenPos : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                // 오브젝트 중심에서 카메라 쪽 평면으로 사각형을 세운다. 기본 Quad 메시는 한 변이 1이다
                float3 centerWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float scale = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20));
                float3 positionVS = TransformWorldToView(centerWS) + float3(input.positionOS.xy * scale, 0.0);

                output.positionCS = TransformWViewToHClip(positionVS);
                output.uv = input.positionOS.xy * 2.0;
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.centerScreenPos = ComputeScreenPos(TransformWorldToHClip(centerWS));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float r = length(input.uv);

                // 중심에 가까울수록 많이 휜다(질량이 휘는 각도는 거리에 반비례). 가장자리로 가며 0이 되어 경계가 보이지 않는다
                float bend = _EinsteinRadius * _EinsteinRadius / max(r * r, 0.0001);
                bend = min(bend, 2.0) * smoothstep(1.0, 0.45, r) * _Strength;

                // 중심에서 픽셀까지의 거리를 줄여 뒤쪽 배경을 끌어온다. 아인슈타인 반지름 안쪽은 반대편 배경이 비친다
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float2 centerUV = input.centerScreenPos.xy / input.centerScreenPos.w;
                half3 scene = SampleSceneColor(centerUV + (screenUV - centerUV) * (1.0 - bend));

                float alpha = smoothstep(_CoreRadius * 0.95, _CoreRadius * 1.05, r) * step(r, 1.0);
                return half4(scene, alpha);
            }
            ENDHLSL
        }
    }
}
