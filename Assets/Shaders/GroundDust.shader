Shader "TeamOdd/Ground Dust"
{
    // 보스가 착지·돌진할 때 발밑에서 일어나는 흙먼지(GroundDust 파티클), 색과 투명도는 정점 색을 따른다.
    // _Mode는 GroundDust가 시스템마다 머티리얼 사본에 넣는다.
    //   0 뭉게 먼지(Billboard): 노이즈로 가장자리를 부풀리고, 안쪽 무늬가 소용돌이치며 수명이 지날수록 얇은 곳부터 깎여 나간다.
    //   1 지면 충격파(Horizontal Billboard): 바닥에 깔린 먼지 고리가 가운데에서 바깥으로 밀려 나가고 안쪽에 옅은 흙막이 남는다.
    //   2 흙 파편(Billboard): 단단한 가장자리의 작은 돌 조각.
    // 장면의 주 광원과 환경광(SH)으로 칠하고, 빛 쪽으로 한 걸음 노이즈를 더 읽어 두꺼운 곳 뒤편을 어둡게(자기 그림자),
    // 역광일 때는 얇은 가장자리가 빛을 통과시켜 밝게 해 부피감을 준다. 바닥·카메라와 닿는 곳은 거리만큼 흐리게 한다(깊이 텍스처 필요).
    // 파티클 정점 스트림: TEXCOORD0.xy = UV, .z = StableRandom.x, .w = AgePercent
    Properties
    {
        _Color ("Color", Color) = (0.74, 0.69, 0.62, 0.55)
        _ChunkColor ("Chunk Color", Color) = (0.36, 0.32, 0.28, 1)
        _NoiseScale ("Noise Scale", Range(0.5, 6)) = 2.2
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.55
        _EdgeSoftness ("Edge Softness", Range(0.02, 0.6)) = 0.22
        _Erosion ("Erosion Over Life", Range(0, 1)) = 0.55
        _Swirl ("Swirl", Range(0, 2)) = 0.8
        _LightStrength ("Light Strength", Range(0, 2)) = 1
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 1
        _SelfShadow ("Self Shadow", Range(0, 1)) = 0.65
        _Scatter ("Back Scatter", Range(0, 2)) = 0.8
        _DepthFade ("Depth Fade (m)", Range(0.01, 2)) = 0.4
        _CameraFade ("Camera Fade (m)", Range(0.01, 3)) = 0.8
        [HideInInspector] _Mode ("Mode", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+70" "IgnoreProjector"="True" }

        Pass
        {
            Name "GroundDust"
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _ChunkColor;
                float _NoiseScale;
                float _NoiseStrength;
                float _EdgeSoftness;
                float _Erosion;
                float _Swirl;
                float _LightStrength;
                float _AmbientStrength;
                float _SelfShadow;
                float _Scatter;
                float _DepthFade;
                float _CameraFade;
                float _Mode;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float4 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float4 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float eyeDepth : TEXCOORD3;
            };

            float Hash(float2 value)
            {
                return frac(sin(dot(value, float2(127.1, 311.7))) * 43758.5453);
            }

            float ValueNoise(float2 value)
            {
                float2 cell = floor(value);
                float2 local = frac(value);
                float2 blend = local * local * (3.0 - 2.0 * local);
                float a = Hash(cell);
                float b = Hash(cell + float2(1.0, 0.0));
                float c = Hash(cell + float2(0.0, 1.0));
                float d = Hash(cell + float2(1.0, 1.0));
                return lerp(lerp(a, b, blend.x), lerp(c, d, blend.x), blend.y);
            }

            float Fbm(float2 value)
            {
                float sum = 0.0;
                float amplitude = 0.5;
                for (int octave = 0; octave < 3; octave++)
                {
                    sum += ValueNoise(value) * amplitude;
                    value = value * 2.03 + 17.17;
                    amplitude *= 0.5;
                }
                return sum / 0.875;
            }

            float2 Rotate(float2 value, float angle)
            {
                float s, c;
                sincos(angle, s, c);
                return float2(c * value.x - s * value.y, s * value.x + c * value.y);
            }

            // 입자마다 다른 방향으로 천천히 돌고, 낮은 주파수 노이즈로 좌표를 비틀어 안쪽 무늬가 굽이치게 함
            float PuffBody(float2 p, float seed, float age)
            {
                float spin = (seed > 0.5 ? 1.0 : -1.0) * age * _Swirl * 1.6;
                float2 q = Rotate(p, seed * 6.2832 + spin) * _NoiseScale / (1.0 + age * 0.6);
                float2 warp = float2(ValueNoise(q * 0.6 + seed * 13.0 + age * 0.9), ValueNoise(q * 0.6 + seed * 29.0 - age * 0.9)) - 0.5;
                float noise = Fbm(q + warp * _Swirl * 1.6 + seed * 97.0);
                return (1.0 - length(p)) + (noise - 0.5) * _NoiseStrength * 2.0;
            }

            // 고리 앞머리가 바깥으로 빠르게 밀려 나가다 느려지고, 지나간 자리에 옅은 흙막이 남음
            float RingBody(float2 p, float seed, float age)
            {
                float radius = length(p);
                float front = lerp(0.2, 0.9, 1.0 - pow(1.0 - age, 3.0));
                float width = lerp(0.3, 0.14, age);
                float band = 1.0 - abs(radius - front) / width;
                float wake = radius < front ? 0.45 * (radius / front) : 0.0;
                float noise = Fbm(Rotate(p, seed * 6.2832) * _NoiseScale * 1.8 + seed * 97.0);
                float body = max(band, wake) + (noise - 0.5) * _NoiseStrength * 2.4;
                return body * smoothstep(1.0, 0.85, radius);
            }

            // 낮은 주파수 노이즈로 둘레를 찌그러뜨린 돌 조각
            float ChunkBody(float2 p, float seed)
            {
                float noise = ValueNoise(p * 1.7 + seed * 53.0);
                return 1.0 - length(p) * (1.0 + (noise - 0.5) * 1.1);
            }

            float Body(float2 p, float seed, float age)
            {
                if (_Mode > 1.5) return ChunkBody(p, seed);
                return _Mode > 0.5 ? RingBody(p, seed, age) : PuffBody(p, seed, age);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.color = input.color;
                output.uv = float4(input.uv.xy * 2.0 - 1.0, input.uv.zw);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.eyeDepth = -TransformWorldToView(output.positionWS).z;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv.xy;
                float seed = input.uv.z;
                float age = saturate(input.uv.w);
                bool isFlat = _Mode > 0.5 && _Mode < 1.5;
                bool isChunk = _Mode > 1.5;

                float body = Body(p, seed, age);
                float amount;
                if (isChunk)
                {
                    amount = smoothstep(0.0, 0.08, body);
                }
                else
                {
                    // 얇은 곳(body가 작은 곳)부터 깎여 나가고, 가장자리는 속보다 성기게
                    float threshold = _Erosion * age;
                    amount = smoothstep(threshold, threshold + _EdgeSoftness, body);
                    amount *= lerp(0.55, 1.0, smoothstep(threshold, threshold + 0.5, body));
                }

                // 사각형 판의 축: 서 있는 판은 카메라 오른쪽·위, 바닥에 깔린 판은 월드 x·z
                float3 axisX = isFlat ? float3(1.0, 0.0, 0.0) : UNITY_MATRIX_V[0].xyz;
                float3 axisY = isFlat ? float3(0.0, 0.0, 1.0) : UNITY_MATRIX_V[1].xyz;
                float3 axisZ = isFlat ? float3(0.0, 1.0, 0.0) : UNITY_MATRIX_V[2].xyz;

                // 둥근 덩어리의 면 방향, 바닥 판은 거의 위를 향함
                float bulge = isFlat ? 0.35 : 1.0;
                float3 normalLocal = normalize(float3(p * bulge, sqrt(saturate(1.0 - dot(p, p))) + 0.25));
                float3 normalWS = normalize(axisX * normalLocal.x + axisY * normalLocal.y + axisZ * normalLocal.z);

                Light mainLight = GetMainLight();
                float3 lightDir = mainLight.direction;
                float wrap = saturate((dot(normalWS, lightDir) + 0.5) / 1.5);

                // 빛 쪽으로 한 걸음 옮겨 읽은 밀도가 더 높으면 두꺼운 덩어리의 뒤편
                float2 lightOnQuad = float2(dot(lightDir, axisX), dot(lightDir, axisY)) * 0.3;
                float occlusion = saturate((Body(p + lightOnQuad, seed, age) - body) * 2.5);
                float shadow = 1.0 - occlusion * _SelfShadow;

                half3 albedo = (isChunk ? _ChunkColor.rgb : _Color.rgb) * input.color.rgb;
                half3 lighting = mainLight.color * (wrap * shadow * _LightStrength) + SampleSH(normalWS) * _AmbientStrength;
                half3 color = albedo * lighting;

                if (isChunk == false)
                {
                    // 역광일 때 얇은 가장자리로 빛이 새어 나옴
                    float3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);
                    float backLight = pow(saturate(dot(-viewDir, lightDir)), 4.0);
                    float thin = 1.0 - saturate(body * 2.0);
                    color += mainLight.color * albedo * backLight * thin * _Scatter;
                }

                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float depthFade = isChunk ? 0.05 : (isFlat ? _DepthFade * 0.25 : _DepthFade);
                amount *= saturate((sceneDepth - input.eyeDepth) / depthFade);
                amount *= saturate((input.eyeDepth - _ProjectionParams.y) / _CameraFade);

                half opacity = isChunk ? _ChunkColor.a : _Color.a;
                return half4(color, amount * opacity * input.color.a);
            }
            ENDHLSL
        }
    }
}
