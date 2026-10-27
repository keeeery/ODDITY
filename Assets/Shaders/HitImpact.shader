Shader "TeamOdd/Hit Impact"
{
    // 강한 피격 지점의 흰색 충격(빛줄기 두 겹, 베기 섬광, 중심 섬광), 카메라를 향한 사각형에 절차적으로 그린다.
    // 진행도(_Progress)와 시드(_Seed)는 HitImpact가 넘긴다.
    // 빛줄기는 카메라 깊이와 비교해 몸에 가려진 곳에서 흐려진다(깊이 텍스처 필요).
    Properties
    {
        [HDR] _Color ("Color", Color) = (1.35, 1.32, 1.28, 1)
        [HDR] _TipColor ("Tip Color", Color) = (0.5, 0.95, 1.5, 1)
        _Squash ("Squash (Vertical)", Range(0.3, 1)) = 0.72
        _AxisBias ("Slash Axis Bias", Range(0, 1)) = 0.35
        _RayDensity ("Ray Density", Range(0, 1)) = 0.3
        _RayWidth ("Ray Width", Range(0.002, 0.08)) = 0.042
        _FineRayWidth ("Fine Ray Width", Range(0.001, 0.03)) = 0.012
        _CoreSize ("Core Size", Range(0, 0.5)) = 0.16
        _SlashStrength ("Slash Strength", Range(0, 2)) = 1.2
        _BackDarken ("Back Darken", Range(0, 1)) = 0.35
        _DepthSoftness ("Depth Softness (m)", Range(0.01, 2)) = 0.35
        _CoreOnTop ("Core On Top", Range(0, 1)) = 0.8
        [HideInInspector] _Progress ("Progress", Range(0, 1)) = 0
        [HideInInspector] _Seed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+100" "IgnoreProjector"="True" }

        Pass
        {
            Name "HitImpact"
            Cull Off
            ZWrite Off
            ZTest Always
            // 미리 곱한 알파, 알파만큼 배경을 눌러 밝은 곳에서도 보이게 함
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _TipColor;
                float _Squash;
                float _AxisBias;
                float _RayDensity;
                float _RayWidth;
                float _FineRayWidth;
                float _CoreSize;
                float _SlashStrength;
                float _BackDarken;
                float _DepthSoftness;
                float _CoreOnTop;
                float _Progress;
                float _Seed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float eyeDepth : TEXCOORD1; };

            struct RayLayer
            {
                float Count;
                float Density;    // 칸에 줄기가 있을 확률
                float Width;
                float MinLength;
                float MaxLength;
                float Speed;
                float Salt;       // 겹마다 무늬를 다르게 하는 시드 오프셋
                float Brightness;
            };

            float Hash(float value)
            {
                return frac(sin(value * 12.9898 + _Seed * 78.233) * 43758.5453);
            }

            float EaseOut(float value)
            {
                float inverse = 1.0 - saturate(value);
                return 1.0 - inverse * inverse * inverse;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv * 2.0 - 1.0;
                output.eyeDepth = -TransformWorldToView(positionWS).z;
                return output;
            }

            // 각도 칸 하나의 빛줄기, x: 덮인 정도, y: 뿌리(0)~끝(1) 위치
            float2 Ray(RayLayer layer, float cell, float angle01, float radius, float progress, float pixelSize)
            {
                float slot = fmod(cell + layer.Count, layer.Count) + layer.Salt;
                float exists = step(Hash(slot), layer.Density);
                float jitter = Hash(slot + 17.31);
                float lengthRandom = Hash(slot + 41.77);
                float widthRandom = Hash(slot + 73.13);
                float delayRandom = Hash(slot + 5.71);

                float center = (cell + 0.2 + 0.6 * jitter) / layer.Count;
                // 베기 방향에 가까울수록 길게
                float axis = cos((center - 0.5) * TWO_PI);
                float rayLength = lerp(layer.MinLength, layer.MaxLength, lengthRandom) * (1.0 - _AxisBias + _AxisBias * axis * axis);
                float width = layer.Width * lerp(0.3, 1.0, widthRandom * widthRandom);

                // 줄기마다 출발을 조금씩 어긋나게
                float rayProgress = saturate(progress - delayRandom * 0.12);
                float head = rayLength * EaseOut(rayProgress * layer.Speed);
                float tail = rayLength * pow(saturate(rayProgress * 1.3 - 0.15), 1.4);
                float along = (radius - tail) / max(head - tail, 0.0001);
                float inside = step(0.0, along) * step(along, 1.0);

                float perpendicular = abs(sin((angle01 - center) * TWO_PI)) * radius;
                float taper = pow(saturate(1.0 - along), 0.8) * saturate(along * 4.0 + 0.1);
                float coverage = saturate((width * taper - perpendicular) / pixelSize + 0.5);
                return float2(coverage * exists * inside * lerp(0.6, 1.0, jitter) * layer.Brightness, saturate(along));
            }

            float2 Brighter(float2 a, float2 b)
            {
                return b.x > a.x ? b : a;
            }

            // 칸 경계에서 잘리지 않도록 이웃 칸까지 봄
            float2 RayLayerAmount(RayLayer layer, float angle01, float radius, float progress, float pixelSize)
            {
                float cell = floor(angle01 * layer.Count);
                float2 amount = Ray(layer, cell - 1.0, angle01, radius, progress, pixelSize);
                amount = Brighter(amount, Ray(layer, cell, angle01, radius, progress, pixelSize));
                return Brighter(amount, Ray(layer, cell + 1.0, angle01, radius, progress, pixelSize));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                // 세로로 눌러 타원으로
                float2 squashed = float2(uv.x, uv.y / max(_Squash, 0.01));
                float progress = saturate(_Progress);
                float fade = 1.0 - progress;
                float radius = length(squashed);
                float angle01 = atan2(squashed.y, squashed.x) / TWO_PI + 0.5;
                float pixelSize = max(fwidth(radius), 0.0001);

                RayLayer thick = { 20.0, _RayDensity, _RayWidth, 0.5, 1.0, 2.6, 0.0, 1.0 };
                RayLayer fine = { 44.0, _RayDensity * 0.75, _FineRayWidth, 0.6, 1.0, 3.2, 100.0, 0.85 };
                float2 rays = Brighter(RayLayerAmount(thick, angle01, radius, progress, pixelSize),
                                       RayLayerAmount(fine, angle01, radius, progress, pixelSize));
                rays.x *= 1.0 - smoothstep(0.5, 1.0, progress);

                float coreSize = max(_CoreSize * lerp(1.0, 0.3, progress), 0.001);
                float core = exp(-(radius * radius) / (coreSize * coreSize)) * fade * fade * 1.6;
                float halo = exp(-(radius * radius) / 0.15) * 0.4 * fade * fade;

                float slashLength = 0.25 + 0.75 * EaseOut(progress * 2.2);
                float slashThickness = max(0.02 * fade, 0.001);
                float slash = exp(-(uv.x * uv.x) / (slashLength * slashLength)) * exp(-(uv.y * uv.y) / (slashThickness * slashThickness)) * fade * _SlashStrength;

                // 앞을 가린 물체만큼 흐려짐
                float2 screenUV = input.positionCS.xy / _ScaledScreenParams.xy;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float visible = saturate((sceneDepth - input.eyeDepth) / _DepthSoftness + 1.0);
                rays.x *= visible;
                halo *= visible;
                float glowVisible = lerp(visible, 1.0, _CoreOnTop);
                core *= glowVisible;
                slash *= glowVisible;

                float glow = max(core, slash);
                float amount = saturate(max(rays.x, glow) + halo);
                // 사각형 모서리 숨김
                amount *= saturate((1.0 - length(uv)) * 6.0);

                // 줄기 끝과 바깥쪽일수록 _TipColor로
                float tint = saturate((rays.x > glow ? rays.y : 0.0) + saturate(radius - 0.3) * 0.3);
                half3 color = lerp(_Color.rgb, _TipColor.rgb, tint);

                return half4(color * amount, amount * _BackDarken * _Color.a);
            }
            ENDHLSL
        }
    }
}
