using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TeamOdd.Player
{
    // 미니맵 카메라 전용 패스, 불투명 렌더링 뒤에 Wall 레이어를 단색 머티리얼로 다시 그려 바닥을 가린다.
    public sealed class MinimapWallPass : ScriptableRenderPass
    {
        private static readonly List<ShaderTagId> shaderTags = new()
        {
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("SRPDefaultUnlit"),
        };

        private readonly Material material;

        public LayerMask WallLayers { get; set; }

        private sealed class PassData
        {
            public RendererListHandle RendererList;
        }

        public MinimapWallPass(Material material)
        {
            this.material = material;
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();
            UniversalResourceData resources = frameData.Get<UniversalResourceData>();

            DrawingSettings drawing = RenderingUtils.CreateDrawingSettings(shaderTags, renderingData, cameraData, lightData,
                SortingCriteria.CommonOpaque);
            drawing.overrideMaterial = material;
            drawing.overrideMaterialPassIndex = 0;
            FilteringSettings filtering = new FilteringSettings(RenderQueueRange.opaque, WallLayers);

            using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>("Minimap wall", out PassData data))
            {
                data.RendererList = renderGraph.CreateRendererList(
                    new RendererListParams(renderingData.cullResults, drawing, filtering));
                builder.UseRendererList(data.RendererList);
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.ReadWrite);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                    context.cmd.DrawRendererList(data.RendererList));
            }
        }
    }
}
