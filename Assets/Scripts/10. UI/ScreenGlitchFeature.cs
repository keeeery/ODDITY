using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace TeamOdd.UI
{
    // ScreenGlitch 값으로 후처리가 끝난 화면을 한 번 더 그리는 렌더러 기능(사망 화면의 신호 끊김 연출)
    // 값이 모두 0이면 패스를 넣지 않으므로 평소에는 비용이 없다.
    public sealed class ScreenGlitchFeature : ScriptableRendererFeature
    {
        private static readonly int GlitchId = Shader.PropertyToID("_Glitch");
        private static readonly int DesaturateId = Shader.PropertyToID("_Desaturate");
        private static readonly int VignetteId = Shader.PropertyToID("_Vignette");
        private static readonly int PowerOffId = Shader.PropertyToID("_PowerOff");
        private static readonly int GlitchTimeId = Shader.PropertyToID("_GlitchTime");

        [Tooltip("Hidden/TeamOdd/Screen Glitch 셰이더")]
        [SerializeField] private Shader shader;

        private Material material;
        private GlitchPass pass;

        public override void Create()
        {
            pass = new GlitchPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (ScreenGlitch.IsActive == false || shader == null) return;

            // 화면에 바로 나가는 게임 카메라만, 미니맵처럼 텍스처에 그리는 카메라와 씬 뷰는 건너뜀
            Camera camera = renderingData.cameraData.camera;
            if (camera.cameraType != CameraType.Game || camera.targetTexture != null) return;
            if (renderingData.cameraData.renderType != CameraRenderType.Base) return;

            if (material == null) material = CoreUtils.CreateEngineMaterial(shader);
            material.SetFloat(GlitchId, ScreenGlitch.Glitch);
            material.SetFloat(DesaturateId, ScreenGlitch.Desaturate);
            material.SetFloat(VignetteId, ScreenGlitch.Vignette);
            material.SetFloat(PowerOffId, ScreenGlitch.PowerOff);
            // 히트스탑으로 시간이 멈춰도 지직거리도록 실제 시간을 쓴다.
            material.SetFloat(GlitchTimeId, Time.unscaledTime);

            pass.Material = material;
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
        }

        private sealed class GlitchPass : ScriptableRenderPass
        {
            public Material Material;

            public GlitchPass()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                // 화면 색을 읽어 다시 그려야 하므로 백버퍼 대신 중간 텍스처를 쓰게 한다.
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;

                TextureHandle source = resources.activeColorTexture;
                TextureDesc desc = renderGraph.GetTextureDesc(source);
                desc.name = "_ScreenGlitchColor";
                desc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(desc);

                renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, destination, Material, 0), "Screen glitch");
                resources.cameraColor = destination;
            }
        }
    }
}
