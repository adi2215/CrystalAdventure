using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class RadialBlurEffect : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material material;
        public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingTransparents;
    }

    public Settings settings = new Settings();

    class Pass : ScriptableRenderPass
    {
        Material mat;

        RenderTargetIdentifier source;
        int tempTexID = Shader.PropertyToID("_TempTex");

        public Pass(Material material, RenderPassEvent evt)
        {
            mat = material;
            renderPassEvent = evt;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            source = renderingData.cameraData.renderer.cameraColorTargetHandle;

            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;

            cmd.GetTemporaryRT(tempTexID, desc, FilterMode.Bilinear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (mat == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get("RadialBlur");

            // 1: source → temp
            cmd.Blit(source, tempTexID);

            // 2: temp → source через материал
            cmd.Blit(tempTexID, source, mat);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(tempTexID);
        }
    }

    Pass pass;

    public override void Create()
    {
        pass = new Pass(settings.material, settings.passEvent);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.material == null)
            return;

        renderer.EnqueuePass(pass);
    }

}


