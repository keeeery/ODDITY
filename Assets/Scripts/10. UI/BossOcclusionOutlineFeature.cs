using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TeamOdd.UI
{
    // 보스와 다른 물체가 서로 가릴 때 가려진 쪽을 화면에 드러내는 기능
    // - 보스가 플레이어를 가리면 가려진 부분에만 플레이어 실루엣의 외곽선을 그린다.
    // - 보스가 플레이어가 아닌 다른 물체에 가리면 가려진 부분을 보스 실루엣으로 채워 그린다. 지정한 레이어(예: 바닥, 수련장 벽)와 작은 파편에 가린 부분은 뺀다.
    // - 채워질 부분이 보스 실루엣에서 차지하는 비율(HiddenRatio)을 재어 BossHUD가 보일지 정한다.
    // 대상과 색은 BossHUD가 SetTargets, SetStyle, SetFillStyle로 넘겨준다.
    public sealed class BossOcclusionOutlineFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader outlineShader;
        private Material material;
        private OutlinePass pass;

        private static readonly List<Renderer> bossRenderers = new List<Renderer>();
        private static readonly List<Renderer> playerRenderers = new List<Renderer>();
        private static Color outlineColor = Color.white;
        private static float outlineWidthPixels = 2f;
        private static Color fillColor = Color.clear;
        private static float fillMinGap = 0.3f;
        private static LayerMask fillIgnoredOccluders;
        private static float fillOpacity = 1f;
        private static readonly List<Renderer> smallOccluders = new List<Renderer>();

        // 게임 카메라 화면에서 보스 실루엣 중 다른 물체에 가려 채워질 부분의 비율(0~1), GPU에서 재어 한두 프레임 늦게 들어온다.
        public static float HiddenRatio { get; private set; }

        public static void SetTargets(GameObject boss, GameObject player)
        {
            CollectRenderers(boss, bossRenderers);
            CollectRenderers(player, playerRenderers);
        }

        public static void SetStyle(Color color, float widthPixels)
        {
            outlineColor = color;
            outlineWidthPixels = widthPixels;
        }

        // 가려진 보스 실루엣의 채우기 색과, 보스 앞면과 가린 물체 사이가 이 거리(m)보다 멀어야 채우기 시작하는 값
        // ignoredOccluders 레이어의 물체(예: 수련장 벽)가 보스 앞을 막고 있으면 그 부분은 채우지 않는다.
        // 색의 알파가 0이면 채우기를 그리지 않는다.
        public static void SetFillStyle(Color color, float minGap, LayerMask ignoredOccluders)
        {
            fillColor = color;
            fillMinGap = minGap;
            fillIgnoredOccluders = ignoredOccluders;
        }

        // 채우기를 화면에 그리는 진하기(0~1), 0이어도 가린 비율(HiddenRatio)은 계속 잰다.
        public static void SetFillOpacity(float opacity)
        {
            fillOpacity = Mathf.Clamp01(opacity);
        }

        // 작은 파편처럼 가려도 실루엣을 채우지 않을 렌더러, 화면 맨 앞에 보이는 부분만 채우기에서 뺀다.
        public static void SetSmallOccluders(List<Renderer> renderers)
        {
            smallOccluders.Clear();
            if (renderers != null) smallOccluders.AddRange(renderers);
        }

        public static void ClearTargets()
        {
            bossRenderers.Clear();
            playerRenderers.Clear();
            smallOccluders.Clear();
            HiddenRatio = 0f;
        }

        public override void Create()
        {
            CoreUtils.Destroy(material);
            material = CoreUtils.CreateEngineMaterial(outlineShader);
            pass = new OutlinePass(material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (bossRenderers.Count == 0) return;
            Camera camera = renderingData.cameraData.camera;
            if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView) return;
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
        }

        // 메시를 그릴 수 있는 렌더러만 모은다. 파티클·트레일 같은 효과는 실루엣에서 뺀다.
        private static void CollectRenderers(GameObject root, List<Renderer> renderers)
        {
            renderers.Clear();
            if (root == null) return;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                if (renderer is SkinnedMeshRenderer || renderer is MeshRenderer) renderers.Add(renderer);
        }

        private static int GetSubMeshCount(Renderer renderer)
        {
            Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                : renderer.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh
                : null;
            return mesh != null ? mesh.subMeshCount : 0;
        }

        private sealed class OutlinePass : ScriptableRenderPass
        {
            private const int DepthMaskPass = 0;
            private const int PlayerSilhouettePass = 1;
            private const int ScreenOutlinePass = 2;
            private const int ScreenFillPass = 3;
            private const int HiddenRatioPass = 4;
            // 가린 비율을 잴 때 보스 화면 영역을 이 크기(픽셀)의 격자로 나눠 표본을 얻는다. 셰이더의 HIDDEN_RATIO_SIZE와 같아야 한다.
            private const int HiddenRatioSize = 64;
            private static readonly int DebrisMaskId = Shader.PropertyToID("_BossOcclusionDebrisMask");
            private static readonly int BossDepthId = Shader.PropertyToID("_BossOcclusionBossDepth");
            private static readonly int PlayerMaskId = Shader.PropertyToID("_BossOcclusionPlayerMask");
            private static readonly int IgnoredDepthId = Shader.PropertyToID("_BossOcclusionIgnoredDepth");
            private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
            private static readonly int WidthId = Shader.PropertyToID("_OutlineWidthPixels");
            private static readonly int RectId = Shader.PropertyToID("_OutlineRect");
            private static readonly int FillColorId = Shader.PropertyToID("_FillColor");
            private static readonly int FillMinGapId = Shader.PropertyToID("_FillMinGap");
            private static readonly int FillRectId = Shader.PropertyToID("_FillRect");
            private static readonly Vector4 ScreenRect = new Vector4(0f, 0f, 1f, 1f);
            private static readonly List<ShaderTagId> OccluderShaderTags = new List<ShaderTagId>
            {
                new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly"), new ShaderTagId("SRPDefaultUnlit"),
            };
            private readonly Material material;
            private readonly MaterialPropertyBlock outlineProperties = new MaterialPropertyBlock();
            private readonly MaterialPropertyBlock fillProperties = new MaterialPropertyBlock();
            private readonly List<Material> materialBuffer = new List<Material>();

            private struct DrawItem
            {
                public Renderer Renderer;
                public int SubMesh;
            }

            private sealed class MaskData
            {
                public Material Material;
                public int ShaderPass;
                public readonly List<DrawItem> Items = new List<DrawItem>();
            }

            private sealed class OccluderData
            {
                public RendererListHandle Renderers;
            }

            private sealed class CompositeData
            {
                public Material Material;
                public int ShaderPass;
                public MaterialPropertyBlock Properties;
                public TextureHandle PlayerMask;
                public TextureHandle IgnoredDepth;
                public TextureHandle DebrisMask;
            }

            private sealed class ReadbackData
            {
                public TextureHandle Source;
            }

            private static readonly System.Action<AsyncGPUReadbackRequest> OnHiddenRatioRead = ReadHiddenRatio;

            public OutlinePass(Material material)
            {
                this.material = material;
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                // 가린 비율은 게임 카메라 화면 기준으로만 잰다. 보스가 화면 밖이면 0으로 둔다.
                bool measuresRatio = cameraData.camera.cameraType == CameraType.Game;
                if (resources.cameraDepthTexture.IsValid() == false) return;
                // 보스가 화면 밖이면 아무것도 그리지 않는다.
                Vector4 bossRect = TryGetBounds(bossRenderers, out Bounds bossBounds) ? Intersect(GetScreenRect(cameraData, bossBounds), ScreenRect) : Vector4.zero;
                if (IsEmpty(bossRect))
                {
                    if (measuresRatio) HiddenRatio = 0f;
                    return;
                }

                // 외곽선과 플레이어 마스크는 두 대상이 화면에서 겹칠 때만 필요하다.
                bool hasPlayer = TryGetBounds(playerRenderers, out Bounds playerBounds);
                Vector4 overlapRect = hasPlayer ? Intersect(bossRect, GetScreenRect(cameraData, playerBounds)) : Vector4.zero;
                bool overlapsPlayer = hasPlayer && IsEmpty(overlapRect) == false;
                bool drawsFill = fillColor.a > 0f;
                if (overlapsPlayer == false && drawsFill == false) return;

                TextureHandle bossDepth = CreateMaskTexture(renderGraph, resources, "Boss occlusion depth", GraphicsFormat.R32G32_SFloat);
                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<MaskData>("Boss occlusion depth", out MaskData data))
                {
                    SetMaskData(data, DepthMaskPass, bossRenderers);
                    builder.SetRenderAttachment(bossDepth, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(CreateMaskZBuffer(renderGraph, bossDepth, "Boss occlusion z-buffer"), AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(bossDepth, BossDepthId);
                    builder.SetRenderFunc(static (MaskData data, RasterGraphContext context) => DrawMask(data, context));
                }

                // 플레이어가 보스와 겹치지 않으면 채우기에서 비울 플레이어 부분도 없으므로 검은 텍스처로 대신한다.
                TextureHandle playerMask = renderGraph.defaultResources.blackTexture;
                if (overlapsPlayer)
                {
                    // 0~1 값만 담으므로 8비트로 충분하다.
                    playerMask = CreateMaskTexture(renderGraph, resources, "Boss occlusion player silhouette", GraphicsFormat.R8G8B8A8_UNorm);
                    using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<MaskData>("Boss occlusion player silhouette", out MaskData data))
                    {
                        SetMaskData(data, PlayerSilhouettePass, playerRenderers);
                        builder.UseTexture(bossDepth, AccessFlags.Read);
                        builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                        builder.SetRenderAttachment(playerMask, 0, AccessFlags.Write);
                        builder.SetRenderAttachmentDepth(CreateMaskZBuffer(renderGraph, playerMask, "Boss occlusion player z-buffer"), AccessFlags.Write);
                        builder.SetGlobalTextureAfterPass(playerMask, PlayerMaskId);
                        builder.SetRenderFunc(static (MaskData data, RasterGraphContext context) => DrawMask(data, context));
                    }
                }

                if (drawsFill)
                {
                    TextureHandle ignoredDepth = DrawIgnoredOccluders(renderGraph, frameData, resources, cameraData);
                    TextureHandle debrisMask = DrawSmallOccluders(renderGraph, resources, bossDepth);
                    fillProperties.Clear();
                    fillProperties.SetColor(FillColorId, new Color(fillColor.r, fillColor.g, fillColor.b, fillColor.a * fillOpacity));
                    fillProperties.SetFloat(FillMinGapId, fillMinGap);
                    fillProperties.SetVector(FillRectId, bossRect);

                    // 보스 화면 영역을 작은 격자로 나눠 가려진 비율을 재고 CPU로 읽어 온다.
                    if (measuresRatio)
                    {
                        TextureDesc ratioDescriptor = new TextureDesc(HiddenRatioSize, HiddenRatioSize)
                        {
                            name = "Boss occlusion hidden ratio",
                            colorFormat = GraphicsFormat.R8G8_UNorm,
                            filterMode = FilterMode.Point,
                            clearBuffer = true,
                            clearColor = Color.clear,
                        };
                        TextureHandle ratio = renderGraph.CreateTexture(ratioDescriptor);
                        using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<CompositeData>("Boss occlusion hidden ratio", out CompositeData data))
                        {
                            SetCompositeData(data, HiddenRatioPass, fillProperties, playerMask, ignoredDepth, debrisMask);
                            UseFillInputs(builder, resources, bossDepth, playerMask, ignoredDepth, debrisMask);
                            builder.SetRenderAttachment(ratio, 0, AccessFlags.Write);
                            builder.SetRenderFunc(static (CompositeData data, RasterGraphContext context) => DrawComposite(data, context));
                        }
                        using (IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass<ReadbackData>("Boss occlusion hidden ratio readback", out ReadbackData data))
                        {
                            data.Source = ratio;
                            builder.UseTexture(ratio, AccessFlags.Read);
                            builder.AllowPassCulling(false);
                            builder.SetRenderFunc(static (ReadbackData data, UnsafeGraphContext context) =>
                                context.cmd.RequestAsyncReadback((Texture)data.Source, OnHiddenRatioRead));
                        }
                    }

                    if (fillOpacity > 0f)
                    {
                        using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<CompositeData>("Boss occlusion fill", out CompositeData data))
                        {
                            SetCompositeData(data, ScreenFillPass, fillProperties, playerMask, ignoredDepth, debrisMask);
                            UseFillInputs(builder, resources, bossDepth, playerMask, ignoredDepth, debrisMask);
                            builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                            builder.SetRenderFunc(static (CompositeData data, RasterGraphContext context) => DrawComposite(data, context));
                        }
                    }
                }
                else if (measuresRatio) HiddenRatio = 0f;

                if (overlapsPlayer)
                {
                    outlineProperties.Clear();
                    outlineProperties.SetColor(ColorId, outlineColor);
                    outlineProperties.SetFloat(WidthId, outlineWidthPixels);
                    outlineProperties.SetVector(RectId, overlapRect);
                    using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<CompositeData>("Boss occlusion outline", out CompositeData data))
                    {
                        TextureHandle black = renderGraph.defaultResources.blackTexture;
                        SetCompositeData(data, ScreenOutlinePass, outlineProperties, playerMask, black, black);
                        builder.UseTexture(playerMask, AccessFlags.Read);
                        builder.UseTexture(black, AccessFlags.Read);
                        builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                        builder.SetRenderFunc(static (CompositeData data, RasterGraphContext context) => DrawComposite(data, context));
                    }
                }
            }

            // 채우기에서 무시할 레이어의 불투명 물체를 화면에 보이는 것만 골라 가장 가까운 면의 깊이로 그린다.
            // 무시할 레이어가 없으면 검은 텍스처(커버리지 0)를 돌려준다.
            private TextureHandle DrawIgnoredOccluders(RenderGraph renderGraph, ContextContainer frameData, UniversalResourceData resources, UniversalCameraData cameraData)
            {
                if (fillIgnoredOccluders.value == 0) return renderGraph.defaultResources.blackTexture;

                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();
                DrawingSettings drawing = RenderingUtils.CreateDrawingSettings(OccluderShaderTags, renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
                drawing.overrideMaterial = material;
                drawing.overrideMaterialPassIndex = DepthMaskPass;
                FilteringSettings filtering = new FilteringSettings(RenderQueueRange.opaque, fillIgnoredOccluders);

                TextureHandle ignoredDepth = CreateMaskTexture(renderGraph, resources, "Boss occlusion ignored occluders", GraphicsFormat.R32G32_SFloat);
                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<OccluderData>("Boss occlusion ignored occluders", out OccluderData data))
                {
                    data.Renderers = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, drawing, filtering));
                    builder.UseRendererList(data.Renderers);
                    builder.SetRenderAttachment(ignoredDepth, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(CreateMaskZBuffer(renderGraph, ignoredDepth, "Boss occlusion ignored occluders z-buffer"), AccessFlags.Write);
                    builder.SetRenderFunc(static (OccluderData data, RasterGraphContext context) => context.cmd.DrawRendererList(data.Renderers));
                }
                return ignoredDepth;
            }

            // 작은 파편 중 화면 맨 앞에 보이는 부분을 그린다(b 채널). 파편이 없으면 검은 텍스처를 돌려준다.
            private TextureHandle DrawSmallOccluders(RenderGraph renderGraph, UniversalResourceData resources, TextureHandle bossDepth)
            {
                if (TryGetBounds(smallOccluders, out _) == false) return renderGraph.defaultResources.blackTexture;

                TextureHandle debrisMask = CreateMaskTexture(renderGraph, resources, "Boss occlusion small occluders", GraphicsFormat.R8G8B8A8_UNorm);
                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<MaskData>("Boss occlusion small occluders", out MaskData data))
                {
                    // 플레이어 마스크와 같은 판정(맨 앞에 보이는 면)을 쓴다.
                    SetMaskData(data, PlayerSilhouettePass, smallOccluders);
                    builder.UseTexture(bossDepth, AccessFlags.Read);
                    builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(debrisMask, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(CreateMaskZBuffer(renderGraph, debrisMask, "Boss occlusion small occluders z-buffer"), AccessFlags.Write);
                    builder.SetRenderFunc(static (MaskData data, RasterGraphContext context) => DrawMask(data, context));
                }
                return debrisMask;
            }

            private static void UseFillInputs(IRasterRenderGraphBuilder builder, UniversalResourceData resources,
                TextureHandle bossDepth, TextureHandle playerMask, TextureHandle ignoredDepth, TextureHandle debrisMask)
            {
                builder.UseTexture(bossDepth, AccessFlags.Read);
                builder.UseTexture(playerMask, AccessFlags.Read);
                builder.UseTexture(ignoredDepth, AccessFlags.Read);
                builder.UseTexture(debrisMask, AccessFlags.Read);
                builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
            }

            // 격자 표본의 r = 보스 커버리지, g = 채워질 양을 모두 더해 비율을 구한다.
            private static void ReadHiddenRatio(AsyncGPUReadbackRequest request)
            {
                if (request.hasError) return;
                Unity.Collections.NativeArray<byte> pixels = request.GetData<byte>();
                float coverage = 0f;
                float hidden = 0f;
                for (int index = 0; index + 1 < pixels.Length; index += 2)
                {
                    coverage += pixels[index];
                    hidden += pixels[index + 1];
                }
                HiddenRatio = coverage > 0f ? Mathf.Clamp01(hidden / coverage) : 0f;
            }

            private static TextureHandle CreateMaskTexture(RenderGraph renderGraph, UniversalResourceData resources, string name, GraphicsFormat format)
            {
                TextureDesc descriptor = renderGraph.GetTextureDesc(resources.activeColorTexture);
                descriptor.name = name;
                descriptor.colorFormat = format;
                // 대상 경계의 커버리지를 얻기 위해 MSAA로 그리고, 읽을 때 자동 Resolve된 값 사용
                descriptor.msaaSamples = MSAASamples.MSAA4x;
                descriptor.bindTextureMS = false;
                descriptor.depthBufferBits = DepthBits.None;
                descriptor.clearBuffer = true;
                descriptor.clearColor = Color.clear;
                descriptor.filterMode = FilterMode.Point;
                return renderGraph.CreateTexture(descriptor);
            }

            // 마스크 대상 메시끼리의 앞뒤만 가리는 전용 깊이 버퍼
            private static TextureHandle CreateMaskZBuffer(RenderGraph renderGraph, TextureHandle mask, string name)
            {
                TextureDesc descriptor = renderGraph.GetTextureDesc(mask);
                descriptor.name = name;
                descriptor.depthBufferBits = DepthBits.Depth32;
                return renderGraph.CreateTexture(descriptor);
            }

            private void SetMaskData(MaskData data, int shaderPass, List<Renderer> renderers)
            {
                data.Material = material;
                data.ShaderPass = shaderPass;
                data.Items.Clear();
                foreach (Renderer renderer in renderers)
                {
                    if (IsVisible(renderer) == false) continue;
                    // 반투명 재질(효과·텔레그래프 등)은 깊이에 남지 않아 실루엣으로 보면 엉뚱한 곳이 채워지므로 불투명 서브메시만 그린다.
                    renderer.GetSharedMaterials(materialBuffer);
                    int subMeshCount = Mathf.Min(GetSubMeshCount(renderer), materialBuffer.Count);
                    for (int index = 0; index < subMeshCount; index++)
                        if (IsOpaque(materialBuffer[index])) data.Items.Add(new DrawItem { Renderer = renderer, SubMesh = index });
                }
                materialBuffer.Clear();
            }

            private void SetCompositeData(CompositeData data, int shaderPass, MaterialPropertyBlock properties,
                TextureHandle playerMask, TextureHandle ignoredDepth, TextureHandle debrisMask)
            {
                data.Material = material;
                data.ShaderPass = shaderPass;
                data.Properties = properties;
                data.PlayerMask = playerMask;
                data.IgnoredDepth = ignoredDepth;
                data.DebrisMask = debrisMask;
            }

            private static void DrawMask(MaskData data, RasterGraphContext context)
            {
                foreach (DrawItem item in data.Items)
                    if (item.Renderer != null) context.cmd.DrawRenderer(item.Renderer, data.Material, item.SubMesh, data.ShaderPass);
            }

            private static void DrawComposite(CompositeData data, RasterGraphContext context)
            {
                // 마스크를 그리지 않은 프레임에는 검은 텍스처가 들어가도록 전역 값 대신 직접 넘긴다.
                data.Properties.SetTexture(PlayerMaskId, OrBlack(data.PlayerMask));
                data.Properties.SetTexture(IgnoredDepthId, OrBlack(data.IgnoredDepth));
                data.Properties.SetTexture(DebrisMaskId, OrBlack(data.DebrisMask));
                context.cmd.DrawProcedural(Matrix4x4.identity, data.Material, data.ShaderPass, MeshTopology.Triangles, 3, 1, data.Properties);
            }

            // 텍스처를 얻지 못하면 MaterialPropertyBlock이 null을 받지 않으므로 검은 텍스처로 대신한다.
            private static Texture OrBlack(TextureHandle handle)
            {
                Texture texture = handle;
                return texture != null ? texture : Texture2D.blackTexture;
            }

            private static bool IsVisible(Renderer renderer) =>
                renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy
                && renderer.forceRenderingOff == false && renderer.shadowCastingMode != ShadowCastingMode.ShadowsOnly;

            private static bool IsOpaque(Material material) =>
                material != null && material.renderQueue <= (int)RenderQueue.GeometryLast;

            private static bool IsEmpty(Vector4 rect) => rect.x >= rect.z || rect.y >= rect.w;

            private static bool TryGetBounds(List<Renderer> renderers, out Bounds bounds)
            {
                bounds = default;
                bool found = false;
                foreach (Renderer renderer in renderers)
                {
                    if (IsVisible(renderer) == false) continue;
                    if (found) bounds.Encapsulate(renderer.bounds);
                    else bounds = renderer.bounds;
                    found = true;
                }
                return found;
            }

            private static Vector4 Intersect(Vector4 a, Vector4 b) =>
                new Vector4(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y), Mathf.Min(a.z, b.z), Mathf.Min(a.w, b.w));

            private static Vector4 GetScreenRect(UniversalCameraData cameraData, Bounds bounds)
            {
                Camera camera = cameraData.camera;
                Matrix4x4 view = cameraData.GetViewMatrix();
                Matrix4x4 viewProjection = cameraData.GetGPUProjectionMatrix() * view;
                Vector2 minimum = Vector2.one;
                Vector2 maximum = Vector2.zero;
                for (int index = 0; index < 8; index++)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((index & 1) == 0 ? -1f : 1f, (index & 2) == 0 ? -1f : 1f, (index & 4) == 0 ? -1f : 1f));
                    if (-view.MultiplyPoint(corner).z <= camera.nearClipPlane) return ScreenRect;
                    Vector4 clip = viewProjection * new Vector4(corner.x, corner.y, corner.z, 1f);
                    Vector2 viewport = new Vector2(clip.x, clip.y) / clip.w * 0.5f + Vector2.one * 0.5f;
                    // GPU 투영 결과를 셰이더의 SV_POSITION과 같은 픽셀 좌표계로 변환한다.
                    if (SystemInfo.graphicsUVStartsAtTop) viewport.y = 1f - viewport.y;
                    minimum = Vector2.Min(minimum, viewport);
                    maximum = Vector2.Max(maximum, viewport);
                }
                Vector2 padding = new Vector2(10f / camera.scaledPixelWidth, 10f / camera.scaledPixelHeight);
                return new Vector4(minimum.x - padding.x, minimum.y - padding.y, maximum.x + padding.x, maximum.y + padding.y);
            }
        }
    }
}
