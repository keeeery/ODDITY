Shader "TeamOdd/Boss Occlusion Outline"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1,1,1,1)
        _OutlineWidthPixels ("Width (Pixels)", Range(1,8)) = 2
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
        CBUFFER_END
        TEXTURE2D_X(_BossOcclusionBossDepth);
        TEXTURE2D_X(_BossOcclusionPlayerMask);
        float EyeDepth(float rawDepth)
        {
            return unity_OrthoParams.w > 0.5 ? LinearDepthToEyeDepth(rawDepth) : LinearEyeDepth(rawDepth, _ZBufferParams);
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
        ENDHLSL

        Pass
        {
            Name "Boss Depth"
            Cull Off
            // 보스 메시끼리 겹치면 가장 가까운 면만 남기도록 전용 깊이 버퍼로 판정한다.
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex MaskVertex
            #pragma fragment BossFragment
            // MSAA로 그린 뒤 Resolve되어 r = 보스 커버리지, g = 커버리지 × 깊이가 된다.
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
            ZWrite Off
            ZTest Always
            HLSLPROGRAM
            #pragma vertex MaskVertex
            #pragma fragment PlayerFragment
            float2 PlayerFragment(Varyings input) : SV_Target
            {
                float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;
                float2 boss = SAMPLE_TEXTURE2D_X(_BossOcclusionBossDepth, sampler_PointClamp, uv).rg;
                if (boss.r <= 0.001) return float2(1.0, 0.0);
                float bossDepth = boss.g / boss.r;
                float sceneDepth = EyeDepth(SampleSceneDepth(uv));
                // 보스가 플레이어보다 앞에 있고, 그 보스가 다른 물체에 가리지 않고 화면에 보일 때만 가려진 부분으로 본다.
                // 보스 커버리지만큼만 가려졌다고 보아 보스 경계에서 외곽선이 부드럽게 잘리게 한다.
                float hidden = bossDepth < input.eyeDepth - 0.02 && sceneDepth + 0.05 >= bossDepth ? boss.r : 0.0;
                return float2(1.0, hidden);
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
            struct ScreenVaryings { float4 positionCS : SV_POSITION; };
            ScreenVaryings ScreenVertex(uint vertexID : SV_VertexID)
            {
                ScreenVaryings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(vertexID);
                return output;
            }
            half4 ScreenFragment(ScreenVaryings input) : SV_Target
            {
                float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;
                clip(min(min(uv.x - _OutlineRect.x, uv.y - _OutlineRect.y), min(_OutlineRect.z - uv.x, _OutlineRect.w - uv.y)));
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
    }
}
