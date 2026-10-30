using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TeamOdd.UI
{
    // 보스가 플레이어를 가리면 가려진 부분에만 플레이어 실루엣의 외곽선을 그리는 기능
    // 대상과 색은 BossHUD가 SetTargets로 넘겨준다.
    public sealed class BossOcclusionOutlineFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader outlineShader;
        private Material material;
        private OutlinePass pass;

        private static readonly List<Renderer> bossRenderers = new List<Renderer>();
        private static readonly List<Renderer> playerRenderers = new List<Renderer>();
        private static Color outlineColor = Color.white;
        private static float outlineWidthPixels = 2f;

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

        public static void ClearTargets()
        {
            bossRenderers.Clear();
            playerRenderers.Clear();
        }

        public override void Create()
        {
            CoreUtils.Destroy(material);
            material = CoreUtils.CreateEngineMaterial(outlineShader);
            pass = new OutlinePass(material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (bossRenderers.Count == 0 || playerRenderers.Count == 0) return;
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
            private const int BossDepthPass = 0;
            private const int PlayerSilhouettePass = 1;
            private const int ScreenOutlinePass = 2;
            private static readonly int BossDepthId = Shader.PropertyToID("_BossOcclusionBossDepth");
            private static readonly int PlayerMaskId = Shader.PropertyToID("_BossOcclusionPlayerMask");
            private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
            private static readonly int WidthId = Shader.PropertyToID("_OutlineWidthPixels");
            private static readonly int RectId = Shader.PropertyToID("_OutlineRect");
            private readonly Material material;
            private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();

            private sealed class MaskData
            {
                public Material Material;
                public int ShaderPass;
                public readonly List<Renderer> Renderers = new List<Renderer>();
            }

            private sealed class CompositeData
            {
                public Material Material;
                public MaterialPropertyBlock Properties;
            }

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
                // 두 대상이 화면에서 겹치는 영역이 없으면 마스크도 그리지 않는다.
                if (TryGetBounds(bossRenderers, out Bounds bossBounds) == false) return;
                if (TryGetBounds(playerRenderers, out Bounds playerBounds) == false) return;
                Vector4 rect = Intersect(GetScreenRect(cameraData, bossBounds), GetScreenRect(cameraData, playerBounds));
                if (rect.x >= rect.z || rect.y >= rect.w) return;

                TextureDesc bossDescriptor = renderGraph.GetTextureDesc(resources.activeColorTexture);
                bossDescriptor.name = "Boss occlusion depth";
                bossDescriptor.colorFormat = GraphicsFormat.R32G32_SFloat;
                // 보스 경계의 커버리지를 얻기 위해 MSAA로 그리고, 읽을 때 자동 Resolve된 값 사용
                bossDescriptor.msaaSamples = MSAASamples.MSAA4x;
                bossDescriptor.bindTextureMS = false;
                bossDescriptor.depthBufferBits = DepthBits.None;
                bossDescriptor.clearBuffer = true;
                bossDescriptor.clearColor = Color.clear;
                bossDescriptor.filterMode = FilterMode.Point;
                TextureHandle bossDepth = renderGraph.CreateTexture(bossDescriptor);

                // 보스 메시끼리의 앞뒤만 가리는 전용 깊이 버퍼
                TextureDesc bossZDescriptor = bossDescriptor;
                bossZDescriptor.name = "Boss occlusion z-buffer";
                bossZDescriptor.depthBufferBits = DepthBits.Depth32;
                TextureHandle bossZ = renderGraph.CreateTexture(bossZDescriptor);

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<MaskData>("Boss occlusion depth", out MaskData data))
                {
                    SetMaskData(data, BossDepthPass, bossRenderers);
                    builder.SetRenderAttachment(bossDepth, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(bossZ, AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(bossDepth, BossDepthId);
                    builder.SetRenderFunc(static (MaskData data, RasterGraphContext context) => DrawMask(data, context));
                }

                TextureDesc playerDescriptor = renderGraph.GetTextureDesc(resources.activeColorTexture);
                playerDescriptor.name = "Boss occlusion player silhouette";
                playerDescriptor.colorFormat = GraphicsFormat.R16G16_SFloat;
                // 가장자리 커버리지를 얻기 위해 MSAA로 그리고, 읽을 때 자동 Resolve된 값 사용
                playerDescriptor.msaaSamples = MSAASamples.MSAA4x;
                playerDescriptor.bindTextureMS = false;
                playerDescriptor.depthBufferBits = DepthBits.None;
                playerDescriptor.clearBuffer = true;
                playerDescriptor.clearColor = Color.clear;
                playerDescriptor.filterMode = FilterMode.Point;
                TextureHandle playerMask = renderGraph.CreateTexture(playerDescriptor);

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<MaskData>("Boss occlusion player silhouette", out MaskData data))
                {
                    SetMaskData(data, PlayerSilhouettePass, playerRenderers);
                    builder.UseTexture(bossDepth, AccessFlags.Read);
                    builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(playerMask, 0, AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(playerMask, PlayerMaskId);
                    builder.SetRenderFunc(static (MaskData data, RasterGraphContext context) => DrawMask(data, context));
                }

                properties.Clear();
                properties.SetColor(ColorId, outlineColor);
                properties.SetFloat(WidthId, outlineWidthPixels);
                properties.SetVector(RectId, rect);
                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<CompositeData>("Boss occlusion outline", out CompositeData data))
                {
                    data.Material = material;
                    data.Properties = properties;
                    builder.UseTexture(playerMask, AccessFlags.Read);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderFunc(static (CompositeData data, RasterGraphContext context) =>
                        context.cmd.DrawProcedural(Matrix4x4.identity, data.Material, ScreenOutlinePass, MeshTopology.Triangles, 3, 1, data.Properties));
                }
            }

            private void SetMaskData(MaskData data, int shaderPass, List<Renderer> renderers)
            {
                data.Material = material;
                data.ShaderPass = shaderPass;
                data.Renderers.Clear();
                foreach (Renderer renderer in renderers)
                    if (IsVisible(renderer)) data.Renderers.Add(renderer);
            }

            private static void DrawMask(MaskData data, RasterGraphContext context)
            {
                foreach (Renderer renderer in data.Renderers)
                {
                    int subMeshCount = GetSubMeshCount(renderer);
                    for (int index = 0; index < subMeshCount; index++)
                        context.cmd.DrawRenderer(renderer, data.Material, index, data.ShaderPass);
                }
            }

            private static bool IsVisible(Renderer renderer) =>
                renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy;

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
                    if (-view.MultiplyPoint(corner).z <= camera.nearClipPlane) return new Vector4(0f, 0f, 1f, 1f);
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
