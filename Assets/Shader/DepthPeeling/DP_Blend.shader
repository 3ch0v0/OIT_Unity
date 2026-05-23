Shader "OIT/DP_Blend"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        

        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            //color:Src * DstAlpha + Dst * 1
            // Alpha：Src * 0 + Dst * (1 - SrcAlpha)
            Blend DstAlpha One, zero OneMinusSrcAlpha
            
            ColorMask RGBA
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "../Lighting.hlsl"
            #include "DPCommon.hlsl"

            struct Attributes
            {
                uint vertexID : SV_VertexID;
               
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
                float4 positionSS : TEXCOORD1;
            };
            
            TEXTURE2D(_LayerColorTex);
            SAMPLER(sampler_LayerColorTex);
            
            Varyings vert (Attributes i)
            {
                Varyings o;
                o.uv = GetFullScreenTriangleTexCoord(i.vertexID);
                o.positionCS=GetFullScreenTriangleVertexPosition(i.vertexID);
                o.positionSS = ComputeScreenPos(o.positionCS);
                
                return o;
            }


            
            float4 frag (Varyings i): SV_Target
            {
               
                float4 layerCol=SAMPLE_TEXTURE2D(_LayerColorTex,sampler_LayerColorTex, i.uv);
                //layerCol.rgb*=layerCol.a;
                                
                return layerCol;
                                                                  
                
            }
            ENDHLSL
        }
    }
    FallBack "Diffuse"
}
