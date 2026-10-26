Shader "TeamOdd/Hit Flash"
{
    // 피격 대상의 메시 위에 덧그리는 가산 림 번쩍임, HitFlashFeature가 그린다.
    // 세기(_HitFlashIntensity)는 대상마다 전역 값으로 받는다.
    Properties
    {
        [HDR] _Color ("Color", Color) = (1.6, 1.5, 1.4, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2
        _FillAmount ("Fill Amount", Range(0, 1)) = 0.15
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }

        Pass
        {
            Name "HitFlash"
            Cull Back
            ZWrite Off
            ZTest LEqual
            // 원래 메시와 Z-파이팅 방지
            Offset -1, -1
            Blend SrcAlpha One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _RimPower;
                float _FillAmount;
            CBUFFER_END

            float _HitFlashIntensity;

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
                float3 normal = normalize(input.normalWS);
                float3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(1.0 - saturate(dot(normal, view)), _RimPower);

                half amount = saturate(lerp(_FillAmount, 1.0, rim)) * _HitFlashIntensity;
                return half4(_Color.rgb, _Color.a * amount);
            }
            ENDHLSL
        }
    }
}
