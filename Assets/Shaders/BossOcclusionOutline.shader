Shader "TeamOdd/Boss Occlusion Outline"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1,1,1,1)
        _OutlineWidthPixels ("Width (Pixels)", Range(1,8)) = 2
        _FillColor ("Fill Color", Color) = (1,0.1,0.1,0.5)
        _FillMinGap ("Fill Min Gap", Float) = 0.3
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _OutlineWidthPixels;
            float4 _OutlineRect;
            half4 _FillColor;
            float _FillMinGap;
            float4 _FillRect;
        CBUFFER_END
        TEXTURE2D_X(_BossOcclusionBossDepth);
        TEXTURE2D_X(_BossOcclusionPlayerMask);
        TEXTURE2D_X(_BossOcclusionIgnoredDepth);
        TEXTURE2D_X(_BossOcclusionDebrisMask);
        float EyeDepth(float rawDepth)
        {
            return unity_OrthoParams.w > 0.5 ? LinearDepthToEyeDepth(rawDepth) : LinearEyeDepth(rawDepth, _ZBufferParams);
        }
        // rect(xMin, yMin, xMax, yMax) 밖의 픽셀은 버린다.
        void ClipToRect(float2 uv, float4 rect)
        {
            clip(min(min(uv.x - rect.x, uv.y - rect.y), min(rect.z - uv.x, rect.w - uv.y)));
        }
        struct Attributes { float4 positionOS : POSITION; };
        struct Varyings { float4 positionCS : SV_POSITION; float eyeDepth : TEXCOORD0; };
        Varyings MaskVertex(Attributes input)
        {
            Varyings output;
            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(positionWS);
            output.eyeDepth = -TransformWorldToView(positionWS).z;
            return output;
        }
        struct ScreenVaryings { float4 positionCS : SV_POSITION; };
        ScreenVaryings ScreenVertex(uint vertexID : SV_VertexID)
        {
            ScreenVaryings output;
            output.positionCS = GetFullScreenTriangleVertexPosition(vertexID);
            return output;
        }
        // 최소 간격을 넘은 뒤 이 거리(m)에 걸쳐 실루엣이 차오른다.
        #define FILL_FADE_DISTANCE 0.4
        // 보스 실루엣을 채울 양(0~1)을 구하고, coverage에는 그 픽셀의 보스 커버리지를 돌려준다.
        half FillAmount(float2 uv, out half coverage)
        {
            float2 boss = SAMPLE_TEXTURE2D_X(_BossOcclusionBossDepth, sampler_PointClamp, uv).rg;
            coverage = boss.r;
            if (boss.r <= 0.001) return 0.0;
            float bossDepth = boss.g / boss.r;
            float sceneDepth = EyeDepth(SampleSceneDepth(uv));
            // 보스 앞면보다 다른 물체가 충분히 앞에 있을 때만 가려졌다고 본다.
            // 땅에 살짝 묻힌 발처럼 맞닿은 부분은 간격에 따라 서서히 걸러 깜빡이지 않게 한다.
            half hidden = saturate((bossDepth - sceneDepth - _FillMinGap) / FILL_FADE_DISTANCE);
            // 맨 앞에 보이는 면이 플레이어나 작은 파편인 부분은 비운다.
            half player = SAMPLE_TEXTURE2D_X(_BossOcclusionPlayerMask, sampler_PointClamp, uv).b;
            half debris = SAMPLE_TEXTURE2D_X(_BossOcclusionDebrisMask, sampler_PointClamp, uv).b;
            // 무시할 물체(예: 바닥, 수련장 벽)가 보스 앞을 막고 있으면 그 뒤에 있는 것으로 보아 채우지 않는다.
            float2 ignored = SAMPLE_TEXTURE2D_X(_BossOcclusionIgnoredDepth, sampler_PointClamp, uv).rg;
            half behindIgnored = ignored.r > 0.001 && ignored.g / ignored.r < bossDepth - _FillMinGap ? ignored.r : 0.0;
            // 보스 커버리지를 곱해 실루엣 경계를 부드럽게 한다.
            return boss.r * hidden * (1.0 - player) * (1.0 - debris) * (1.0 - behindIgnored);
        }
        ENDHLSL

        Pass
        {
            Name "Depth Mask"
            Cull Off
            // 보스 메시끼리(채우기에서 무시할 물체끼리) 겹치면 가장 가까운 면만 남기도록 전용 깊이 버퍼로 판정한다.
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex MaskVertex
            #pragma fragment BossFragment
            // MSAA로 그린 뒤 Resolve되어 r = 커버리지, g = 커버리지 × 깊이가 된다.
            float2 BossFragment(Varyings input) : SV_Target
            {
                return float2(1.0, input.eyeDepth);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Player Silhouette"
            Cull Off
            // 플레이어 메시끼리 겹치면 가장 가까운 면으로만 판정하도록 전용 깊이 버퍼를 쓴다.
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex MaskVertex
            #pragma fragment PlayerFragment
            // MSAA로 그린 뒤 Resolve되어 r = 플레이어 커버리지, g = 커버리지 × 보스에 가려짐, b = 커버리지 × 화면 맨 앞에 보임이 된다.
            half4 PlayerFragment(Varyings input) : SV_Target
            {
                float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;
                float sceneDepth = EyeDepth(SampleSceneDepth(uv));
                // 다른 물체에 가리지 않은 플레이어 면, 보스 실루엣 채우기가 플레이어 위를 덮지 않게 하는 데 쓴다.
                half visible = input.eyeDepth <= sceneDepth + 0.05 ? 1.0 : 0.0;
                float2 boss = SAMPLE_TEXTURE2D_X(_BossOcclusionBossDepth, sampler_PointClamp, uv).rg;
                if (boss.r <= 0.001) return half4(1.0, 0.0, visible, 0.0);
                float bossDepth = boss.g / boss.r;
                // 보스가 플레이어보다 앞에 있고, 그 보스가 다른 물체에 가리지 않고 화면에 보일 때만 가려진 부분으로 본다.
                // 보스 커버리지만큼만 가려졌다고 보아 보스 경계에서 외곽선이 부드럽게 잘리게 한다.
                half hidden = bossDepth < input.eyeDepth - 0.02 && sceneDepth + 0.05 >= bossDepth ? boss.r : 0.0;
                return half4(1.0, hidden, visible, 0.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Screen Outline"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex ScreenVertex
            #pragma fragment ScreenFragment
            half4 ScreenFragment(ScreenVaryings input) : SV_Target
            {
                float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;
                ClipToRect(uv, _OutlineRect);
                // Mask는 MSAA로 그린 뒤 Resolve되어 r = 플레이어 커버리지, g = 커버리지 × 보스에 가려짐이 된다.
                float2 original = SAMPLE_TEXTURE2D_X(_BossOcclusionPlayerMask, sampler_PointClamp, uv).rg;
                clip(original.g - 0.001);
                float width = clamp(_OutlineWidthPixels, 1.0, 8.0);
                int radius = (int)ceil(width) + 1;
                // 실루엣 안쪽에서 바깥 경계까지의 거리, 이웃 픽셀 중심에서 (커버리지 - 0.5)만큼 떨어져 있다고 근사해 계단을 줄인다.
                float closest = width + 1.0;
                for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                {
                    float2 neighborUV = uv + float2(x, y) / _ScaledScreenParams.xy;
                    float neighbor = SAMPLE_TEXTURE2D_X(_BossOcclusionPlayerMask, sampler_PointClamp, neighborUV).r;
                    if (neighbor >= 0.999) continue;
                    closest = min(closest, length(float2(x, y)) - 0.5 + neighbor);
                }
                // 실루엣 안쪽으로 외곽선을 그리고, 보스에 가려진 픽셀에서만 남겨 겹친 부분만 보이게 한다.
                float alpha = saturate(width - closest) * original.g;
                return half4(_BaseColor.rgb, _BaseColor.a * alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Screen Fill"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex ScreenVertex
            #pragma fragment FillFragment
            half4 FillFragment(ScreenVaryings input) : SV_Target
            {
                float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;
                ClipToRect(uv, _FillRect);
                half coverage;
                half alpha = FillAmount(uv, coverage);
                clip(alpha - 0.001);
                return half4(_FillColor.rgb, _FillColor.a * alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Hidden Ratio"
            Cull Off
            ZWrite Off
            ZTest Always
            HLSLPROGRAM
            #pragma vertex ScreenVertex
            #pragma fragment RatioFragment
            // C#의 HiddenRatioSize와 같아야 한다.
            #define HIDDEN_RATIO_SIZE 64.0
            // 보스 화면 영역(_FillRect)을 격자로 나눈 표본, r = 보스 커버리지, g = 채워질 양
            float2 RatioFragment(ScreenVaryings input) : SV_Target
            {
                float2 uv = lerp(_FillRect.xy, _FillRect.zw, input.positionCS.xy / HIDDEN_RATIO_SIZE);
                half coverage;
                half amount = FillAmount(uv, coverage);
                return float2(coverage, amount);
            }
            ENDHLSL
        }
    }
}
