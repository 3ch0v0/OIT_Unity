using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DPRendererFeature : ScriptableRendererFeature
{
    public LayerMask depthPeelingLayerMask=-1;
    public int peelLayerCount=4;
    public Material depthPeelingInitialMat;
    public Material depthPeelingPeelingMat;
    public Material depthPeelingBlendMat;
    public Material depthPeelingCompositeMat;
    public Shader depthPeelingInitialShader;
        
    class DPRenderPass : ScriptableRenderPass
    {
        
        LayerMask layerMask;
        Material initialMat;
        Material peelingMat;
        Material blendMat;
        Material compositeMat;
        int layers;
        
        RTHandle sourceColorRT;
        RTHandle sourceDepthRT;
        
        RTHandle accumulateColorRT;
        RTHandle currentColorRT;
        RTHandle depth0RT;
        RTHandle depth1RT;

        Shader initialShader;
        Shader peelingShader;
        Shader blendShader;
        
        public void SetUp(RTHandle color, RTHandle depth)
        {
            sourceColorRT = color;
            sourceDepthRT = depth;
        }

        public DPRenderPass(LayerMask layermask,Material initialMaterial, Material peelingMaterial, Material blendMaterial, Material compositeMaterial,  int peelLayerCount, Shader DPPeelingShader, Shader blendShader=null)
        {
            layerMask = layermask;
            initialMat = initialMaterial;
            peelingMat = peelingMaterial;
            blendMat = blendMaterial;
            compositeMat = compositeMaterial;
            peelingShader=DPPeelingShader;
            layers = peelLayerCount;
            
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            // mrt[0] = colorRT[0];
            // mrt[1] = depthRT[0];
            //
            //
            // ConfigureTarget(mrt,peelDepthAttachment);
            // ConfigureClear(ClearFlag.All,new Color(0.0f,0.0f,0.0f,0.0f));
        }
        
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var colDesc= renderingData.cameraData.cameraTargetDescriptor;
            colDesc.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
            colDesc.depthBufferBits = 0;
            colDesc.msaaSamples = 1;
            colDesc.bindMS = false;
            
            var depthDesc = renderingData.cameraData.cameraTargetDescriptor;
            depthDesc.graphicsFormat = GraphicsFormat.None;
            depthDesc.depthBufferBits = 24;
            depthDesc.msaaSamples = 1;
            depthDesc.bindMS = false;
            
            RenderingUtils.ReAllocateIfNeeded(ref accumulateColorRT, colDesc, name: "DP_AccumulateColor");
            RenderingUtils.ReAllocateIfNeeded(ref currentColorRT, colDesc, name: "DP_CurrentColor");
            RenderingUtils.ReAllocateIfNeeded(ref depth0RT, depthDesc, name: "DP_Depth0");
            RenderingUtils.ReAllocateIfNeeded(ref depth1RT, depthDesc, name: "DP_Depth1");
        }

       
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            
            int peelCount = OITRegistry.Layers;
            
            CommandBuffer cmd = CommandBufferPool.Get("DepthPeelingPass");
            
            var filteringSettings = new FilteringSettings(RenderQueueRange.all, layerMask);
            var shaderTagId = new ShaderTagId("DP_Peeling");
            var drawingSettings = CreateDrawingSettings(shaderTagId, ref renderingData, SortingCriteria.CommonTransparent);
            
            var renderer = renderingData.cameraData.renderer;
            sourceColorRT = renderer.cameraColorTargetHandle;
            sourceDepthRT = renderer.cameraDepthTargetHandle;

            //using (new ProfilingScope(cmd, new ProfilingSampler("Depth Peeling")))
            //{
            cmd.BeginSample("Depth Peeling Process");
                //------- Initial Pass: clean
                CoreUtils.SetRenderTarget(cmd, accumulateColorRT);
                CoreUtils.ClearRenderTarget(cmd, ClearFlag.Color, new Color(0.0f,0.0f,0.0f,1.0f));

                CoreUtils.SetRenderTarget(cmd, currentColorRT, depth0RT);
                CoreUtils.ClearRenderTarget(cmd, ClearFlag.All, Color.clear);

                CoreUtils.SetRenderTarget(cmd, currentColorRT, depth1RT);
                CoreUtils.ClearRenderTarget(cmd, ClearFlag.All, Color.clear);
                
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                
                //-------- Peeling & blend Passes-------------
                for (int i = 0; i < peelCount; i++)
                {
                    cmd.BeginSample("DP_PeelingPass"+i);
                    RTHandle currentDepthRT = (i % 2 == 0) ? depth0RT : depth1RT;
                    RTHandle prevDepthRT = (i%2 ==0 ) ? depth1RT : depth0RT;
                    //peel
                    CoreUtils.SetRenderTarget(cmd, currentColorRT, currentDepthRT);
                    CoreUtils.ClearRenderTarget(cmd,ClearFlag.All, Color.clear);
                    cmd.SetGlobalTexture("_PrevDepthTex", prevDepthRT);
                    drawingSettings.overrideMaterial=(i==0)? initialMat : peelingMat;
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                    context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);
                    
                    //blend:front to back
                    CoreUtils.SetRenderTarget(cmd, accumulateColorRT);
                    cmd.SetGlobalTexture("_LayerColorTex", currentColorRT);
                    cmd.DrawProcedural(Matrix4x4.identity, blendMat, 0, MeshTopology.Triangles, 3, 1);
                    
                    cmd.EndSample("DP_PeelingPass"+i);
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                }

                //-------Composite pass-------------
                cmd.BeginSample("DepthPeeling_CompositePass");
                CoreUtils.SetRenderTarget(cmd,sourceColorRT, sourceDepthRT);
                cmd.SetGlobalTexture("_LayerColorTex", accumulateColorRT);
                cmd.DrawProcedural(Matrix4x4.identity, compositeMat,0,MeshTopology.Triangles, 3, 1);
                cmd.EndSample("DepthPeeling_CompositePass");
                cmd.EndSample("Depth Peeling Process");
            
            context.ExecuteCommandBuffer(cmd); 
            CommandBufferPool.Release(cmd);
            
        }

      
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
        }
        
        public void Dispose()
        {
            accumulateColorRT?.Release();
            currentColorRT?.Release();
            depth0RT?.Release();
            depth1RT?.Release();
            
          
        }
    }

    DPRenderPass m_ScriptablePass;
    
    public override void Create()
    {
        m_ScriptablePass = new DPRenderPass(depthPeelingLayerMask, depthPeelingInitialMat,depthPeelingPeelingMat, depthPeelingBlendMat, depthPeelingCompositeMat,peelLayerCount, depthPeelingInitialShader,
            depthPeelingBlendMat.shader);
        m_ScriptablePass.ConfigureInput(ScriptableRenderPassInput.Color); 
        m_ScriptablePass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
    }
    
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        m_ScriptablePass.ConfigureInput(ScriptableRenderPassInput.Depth);
        if (OITRegistry.Objects[OITAlgorithm.DepthPeeling].Count == 0)
        {
            return;
        }
        
        renderer.EnqueuePass(m_ScriptablePass);
    }
    
    protected override void Dispose(bool disposing)
    {
        m_ScriptablePass.Dispose();
    }
    
}


