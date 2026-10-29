Shader "TeamOdd/Force Ripple"
{
    // 염동력 기본 공격(염동파)의 공간 일그러짐, 사각형에 절차적으로 그린다(불투명 텍스처 필요).
    // _Fill이 0이면 퍼져 나가는 고리(손 앞 응축, 꼬리 고리, 착탄 충격파), 1이면 볼록 렌즈(날아가는 염동탄)
    // 굴절 방향은 화면에서 사각형 중심 → 픽셀 방향이라, 기울어진 고리도 화면 기준으로 자연스럽게 밀린다.
    // 진행도(_Progress), 고리 반지름(_StartRadius → _EndRadius), 렌즈 여부(_Fill), 전체 세기(_Fade)는 PsychokinesisForceWave가 넘긴다.
    Properties
    {
        [HDR] _RimColor ("Rim Color", Color) = (0.8, 0.92, 1, 0.25)
        _Distortion ("Distortion", Range(0, 0.3)) = 0.08
        _Thickness ("Ring Thickness", Range(0.02, 0.6)) = 0.18
        [HideInInspector] _Progress ("Progress", Range(0, 1)) = 0
        [HideInInspector] _StartRadius ("Start Radius", Range(0, 1)) = 0.1
        [HideInInspector] _EndRadius ("End Radius", Range(0, 1)) = 0.85
        [HideInInspector] _Fill ("Fill", Range(0, 1)) = 0
        [HideInInspector] _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+80" "IgnoreProjector"="True" }

        Pass
        {
            Name "ForceRipple"
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
                half4 _RimColor;
                float _Distortion;
                float _Thickness;
                float _Progress;
                float _StartRadius;
                float _EndRadius;
                float _Fill;
                float _Fade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float4 centerScreenPos : TEXCOORD2;
                float eyeDepth : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv * 2.0 - 1.0;
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.centerScreenPos = ComputeScreenPos(TransformObjectToHClip(float3(0.0, 0.0, 0.0)));
                output.eyeDepth = -TransformWorldToView(positionWS).z;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float r = length(input.uv);
                float progress = saturate(_Progress);
                float eased = 1.0 - pow(1.0 - progress, 3.0);

                // 고리: 중심선 안쪽은 바깥으로, 바깥쪽은 안으로 당겨 힘이 응축된 띠처럼 보이게 함
                float radius = lerp(_StartRadius, _EndRadius, eased);
                float thickness = _Thickness * lerp(1.0, 0.45, progress);
                float d = (r - radius) / thickness;
                float ringFade = (1.0 - progress) * (1.0 - progress);
                float ringShape = exp(-d * d * 3.0);
                float ringOffset = -d * ringShape * ringFade;
                float ringZone = exp(-d * d * 0.8) * ringFade;
                float rim = ringShape * ringFade;

                // 렌즈: 가운데가 볼록하게 부풀어 뒤 배경이 바깥으로 밀려 보임
                float lens = saturate(1.0 - r * r);
                lens *= lens;
                float lensOffset = -lens * r;
                float lensRim = smoothstep(0.55, 0.85, r) * smoothstep(1.0, 0.85, r);

                float edge = saturate((1.0 - r) * 8.0);
                float offsetAmount = lerp(ringOffset, lensOffset, _Fill) * _Fade * edge;
                float zone = saturate(lerp(ringZone, lens * 1.5, _Fill) * 1.5) * _Fade * edge;
                float rimAmount = lerp(rim, lensRim, _Fill) * _Fade * edge;

                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float2 centerUV = input.centerScreenPos.xy / input.centerScreenPos.w;
                float aspect = _ScaledScreenParams.x / _ScaledScreenParams.y;
                float2 fromCenter = (screenUV - centerUV) * float2(aspect, 1.0);
                float2 direction = fromCenter / max(length(fromCenter), 0.0001);
                direction.x /= aspect;

                // 멀수록 화면에서 작게 보이므로 굴절도 거리만큼 줄임
                float2 offset = direction * offsetAmount * _Distortion / max(input.eyeDepth, 0.5);
                half3 scene = SampleSceneColor(screenUV + offset);
                half3 color = scene + _RimColor.rgb * _RimColor.a * rimAmount;
                return half4(color, zone);
            }
            ENDHLSL
        }
    }
}
