Shader "OIT/DFAOIT_Composite"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            ZWrite Off
            ZTest off
            Cull Off
            //Blend one OneMinusSrcAlpha
            //Blend DstAlpha One, zero OneMinusSrcAlpha
            //Blend One OneMinusSrcAlpha
            Blend One OneMinusSrcAlpha
            ColorMask RGBA

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "DFAOITCommon.hlsl"

            struct AttributesComposite
            {
                uint vertexID : SV_VertexID;
            };

            struct VaryingsComposite
            {
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
            };

            TEXTURE2D(_DFAFrontLayer0Tex);
            SAMPLER(sampler_DFAFrontLayer0Tex);
            TEXTURE2D(_DFAFrontLayer1Tex);
            SAMPLER(sampler_DFAFrontLayer1Tex);
            TEXTURE2D(_DFATailSumTex);
            SAMPLER(sampler_DFATailSumTex);
            TEXTURE2D(_DFATailAuxTex);
            SAMPLER(sampler_DFATailAuxTex);
            TEXTURE2D(_DFAFragCountTex);
            SAMPLER(sampler_DFAFragCountTex);

            VaryingsComposite vert(AttributesComposite i)
            {
                VaryingsComposite o;
                o.uv = GetFullScreenTriangleTexCoord(i.vertexID);
                o.positionCS = GetFullScreenTriangleVertexPosition(i.vertexID);
                return o;
            }

            float4 frag(VaryingsComposite i) : SV_Target
            {
                float4 layer0 = SAMPLE_TEXTURE2D(_DFAFrontLayer0Tex, sampler_DFAFrontLayer0Tex, i.uv);
                float4 layer1 = SAMPLE_TEXTURE2D(_DFAFrontLayer1Tex, sampler_DFAFrontLayer1Tex, i.uv);
                float4 tailSum = SAMPLE_TEXTURE2D(_DFATailSumTex, sampler_DFATailSumTex, i.uv);
                float4 tailAuccm = SAMPLE_TEXTURE2D(_DFATailAuxTex, sampler_DFATailAuxTex, i.uv);
                float fragCount = SAMPLE_TEXTURE2D(_DFAFragCountTex, sampler_DFAFragCountTex, i.uv).r;

                //tailSum = tailSum -layer1-layer0;
                
                layer0=float4(layer0.rgb*layer0.a, layer0.a);
                layer1=float4(layer1.rgb*layer1.a, layer1.a);

                //tailAuccm.rgb=tailAuccm.rgb - layer1.rgb-layer0.rgb;
                
                float a0 = saturate(layer0.a);
                float a1 = saturate(layer1.a);

                float remFront = (1.0 - a0) * (1.0 - a1);
                float frontAlpha=a0 + a1 * (1.0 - a0);
                float3 frontPremul = layer0.rgb + layer1.rgb * (1.0 - a0);
                float3 Coit=frontPremul;
                
                //float tailAlphaAccum = max(tailSum.a, 1e-4);
                float4 avgTailColor = tailSum.rgba/ max(fragCount*1000, 1e-4);
                float3 Cavg=avgTailColor;
                
                float Aavg=avgTailColor.a;
                
                float3 Caccum=tailAuccm;
                
                //float tailLogT = max(tailAuccm.a, 0.0);
                //float tailTrans = exp(-tailLogT);
                //float tailAlpha = 1.0 - tailTrans;
                //float3 tailPremul = avgTailColor * tailAlpha;

                //float3 transparentPremul = frontPremul + remFront * tailPremul;
                float totalAlpha = saturate(1.0 - remFront * tailAuccm.a);
                

                float3 predictedTailCol= DFAMLP(Aavg, Cavg, Caccum, Coit);
                float3 finalColor =  Coit + predictedTailCol*remFront;
                return float4(finalColor,totalAlpha);
                
            }
            ENDHLSL
        }
    }
}
