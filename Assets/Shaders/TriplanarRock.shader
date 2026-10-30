Shader "TeamOdd/Triplanar Rock"
{
    // UV·탄젠트가 없는 생성 파편 메시에 돌 텍스처를 오브젝트 공간 트라이플래너로 입힌다 (던져도 무늬가 돌과 함께 움직임).
    // 조명은 URP Lit과 같은 UniversalFragmentPBR을 써서 주변 환경과 같은 빛·그림자·SSAO·안개를 받는다.
    Properties
    {
        [MainTexture][NoScaleOffset] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)
        [Normal][NoScaleOffset] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Range(0, 2)) = 1
        _Tiling ("Tiling (Per Meter)", Float) = 0.6
        _BlendSharpness ("Blend Sharpness", Range(1, 16)) = 6
        // 큰 얼룩으로 반복 무늬를 깨고 돌마다 명암이 달라 보이게 함
        _MacroTiling ("Macro Tiling (Per Meter)", Float) = 0.12
        _MacroStrength ("Macro Variation", Range(0, 1)) = 0.6
        // 알베도가 어두운 틈일수록 더 어둡고 거칠게 (마스크 텍스처가 없어서 밝기로 대신함)
        _CavityStrength ("Cavity Strength", Range(0, 2)) = 0.8
        _Smoothness ("Smoothness", Range(0, 1)) = 0.22
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "UniversalMaterialType"="Lit" "IgnoreProjector"="True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _BumpScale;
            float _Tiling;
            half _BlendSharpness;
            float _MacroTiling;
            half _MacroStrength;
            half _CavityStrength;
            half _Smoothness;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            // 던지고 굴러다니는 오브젝트라 라이트맵 변형은 만들지 않고 라이트 프로브로만 간접광을 받는다
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                // 오브젝트 크기를 곱한 오브젝트 공간 위치, 스케일이 달라도 1m당 텍스처 밀도가 같다
                float3 positionOS : TEXCOORD1;
                float3 normalOS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                half3 vertexSH : TEXCOORD4;
                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                    half3 vertexLight : TEXCOORD5;
                #endif
                #ifdef USE_APV_PROBE_OCCLUSION
                    float4 probeOcclusion : TEXCOORD6;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                float3 objectScale = float3(length(GetObjectToWorldMatrix()._m00_m10_m20),
                    length(GetObjectToWorldMatrix()._m01_m11_m21), length(GetObjectToWorldMatrix()._m02_m12_m22));

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.positionOS = input.positionOS.xyz * objectScale;
                output.normalOS = input.normalOS;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);

                half3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                OUTPUT_SH4(positionInputs.positionWS, normalWS, GetWorldSpaceNormalizeViewDir(positionInputs.positionWS), output.vertexSH, output.probeOcclusion);
                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                    output.vertexLight = VertexLighting(positionInputs.positionWS, normalWS);
                #endif
                return output;
            }

            half3 SampleTriplanarAlbedo(float3 position, half3 weights)
            {
                half3 x = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, position.zy).rgb;
                half3 y = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, position.xz).rgb;
                half3 z = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, position.xy).rgb;
                return x * weights.x + y * weights.y + z * weights.z;
            }

            // 축마다 탄젠트 공간 노멀을 오브젝트 공간으로 돌려 합친다 (Whiteout 블렌드, 음의 축은 부호를 뒤집어 거울상 방지)
            half3 SampleTriplanarNormal(float3 position, half3 normal, half3 weights)
            {
                half3 axisSign = step(0.0h, normal) * 2.0h - 1.0h;
                float2 uvX = position.zy * float2(axisSign.x, 1);
                float2 uvY = position.xz * float2(axisSign.y, 1);
                float2 uvZ = position.xy * float2(-axisSign.z, 1);

                half3 tangentX = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvX), _BumpScale);
                half3 tangentY = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvY), _BumpScale);
                half3 tangentZ = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvZ), _BumpScale);
                tangentX.x *= axisSign.x;
                tangentY.x *= axisSign.y;
                tangentZ.x *= -axisSign.z;

                tangentX = half3(tangentX.xy + normal.zy, abs(tangentX.z) * normal.x);
                tangentY = half3(tangentY.xy + normal.xz, abs(tangentY.z) * normal.y);
                tangentZ = half3(tangentZ.xy + normal.xy, abs(tangentZ.z) * normal.z);
                return normalize(tangentX.zyx * weights.x + tangentY.xzy * weights.y + tangentZ.xyz * weights.z);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 normalOS = normalize(input.normalOS);
                half3 weights = pow(abs(normalOS), _BlendSharpness);
                weights /= max(dot(weights, 1.0h), 1e-4h);

                float3 position = input.positionOS * _Tiling;
                half3 albedo = SampleTriplanarAlbedo(position, weights);
                half3 macro = SampleTriplanarAlbedo(input.positionOS * _MacroTiling + 0.37, weights);
                // 텍스처 평균 밝기(약 0.17, 선형)를 기준으로 나눠 큰 얼룩만 남긴다
                half macroShade = Luminance(macro) / 0.17h;
                albedo *= lerp(1.0h, clamp(macroShade, 0.55h, 1.45h), _MacroStrength);

                half cavity = saturate(Luminance(albedo) / 0.17h);
                half occlusion = lerp(1.0h, cavity, _CavityStrength * 0.5h);
                albedo *= lerp(1.0h, cavity, _CavityStrength * 0.35h);

                half3 normalWS = TransformObjectToWorldNormal(SampleTriplanarNormal(position, normalOS, weights));

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = NormalizeNormalPerPixel(normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                    inputData.vertexLighting = input.vertexLight;
                #endif
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                #if defined(USE_APV_PROBE_OCCLUSION)
                    inputData.probeOcclusion = input.probeOcclusion;
                #endif
                #if defined(_SCREEN_SPACE_IRRADIANCE)
                    inputData.bakedGI = SAMPLE_GI(_ScreenSpaceIrradiance, input.positionCS.xy);
                #elif defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2)
                    inputData.bakedGI = SAMPLE_GI(input.vertexSH,
                        GetAbsolutePositionWS(inputData.positionWS),
                        inputData.normalWS,
                        inputData.viewDirectionWS,
                        input.positionCS.xy,
                        input.probeOcclusion,
                        inputData.shadowMask);
                #else
                    inputData.bakedGI = SAMPLE_GI(0, input.vertexSH, inputData.normalWS);
                    inputData.shadowMask = SAMPLE_SHADOWMASK(0);
                #endif

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo * _BaseColor.rgb;
                surfaceData.metallic = 0.0h;
                surfaceData.specular = 0.0h;
                surfaceData.smoothness = _Smoothness * lerp(0.6h, 1.0h, cavity);
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.occlusion = occlusion;
                surfaceData.alpha = 1.0h;

                #ifdef _DBUFFER
                    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
                #endif

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        // SSAO가 쓰는 노멀, 큰 덩어리 그늘만 필요해서 정점 노멀을 그대로 쓴다
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            void Frag(Varyings input, out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                outNormalWS = half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
                #ifdef _WRITE_RENDERING_LAYERS
                    outRenderingLayers = EncodeMeshRenderingLayer();
                #endif
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
