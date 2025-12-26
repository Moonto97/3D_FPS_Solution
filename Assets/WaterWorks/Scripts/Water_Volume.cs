using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class Water_Volume : ScriptableRendererFeature
{
    class CustomRenderPass : ScriptableRenderPass
    {
        private Material _material;
        private RTHandle _tempRT;

        public CustomRenderPass(Material mat)
        {
            _material = mat;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // 임시 렌더 타겟 생성
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            RenderingUtils.ReAllocateHandleIfNeeded(ref _tempRT, descriptor, name: "_TemporaryColourTexture");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // 리플렉션 카메라는 제외
            if (renderingData.cameraData.cameraType == CameraType.Reflection)
                return;

            if (_material == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get("Water_Volume");

            // 카메라 컬러 타겟 가져오기
            RTHandle cameraColorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;

            // Blit: 카메라 → 임시RT (머티리얼 적용) → 카메라
            Blitter.BlitCameraTexture(cmd, cameraColorTarget, _tempRT, _material, 0);
            Blitter.BlitCameraTexture(cmd, _tempRT, cameraColorTarget);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // RTHandle은 ReAllocateHandleIfNeeded로 관리되므로 여기서 해제 불필요
        }

        public void Dispose()
        {
            _tempRT?.Release();
        }
    }

    [System.Serializable]
    public class _Settings
    {
        public Material material = null;
        public RenderPassEvent renderPass = RenderPassEvent.AfterRenderingSkybox;
    }

    public _Settings settings = new _Settings();
    CustomRenderPass _pass;

    public override void Create()
    {
        if (settings.material == null)
        {
            settings.material = (Material)Resources.Load("Water_Volume");
        }

        _pass = new CustomRenderPass(settings.material);
        _pass.renderPassEvent = settings.renderPass;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        _pass?.Dispose();
    }
}
