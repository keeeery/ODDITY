using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TeamOdd.Psychokinesis
{
    public sealed class PsychokinesisOutlineFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader outlineShader;
        private Material material;
        private OutlinePass pass;

        public override void Create()
        {
            CoreUtils.Destroy(material);
            material = CoreUtils.CreateEngineMaterial(outlineShader);
            pass = new OutlinePass(material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (PsychokinesisObject.OutlinedObject == null) return;
            Camera camera = renderingData.cameraData.camera;
            if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView) return;
            if ((camera.cullingMask & (1 << PsychokinesisObject.OutlinedObject.gameObject.layer)) == 0) return;
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
        }

        private sealed class OutlinePass : ScriptableRenderPass
        {
            private static readonly int MaskId = Shader.PropertyToID("_PsychokinesisOutlineMask");
            private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
            private static readonly int WidthId = Shader.PropertyToID("_OutlineWidthPixels");
            private static readonly int RectId = Shader.PropertyToID("_OutlineRect");
            private readonly Material material;
            private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();

            private sealed class MaskData
            {
                public Material Material;
                public Renderer Renderer;
                public int SubMeshCount;
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
                PsychokinesisObject target = PsychokinesisObject.OutlinedObject;
                if (target == null || target.OutlineRenderer.enabled == false) return;
                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                TextureDesc descriptor = renderGraph.GetTextureDesc(resources.activeColorTexture);
                descriptor.name = "Psychokinesis silhouette";
                descriptor.colorFormat = GraphicsFormat.R16G16_SFloat;
                // 가장자리 커버리지를 얻기 위해 MSAA로 그리고, 읽을 때 자동 Resolve된 값 사용
                descriptor.msaaSamples = MSAASamples.MSAA4x;
                descriptor.bindTextureMS = false;
                descriptor.depthBufferBits = DepthBits.None;
                descriptor.clearBuffer = true;
                descriptor.clearColor = Color.clear;
                descriptor.filterMode = FilterMode.Point;
                TextureHandle mask = renderGraph.CreateTexture(descriptor);

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<MaskData>("Psychokinesis silhouette", out MaskData data))
                {
                    data.Material = material;
                    data.Renderer = target.OutlineRenderer;
                    data.SubMeshCount = target.SubMeshCount;
                    builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(mask, MaskId);
                    builder.SetRenderFunc(static (MaskData data, RasterGraphContext context) =>
                    {
                        for (int index = 0; index < data.SubMeshCount; index++)
                            context.cmd.DrawRenderer(data.Renderer, data.Material, index, 0);
                    });
                }

                properties.Clear();
                properties.SetColor(ColorId, target.OutlineColor);
                properties.SetFloat(WidthId, target.OutlineWidthPixels);
                properties.SetVector(RectId, GetScreenRect(cameraData, target.OutlineRenderer.bounds));
                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<CompositeData>("Psychokinesis screen outline", out CompositeData data))
                {
                    data.Material = material;
                    data.Properties = properties;
                    builder.UseTexture(mask, AccessFlags.Read);
                    builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderFunc(static (CompositeData data, RasterGraphContext context) =>
                        context.cmd.DrawProcedural(Matrix4x4.identity, data.Material, 1, MeshTopology.Triangles, 3, 1, data.Properties));
                }
            }

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
                // 대상 주변에서만 이웃 픽셀을 검사해 화면 전체의 반복 샘플링을 피한다.
                Vector2 padding = new Vector2(10f / camera.scaledPixelWidth, 10f / camera.scaledPixelHeight);
                return new Vector4(minimum.x - padding.x, minimum.y - padding.y, maximum.x + padding.x, maximum.y + padding.y);
            }
        }
    }
}
