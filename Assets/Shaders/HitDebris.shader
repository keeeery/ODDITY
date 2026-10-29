Shader "TeamOdd/Hit Debris"
{
    // 강한 피격 때 흩날리는 빛 입자(HitImpact의 Stretched Billboard 파티클), 색과 투명도는 정점 색을 따른다.
    // Streak이 0이면 모서리가 선 다이아몬드, 1이면 끝이 둥글게 흐려지는 빛줄기(늘어난 사각형 안의 가늘고 긴 타원과 밝은 심지)
    Properties
    {
        [HDR] _Color ("Color", Color) = (1.4, 1.4, 1.4, 1)
        _Sharpness ("Sharpness", Range(0.5, 4)) = 1.6
        _BackDarken ("Back Darken", Range(0, 1)) = 0.3
        _Streak ("Streak", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+90" "IgnoreProjector"="True" }

        Pass
        {
            Name "HitDebris"
            Cull Off
            ZWrite Off
            ZTest LEqual
            // 미리 곱한 알파, 알파만큼 배경을 눌러 밝은 곳에서도 보이게 함
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Sharpness;
                float _BackDarken;
                float _Streak;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv * 2.0 - 1.0;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float diamond = abs(input.uv.x) + abs(input.uv.y);
                float diamondAmount = pow(saturate(1.0 - diamond), _Sharpness);

                // 둥근 거리로 재 모서리를 없애고, 가운데에 진한 심지를 더해 빛줄기처럼 보이게 함
                float radius = length(input.uv);
                float glow = saturate(1.0 - radius);
                float streakAmount = saturate(pow(glow, _Sharpness * 1.5) + pow(glow, _Sharpness * 4.0) * 0.6);

                float amount = lerp(diamondAmount, streakAmount, _Streak) * input.color.a;
                half3 color = _Color.rgb * input.color.rgb;
                return half4(color * amount, amount * _BackDarken * _Color.a);
            }
            ENDHLSL
        }
    }
}
