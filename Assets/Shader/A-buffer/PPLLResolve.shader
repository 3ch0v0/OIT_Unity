Shader "OIT/PPLL_Resolve"
{
    Properties {}
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "PPLLCommon.hlsl"

            #define MAX_SORTED_PIXELS 64
            StructuredBuffer<PPLLLinkedListNode> fragLinkedBuffer;
            ByteAddressBuffer startOffetBuffer;
            
            struct Attributes
            {
                uint vertexID : SV_VertexID; 
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float x = -1.0 + 2.0 * ((input.vertexID & 1) << 1);
                float y = -1.0 + 2.0 * ((input.vertexID & 2));
                output.positionCS = float4(x, y, 0.0, 1.0);
                return output;
            }
            
            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                //float4 col = SAMPLE_TEXTURE2D(_PPLL_BlitRT, sampler_PPLL_BlitRT, input.texcoord);
                
                uint2 pixelCoord = uint2(input.positionCS.xy);
                uint uStartOffsetAddress = 4 * (pixelCoord.y * (uint)_ScreenParams.x + pixelCoord.x);
                uint uOffset = startOffetBuffer.Load(uStartOffsetAddress);

                PPLLLinkedListNode SortedPixels[MAX_SORTED_PIXELS];
                int nNumPixels = 0;

                
                while (uOffset != 0 && nNumPixels < MAX_SORTED_PIXELS)
                {
                    PPLLLinkedListNode Element = fragLinkedBuffer[uOffset];
                    SortedPixels[nNumPixels] = Element;
                    nNumPixels++;
                    uOffset = Element.next;
                }

                if (nNumPixels == 0) discard;

                // Sort,Back-to-Front
                for (int i = 0; i < nNumPixels - 1; i++)
                {
                    for (int j = i + 1; j > 0; j--)
                    {
                        float depth1 = UnpackDepth(SortedPixels[j].uDepthSampleIdx);
                        float depth2 = UnpackDepth(SortedPixels[j - 1].uDepthSampleIdx);
                        
                        if (depth2 < depth1)
                        {
                            PPLLLinkedListNode temp = SortedPixels[j - 1];
                            SortedPixels[j - 1] = SortedPixels[j];
                            SortedPixels[j] = temp;
                        }
                    }
                }
                float4 accumColor = float4(0.0, 0.0, 0.0, 0.0);
                // Blend, Back-to-Front
                for (int k = 0; k < nNumPixels; k++)
                {
                    float4 vPixColor = UnpackRGBA(SortedPixels[k].pixelColor);
                    
                    // SrcAlpha * SrcColor + (1 - SrcAlpha) * DstColor
                    accumColor.rgb = vPixColor.rgb * vPixColor.a + accumColor.rgb * (1.0 - vPixColor.a);
                    accumColor.a   = vPixColor.a + accumColor.a * (1.0 - vPixColor.a);
                }
                return accumColor;
            }
            ENDHLSL
        }
    }
}