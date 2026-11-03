using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TeamOdd.Boss
{
    // 공격 범위 예고(AttackTelegraph)가 바닥(Ground 레이어) 외의 물체 위에 그려지지 않도록, 물체가 보이는 픽셀에 스텐실 비트를 표시하는 기능
    // 예고는 바닥 근처 상자 안의 위를 향한 면에 그리는 화면 공간 데칼이라, 상자 안에 들어온 상자·잔해 윗면, 다리·칼·어깨 윗면에도 묻는다.
    // 불투명 렌더링 직후 Ground 레이어가 아닌 불투명 물체와 등록된 렌더러를 마스크 셰이더(TelegraphMask)로 다시 그려 스텐실 128을 쓰고,
    // BossTelegraph 셰이더는 그 픽셀을 건너뛴다.
    // 보스는 BossMonster가 예고를 띄울 때, 플레이어는 AttackTelegraph가 등록하며, 보이는 예고가 없으면 그리지 않는다.
    public sealed class TelegraphMaskFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader maskShader;
        // 예고를 그릴 바닥 레이어, 비어 있으면 Ground 레이어를 씀
        [SerializeField] private LayerMask groundLayers;
        private Material material;
        private MaskPass pass;

        // 주인별 렌더러, 주인이 사라지면 렌더러도 null이 되어 그리지 않음
        private static readonly Dictionary<GameObject, Renderer[]> owners = new();
        private static readonly List<GameObject> removedOwners = new();

        // 이미 등록한 주인은 다시 모으지 않음
        public static void Add(GameObject owner)
        {
            if (owner == null || owners.ContainsKey(owner)) return;

            List<Renderer> renderers = new();
            foreach (Renderer renderer in owner.GetComponentsInChildren<Renderer>(true))
                if (renderer is SkinnedMeshRenderer || renderer is MeshRenderer) renderers.Add(renderer);

            owners.Add(owner, renderers.ToArray());
        }

        public override void Create()
        {
            CoreUtils.Destroy(material);
            if (maskShader == null) return;

            material = CoreUtils.CreateEngineMaterial(maskShader);
            int ground = groundLayers.value != 0 ? groundLayers.value : LayerMask.GetMask("Ground");
            pass = new MaskPass(material, ~ground);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (pass == null || AttackTelegraph.ActiveCount == 0) return;

            Camera camera = renderingData.cameraData.camera;
            if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView) return;

            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
        }

        private static void CollectVisible(List<Renderer> result)
        {
            result.Clear();
            removedOwners.Clear();

            foreach (KeyValuePair<GameObject, Renderer[]> pair in owners)
            {
                if (pair.Key == null)
                {
                    removedOwners.Add(pair.Key);
                    continue;
                }

                foreach (Renderer renderer in pair.Value)
                    if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy) result.Add(renderer);
            }

            foreach (GameObject owner in removedOwners) owners.Remove(owner);
        }

        private static int GetSubMeshCount(Renderer renderer)
        {
            Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                : renderer.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh
                : null;
            return mesh != null ? mesh.subMeshCount : 0;
        }

        private sealed class MaskPass : ScriptableRenderPass
        {
            private static readonly List<ShaderTagId> shaderTags = new()
            {
                new("UniversalForward"), new("UniversalForwardOnly"), new("SRPDefaultUnlit")
            };

            private readonly Material material;
            private readonly int maskedLayers;

            private sealed class PassData
            {
                public Material Material;
                public RendererListHandle MaskedObjects;
                public readonly List<Renderer> Renderers = new();
            }

            public MaskPass(Material material, int maskedLayers)
            {
                this.material = material;
                this.maskedLayers = maskedLayers;
                renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resources = frameData.Get<UniversalResourceData>();

                using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Telegraph mask", out PassData data);
                data.Material = material;
                CollectVisible(data.Renderers);

                // 바닥 레이어가 아닌 불투명 물체 전부를 마스크 셰이더로 다시 그림
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();
                DrawingSettings drawing = RenderingUtils.CreateDrawingSettings(shaderTags, renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
                drawing.overrideMaterial = material;
                drawing.overrideMaterialPassIndex = 0;
                FilteringSettings filtering = new(RenderQueueRange.opaque, maskedLayers);
                data.MaskedObjects = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, drawing, filtering));
                builder.UseRendererList(data.MaskedObjects);

                // 색은 쓰지 않지만 같은 대상에 이어 그려지도록 색·깊이를 함께 붙이고, 깊이 첨부의 스텐실에 표시
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawRendererList(data.MaskedObjects);

                    foreach (Renderer renderer in data.Renderers)
                    {
                        int subMeshCount = GetSubMeshCount(renderer);
                        for (int index = 0; index < subMeshCount; index++)
                            context.cmd.DrawRenderer(renderer, data.Material, index, 0);
                    }
                });
            }
        }
    }
}
