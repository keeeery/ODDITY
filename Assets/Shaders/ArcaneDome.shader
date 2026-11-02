Shader "TeamOdd/Arcane Dome"
{
    // 염동 충격파의 투명한 반구. 가장자리(비스듬히 보이는 곳)일수록 밝고 가운데는 비쳐 보인다
    // 기본 구 메시에서 바닥 아래쪽 절반은 잘라 반구로 그리고, 바닥과 닿는 밑동은 한 번 더 밝게 빛낸다
    // 겹칠수록 밝아지는(Additive) 방식이라 따로 투명도가 없다. _Fade를 0으로 내리면 사라진다 (ShockwaveEffect가 값을 바꾼다)
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.5, 0.25, 0.9, 1)
        [HDR] _RimColor ("Rim Color", Color) = (1.6, 0.8, 2.6, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _GroundPower ("Ground Glow Power", Range(1, 16)) = 6
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }

        Pass
        {
            Name "ArcaneDome"
            Blend One One
            ZWrite Off
            Cull Off // 카메라가 반구 안에 들어와도 안쪽 면이 보이게

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _RimColor;
                float _RimPower;
                float _GroundPower;
                float _Fade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; float height : TEXCOORD2; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.height = input.positionOS.y / 0.5; // 기본 구 메시는 반지름 0.5라서 꼭대기가 1, 바닥이 0이 되게 나눈다
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                clip(input.height); // 아래쪽 절반은 그리지 않는다

                float3 normal = normalize(input.normalWS);
                float3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                // 안쪽 면은 법선이 반대라서 abs로 바깥 면과 똑같이 계산한다
                half rim = pow(1.0 - abs(dot(normal, view)), _RimPower);
                half ground = pow(1.0 - saturate(input.height), _GroundPower);

                half3 color = _Color.rgb + _RimColor.rgb * (rim + ground);
                return half4(color * _Fade, 1);
            }
            ENDHLSL
        }
    }
}
