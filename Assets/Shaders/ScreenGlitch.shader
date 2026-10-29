// ScreenGlitchFeature가 후처리 뒤 화면 전체에 거는 셰이더
// 가로 띠 밀림·RGB 색 분리·깜빡임(_Glitch), 채도 빠짐(_Desaturate), 가장자리 어두워짐(_Vignette),
// 화면 꺼짐(_PowerOff: 0~1 위아래로 접혀 선, 1~2 선이 점으로, 2~3 점이 사라짐)
Shader "Hidden/TeamOdd/Screen Glitch"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "Screen Glitch"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Glitch;
            float _Desaturate;
            float _Vignette;
            float _PowerOff;
            float _GlitchTime;

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            half3 SampleScreen(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float2 pixel = 1.0 / _ScreenParams.xy;
                float2 offset = uv - 0.5;

                // 화면 꺼짐: 보이는 범위를 위아래로 접고, 다 접히면 가로로 줄여 점으로 만든다. 남은 범위에 화면 전체를 눌러 담는다.
                float fold = saturate(_PowerOff);
                float shrink = saturate(_PowerOff - 1.0);
                float fade = saturate(_PowerOff - 2.0);
                float2 extent = float2(lerp(0.5, pixel.x * 1.5, shrink), lerp(0.5, pixel.y * 0.75, fold));
                float2 inside2 = saturate((extent - abs(offset)) / pixel + 0.5);
                float inside = inside2.x * inside2.y;
                float2 screenUv = 0.5 + offset * 0.5 / extent;

                // 값은 초당 20번만 바꿔 디지털 신호처럼 끊겨 보이게 한다.
                float tick = floor(_GlitchTime * 20.0);

                // 굵은 가로 띠 몇 개가 옆으로 밀림
                float band = floor(screenUv.y * 24.0 + Hash(float2(tick, 1.0)) * 3.0);
                float shift = step(1.0 - 0.4 * _Glitch, Hash(float2(band, tick))) * (Hash(float2(band, tick + 7.0)) - 0.5) * 0.15;
                // 가는 줄 노이즈
                float row = floor(screenUv.y * _ScreenParams.y / 3.0);
                shift += step(1.0 - 0.08 * _Glitch, Hash(float2(row, tick + 3.0))) * (Hash(float2(row, tick)) - 0.5) * 0.05;
                screenUv.x += shift * _Glitch;

                float split = (0.002 + 0.008 * Hash(float2(tick, 5.0))) * _Glitch;
                half3 color;
                color.r = SampleScreen(screenUv + float2(split, 0.0)).r;
                color.g = SampleScreen(screenUv).g;
                color.b = SampleScreen(screenUv - float2(split, 0.0)).b;

                // 깜빡임
                color *= 1.0 - _Glitch * 0.5 * step(0.75, Hash(float2(tick, 11.0)));

                half luma = dot(color, half3(0.2126, 0.7152, 0.0722));
                color = lerp(color, luma.xxx, _Desaturate);

                float2 aspectOffset = offset * float2(_ScreenParams.x / _ScreenParams.y, 1.0);
                color *= 1.0 - _Vignette * 0.9 * smoothstep(0.35, 1.05, length(aspectOffset));

                // 접혀 갈수록 남은 선·점이 하얗게 빛난다.
                color = lerp(color, half3(0.95, 0.94, 0.92), fold * fold * 0.75);
                color *= inside * (1.0 - fade);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
