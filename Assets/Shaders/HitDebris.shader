Shader "TeamOdd/Hit Debris"
{
    // 강한 피격 때 흩날리는 빛 입자(HitImpact의 Stretched Billboard 파티클), 색과 투명도는 정점 색을 따른다.
    Properties
    {
        [HDR] _Color ("Color", Color) = (1.4, 1.4, 1.4, 1)
        _Sharpness ("Sharpness", Range(0.5, 4)) = 1.6
        _BackDarken ("Back Darken", Range(0, 1)) = 0.3
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
                float amount = pow(saturate(1.0 - diamond), _Sharpness) * input.color.a;
                half3 color = _Color.rgb * input.color.rgb;
                return half4(color * amount, amount * _BackDarken * _Color.a);
            }
            ENDHLSL
        }
    }
}
