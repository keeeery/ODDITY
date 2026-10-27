// Knife/Hologram Shader Unlit(ASE, URP)의 Forward 패스를 복사한 시간 정지 막, TimeFreezeOverlayFeature가 그린다.
// 추가: 높이 기준 쓸어 올리기(전역 진행도·높이 범위), 경계선 띠, 정지 순간 글리치(_TimeFreezeOverlayGlitch).
Shader "TeamOdd/Time Freeze Hologram"
{
	Properties
	{
		[HideInInspector] _EmissionColor("Emission Color", Color) = (1,1,1,1)
		[HideInInspector] _AlphaCutoff("Alpha Cutoff ", Range(0, 1)) = 0.5
		[HDR]_MainColor("Main Color", Color) = (0.620945,1.420074,3.953349,0.05098039)
		[NoScaleOffset]_Line1("Line 1", 2D) = "white" {}
		_Line1Speed("Line 1 Speed", Float) = -3.57
		_Line1Frequency("Line 1 Frequency", Float) = 100
		_Line1Hardness("Line 1 Hardness", Float) = 1.45
		_Line1InvertedThickness("Line 1 Inverted Thickness", Range( 0 , 1)) = 0
		_Line1Alpha("Line 1 Alpha", Float) = 0.15
		[NoScaleOffset]_Line2("Line 2", 2D) = "white" {}
		_Line2Speed("Line 2 Speed", Float) = -1
		_Line2Frequency("Line 2 Frequency", Float) = 1
		_Line2Hardness("Line 2 Hardness", Float) = 2
		_Line2InvertedThickness("Line 2 Inverted Thickness", Range( 0 , 1)) = 0.255
		_Line2Alpha("Line 2 Alpha", Float) = 0.1
		[NoScaleOffset]_LineGlitch("Line Glitch", 2D) = "white" {}
		_LineGlitchOffset("Line Glitch Offset", Vector) = (0.03,0,0,0)
		_RandomGlitchOffset("Random Glitch Offset", Vector) = (-0.5,0,0,0)
		_RandomGlitchAmount("Random Glitch Amount", Range( 0 , 1)) = 0.089
		_ColorGlitchAffect("Color Glitch Affect", Range( 0 , 1)) = 0.5
		_LineGlitchSpeed("Line Glitch Speed", Float) = -0.26
		_LineGlitchFrequency("Line Glitch Frequency", Float) = 0.2
		_LineGlitchHardness("Line Glitch Hardness", Float) = 5
		_LineGlitchInvertedThickness("Line Glitch Inverted Thickness", Range( 0 , 1)) = 0.825
		_FresnelScale("Fresnel Scale", Float) = 1
		_FresnelPower("Fresnel Power", Float) = 2
		_FresnelAlphaScale("Fresnel Alpha Scale", Float) = 1
		_FresnelAlphaPower("Fresnel Alpha Power", Float) = 2
		_NormalMap("NormalMap", 2D) = "bump" {}
		_NormalScale("NormalScale", Float) = 0
		_SoftIntersection2Distance("Soft Intersection 2 Distance", Float) = 0
		_SoftIntersection1Distance("Soft Intersection 1 Distance", Float) = 0
		_NormalAffect("NormalAffect", Range( 0 , 1)) = 0
		_MaskCenter("Mask Center", Vector) = (0,0,0,0)
		_SoftIntersection2Intensity("Soft Intersection 2 Intensity", Float) = 1
		_SoftIntersection2Affect("Soft Intersection 2 Affect", Range( 0 , 1)) = 1
		_SoftIntersection1Intensity("Soft Intersection 1 Intensity", Float) = 1
		_SoftIntersection1Affect("Soft Intersection 1 Affect", Range( 0 , 1)) = 1
		_MaskSize("Mask Size", Vector) = (0,0,0,0)
		_RandomGlitchConstant("Random Glitch Constant", Range( 0 , 1)) = 0
		_DissolveScale("Dissolve Scale", Vector) = (0.1,1.01,5,0)
		_GrainScale("Grain Scale", Vector) = (50,50,50,0)
		_GrainAffect("Grain Affect", Range( 0 , 1)) = 1
		[Toggle(_COLORGLITCHFEATURE_ON)] _ColorGlitchFeature("Color Glitch Feature", Float) = 0
		[Toggle(_GRAINFEATURE_ON)] _GrainFeature("Grain Feature", Float) = 0
		_GrainValues("Grain Values", Vector) = (0,1,0,0)
		_RandomGlitchTiling("Random Glitch Tiling", Float) = 2.83
		_MaskFalloff("Mask Falloff", Float) = 0
		[Toggle(_LINE2FEATURE_ON)] _Line2Feature("Line 2 Feature", Float) = 0
		[Toggle(_FRESNELFEATURE_ON)] _FresnelFeature("Fresnel Feature", Float) = 0
		[Toggle(_LINEGLITCHFEATURE_ON)] _LineGlitchFeature("Line Glitch Feature", Float) = 0
		[Toggle(_RANDOMGLITCHFEATURE_ON)] _RandomGlitchFeature("Random Glitch Feature", Float) = 0
		[KeywordEnum(Off,Alpha,Color)] _SoftIntersection2Feature("Soft Intersection 2 Feature", Float) = 0
		[KeywordEnum(Off,Alpha,Color)] _SoftIntersection1Feature("Soft Intersection 1 Feature", Float) = 0
		[Toggle(_LINE1FEATURE_ON)] _Line1Feature("Line 1 Feature", Float) = 0
		[Toggle(_LINEBOTHFEATURE_ON)] _LineBothFeature("Line Both Feature", Float) = 0
		_DissolveHide("Dissolve Hide", Range( -1 , 1)) = -1
		[Toggle(_DISSOLVEFEATURE_ON)] _DissolveFeature("Dissolve Feature", Float) = 0
		[Toggle(_MASKFEATURE_ON)] _MaskFeature("Mask Feature", Float) = 0
		[Toggle(_NORMALMAPFEATURE_ON)] _NormalMapFeature("Normal Map Feature", Float) = 0
		[KeywordEnum(X,Y,Z)] _PositionFeature("Position Feature", Float) = 1
		[KeywordEnum(World,Local,Custom)] _PositionSpaceFeature("Position Space Feature", Float) = 0
		_PositionDirection("Position Direction", Float) = 1
		_RandomOffset("Random Offset", Float) = 0
		_AlphaMask("Alpha Mask", 2D) = "white" {}
		_AlphaMaskAffect("Alpha Mask Affect", Range( 0 , 1)) = 0.5
		[Toggle(_ALPHAMASKFEATURE_ON)] _AlphaMaskFeature("Alpha Mask Feature", Float) = 0
		[HideInInspector][Toggle]_ZWrite("ZWrite", Float) = 0
		[Enum(UnityEngine.Rendering.CullMode)]_CullMode("Cull Mode", Float) = 2
		[Toggle(_MASKLOCALFEATURE_ON)] _MaskLocalFeature("Mask Local Feature", Float) = 0
		_MaskInversion("Mask Inversion", Range( 0 , 1)) = 0
		_Voxelization("Voxelization", Float) = 100
		_VoxelizationAffect("Voxelization Affect", Range( 0 , 1)) = 1
		[Toggle(_VOXELIZATIONFEATURE_ON)] _VoxelizationFeature("Voxelization Feature", Float) = 0
		_Alpha("Alpha", Range( 0 , 1)) = 1
		[Header(Time Freeze)]
		[HDR]_EdgeColor("Edge Color", Color) = (0.8,0.95,2,0.9)
		_EdgeWidth("Edge Width (m)", Float) = 0.15
		_FreezeGlitchAmount("Freeze Glitch Amount", Range( 0 , 1)) = 0.35
		[HideInInspector] _texcoord( "", 2D ) = "white" {}

		[HideInInspector]_QueueOffset("_QueueOffset", Float) = 0
        [HideInInspector]_QueueControl("_QueueControl", Float) = -1
		//_TessPhongStrength( "Tess Phong Strength", Range( 0, 1 ) ) = 0.5
		//_TessValue( "Tess Max Tessellation", Range( 1, 32 ) ) = 16
		//_TessMin( "Tess Min Distance", Float ) = 10
		//_TessMax( "Tess Max Distance", Float ) = 25
		//_TessEdgeLength ( "Tess Edge length", Range( 2, 50 ) ) = 16
		//_TessMaxDisp( "Tess Max Displacement", Float ) = 25
	}

	SubShader
	{
		LOD 0

		
		Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
		
		Cull [_CullMode]
		AlphaToMask Off
		
		HLSLINCLUDE
		#pragma target 3.0

		#pragma prefer_hlslcc gles
		#pragma exclude_renderers d3d11_9x 

		#ifndef ASE_TESS_FUNCS
		#define ASE_TESS_FUNCS
		float4 FixedTess( float tessValue )
		{
			return tessValue;
		}
		
		float CalcDistanceTessFactor (float4 vertex, float minDist, float maxDist, float tess, float4x4 o2w, float3 cameraPos )
		{
			float3 wpos = mul(o2w,vertex).xyz;
			float dist = distance (wpos, cameraPos);
			float f = clamp(1.0 - (dist - minDist) / (maxDist - minDist), 0.01, 1.0) * tess;
			return f;
		}

		float4 CalcTriEdgeTessFactors (float3 triVertexFactors)
		{
			float4 tess;
			tess.x = 0.5 * (triVertexFactors.y + triVertexFactors.z);
			tess.y = 0.5 * (triVertexFactors.x + triVertexFactors.z);
			tess.z = 0.5 * (triVertexFactors.x + triVertexFactors.y);
			tess.w = (triVertexFactors.x + triVertexFactors.y + triVertexFactors.z) / 3.0f;
			return tess;
		}

		float CalcEdgeTessFactor (float3 wpos0, float3 wpos1, float edgeLen, float3 cameraPos, float4 scParams )
		{
			float dist = distance (0.5 * (wpos0+wpos1), cameraPos);
			float len = distance(wpos0, wpos1);
			float f = max(len * scParams.y / (edgeLen * dist), 1.0);
			return f;
		}

		float DistanceFromPlane (float3 pos, float4 plane)
		{
			float d = dot (float4(pos,1.0f), plane);
			return d;
		}

		bool WorldViewFrustumCull (float3 wpos0, float3 wpos1, float3 wpos2, float cullEps, float4 planes[6] )
		{
			float4 planeTest;
			planeTest.x = (( DistanceFromPlane(wpos0, planes[0]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos1, planes[0]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos2, planes[0]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.y = (( DistanceFromPlane(wpos0, planes[1]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos1, planes[1]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos2, planes[1]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.z = (( DistanceFromPlane(wpos0, planes[2]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos1, planes[2]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos2, planes[2]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.w = (( DistanceFromPlane(wpos0, planes[3]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos1, planes[3]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos2, planes[3]) > -cullEps) ? 1.0f : 0.0f );
			return !all (planeTest);
		}

		float4 DistanceBasedTess( float4 v0, float4 v1, float4 v2, float tess, float minDist, float maxDist, float4x4 o2w, float3 cameraPos )
		{
			float3 f;
			f.x = CalcDistanceTessFactor (v0,minDist,maxDist,tess,o2w,cameraPos);
			f.y = CalcDistanceTessFactor (v1,minDist,maxDist,tess,o2w,cameraPos);
			f.z = CalcDistanceTessFactor (v2,minDist,maxDist,tess,o2w,cameraPos);

			return CalcTriEdgeTessFactors (f);
		}

		float4 EdgeLengthBasedTess( float4 v0, float4 v1, float4 v2, float edgeLength, float4x4 o2w, float3 cameraPos, float4 scParams )
		{
			float3 pos0 = mul(o2w,v0).xyz;
			float3 pos1 = mul(o2w,v1).xyz;
			float3 pos2 = mul(o2w,v2).xyz;
			float4 tess;
			tess.x = CalcEdgeTessFactor (pos1, pos2, edgeLength, cameraPos, scParams);
			tess.y = CalcEdgeTessFactor (pos2, pos0, edgeLength, cameraPos, scParams);
			tess.z = CalcEdgeTessFactor (pos0, pos1, edgeLength, cameraPos, scParams);
			tess.w = (tess.x + tess.y + tess.z) / 3.0f;
			return tess;
		}

		float4 EdgeLengthBasedTessCull( float4 v0, float4 v1, float4 v2, float edgeLength, float maxDisplacement, float4x4 o2w, float3 cameraPos, float4 scParams, float4 planes[6] )
		{
			float3 pos0 = mul(o2w,v0).xyz;
			float3 pos1 = mul(o2w,v1).xyz;
			float3 pos2 = mul(o2w,v2).xyz;
			float4 tess;

			if (WorldViewFrustumCull(pos0, pos1, pos2, maxDisplacement, planes))
			{
				tess = 0.0f;
			}
			else
			{
				tess.x = CalcEdgeTessFactor (pos1, pos2, edgeLength, cameraPos, scParams);
				tess.y = CalcEdgeTessFactor (pos2, pos0, edgeLength, cameraPos, scParams);
				tess.z = CalcEdgeTessFactor (pos0, pos1, edgeLength, cameraPos, scParams);
				tess.w = (tess.x + tess.y + tess.z) / 3.0f;
			}
			return tess;
		}
		#endif //ASE_TESS_FUNCS

		ENDHLSL

		
		Pass
		{
			
			Name "TimeFreezeHologram"
			
			Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
			ZWrite Off
			ZTest LEqual
			// 원래 메시와 Z-파이팅 방지
			Offset -1 , -1
			ColorMask RGBA
			

			HLSLPROGRAM
			
			#define ASE_ABSOLUTE_VERTEX_POS 1
			#define _RECEIVE_SHADOWS_OFF 1
			#define ASE_SRP_VERSION 999999
			#define REQUIRE_DEPTH_TEXTURE 1

			
			#define SHADERPASS SHADERPASS_UNLIT


			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/Debugging3D.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"


			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_FRAG_POSITION
			#define ASE_NEEDS_FRAG_COLOR
			#pragma shader_feature _VOXELIZATIONFEATURE_ON
			#pragma shader_feature _LINEGLITCHFEATURE_ON
			#pragma shader_feature _POSITIONSPACEFEATURE_WORLD _POSITIONSPACEFEATURE_LOCAL _POSITIONSPACEFEATURE_CUSTOM
			#pragma shader_feature _POSITIONFEATURE_X _POSITIONFEATURE_Y _POSITIONFEATURE_Z
			#pragma multi_compile_instancing
			#pragma shader_feature _RANDOMGLITCHFEATURE_ON
			#pragma shader_feature _COLORGLITCHFEATURE_ON
			#pragma shader_feature _FRESNELFEATURE_ON
			#pragma shader_feature _NORMALMAPFEATURE_ON
			#pragma shader_feature _LINEBOTHFEATURE_ON
			#pragma shader_feature _LINE2FEATURE_ON
			#pragma shader_feature _LINE1FEATURE_ON
			#pragma shader_feature _SOFTINTERSECTION1FEATURE_OFF _SOFTINTERSECTION1FEATURE_ALPHA _SOFTINTERSECTION1FEATURE_COLOR
			#pragma shader_feature _GRAINFEATURE_ON
			#pragma shader_feature _SOFTINTERSECTION2FEATURE_OFF _SOFTINTERSECTION2FEATURE_ALPHA _SOFTINTERSECTION2FEATURE_COLOR
			#pragma shader_feature _DISSOLVEFEATURE_ON
			#pragma shader_feature _MASKFEATURE_ON
			#pragma shader_feature _MASKLOCALFEATURE_ON
			#pragma shader_feature _ALPHAMASKFEATURE_ON


			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 ase_normal : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_tangent : TANGENT;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 worldPos : TEXCOORD0;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
				float4 shadowCoord : TEXCOORD1;
				#endif
				#ifdef ASE_FOG
				float fogFactor : TEXCOORD2;
				#endif
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord4 : TEXCOORD4;
				float4 ase_texcoord5 : TEXCOORD5;
				float4 ase_texcoord6 : TEXCOORD6;
				float4 ase_texcoord7 : TEXCOORD7;
				float4 ase_texcoord8 : TEXCOORD8;
				float4 ase_texcoord9 : TEXCOORD9;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _MainColor;
			float3 _LineGlitchOffset;
			float3 _MaskSize;
			float3 _MaskCenter;
			float3 _DissolveScale;
			float3 _RandomGlitchOffset;
			float3 _GrainScale;
			float2 _GrainValues;
			float _CullMode;
			float _Line2Frequency;
			float _Line2Speed;
			float _Line2InvertedThickness;
			float _Line2Hardness;
			float _Line2Alpha;
			float _SoftIntersection1Distance;
			float _SoftIntersection1Intensity;
			float _GrainAffect;
			float _Line1Alpha;
			float _SoftIntersection2Distance;
			float _SoftIntersection2Intensity;
			float _SoftIntersection2Affect;
			float _DissolveHide;
			float _MaskFalloff;
			float _MaskInversion;
			float _SoftIntersection1Affect;
			float _Line1Hardness;
			float _Line1Speed;
			float _AlphaMaskAffect;
			float _ZWrite;
			float _PositionDirection;
			float _LineGlitchFrequency;
			float _LineGlitchSpeed;
			float _RandomOffset;
			float _LineGlitchInvertedThickness;
			float _LineGlitchHardness;
			float _RandomGlitchTiling;
			float _RandomGlitchConstant;
			float _Line1InvertedThickness;
			float _RandomGlitchAmount;
			float _VoxelizationAffect;
			float _ColorGlitchAffect;
			float _FresnelScale;
			float _FresnelPower;
			float _NormalScale;
			float _NormalAffect;
			float _FresnelAlphaScale;
			float _FresnelAlphaPower;
			float _Line1Frequency;
			float _Voxelization;
			float _Alpha;
			float4 _EdgeColor;
			float _EdgeWidth;
			float _FreezeGlitchAmount;
			#ifdef TESSELLATION_ON
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END
			sampler2D _LineGlitch;
			sampler2D _NormalMap;
			sampler2D _Line1;
			sampler2D _Line2;
			// URP 17에서는 DeclareDepthTexture.hlsl이 이미 선언하므로 중복 선언 방지
			#ifndef UNITY_DECLARE_DEPTH_TEXTURE_INCLUDED
			uniform float4 _CameraDepthTexture_TexelSize;
			#endif
			sampler2D _AlphaMask;
			// TimeFreezeOverlayFeature가 오브젝트마다 넘기는 값
			float _TimeFreezeOverlayProgress;
			// x: 최저 월드 높이, y: 최고 월드 높이
			float4 _TimeFreezeOverlayBounds;
			float _TimeFreezeOverlayGlitch;
			UNITY_INSTANCING_BUFFER_START(KnifeHologramShaderUnlit)
				UNITY_DEFINE_INSTANCED_PROP(float4x4, _CustomMatrix)
				UNITY_DEFINE_INSTANCED_PROP(float4, _NormalMap_ST)
				UNITY_DEFINE_INSTANCED_PROP(float4, _AlphaMask_ST)
			UNITY_INSTANCING_BUFFER_END(KnifeHologramShaderUnlit)


			float3 mod2D289( float3 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float2 mod2D289( float2 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float3 permute( float3 x ) { return mod2D289( ( ( x * 34.0 ) + 1.0 ) * x ); }
			float snoise( float2 v )
			{
				const float4 C = float4( 0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439 );
				float2 i = floor( v + dot( v, C.yy ) );
				float2 x0 = v - i + dot( i, C.xx );
				float2 i1;
				i1 = ( x0.x > x0.y ) ? float2( 1.0, 0.0 ) : float2( 0.0, 1.0 );
				float4 x12 = x0.xyxy + C.xxzz;
				x12.xy -= i1;
				i = mod2D289( i );
				float3 p = permute( permute( i.y + float3( 0.0, i1.y, 1.0 ) ) + i.x + float3( 0.0, i1.x, 1.0 ) );
				float3 m = max( 0.5 - float3( dot( x0, x0 ), dot( x12.xy, x12.xy ), dot( x12.zw, x12.zw ) ), 0.0 );
				m = m * m;
				m = m * m;
				float3 x = 2.0 * frac( p * C.www ) - 1.0;
				float3 h = abs( x ) - 0.5;
				float3 ox = floor( x + 0.5 );
				float3 a0 = x - ox;
				m *= 1.79284291400159 - 0.85373472095314 * ( a0 * a0 + h * h );
				float3 g;
				g.x = a0.x * x0.x + h.x * x0.y;
				g.yz = a0.yz * x12.xz + h.yz * x12.yw;
				return 130.0 * dot( m, g );
			}
			
			float3 mod3D289( float3 x ) { return x - floor( x / 289.0 ) * 289.0; }
			float4 mod3D289( float4 x ) { return x - floor( x / 289.0 ) * 289.0; }
			float4 permute( float4 x ) { return mod3D289( ( x * 34.0 + 1.0 ) * x ); }
			float4 taylorInvSqrt( float4 r ) { return 1.79284291400159 - r * 0.85373472095314; }
			float snoise( float3 v )
			{
				const float2 C = float2( 1.0 / 6.0, 1.0 / 3.0 );
				float3 i = floor( v + dot( v, C.yyy ) );
				float3 x0 = v - i + dot( i, C.xxx );
				float3 g = step( x0.yzx, x0.xyz );
				float3 l = 1.0 - g;
				float3 i1 = min( g.xyz, l.zxy );
				float3 i2 = max( g.xyz, l.zxy );
				float3 x1 = x0 - i1 + C.xxx;
				float3 x2 = x0 - i2 + C.yyy;
				float3 x3 = x0 - 0.5;
				i = mod3D289( i);
				float4 p = permute( permute( permute( i.z + float4( 0.0, i1.z, i2.z, 1.0 ) ) + i.y + float4( 0.0, i1.y, i2.y, 1.0 ) ) + i.x + float4( 0.0, i1.x, i2.x, 1.0 ) );
				float4 j = p - 49.0 * floor( p / 49.0 );  // mod(p,7*7)
				float4 x_ = floor( j / 7.0 );
				float4 y_ = floor( j - 7.0 * x_ );  // mod(j,N)
				float4 x = ( x_ * 2.0 + 0.5 ) / 7.0 - 1.0;
				float4 y = ( y_ * 2.0 + 0.5 ) / 7.0 - 1.0;
				float4 h = 1.0 - abs( x ) - abs( y );
				float4 b0 = float4( x.xy, y.xy );
				float4 b1 = float4( x.zw, y.zw );
				float4 s0 = floor( b0 ) * 2.0 + 1.0;
				float4 s1 = floor( b1 ) * 2.0 + 1.0;
				float4 sh = -step( h, 0.0 );
				float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
				float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;
				float3 g0 = float3( a0.xy, h.x );
				float3 g1 = float3( a0.zw, h.y );
				float3 g2 = float3( a1.xy, h.z );
				float3 g3 = float3( a1.zw, h.w );
				float4 norm = taylorInvSqrt( float4( dot( g0, g0 ), dot( g1, g1 ), dot( g2, g2 ), dot( g3, g3 ) ) );
				g0 *= norm.x;
				g1 *= norm.y;
				g2 *= norm.z;
				g3 *= norm.w;
				float4 m = max( 0.6 - float4( dot( x0, x0 ), dot( x1, x1 ), dot( x2, x2 ), dot( x3, x3 ) ), 0.0 );
				m = m* m;
				m = m* m;
				float4 px = float4( dot( x0, g0 ), dot( x1, g1 ), dot( x2, g2 ), dot( x3, g3 ) );
				return 42.0 * dot( m, px);
			}
			
			
			VertexOutput VertexFunction ( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				float C_ZERO287 = 0.0;
				float3 temp_cast_0 = (C_ZERO287).xxx;
				float3 viewToObjDir94 = mul( UNITY_MATRIX_T_MV, float4( _LineGlitchOffset, 0 ) ).xyz;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 ase_worldPos = mul(GetObjectToWorldMatrix(), v.vertex).xyz;
				#if defined(_POSITIONFEATURE_X)
				float staticSwitch350 = ase_worldPos.x;
				#elif defined(_POSITIONFEATURE_Y)
				float staticSwitch350 = ase_worldPos.y;
				#elif defined(_POSITIONFEATURE_Z)
				float staticSwitch350 = ase_worldPos.z;
				#else
				float staticSwitch350 = ase_worldPos.y;
				#endif
				#if defined(_POSITIONFEATURE_X)
				float staticSwitch351 = v.vertex.xyz.x;
				#elif defined(_POSITIONFEATURE_Y)
				float staticSwitch351 = v.vertex.xyz.y;
				#elif defined(_POSITIONFEATURE_Z)
				float staticSwitch351 = v.vertex.xyz.z;
				#else
				float staticSwitch351 = v.vertex.xyz.y;
				#endif
				float4x4 _CustomMatrix_Instance = UNITY_ACCESS_INSTANCED_PROP(KnifeHologramShaderUnlit,_CustomMatrix);
				float3 temp_output_377_0 = mul( _CustomMatrix_Instance, float4( v.vertex.xyz , 0.0 ) ).xyz;
				float3 break379 = temp_output_377_0;
				#if defined(_POSITIONFEATURE_X)
				float staticSwitch378 = break379.x;
				#elif defined(_POSITIONFEATURE_Y)
				float staticSwitch378 = break379.y;
				#elif defined(_POSITIONFEATURE_Z)
				float staticSwitch378 = break379.z;
				#else
				float staticSwitch378 = break379.y;
				#endif
				#if defined(_POSITIONSPACEFEATURE_WORLD)
				float staticSwitch352 = staticSwitch350;
				#elif defined(_POSITIONSPACEFEATURE_LOCAL)
				float staticSwitch352 = staticSwitch351;
				#elif defined(_POSITIONSPACEFEATURE_CUSTOM)
				float staticSwitch352 = staticSwitch378;
				#else
				float staticSwitch352 = staticSwitch350;
				#endif
				float vertexToFrag497 = ( staticSwitch352 * _PositionDirection );
				float pos353 = vertexToFrag497;
				float mulTime3_g146 = _TimeParameters.x * _LineGlitchSpeed;
				float randomoffset385 = _RandomOffset;
				float2 temp_cast_3 = ((pos353*_LineGlitchFrequency + ( mulTime3_g146 + randomoffset385 ))).xx;
				float clampResult42_g146 = clamp( ( ( tex2Dlod( _LineGlitch, float4( temp_cast_3, 0, 0.0) ).r - _LineGlitchInvertedThickness ) * _LineGlitchHardness ) , 0.0 , 1.0 );
				#ifdef _LINEGLITCHFEATURE_ON
				float3 staticSwitch307 = ( ( viewToObjDir94 / ase_objectScale ) * clampResult42_g146 );
				#else
				float3 staticSwitch307 = temp_cast_0;
				#endif
				float3 lineglitch491 = staticSwitch307;
				float3 temp_cast_4 = (C_ZERO287).xxx;
				float3 viewToObjDir122 = mul( UNITY_MATRIX_T_MV, float4( _RandomGlitchOffset, 0 ) ).xyz;
				float mulTime149 = _TimeParameters.x * -2.3;
				float mulTime155 = _TimeParameters.x * -2.05;
				float2 appendResult153 = (float2((pos353*_RandomGlitchTiling + ( mulTime149 + randomoffset385 )) , ( randomoffset385 + mulTime155 )));
				float simplePerlin2D186 = snoise( appendResult153 );
				simplePerlin2D186 = simplePerlin2D186*0.5 + 0.5;
				float4 matrixToPos373 = float4( float4x4( 1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1 )[0][3],float4x4( 1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1 )[1][3],float4x4( 1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1 )[2][3],float4x4( 1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1 )[3][3]);
				#if defined(_POSITIONSPACEFEATURE_WORLD)
				float staticSwitch365 = ( matrixToPos373.x + matrixToPos373.y + matrixToPos373.z );
				#elif defined(_POSITIONSPACEFEATURE_LOCAL)
				float staticSwitch365 = C_ZERO287;
				#elif defined(_POSITIONSPACEFEATURE_CUSTOM)
				float staticSwitch365 = 0.0;
				#else
				float staticSwitch365 = ( matrixToPos373.x + matrixToPos373.y + matrixToPos373.z );
				#endif
				float mulTime139 = _TimeParameters.x * -5.74;
				float mulTime157 = _TimeParameters.x * -0.83;
				float2 appendResult158 = (float2((staticSwitch365*223.0 + ( mulTime139 + randomoffset385 )) , ( randomoffset385 + mulTime157 )));
				float simplePerlin2D187 = snoise( appendResult158 );
				simplePerlin2D187 = simplePerlin2D187*0.5 + 0.5;
				float clampResult148 = clamp( (-1.0 + (( simplePerlin2D187 + _RandomGlitchConstant ) - 0.0) * (1.0 - -1.0) / (1.0 - 0.0)) , 0.0 , 1.0 );
				float temp_output_197_0 = ( (-1.0 + (simplePerlin2D186 - 0.0) * (1.0 - -1.0) / (1.0 - 0.0)) * clampResult148 );
				float2 break190 = appendResult153;
				float2 appendResult195 = (float2(( 20.0 * break190.x ) , break190.y));
				float simplePerlin2D188 = snoise( appendResult195 );
				simplePerlin2D188 = simplePerlin2D188*0.5 + 0.5;
				float clampResult192 = clamp( (-1.0 + (simplePerlin2D188 - 0.0) * (1.0 - -1.0) / (1.0 - 0.0)) , 0.0 , 1.0 );
				float lerpResult199 = lerp( 0.0 , clampResult192 , 2.0);
				#ifdef _RANDOMGLITCHFEATURE_ON
				float3 staticSwitch311 = ( ( viewToObjDir122 / ase_objectScale ) * ( temp_output_197_0 + ( temp_output_197_0 * lerpResult199 ) ) * _RandomGlitchAmount );
				#else
				float3 staticSwitch311 = temp_cast_4;
				#endif
				float3 randomglitch493 = staticSwitch311;
				// 정지 순간 글리치, 키워드와 상관없이 세기로만 켬
				float freezeglitch = _TimeFreezeOverlayGlitch * _FreezeGlitchAmount;
				float3 freezeglitchoffset = ( viewToObjDir122 / ase_objectScale ) * ( simplePerlin2D186 * 2.0 - 1.0 ) * ( 1.0 + lerpResult199 ) * freezeglitch;
				float3 vertexoffset410 = ( lineglitch491 + randomglitch493 + freezeglitchoffset );
				float3 temp_output_512_0 = ( v.vertex.xyz + vertexoffset410 );
				float3 lerpResult517 = lerp( temp_output_512_0 , ( round( ( temp_output_512_0 * _Voxelization ) ) / _Voxelization ) , _VoxelizationAffect);
				#ifdef _VOXELIZATIONFEATURE_ON
				float3 staticSwitch518 = lerpResult517;
				#else
				float3 staticSwitch518 = temp_output_512_0;
				#endif
				float3 ModifiedVertexPosition519 = staticSwitch518;
				
				float3 ase_worldNormal = TransformObjectToWorldNormal(v.ase_normal);
				o.ase_texcoord3.xyz = ase_worldNormal;
				float3 ase_worldTangent = TransformObjectToWorldDir(v.ase_tangent.xyz);
				o.ase_texcoord5.xyz = ase_worldTangent;
				float ase_vertexTangentSign = v.ase_tangent.w * unity_WorldTransformParams.w;
				float3 ase_worldBitangent = cross( ase_worldNormal, ase_worldTangent ) * ase_vertexTangentSign;
				o.ase_texcoord6.xyz = ase_worldBitangent;
				o.ase_texcoord3.w = vertexToFrag497;
				float3 vertexPos238 = ( v.vertex.xyz + vertexoffset410 );
				float4 ase_clipPos238 = TransformObjectToHClip((vertexPos238).xyz);
				float4 screenPos238 = ComputeScreenPos(ase_clipPos238);
				o.ase_texcoord7 = screenPos238;
				float3 vertexPos417 = ( v.vertex.xyz + vertexoffset410 );
				float4 ase_clipPos417 = TransformObjectToHClip((vertexPos417).xyz);
				float4 screenPos417 = ComputeScreenPos(ase_clipPos417);
				o.ase_texcoord8 = screenPos417;
				
				o.ase_texcoord4.xy = v.ase_texcoord.xy;
				o.ase_texcoord9 = v.vertex;
				o.ase_color = v.ase_color;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord4.zw = 0;
				o.ase_texcoord5.w = 0;
				o.ase_texcoord6.w = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = ModifiedVertexPosition519;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif
				v.ase_normal = v.ase_normal;

				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );
				float4 positionCS = TransformWorldToHClip( positionWS );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				o.worldPos = positionWS;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
				VertexPositionInputs vertexInput = (VertexPositionInputs)0;
				vertexInput.positionWS = positionWS;
				vertexInput.positionCS = positionCS;
				o.shadowCoord = GetShadowCoord( vertexInput );
				#endif
				#ifdef ASE_FOG
				o.fogFactor = ComputeFogFactor( positionCS.z );
				#endif
				o.clipPos = positionCS;
				return o;
			}

			#if defined(TESSELLATION_ON)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 ase_normal : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_tangent : TANGENT;
				float4 ase_color : COLOR;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( VertexInput v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.vertex = v.vertex;
				o.ase_normal = v.ase_normal;
				o.ase_texcoord = v.ase_texcoord;
				o.ase_tangent = v.ase_tangent;
				o.ase_color = v.ase_color;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
			   return patch[id];
			}

			[domain("tri")]
			VertexOutput DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				VertexInput o = (VertexInput) 0;
				o.vertex = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				o.ase_normal = patch[0].ase_normal * bary.x + patch[1].ase_normal * bary.y + patch[2].ase_normal * bary.z;
				o.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				o.ase_tangent = patch[0].ase_tangent * bary.x + patch[1].ase_tangent * bary.y + patch[2].ase_tangent * bary.z;
				o.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.vertex.xyz - patch[i].ase_normal * (dot(o.vertex.xyz, patch[i].ase_normal) - dot(patch[i].vertex.xyz, patch[i].ase_normal));
				float phongStrength = _TessPhongStrength;
				o.vertex.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.vertex.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}
			#endif

			half4 frag ( VertexOutput IN  ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 WorldPosition = IN.worldPos;
				#endif
				float4 ShadowCoords = float4( 0, 0, 0, 0 );

				#if defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
						ShadowCoords = IN.shadowCoord;
					#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
						ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
					#endif
				#endif
				// 진행도 0/1에서 막이 완전히 없거나 다 덮이도록 띠 폭만큼 여유를 둠
				float edgeWidth = max( _EdgeWidth, 0.001 );
				float sweepHeight = lerp( _TimeFreezeOverlayBounds.x - edgeWidth, _TimeFreezeOverlayBounds.y + edgeWidth, _TimeFreezeOverlayProgress );
				float covered = sweepHeight - WorldPosition.y;
				clip( covered );

				float3 objToWorld161 = mul( GetObjectToWorldMatrix(), float4( float3( 0,0,0 ), 1 ) ).xyz;
				float C_ZERO287 = 0.0;
				#if defined(_POSITIONSPACEFEATURE_WORLD)
				float staticSwitch360 = ( objToWorld161.x + objToWorld161.y + objToWorld161.z );
				#elif defined(_POSITIONSPACEFEATURE_LOCAL)
				float staticSwitch360 = C_ZERO287;
				#elif defined(_POSITIONSPACEFEATURE_CUSTOM)
				float staticSwitch360 = 0.0;
				#else
				float staticSwitch360 = ( objToWorld161.x + objToWorld161.y + objToWorld161.z );
				#endif
				float randomoffset385 = _RandomOffset;
				float mulTime165 = _TimeParameters.x * -15.0;
				float mulTime167 = _TimeParameters.x * -0.5;
				float2 appendResult168 = (float2((staticSwitch360*223.0 + ( randomoffset385 + mulTime165 )) , ( randomoffset385 + mulTime167 )));
				float simplePerlin2D176 = snoise( appendResult168 );
				simplePerlin2D176 = simplePerlin2D176*0.5 + 0.5;
				float clampResult175 = clamp( (-0.61 + (simplePerlin2D176 - 0.0) * (2.0 - -0.61) / (1.0 - 0.0)) , 0.0 , 1.0 );
				float lerpResult181 = lerp( 1.0 , clampResult175 , _ColorGlitchAffect);
				#ifdef _COLORGLITCHFEATURE_ON
				float staticSwitch279 = lerpResult181;
				#else
				float staticSwitch279 = 1.0;
				#endif
				float4 maincolor232 = _MainColor;
				float C_ONE288 = 1.0;
				float3 ase_worldViewDir = ( _WorldSpaceCameraPos.xyz - WorldPosition );
				ase_worldViewDir = normalize(ase_worldViewDir);
				float3 ase_worldNormal = IN.ase_texcoord3.xyz;
				float fresnelNdotV2 = dot( ase_worldNormal, ase_worldViewDir );
				float fresnelNode2 = ( 0.0 + _FresnelScale * pow( max( 1.0 - fresnelNdotV2 , 0.0001 ), _FresnelPower ) );
				float4 _NormalMap_ST_Instance = UNITY_ACCESS_INSTANCED_PROP(KnifeHologramShaderUnlit,_NormalMap_ST);
				float2 uv_NormalMap = IN.ase_texcoord4.xy * _NormalMap_ST_Instance.xy + _NormalMap_ST_Instance.zw;
				float3 unpack95 = UnpackNormalScale( tex2D( _NormalMap, uv_NormalMap ), _NormalScale );
				unpack95.z = lerp( 1, unpack95.z, saturate(_NormalScale) );
				float3 ase_worldTangent = IN.ase_texcoord5.xyz;
				float3 ase_worldBitangent = IN.ase_texcoord6.xyz;
				float3 tanToWorld0 = float3( ase_worldTangent.x, ase_worldBitangent.x, ase_worldNormal.x );
				float3 tanToWorld1 = float3( ase_worldTangent.y, ase_worldBitangent.y, ase_worldNormal.y );
				float3 tanToWorld2 = float3( ase_worldTangent.z, ase_worldBitangent.z, ase_worldNormal.z );
				float3 ase_tanViewDir =  tanToWorld0 * ase_worldViewDir.x + tanToWorld1 * ase_worldViewDir.y  + tanToWorld2 * ase_worldViewDir.z;
				ase_tanViewDir = normalize(ase_tanViewDir);
				float dotResult101 = dot( unpack95 , ase_tanViewDir );
				float lerpResult106 = lerp( 1.0 , (0.0 + (dotResult101 - -1.0) * (1.0 - 0.0) / (1.0 - -1.0)) , _NormalAffect);
				#ifdef _NORMALMAPFEATURE_ON
				float staticSwitch341 = ( 1.0 - lerpResult106 );
				#else
				float staticSwitch341 = C_ZERO287;
				#endif
				float NormalAffect116 = staticSwitch341;
				#ifdef _FRESNELFEATURE_ON
				float staticSwitch303 = ( fresnelNode2 + NormalAffect116 );
				#else
				float staticSwitch303 = C_ONE288;
				#endif
				float fresnelNdotV53 = dot( ase_worldNormal, ase_worldViewDir );
				float fresnelNode53 = ( 0.0 + _FresnelAlphaScale * pow( max( 1.0 - fresnelNdotV53 , 0.0001 ), _FresnelAlphaPower ) );
				#ifdef _FRESNELFEATURE_ON
				float staticSwitch305 = fresnelNode53;
				#else
				float staticSwitch305 = C_ZERO287;
				#endif
				float clampResult57 = clamp( ( staticSwitch305 + NormalAffect116 ) , 0.0 , 1.0 );
				float4 fresnelcolor436 = ( staticSwitch303 * maincolor232 * clampResult57 );
				float4 temp_cast_0 = (C_ZERO287).xxxx;
				float vertexToFrag497 = IN.ase_texcoord3.w;
				float pos353 = vertexToFrag497;
				float mulTime3_g163 = _TimeParameters.x * _Line1Speed;
				float2 temp_cast_1 = ((pos353*_Line1Frequency + ( mulTime3_g163 + randomoffset385 ))).xx;
				float clampResult42_g163 = clamp( ( ( tex2D( _Line1, temp_cast_1 ).r - _Line1InvertedThickness ) * _Line1Hardness ) , 0.0 , 1.0 );
				float temp_output_477_0 = clampResult42_g163;
				float3 temp_output_293_0 = (( maincolor232 * temp_output_477_0 )).rgb;
				float temp_output_61_0 = ( temp_output_477_0 * _Line1Alpha );
				float4 appendResult292 = (float4(temp_output_293_0 , temp_output_61_0));
				#ifdef _LINE1FEATURE_ON
				float4 staticSwitch284 = appendResult292;
				#else
				float4 staticSwitch284 = temp_cast_0;
				#endif
				float mulTime3_g162 = _TimeParameters.x * _Line2Speed;
				float2 temp_cast_2 = ((pos353*_Line2Frequency + ( mulTime3_g162 + randomoffset385 ))).xx;
				float clampResult42_g162 = clamp( ( ( tex2D( _Line2, temp_cast_2 ).r - _Line2InvertedThickness ) * _Line2Hardness ) , 0.0 , 1.0 );
				float temp_output_476_0 = clampResult42_g162;
				float3 temp_output_294_0 = (( maincolor232 * temp_output_476_0 )).rgb;
				float temp_output_69_0 = ( temp_output_476_0 * _Line2Alpha );
				float4 appendResult295 = (float4(temp_output_294_0 , temp_output_69_0));
				#ifdef _LINE2FEATURE_ON
				float4 staticSwitch285 = appendResult295;
				#else
				float4 staticSwitch285 = staticSwitch284;
				#endif
				float4 appendResult328 = (float4(0.0 , 0.0 , 0.0 , temp_output_61_0));
				float4 appendResult296 = (float4(( temp_output_293_0 * temp_output_294_0 ) , ( temp_output_61_0 * temp_output_69_0 )));
				#ifdef _LINEBOTHFEATURE_ON
				float4 staticSwitch286 = ( appendResult328 + appendResult296 );
				#else
				float4 staticSwitch286 = staticSwitch285;
				#endif
				float4 line_color298 = staticSwitch286;
				float4 temp_cast_4 = (C_ZERO287).xxxx;
				float4 temp_cast_5 = (C_ZERO287).xxxx;
				float4 screenPos238 = IN.ase_texcoord7;
				float4 ase_screenPosNorm238 = screenPos238 / screenPos238.w;
				ase_screenPosNorm238.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm238.z : ase_screenPosNorm238.z * 0.5 + 0.5;
				float screenDepth238 = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH( ase_screenPosNorm238.xy ),_ZBufferParams);
				float distanceDepth238 = saturate( abs( ( screenDepth238 - LinearEyeDepth( ase_screenPosNorm238.z,_ZBufferParams ) ) / ( _SoftIntersection1Distance ) ) );
				float saferPower344 = abs( distanceDepth238 );
				float lerpResult467 = lerp( 1.0 , pow( saferPower344 , _SoftIntersection1Intensity ) , _SoftIntersection1Affect);
				float4 appendResult321 = (float4(0.0 , 0.0 , 0.0 , lerpResult467));
				float temp_output_242_0 = ( 1.0 - distanceDepth238 );
				float4 appendResult314 = (float4((( maincolor232 * temp_output_242_0 * _SoftIntersection1Intensity * _SoftIntersection1Affect )).rgb , ( temp_output_242_0 * _SoftIntersection1Intensity * _SoftIntersection1Affect )));
				#if defined(_SOFTINTERSECTION1FEATURE_OFF)
				float4 staticSwitch315 = temp_cast_4;
				#elif defined(_SOFTINTERSECTION1FEATURE_ALPHA)
				float4 staticSwitch315 = appendResult321;
				#elif defined(_SOFTINTERSECTION1FEATURE_COLOR)
				float4 staticSwitch315 = appendResult314;
				#else
				float4 staticSwitch315 = temp_cast_4;
				#endif
				float4 intersection1245 = staticSwitch315;
				float mulTime273 = _TimeParameters.x * 100.0;
				float simplePerlin3D264 = snoise( (WorldPosition*_GrainScale + mulTime273) );
				simplePerlin3D264 = simplePerlin3D264*0.5 + 0.5;
				float lerpResult278 = lerp( _GrainValues.x , _GrainValues.y , simplePerlin3D264);
				float lerpResult269 = lerp( 0.0 , lerpResult278 , _GrainAffect);
				#ifdef _GRAINFEATURE_ON
				float staticSwitch282 = lerpResult269;
				#else
				float staticSwitch282 = 0.0;
				#endif
				float grain268 = staticSwitch282;
				float4 temp_cast_7 = (C_ZERO287).xxxx;
				float4 temp_cast_8 = (C_ZERO287).xxxx;
				float4 screenPos417 = IN.ase_texcoord8;
				float4 ase_screenPosNorm417 = screenPos417 / screenPos417.w;
				ase_screenPosNorm417.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm417.z : ase_screenPosNorm417.z * 0.5 + 0.5;
				float screenDepth417 = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH( ase_screenPosNorm417.xy ),_ZBufferParams);
				float distanceDepth417 = saturate( abs( ( screenDepth417 - LinearEyeDepth( ase_screenPosNorm417.z,_ZBufferParams ) ) / ( _SoftIntersection2Distance ) ) );
				float saferPower422 = abs( distanceDepth417 );
				float lerpResult471 = lerp( 1.0 , pow( saferPower422 , _SoftIntersection2Intensity ) , _SoftIntersection2Affect);
				float4 appendResult424 = (float4(0.0 , 0.0 , 0.0 , lerpResult471));
				float temp_output_418_0 = ( 1.0 - distanceDepth417 );
				float4 appendResult425 = (float4((( maincolor232 * temp_output_418_0 * _SoftIntersection2Intensity * _SoftIntersection2Affect )).rgb , ( temp_output_418_0 * _SoftIntersection2Intensity * _SoftIntersection2Affect )));
				#if defined(_SOFTINTERSECTION2FEATURE_OFF)
				float4 staticSwitch429 = temp_cast_7;
				#elif defined(_SOFTINTERSECTION2FEATURE_ALPHA)
				float4 staticSwitch429 = appendResult424;
				#elif defined(_SOFTINTERSECTION2FEATURE_COLOR)
				float4 staticSwitch429 = appendResult425;
				#else
				float4 staticSwitch429 = temp_cast_7;
				#endif
				float4 intersection2430 = staticSwitch429;
				float fresnelalpha434 = clampResult57;
				float intersectionalpha1261 = (staticSwitch315).w;
				#if defined(_SOFTINTERSECTION1FEATURE_OFF)
				float staticSwitch330 = intersectionalpha1261;
				#elif defined(_SOFTINTERSECTION1FEATURE_ALPHA)
				float staticSwitch330 = C_ZERO287;
				#elif defined(_SOFTINTERSECTION1FEATURE_COLOR)
				float staticSwitch330 = intersectionalpha1261;
				#else
				float staticSwitch330 = intersectionalpha1261;
				#endif
				float intersectionalpha2431 = (staticSwitch429).w;
				#if defined(_SOFTINTERSECTION2FEATURE_OFF)
				float staticSwitch447 = intersectionalpha2431;
				#elif defined(_SOFTINTERSECTION2FEATURE_ALPHA)
				float staticSwitch447 = C_ZERO287;
				#elif defined(_SOFTINTERSECTION2FEATURE_COLOR)
				float staticSwitch447 = intersectionalpha2431;
				#else
				float staticSwitch447 = intersectionalpha2431;
				#endif
				#if defined(_SOFTINTERSECTION1FEATURE_OFF)
				float staticSwitch334 = C_ONE288;
				#elif defined(_SOFTINTERSECTION1FEATURE_ALPHA)
				float staticSwitch334 = intersectionalpha1261;
				#elif defined(_SOFTINTERSECTION1FEATURE_COLOR)
				float staticSwitch334 = C_ONE288;
				#else
				float staticSwitch334 = C_ONE288;
				#endif
				#if defined(_SOFTINTERSECTION2FEATURE_OFF)
				float staticSwitch448 = C_ONE288;
				#elif defined(_SOFTINTERSECTION2FEATURE_ALPHA)
				float staticSwitch448 = intersectionalpha2431;
				#elif defined(_SOFTINTERSECTION2FEATURE_COLOR)
				float staticSwitch448 = C_ONE288;
				#else
				float staticSwitch448 = C_ONE288;
				#endif
				float clampResult62 = clamp( ( ( (maincolor232).a + fresnelalpha434 + (line_color298).w + staticSwitch330 + staticSwitch447 ) * staticSwitch334 * staticSwitch448 ) , 0.0 , 1.0 );
				float4x4 _CustomMatrix_Instance = UNITY_ACCESS_INSTANCED_PROP(KnifeHologramShaderUnlit,_CustomMatrix);
				float3 temp_output_377_0 = mul( _CustomMatrix_Instance, float4( IN.ase_texcoord9.xyz , 0.0 ) ).xyz;
				#if defined(_POSITIONSPACEFEATURE_WORLD)
				float3 staticSwitch375 = WorldPosition;
				#elif defined(_POSITIONSPACEFEATURE_LOCAL)
				float3 staticSwitch375 = IN.ase_texcoord9.xyz;
				#elif defined(_POSITIONSPACEFEATURE_CUSTOM)
				float3 staticSwitch375 = temp_output_377_0;
				#else
				float3 staticSwitch375 = WorldPosition;
				#endif
				float3 vPos357 = staticSwitch375;
				float simplePerlin3D212 = snoise( ( vPos357 * _DissolveScale ) );
				simplePerlin3D212 = simplePerlin3D212*0.5 + 0.5;
				float clampResult218 = clamp( ( simplePerlin3D212 - _DissolveHide ) , 0.0 , 1.0 );
				#ifdef _DISSOLVEFEATURE_ON
				float staticSwitch337 = clampResult218;
				#else
				float staticSwitch337 = C_ONE288;
				#endif
				float dissolve432 = staticSwitch337;
				float3 objToWorld257 = mul( GetObjectToWorldMatrix(), float4( float3( 0,0,0 ), 1 ) ).xyz;
				#ifdef _MASKLOCALFEATURE_ON
				float3 staticSwitch499 = ( _MaskCenter + objToWorld257 );
				#else
				float3 staticSwitch499 = _MaskCenter;
				#endif
				float clampResult256 = clamp( ( distance( max( ( abs( ( WorldPosition - staticSwitch499 ) ) - ( _MaskSize * float3( 0.5,0.5,0.5 ) ) ) , float3( 0,0,0 ) ) , float3( 0,0,0 ) ) / _MaskFalloff ) , 0.0 , 1.0 );
				float lerpResult501 = lerp( clampResult256 , ( 1.0 - clampResult256 ) , _MaskInversion);
				#ifdef _MASKFEATURE_ON
				float staticSwitch339 = lerpResult501;
				#else
				float staticSwitch339 = C_ONE288;
				#endif
				float mask254 = staticSwitch339;
				float4 _AlphaMask_ST_Instance = UNITY_ACCESS_INSTANCED_PROP(KnifeHologramShaderUnlit,_AlphaMask_ST);
				float2 uv_AlphaMask = IN.ase_texcoord4.xy * _AlphaMask_ST_Instance.xy + _AlphaMask_ST_Instance.zw;
				float lerpResult400 = lerp( C_ONE288 , tex2D( _AlphaMask, uv_AlphaMask ).r , _AlphaMaskAffect);
				#ifdef _ALPHAMASKFEATURE_ON
				float staticSwitch404 = lerpResult400;
				#else
				float staticSwitch404 = C_ONE288;
				#endif
				float alphamask402 = staticSwitch404;
				float4 appendResult325 = (float4((( staticSwitch279 * ( maincolor232 + fresnelcolor436 + float4( (line_color298).xyz , 0.0 ) + intersection1245 + grain268 + intersection2430 ) )).rgb , ( clampResult62 * dissolve432 * mask254 * alphamask402 * _Alpha )));
				float4 temp_output_504_0 = ( appendResult325 * IN.ase_color );
				
				float3 BakedAlbedo = 0;
				float3 BakedEmission = 0;
				float3 Color = temp_output_504_0.xyz;
				float Alpha = (temp_output_504_0).w;

				// 경계선 띠
				float edge = 1.0 - saturate( covered / edgeWidth );
				Color = lerp( Color, _EdgeColor.rgb, edge );
				Alpha = max( Alpha, _EdgeColor.a * edge );

				// 정지 순간 깜빡임
				float freezeflicker = lerp( 1.0, clampResult175, saturate( _TimeFreezeOverlayGlitch * _FreezeGlitchAmount * 2.0 ) );
				Color *= freezeflicker;
				Alpha *= lerp( 1.0, freezeflicker, 0.5 );
				float AlphaClipThreshold = 0.5;
				float AlphaClipThresholdShadow = 0.5;

				#ifdef _ALPHATEST_ON
					clip( Alpha - AlphaClipThreshold );
				#endif

				#if defined(_DBUFFER)
					ApplyDecalToBaseColor(IN.clipPos, Color);
				#endif

				#if defined(_ALPHAPREMULTIPLY_ON)
				Color *= Alpha;
				#endif


				#ifdef LOD_FADE_CROSSFADE
					LODDitheringTransition( IN.clipPos.xyz, unity_LODFade.x );
				#endif

				#ifdef ASE_FOG
					Color = MixFog( Color, IN.fogFactor );
				#endif

				return half4( Color, Alpha );
			}

			ENDHLSL
		}

	}
	Fallback Off
}
