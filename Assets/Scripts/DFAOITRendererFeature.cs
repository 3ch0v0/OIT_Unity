using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DFAOITRendererFeature : ScriptableRendererFeature
{
    public LayerMask transparentLayerMask = -1;
    public Shader compositeShader;

    public Material dfaInitialMat;
    public Material dfaPeelingMat;
    public Material dfaBlendMat;
    
    Material m_CompositeMaterial;
    DFAOITPass m_Pass;

    class DFAOITPass : ScriptableRenderPass
    {
        ShaderTagId Peel0Tag = new ShaderTagId("DFAOITPeel0");
        ShaderTagId Peel1Tag = new ShaderTagId("DFAOITPeel1");
        ShaderTagId TailAccumTag = new ShaderTagId("DFAOITTailAccum");
        
        Material initialMat;
        Material peelingMat;
        Material blendMat;

        int FrontDepthTexID = Shader.PropertyToID("_DFAFrontDepthTex");
        int SecondDepthTexID = Shader.PropertyToID("_DFASecondDepthTex");
        int FrontLayer0TexID = Shader.PropertyToID("_DFAFrontLayer0Tex");
        int FrontLayer1TexID = Shader.PropertyToID("_DFAFrontLayer1Tex");
        int TailSumTexID = Shader.PropertyToID("_DFATailSumTex");
        int TailAuxTexID = Shader.PropertyToID("_DFATailAuxTex");

        LayerMask layerMask;
        Material DFAOITPeelMaterial;
        Material DFAOITCompositeMaterial;

        RTHandle sourceColorRT;
        RTHandle sourceDepthRT;
        
        RTHandle fragCountRT;
        RTHandle frontLayer0RT;
        RTHandle frontLayer1RT;
        RTHandle tailSumRT;
        RTHandle tailAuccmRT;
        
        RTHandle depth0RT;
        RTHandle depth1RT;
        RTHandle tailDepthAttachment; 

        readonly RenderTargetIdentifier[] mrt = new RenderTargetIdentifier[3];

        public DFAOITPass(LayerMask transLayerMask,Material initialMaterial, Material peelingMaterial, Material blendMaterial, Material compositeMaterial)
        {
            layerMask = transLayerMask;
            DFAOITCompositeMaterial = compositeMaterial;
            initialMat = initialMaterial;
            peelingMat = peelingMaterial;
            blendMat = blendMaterial;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            //var renderer = renderingData.cameraData.renderer;
            //sourceColorRT = renderer.cameraColorTargetHandle;

            var cameraDesc = renderingData.cameraData.cameraTargetDescriptor;
            
            var countDesc = cameraDesc;
            countDesc.graphicsFormat = GraphicsFormat.R16_SFloat;
            countDesc.depthBufferBits = 0;
            countDesc.msaaSamples = 1;
            countDesc.bindMS = false;
            
            
            var colorDesc = cameraDesc;
            colorDesc.depthBufferBits = 0;
            colorDesc.msaaSamples = 1;
            colorDesc.bindMS = false;
            colorDesc.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;

            var depthDesc = cameraDesc;
            depthDesc.graphicsFormat = GraphicsFormat.None;
            depthDesc.depthBufferBits = 24; 
            depthDesc.msaaSamples = 1;
            depthDesc.bindMS = false;

            RenderingUtils.ReAllocateIfNeeded(ref fragCountRT, countDesc, name: "_DFA_FragCount");
            // color RT
            RenderingUtils.ReAllocateIfNeeded(ref frontLayer0RT, colorDesc,  name: "_DFA_FrontLayer0");
            RenderingUtils.ReAllocateIfNeeded(ref frontLayer1RT, colorDesc,  name: "_DFA_FrontLayer1");
            RenderingUtils.ReAllocateIfNeeded(ref tailSumRT, colorDesc, name: "_DFA_TailSum");
            RenderingUtils.ReAllocateIfNeeded(ref tailAuccmRT, colorDesc, name: "_DFA_TailAux");
            
            // depth RT
            RenderingUtils.ReAllocateIfNeeded(ref depth0RT, depthDesc,  name: "_DFA_Depth0");
            RenderingUtils.ReAllocateIfNeeded(ref depth1RT, depthDesc,  name: "_DFA_Depth1");
            RenderingUtils.ReAllocateIfNeeded(ref tailDepthAttachment, depthDesc,name: "_DFA_TailDepth");
            
        }
        

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("DFAOIT");
            
            var filteringSettings = new FilteringSettings(RenderQueueRange.all, layerMask);
            var shaderTagId = new ShaderTagId("DFA_Peeling");
            var drawingSettings = CreateDrawingSettings(shaderTagId, ref renderingData, SortingCriteria.CommonTransparent);
            
            var renderer = renderingData.cameraData.renderer;
            sourceColorRT = renderer.cameraColorTargetHandle;
            sourceDepthRT = renderer.cameraDepthTargetHandle;
            
            
            cmd.BeginSample("DFA Process");
                //------- Initial Pass: clean
                CoreUtils.SetRenderTarget(cmd, frontLayer0RT);
                CoreUtils.ClearRenderTarget(cmd, ClearFlag.Color, new Color(0.0f,0.0f,0.0f,1.0f));

                CoreUtils.SetRenderTarget(cmd, frontLayer1RT, depth0RT);
                CoreUtils.ClearRenderTarget(cmd, ClearFlag.All, Color.clear);

                CoreUtils.SetRenderTarget(cmd, frontLayer1RT, depth1RT);
                CoreUtils.ClearRenderTarget(cmd, ClearFlag.All, Color.clear);
                
                CoreUtils.SetRenderTarget(cmd, fragCountRT);
                CoreUtils.ClearRenderTarget(cmd, ClearFlag.Color, Color.clear);
                
                CoreUtils.SetRenderTarget(cmd, tailAuccmRT);
                CoreUtils.ClearRenderTarget(cmd, ClearFlag.Color, new Color(0.0f,0.0f,0.0f,1.0f));
                
                CoreUtils.SetRenderTarget(cmd, fragCountRT);
                CoreUtils.ClearRenderTarget(cmd, ClearFlag.Color, Color.clear);
                
                CoreUtils.SetRenderTarget(cmd, tailSumRT, tailDepthAttachment);
                CoreUtils.ClearRenderTarget(cmd, ClearFlag.All, Color.clear);
                    
                
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                
                //----------pass 1 & 2: peeling front layers---------------------
                cmd.BeginSample("DFA_Peel_Layer0");
                CoreUtils.SetRenderTarget(cmd, frontLayer0RT, depth0RT);
                CoreUtils.ClearRenderTarget(cmd, ClearFlag.All, Color.clear);
                drawingSettings.overrideMaterial = initialMat;
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);
                cmd.EndSample("DFA_Peel_Layer0");
                
                cmd.BeginSample("DFA_Peel_Layer1");
                CoreUtils.SetRenderTarget(cmd, frontLayer1RT, depth1RT);
                CoreUtils.ClearRenderTarget(cmd, ClearFlag.All, Color.clear);
                cmd.SetGlobalTexture("_DFA_PrevDepthTex", depth0RT);
                drawingSettings.overrideMaterial = peelingMat;
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);
                cmd.EndSample("DFA_Peel_Layer1");
                
                //-----------pass3: accumulate-------------
                cmd.BeginSample("DFAOIT_Tail");
                mrt[0] = tailSumRT.nameID;
                mrt[1] = tailAuccmRT.nameID;   
                mrt[2]= fragCountRT.nameID;
                cmd.SetRenderTarget(mrt, tailDepthAttachment.nameID);
                
                cmd.SetGlobalTexture(SecondDepthTexID, depth1RT);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                
                drawingSettings.overrideMaterial = blendMat;
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);
                cmd.EndSample("DFAOIT_Tail");
                
                
                //---------Pass 4: network inference and composite
                cmd.BeginSample("DFAOIT_Composite");
                cmd.SetRenderTarget(sourceColorRT.nameID);
                cmd.SetGlobalTexture(FrontLayer0TexID, frontLayer0RT);
                cmd.SetGlobalTexture(FrontLayer1TexID, frontLayer1RT);
                cmd.SetGlobalTexture(TailSumTexID, tailSumRT);
                cmd.SetGlobalTexture(TailAuxTexID, tailAuccmRT);
                cmd.SetGlobalTexture("_DFAFragCountTex", fragCountRT);
                cmd.DrawProcedural(Matrix4x4.identity, DFAOITCompositeMaterial, 0, MeshTopology.Triangles, 3, 1);
                cmd.EndSample("DFAOIT_Composite");
            cmd.EndSample("DFA Process");
            

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            frontLayer0RT?.Release();
            frontLayer1RT?.Release();
            tailSumRT?.Release();
            tailAuccmRT?.Release();
            depth0RT?.Release();
            depth1RT?.Release();
            tailDepthAttachment?.Release();
        }
    }
    
    public override void Create()
    {
        CoreUtils.Destroy(m_CompositeMaterial);
        m_CompositeMaterial = compositeShader != null ? CoreUtils.CreateEngineMaterial(compositeShader) : null;
        m_Pass = new DFAOITPass(transparentLayerMask, dfaInitialMat,dfaPeelingMat, dfaBlendMat,m_CompositeMaterial);
        m_Pass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        
        if (OITRegistry.Objects[OITAlgorithm.DFAOIT].Count == 0)
        {
            return;
        }
            
        renderer.EnqueuePass(m_Pass);
    }
    protected override void Dispose(bool disposing)
    {
        m_Pass?.Dispose();
        CoreUtils.Destroy(m_CompositeMaterial);
    }
}