using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TeamOdd.TimeControl
{
    // 시간 정지된 오브젝트(TimeFreezable.OverlayTargets)의 메시 위에 파란 반투명 막을 한 번 더 그리는 렌더러 기능
    // 원래 머티리얼은 건드리지 않고, 투명 오브젝트까지 그린 뒤 같은 메시를 TimeFreezeOverlay 셰이더로 덧그린다.
    // 막은 오브젝트 높이를 따라 아래에서 위로 덮이고 위에서 아래로 걷히므로,
    // 오브젝트마다 다른 진행도와 높이 범위(렌더러 바운드를 합친 것)를 그리기 전에 전역 값으로 넘긴다.
    public sealed class TimeFreezeOverlayFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader overlayShader;
        [SerializeField] private Color color = new(0.225f, 0.45f, 1f, 0.275f);
        [Tooltip("가장자리(실루엣) 쪽 색, 알파가 높을수록 윤곽이 진해진다.")]
        [SerializeField] private Color rimColor = new(0.425f, 0.65f, 1f, 0.775f);
        [SerializeField, Range(0.5f, 8f)] private float rimPower = 2.5f;
        [Tooltip("막이 씌워지는 경계선의 밝은 띠 색")]
        [SerializeField] private Color edgeColor = new(0.675f, 0.8f, 1f, 0.925f);
        [Tooltip("경계선 띠의 두께(m)")]
        [SerializeField, Min(0.001f)] private float edgeWidth = 0.15f;

        // 막을 그리지 않을 카메라(미니맵 등), DrawRenderer는 컬링 마스크를 무시하므로 따로 걸러야 한다.
        private static readonly HashSet<Camera> excludedCameras = new();

        private Material material;
        private OverlayPass pass;

        public static void ExcludeCamera(Camera camera) => excludedCameras.Add(camera);

        public static void IncludeCamera(Camera camera) => excludedCameras.Remove(camera);

        public override void Create()
        {
            CoreUtils.Destroy(material);
            if (overlayShader == null) return;

            material = CoreUtils.CreateEngineMaterial(overlayShader);
            material.SetColor("_BaseColor", color);
            material.SetColor("_RimColor", rimColor);
            material.SetFloat("_RimPower", rimPower);
            material.SetColor("_EdgeColor", edgeColor);
            material.SetFloat("_EdgeWidth", edgeWidth);
            pass = new OverlayPass(material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material == null || TimeFreezable.OverlayTargets.Count == 0) return;

            Camera camera = renderingData.cameraData.camera;
            if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView) return;
            if (excludedCameras.Contains(camera)) return;

            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
        }

        private sealed class OverlayPass : ScriptableRenderPass
        {
            private static readonly int ProgressId = Shader.PropertyToID("_TimeFreezeOverlayProgress");
            private static readonly int BoundsId = Shader.PropertyToID("_TimeFreezeOverlayBounds");
            private readonly Material material;

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
                public Vector4 Bounds;
            }

            private sealed class PassData
            {
                public Material Material;
                public readonly List<DrawItem> Items = new();
                public readonly List<DrawGroup> Groups = new();
            }

            public OverlayPass(Material material)
            {
                this.material = material;
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resources = frameData.Get<UniversalResourceData>();

                using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Time freeze overlay", out PassData data);
                data.Material = material;
                data.Items.Clear();
                data.Groups.Clear();
                Collect(data.Items, data.Groups);

                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                // 오브젝트마다 진행도와 높이 범위를 전역 값으로 바꿔 가며 그림
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    foreach (DrawGroup group in data.Groups)
                    {
                        context.cmd.SetGlobalFloat(ProgressId, group.Progress);
                        context.cmd.SetGlobalVector(BoundsId, group.Bounds);

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
                        Bounds = new Vector4(minY, maxY, 0f, 0f),
                    });
                }
            }
        }
    }
}
