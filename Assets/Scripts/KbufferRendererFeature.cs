using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class KbufferRenderFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public LayerMask layerMask = -1;
        public ComputeShader clearComputeShader = null;
        public Material buildMaterial = null; 
        public Material compositeMaterial = null;
       
        
        //private int currentK = -1;
    }

    public Settings settings = new Settings();
    KbufferPass m_ScriptablePass;

    public override void Create()
    {
        m_ScriptablePass = new KbufferPass(settings);
        m_ScriptablePass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (OITRegistry.Objects[OITAlgorithm.Kbuffer].Count == 0)
        {
            return;
        }
        
        if (settings.compositeMaterial != null)
        {
            //m_ScriptablePass.SetRenderer(renderer);
            m_ScriptablePass.ConfigureInput(ScriptableRenderPassInput.Depth);
            renderer.EnqueuePass(m_ScriptablePass);
        }
        
    }

    protected override void Dispose(bool disposing)
    {
        m_ScriptablePass?.Dispose();
    }

    class KbufferPass : ScriptableRenderPass
    {
        
        private Settings settings;
        private FilteringSettings filteringSettings;
        private ShaderTagId shaderTagId = new ShaderTagId("Kbuffer");

        private GraphicsBuffer colorBuffer;
        private GraphicsBuffer depthBuffer;

        private int currentWidth = -1;
        private int currentHeight = -1;
        private int currentK = -1;

        public KbufferPass(Settings settings)
        {
            this.settings = settings;
            filteringSettings = new FilteringSettings(RenderQueueRange.all, settings.layerMask);
           
        }
        
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            int k = OITRegistry.Layers;
           
            if(colorBuffer == null|| currentWidth != desc.width || currentHeight != desc.height|| currentK != k)
            { 
                Dispose();
                currentWidth = desc.width;
                currentHeight = desc.height; 
                currentK = k;
                int totalPixelCount = currentWidth * currentHeight * k; 
                colorBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, totalPixelCount, sizeof(float) * 4); 
                depthBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, totalPixelCount, sizeof(float));
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("K-Buffer Pass");
            
            var drawingSettings = CreateDrawingSettings(shaderTagId, ref renderingData, SortingCriteria.CommonTransparent);
            
            var renderer = renderingData.cameraData.renderer;
            
            int k = Mathf.Max(1, currentK);
            
             cmd.SetGlobalInt("_ScreenWidth", currentWidth);
             cmd.SetGlobalInt("_ScreenHeight", currentHeight);
             cmd.SetGlobalInt("_KSize", k);
            
            //-----Clear Buffers-----
            int kernel = settings.clearComputeShader.FindKernel("KClear");
            cmd.SetComputeBufferParam(settings.clearComputeShader, kernel, "_KBufferColor", colorBuffer);
            cmd.SetComputeBufferParam(settings.clearComputeShader, kernel, "_KBufferDepth", depthBuffer);
            int threadGroupX = Mathf.CeilToInt(currentWidth / 8.0f);
            int threadGroupY = Mathf.CeilToInt(currentHeight / 8.0f);
            cmd.DispatchCompute(settings.clearComputeShader, kernel,threadGroupX, threadGroupY, 1);
            
            //------build------
            cmd.SetRenderTarget(renderer.cameraColorTargetHandle, renderer.cameraDepthTargetHandle);
            cmd.SetRandomWriteTarget(1, colorBuffer);
            cmd.SetRandomWriteTarget(2, depthBuffer);
            
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            
            drawingSettings.overrideMaterial = settings.buildMaterial;
            context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);
            
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            //------Resolve Pass-------
            cmd.ClearRandomWriteTargets();
            cmd.SetGlobalBuffer("_KBufferColor", colorBuffer);
            cmd.SetGlobalBuffer("_KBufferDepth", depthBuffer);
            
            cmd.DrawProcedural(Matrix4x4.identity, settings.compositeMaterial, 0, MeshTopology.Triangles, 3);
            
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            colorBuffer?.Release();
            colorBuffer = null;
            depthBuffer?.Release();
            depthBuffer = null;
        }
    }
}