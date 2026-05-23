using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class WBOITRenderFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public LayerMask layerMask = -1;
        public Material compositeMaterial = null;
        public Material wboitMaterial = null;
    }

    public Settings settings = new Settings();
    WBOITPass m_ScriptablePass;

    public override void Create()
    {
        m_ScriptablePass = new WBOITPass(settings);
        m_ScriptablePass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (OITRegistry.Objects[OITAlgorithm.WBOIT].Count == 0)
        {
            return;
        }
        
        if (settings.compositeMaterial != null)
        {
            m_ScriptablePass.SetRenderer(renderer);
            renderer.EnqueuePass(m_ScriptablePass);
        }
    }

    protected override void Dispose(bool disposing)
    {
        m_ScriptablePass?.Dispose();
    }

    class WBOITPass : ScriptableRenderPass
    {
        private Settings settings;
        private FilteringSettings filteringSettings;
        private List<ShaderTagId> shaderTagIdList = new List<ShaderTagId> { new ShaderTagId("WBOIT") };

        private RTHandle accumTex;
        private RTHandle revealTex;

        private ScriptableRenderer m_Renderer;
        private RenderTargetIdentifier[] mrt;

        public WBOITPass(Settings settings)
        {
            this.settings = settings;
            filteringSettings = new FilteringSettings(RenderQueueRange.all, settings.layerMask);
            mrt = new RenderTargetIdentifier[2];
        }

        // 保存 Renderer 引用
        public void SetRenderer(ScriptableRenderer renderer)
        {
            m_Renderer = renderer;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            
            desc.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;
            RenderingUtils.ReAllocateIfNeeded(ref accumTex, desc, name: "_AccumTex");

            desc.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8_UNorm;
            RenderingUtils.ReAllocateIfNeeded(ref revealTex, desc, name: "_RevealTex");

            mrt[0] = accumTex.nameID;
            mrt[1] = revealTex.nameID;
            
            ConfigureTarget(mrt, m_Renderer.cameraDepthTargetHandle);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("WBOIT Pass");
            
            RTHandle cameraColorTarget = m_Renderer.cameraColorTargetHandle;
            RTHandle cameraDepthTarget = m_Renderer.cameraDepthTargetHandle;

            //-------Clean pass-------
            cmd.SetRenderTarget(accumTex);
            cmd.ClearRenderTarget(false, true, new Color(0, 0, 0, 0));

            cmd.SetRenderTarget(revealTex);
            cmd.ClearRenderTarget(false, true, new Color(1, 1, 1, 1));
            
            cmd.SetRenderTarget(mrt, cameraDepthTarget);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            //------Accumulate pass------  
            cmd.SetGlobalTexture("_AccumTex", accumTex);
            cmd.SetGlobalTexture("_RevealTex", revealTex);
            
            var drawingSettings = CreateDrawingSettings(shaderTagIdList, ref renderingData, SortingCriteria.CommonTransparent);
            drawingSettings.overrideMaterial = settings.wboitMaterial;
            context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);
            
            //------Composite pass------
            cmd.SetRenderTarget(cameraColorTarget);
            cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, settings.compositeMaterial, 0, 0);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            accumTex?.Release();
            revealTex?.Release();
        }
    }
}