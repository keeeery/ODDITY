using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TeamOdd.Combat
{
    // HitFlash.Active의 메시 위에 번쩍임을 덧그리는 렌더러 기능
    // Time Freeze Overlay보다 뒤에 두어야 시간 정지 막 위에도 보인다.
    public sealed class HitFlashFeature : ScriptableRendererFeature
    {
        [Tooltip("TeamOdd/Hit Flash 셰이더 머티리얼, 색·윤곽 두께는 머티리얼에서 조정한다.")]
        [SerializeField] private Material flashMaterial;

        // DrawRenderer는 컬링 마스크를 무시하므로 미니맵 등은 따로 거름
        private static readonly HashSet<Camera> excludedCameras = new();

        private FlashPass pass;

        public static void ExcludeCamera(Camera camera) => excludedCameras.Add(camera);

        public static void IncludeCamera(Camera camera) => excludedCameras.Remove(camera);

        public override void Create()
        {
            pass = new FlashPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (flashMaterial == null || HitFlash.Active.Count == 0) return;

            Camera camera = renderingData.cameraData.camera;
            if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView) return;
            if (excludedCameras.Contains(camera)) return;

            pass.Material = flashMaterial;
            renderer.EnqueuePass(pass);
        }

        private sealed class FlashPass : ScriptableRenderPass
        {
            private static readonly int IntensityId = Shader.PropertyToID("_HitFlashIntensity");

            public Material Material;

            private struct DrawItem
            {
                public Renderer Renderer;
                public int SubMeshCount;
            }

            // 한 대상의 렌더러들(Items[First..First+Count])
            private struct DrawGroup
            {
                public int First;
                public int Count;
                public float Intensity;
            }

            private sealed class PassData
            {
                public Material Material;
                public readonly List<DrawItem> Items = new();
                public readonly List<DrawGroup> Groups = new();
            }

            public FlashPass()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resources = frameData.Get<UniversalResourceData>();

                using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Hit flash", out PassData data);
                data.Material = Material;
                data.Items.Clear();
                data.Groups.Clear();
                Collect(data.Items, data.Groups);

                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    foreach (DrawGroup group in data.Groups)
                    {
                        context.cmd.SetGlobalFloat(IntensityId, group.Intensity);

                        for (int itemIndex = group.First; itemIndex < group.First + group.Count; itemIndex++)
                        {
                            DrawItem item = data.Items[itemIndex];
                            for (int index = 0; index < item.SubMeshCount; index++)
                                context.cmd.DrawRenderer(item.Renderer, data.Material, index, 0);
                        }
                    }
                });
            }

            private static void Collect(List<DrawItem> items, List<DrawGroup> groups)
            {
                foreach (HitFlash target in HitFlash.Active)
                {
                    float intensity = target.Intensity;
                    if (intensity <= 0f) continue;

                    int first = items.Count;
                    IReadOnlyList<Renderer> renderers = target.Renderers;
                    for (int index = 0; index < renderers.Count; index++)
                    {
                        Renderer renderer = renderers[index];
                        if (renderer == null || renderer.enabled == false || renderer.gameObject.activeInHierarchy == false || renderer.isVisible == false)
                            continue;

                        int subMeshCount = target.GetSubMeshCount(index);
                        if (subMeshCount == 0) continue;

                        items.Add(new DrawItem { Renderer = renderer, SubMeshCount = subMeshCount });
                    }

                    if (items.Count == first) continue;

                    groups.Add(new DrawGroup { First = first, Count = items.Count - first, Intensity = intensity });
                }
            }
        }
    }
}
