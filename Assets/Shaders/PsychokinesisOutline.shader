Shader "TeamOdd/Psychokinesis Outline"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0,1,0,1)
        _OutlineWidthPixels ("Width (Pixels)", Range(1,8)) = 3
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
        float EyeDepth(float rawDepth)
        {
            return unity_OrthoParams.w > 0.5 ? LinearDepthToEyeDepth(rawDepth) : LinearEyeDepth(rawDepth, _ZBufferParams);
        }
        ENDHLSL

        Pass
        {
            Name "Silhouette"
            Cull Back
            ZWrite Off
            ZTest Always
            HLSLPROGRAM
            #pragma vertex MaskVertex
            #pragma fragment MaskFragment
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
            float2 MaskFragment(Varyings input) : SV_Target
            {
                float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;
                float sceneDepth = EyeDepth(SampleSceneDepth(uv));
                clip(sceneDepth + 0.01 - input.eyeDepth);
                return float2(1.0, input.eyeDepth);
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
            TEXTURE2D_X(_PsychokinesisOutlineMask);
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings ScreenVertex(uint vertexID : SV_VertexID)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(vertexID);
                return output;
            }
            half4 ScreenFragment(Varyings input) : SV_Target
            {
                float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;
                // 원본 픽셀은 칠하지 않고 매 프레임 Mask의 바깥 경계만 합성한다.
                clip(min(min(uv.x - _OutlineRect.x, uv.y - _OutlineRect.y), min(_OutlineRect.z - uv.x, _OutlineRect.w - uv.y)));
                float2 original = SAMPLE_TEXTURE2D_X(_PsychokinesisOutlineMask, sampler_PointClamp, uv).rg;
                clip(0.5 - original.r);
                float sceneDepth = EyeDepth(SampleSceneDepth(uv));
                float width = clamp(_OutlineWidthPixels, 1.0, 8.0);
                int radius = (int)ceil(width);
                float closest = (width + 1.0) * (width + 1.0);
                for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                {
                    float distanceSquared = (float)(x * x + y * y);
                    if (distanceSquared >= closest) continue;
                    float2 neighborUV = uv + float2(x, y) / _ScaledScreenParams.xy;
                    float2 neighbor = SAMPLE_TEXTURE2D_X(_PsychokinesisOutlineMask, sampler_PointClamp, neighborUV).rg;
                    if (neighbor.r > 0.5 && neighbor.g <= sceneDepth + 0.02) closest = distanceSquared;
                }
                float alpha = saturate(width + 0.5 - sqrt(closest));
                return half4(_BaseColor.rgb, _BaseColor.a * alpha);
            }
            ENDHLSL
        }
    }
}
