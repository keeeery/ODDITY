Shader "Hidden/TeamOdd/Telegraph Mask"
{
    // 보스·플레이어 몸이 보이는 픽셀에 스텐실 비트(128)를 표시해, 공격 범위 예고(BossTelegraph)가 그 위에 그려지지 않게 함
    // TelegraphMaskFeature가 불투명 렌더링 직후 등록된 렌더러를 이 셰이더로 다시 그린다. 색과 깊이는 바꾸지 않는다.
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Mask"
            ColorMask 0
            ZWrite Off
            ZTest LEqual
            // 이미 그려진 같은 몸 깊이와 겨뤄도 확실히 통과하도록 살짝 앞으로
            Offset -1, -1
            Cull Back

            Stencil
            {
                Ref 128
                WriteMask 128
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
