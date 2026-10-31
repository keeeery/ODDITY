// 구워 둔 이동 가능 범위 마스크(바닥 1, 빈 곳 0)를 읽어 바닥은 단색으로 채우고 경계에 외곽선을 그리는 UI 셰이더
// Resources 아래에 두어 씬에서 참조하지 않아도 빌드에 포함된다.
Shader "Hidden/TeamOdd/MinimapGround"
{
    Properties
    {
        [PerRendererData] _MainTex ("Mask", 2D) = "black" {}
        _BackgroundColor ("Background", Color) = (0, 0, 0, 0.5)
        _FillColor ("Fill", Color) = (0.3, 0.3, 0.3, 1)
        _OutlineColor ("Outline", Color) = (1, 1, 1, 1)
        _OutlineWidth ("Outline Width (texel)", Float) = 1.5
        _MaskTexelSize ("Mask Texel Size", Vector) = (0.00390625, 0.00390625, 0, 0)
        // xy: 미니맵 Yaw의 sin, cos / zw: 플레이어 위치의 마스크 UV
        _View ("View", Vector) = (0, 1, 0.5, 0.5)
        // xy: 미니맵 지름이 마스크 가로·세로에서 차지하는 비율
        _ViewScale ("View Scale", Vector) = (1, 1, 0, 0)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _BackgroundColor;
            fixed4 _FillColor;
            fixed4 _OutlineColor;
            float _OutlineWidth;
            float4 _MaskTexelSize;
            float4 _View;
            float4 _ViewScale;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                // 미니맵 좌표(위 = Yaw 방향)를 북쪽이 위인 마스크 좌표로 돌린다.
                float2 local = input.uv - 0.5;
                float2 world = float2(local.x * _View.y + local.y * _View.x, -local.x * _View.x + local.y * _View.y);
                output.uv = _View.zw + world * _ViewScale.xy;
                return output;
            }

            float Mask(float2 uv)
            {
                return tex2D(_MainTex, uv).r;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                float mask = Mask(input.uv);

                // 외곽선 두께만큼 떨어진 8방향과 그 절반 거리 8방향 중 가장 빈 곳을 찾는다.
                float2 offset = _MaskTexelSize.xy * _OutlineWidth;
                float2 diagonal = offset * 0.7071;
                float neighbor = 1;
                neighbor = min(neighbor, Mask(input.uv + float2(offset.x, 0)));
                neighbor = min(neighbor, Mask(input.uv - float2(offset.x, 0)));
                neighbor = min(neighbor, Mask(input.uv + float2(0, offset.y)));
                neighbor = min(neighbor, Mask(input.uv - float2(0, offset.y)));
                neighbor = min(neighbor, Mask(input.uv + diagonal));
                neighbor = min(neighbor, Mask(input.uv - diagonal));
                neighbor = min(neighbor, Mask(input.uv + float2(diagonal.x, -diagonal.y)));
                neighbor = min(neighbor, Mask(input.uv + float2(-diagonal.x, diagonal.y)));
                neighbor = min(neighbor, Mask(input.uv + float2(offset.x, 0) * 0.5));
                neighbor = min(neighbor, Mask(input.uv - float2(offset.x, 0) * 0.5));
                neighbor = min(neighbor, Mask(input.uv + float2(0, offset.y) * 0.5));
                neighbor = min(neighbor, Mask(input.uv - float2(0, offset.y) * 0.5));
                neighbor = min(neighbor, Mask(input.uv + diagonal * 0.5));
                neighbor = min(neighbor, Mask(input.uv - diagonal * 0.5));
                neighbor = min(neighbor, Mask(input.uv + float2(diagonal.x, -diagonal.y) * 0.5));
                neighbor = min(neighbor, Mask(input.uv + float2(-diagonal.x, diagonal.y) * 0.5));

                // 바닥인데 주변에 빈 곳이 있으면 외곽선
                float outline = saturate(mask - neighbor);
                fixed4 color = lerp(_BackgroundColor, _FillColor, mask);
                color = lerp(color, _OutlineColor, outline);
                return color * input.color;
            }
            ENDCG
        }
    }
}
