Shader "TeamOdd/Dodge Shell"
{
    // 수동 회피 허물. 그 순간 자세로 굳힌 플레이어 메시를 하얗게 그리고, _Dissolve를 올리면 노이즈 모양으로 바스러져 사라진다 (DodgeShell이 값을 올린다)
    // 바스러지는 경계는 _EdgeColor로 빛나게 해서 조각이 타들어 가듯 떨어져 나가는 느낌을 낸다
    Properties
    {
        [HDR] _Color ("Color", Color) = (1.4, 1.6, 1.7, 1)
        [HDR] _EdgeColor ("Edge Color", Color) = (0.6, 2.4, 3, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _Dissolve ("Dissolve", Range(0, 1)) = 0
        _NoiseScale ("Noise Scale", Float) = 12
        _EdgeWidth ("Edge Width", Range(0, 0.3)) = 0.08
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }

        Pass
        {
            Name "DodgeShell"
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _EdgeColor;
                float _RimPower;
                float _Dissolve;
                float _NoiseScale;
                float _EdgeWidth;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            // 칸마다 0~1 사이의 값 하나를 정한다
            float Hash(float3 cell)
            {
                cell = frac(cell * 0.3183099 + 0.1);
                cell *= 17.0;
                return frac(cell.x * cell.y * cell.z * (cell.x + cell.y + cell.z));
            }

            // 3D 값 노이즈. 이웃한 8칸의 값을 부드럽게 섞어 얼룩 모양을 만든다 (월드 위치 기준이라 허물이 움직이지 않으면 모양도 그대로)
            float Noise(float3 position)
            {
                float3 cell = floor(position);
                float3 t = frac(position);
                t = t * t * (3.0 - 2.0 * t);

                float bottom = lerp(lerp(Hash(cell), Hash(cell + float3(1, 0, 0)), t.x),
                                    lerp(Hash(cell + float3(0, 1, 0)), Hash(cell + float3(1, 1, 0)), t.x), t.y);
                float top = lerp(lerp(Hash(cell + float3(0, 0, 1)), Hash(cell + float3(1, 0, 1)), t.x),
                                 lerp(Hash(cell + float3(0, 1, 1)), Hash(cell + float3(1, 1, 1)), t.x), t.y);
                return lerp(bottom, top, t.z);
            }

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
                // 노이즈 값이 _Dissolve보다 낮은 곳부터 지운다. _Dissolve가 1을 넘을 만큼 경계 폭을 더해야 끝에 다 사라진다
                float noise = Noise(input.positionWS * _NoiseScale);
                float threshold = _Dissolve * (1.0 + _EdgeWidth);
                clip(noise - threshold);

                float3 normal = normalize(input.normalWS);
                float3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(1.0 - saturate(dot(normal, view)), _RimPower);
                half3 color = _Color.rgb + _EdgeColor.rgb * rim;

                // 지워지기 직전의 경계는 빛나게 한다
                if (_Dissolve > 0 && noise - threshold < _EdgeWidth)
                    color = _EdgeColor.rgb;

                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
