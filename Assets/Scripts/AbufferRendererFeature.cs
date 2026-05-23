using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class AbufferRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public LayerMask layerMask = -1;
        public ComputeShader clearComputeShader;
        public Material buildMaterial;
        public Material resolveMaterial;
        
    }

    public Settings settings = new Settings();
    AbufferPass m_ScriptablePass;

    public override void Create()
    {
        m_ScriptablePass = new AbufferPass(settings);
        
        m_ScriptablePass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (OITRegistry.Objects[OITAlgorithm.Abuffer].Count == 0)
        {
            return;
        }
        if (settings.clearComputeShader != null && settings.buildMaterial != null && settings.resolveMaterial!= null)
        {
            m_ScriptablePass.ConfigureInput(ScriptableRenderPassInput.Depth);
            renderer.EnqueuePass(m_ScriptablePass);
        }
    }

    protected override void Dispose(bool disposing)
    {
        m_ScriptablePass?.Dispose();
    }

    class AbufferPass : ScriptableRenderPass
    {
        private Settings settings;
        private FilteringSettings filteringSettings;
        private ShaderTagId shaderTagId = new ShaderTagId("Abuffer");
        
        // Buffers
        ComputeBuffer startOffetBuffer; 
        ComputeBuffer fragLinkedBuffer;

        private int currentWidth = -1;
        private int currentHeight = -1;
        
        // IDs
        int startOffsetBufferID = Shader.PropertyToID("startOffetBuffer");
        int fragLinkedBufferID = Shader.PropertyToID("fragLinkedBuffer");
        //int screenWidthID = Shader.PropertyToID("screenWidth");
        int blitRT_ID = Shader.PropertyToID("_PPLL_BlitRT"); 

        

        public AbufferPass(Settings settings)
        {
            this.settings = settings;
            filteringSettings = new FilteringSettings(RenderQueueRange.transparent, settings.layerMask); 
           
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            
            if(startOffetBuffer == null ||fragLinkedBuffer == null || currentWidth != desc.width || currentHeight != desc.height)
            { 
                Dispose();
                currentWidth = desc.width;
                currentHeight = desc.height; 
                int maxSortedPixels = 64;
                int bufferSize = currentWidth * currentHeight * maxSortedPixels;
                int headBuferSize = currentWidth * currentHeight; 
                int bufferStride = sizeof(uint) * 3;
                int headBufferStride = sizeof(uint);
                
                fragLinkedBuffer = new ComputeBuffer(bufferSize, bufferStride, ComputeBufferType.Counter); 
                startOffetBuffer = new ComputeBuffer(headBuferSize, headBufferStride, ComputeBufferType.Raw);
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            RTHandle sourceColor = renderingData.cameraData.renderer.cameraColorTargetHandle;
            CommandBuffer cmd = CommandBufferPool.Get("Abuffer Pass");
            //-----Clear Pass-----
            cmd.SetGlobalBuffer("fragLinkedBuffer", fragLinkedBuffer);
            cmd.SetGlobalBuffer("startOffetBuffer", startOffetBuffer);
            cmd.SetBufferCounterValue(fragLinkedBuffer, 0);
            int kernel = settings.clearComputeShader.FindKernel("AClear");
            int threadGroupX = Mathf.CeilToInt(currentWidth / 8.0f);
            int threadGroupY = Mathf.CeilToInt(currentHeight / 8.0f);
            
            cmd.SetComputeIntParam(settings.clearComputeShader, "_ScreenWidth", currentWidth);
            cmd.DispatchCompute(settings.clearComputeShader, kernel,threadGroupX, threadGroupY, 1);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            
            //------Build Pass------
            var drawSettings = CreateDrawingSettings(shaderTagId, ref renderingData, SortingCriteria.CommonTransparent);
            var renderer = renderingData.cameraData.renderer;
            drawSettings.overrideMaterial = settings.buildMaterial;
            cmd.SetRenderTarget(renderer.cameraColorTargetHandle, renderer.cameraDepthTargetHandle);
            cmd.SetRandomWriteTarget(1, fragLinkedBuffer);
            cmd.SetRandomWriteTarget(2, startOffetBuffer);
            
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            
            context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings);
            
            cmd.ClearRandomWriteTargets();
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            // -----Resolve Pass---
            cmd.SetRenderTarget(sourceColor);
            settings.resolveMaterial.SetBuffer(fragLinkedBufferID, fragLinkedBuffer);
            settings.resolveMaterial.SetBuffer(startOffsetBufferID, startOffetBuffer);
            cmd.DrawProcedural(Matrix4x4.identity, settings.resolveMaterial, 0, MeshTopology.Triangles, 3);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            startOffetBuffer?.Release();
            fragLinkedBuffer?.Release();
        }
    }
}