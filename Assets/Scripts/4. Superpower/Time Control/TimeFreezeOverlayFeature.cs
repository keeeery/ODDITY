using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TeamOdd.TimeControl
{
    // 시간 정지된 오브젝트(TimeFreezable.OverlayTargets)의 메시 위에 홀로그램 막을 덧그리는 렌더러 기능
    // 오브젝트마다 진행도·높이 범위·글리치 세기를 전역 값으로 넘긴다.
    public sealed class TimeFreezeOverlayFeature : ScriptableRendererFeature
    {
        [Tooltip("TeamOdd/Time Freeze Hologram 셰이더 머티리얼, 스캔라인·프레넬·경계선 띠·정지 글리치 세기는 머티리얼에서 조정한다.")]
        [SerializeField] private Material overlayMaterial;

        // 막을 그리지 않을 카메라(미니맵 등), DrawRenderer는 컬링 마스크를 무시하므로 따로 걸러야 한다.
        private static readonly HashSet<Camera> excludedCameras = new();

        private OverlayPass pass;

        public static void ExcludeCamera(Camera camera) => excludedCameras.Add(camera);

        public static void IncludeCamera(Camera camera) => excludedCameras.Remove(camera);

        public override void Create()
        {
            pass = new OverlayPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (overlayMaterial == null || TimeFreezable.OverlayTargets.Count == 0) return;

            Camera camera = renderingData.cameraData.camera;
            if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView) return;
            if (excludedCameras.Contains(camera)) return;

            pass.Material = overlayMaterial;
            renderer.EnqueuePass(pass);
        }

        private sealed class OverlayPass : ScriptableRenderPass
        {
            private static readonly int ProgressId = Shader.PropertyToID("_TimeFreezeOverlayProgress");
            private static readonly int BoundsId = Shader.PropertyToID("_TimeFreezeOverlayBounds");
            private static readonly int GlitchId = Shader.PropertyToID("_TimeFreezeOverlayGlitch");

            public Material Material;

            private struct DrawItem
            {
                public Renderer Renderer;
                public int SubMeshCount;
            }

            // 한 오브젝트의 렌더러들(Items의 First부터 Count개)이 같은 진행도와 높이 범위를 씀
            private struct DrawGroup
            {
                public int First;
                public int Count;
                public float Progress;
                public float Glitch;
                public Vector4 Bounds;
            }

            private sealed class PassData
            {
                public Material Material;
                public readonly List<DrawItem> Items = new();
                public readonly List<DrawGroup> Groups = new();
            }

            public OverlayPass()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resources = frameData.Get<UniversalResourceData>();

                using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Time freeze overlay", out PassData data);
                data.Material = Material;
                data.Items.Clear();
                data.Groups.Clear();
                Collect(data.Items, data.Groups);

                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                // 오브젝트마다 전역 값을 바꿔 가며 그림
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    foreach (DrawGroup group in data.Groups)
                    {
                        context.cmd.SetGlobalFloat(ProgressId, group.Progress);
                        context.cmd.SetGlobalVector(BoundsId, group.Bounds);
                        context.cmd.SetGlobalFloat(GlitchId, group.Glitch);

                        for (int itemIndex = group.First; itemIndex < group.First + group.Count; itemIndex++)
                        {
                            DrawItem item = data.Items[itemIndex];
                            for (int index = 0; index < item.SubMeshCount; index++)
                                context.cmd.DrawRenderer(item.Renderer, data.Material, index, 0);
                        }
                    }
                });
            }

            // 꺼져 있거나 화면 밖인 렌더러는 건너뛰고, 오브젝트의 높이 범위는 그리는 렌더러들의 바운드를 합쳐 정함
            // 높이 범위는 움직이는 스킨드 메시도 맞도록 매 프레임 다시 잼
            private static void Collect(List<DrawItem> items, List<DrawGroup> groups)
            {
                foreach (TimeFreezable target in TimeFreezable.OverlayTargets)
                {
                    int first = items.Count;
                    float minY = float.MaxValue;
                    float maxY = float.MinValue;

                    IReadOnlyList<Renderer> renderers = target.OverlayRenderers;
                    for (int index = 0; index < renderers.Count; index++)
                    {
                        Renderer renderer = renderers[index];
                        if (renderer == null || renderer.enabled == false || renderer.gameObject.activeInHierarchy == false || renderer.isVisible == false)
                            continue;

                        int subMeshCount = target.GetOverlaySubMeshCount(index);
                        if (subMeshCount == 0) continue;

                        Bounds bounds = renderer.bounds;
                        minY = Mathf.Min(minY, bounds.min.y);
                        maxY = Mathf.Max(maxY, bounds.max.y);
                        items.Add(new DrawItem { Renderer = renderer, SubMeshCount = subMeshCount });
                    }

                    if (items.Count == first) continue;

                    groups.Add(new DrawGroup
                    {
                        First = first,
                        Count = items.Count - first,
                        Progress = target.OverlayProgress,
                        Glitch = target.OverlayGlitch,
                        Bounds = new Vector4(minY, maxY, 0f, 0f),
                    });
                }
            }
        }
    }
}
