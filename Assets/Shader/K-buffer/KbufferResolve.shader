Shader "OIT/KBuffer_Composite"
{
    SubShader
    {
       
        Tags { "RenderPipeline" = "UniversalPipeline"}
        ZWrite Off
        ZTest Always
        
        Blend One OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                uint vertexID : SV_VertexID; 
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            
            StructuredBuffer<float4> _KBufferColor;
            StructuredBuffer<float> _KBufferDepth;
            
            int _ScreenWidth;
            int _KSize;
            #define MAX_K 8

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
                uint2 coords = uint2(floor(input.positionCS.xy));
                uint pixelIdx = coords.y * _ScreenWidth + coords.x;
                 int k=min(_KSize,MAX_K);
                uint baseIdx = pixelIdx * k;

                float4 fragments[MAX_K]; 
                float localDepths[MAX_K];
                
                int count = 0;
                for (int i = 0; i < k; i++)
                {
                    float d = _KBufferDepth[baseIdx + i];
                    if (_KBufferColor[baseIdx + i].a > 0.001)
                    {
                        fragments[count] = _KBufferColor[baseIdx + i];
                        localDepths[count] = d;
                        count++;
                    }
                }
                
                if (count == 0) discard;

                for (int i = 1; i < count; i++) 
                {
                    float4 tempColor = fragments[i];
                    float tempDepth = localDepths[i];
                    int j = i - 1;
                    
                    while (j >= 0 && localDepths[j] > tempDepth) 
                    {
                        fragments[j + 1] = fragments[j];
                        localDepths[j + 1] = localDepths[j];
                        j--;
                    }
                    
                    fragments[j + 1] = tempColor;
                    localDepths[j + 1] = tempDepth;
                }

                float4 accumColor = float4(0.0, 0.0, 0.0, 0.0);
                
                for (int i = 0; i < count; i++) 
                {

                    float4 fragColor = fragments[i];
                    
                    accumColor.rgb = fragColor.rgb * fragColor.a + (1.0 - fragColor.a) * accumColor.rgb;
                    accumColor.a = fragColor.a + (1.0 - fragColor.a) * accumColor.a;
                }
                
                return accumColor;
            }
            ENDHLSL
        }
    }
}