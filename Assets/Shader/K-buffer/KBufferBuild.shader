Shader "OIT/KBuffer_Build"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Alpha("Alpha", Range(0,1)) = 0.5
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _SpecularColor("Specular Color", Color) = (1,1,1,1)
        _Glossiness("Glossiness", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent"}
        LOD 100

        Pass
        {
            Tags { "LightMode" = "Kbuffer" }
            ZWrite Off
            ZTest LEqual
            ColorMask 0
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma require rview
            #pragma target 5.0
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "../Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS: NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 positionSS : TEXCOORD3;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainColor;
                float _Alpha;
                float4 _BaseColor;
                float _Glossiness;
                float4 _SpecularColor;
            CBUFFER_END
            int _ScreenWidth;
            int _KSize;
            #define MAX_K 8
            
            RasterizerOrderedStructuredBuffer<float4> _KBufferColor : register(u1);
            RasterizerOrderedStructuredBuffer<float> _KBufferDepth : register(u2);
            
            Varyings vert (Attributes i)
            {
                Varyings o;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(i.positionOS.xyz);
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                o.positionCS = vertexInput.positionCS;
                o.positionWS = vertexInput.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.positionSS = ComputeScreenPos(o.positionCS);
                return o;
            }

            float4 frag (Varyings input) : SV_Target
            {
                float4 texColor = tex2D(_MainTex, input.uv)*_BaseColor;
                float alpha = texColor.a * _Alpha;

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                float3 finalColor= CalculateLighting( texColor.rgb, viewDirWS,  normalWS,  _Glossiness,  _SpecularColor);
                float4 fragColor = float4(finalColor, alpha);

                float fragDepth= input.positionCS.z/ input.positionCS.w;
                uint2 coords = uint2(floor(input.positionCS.xy));
                uint pixelID = coords.y * _ScreenWidth + coords.x;
                int k=min(_KSize,MAX_K);
                uint baseIdx = pixelID *k;
                
                int minIdx = 0;
                float minD = _KBufferDepth[baseIdx];
                
                for (int i = 1; i < k; i++) 
                {
                    if (_KBufferDepth[baseIdx + i] <minD) 
                    {
                        minD = _KBufferDepth[baseIdx + i];
                        minIdx = i;
                    }
                }
                
                if (fragDepth > minD) 
                {
                    _KBufferDepth[baseIdx + minIdx] = fragDepth;
                    _KBufferColor[baseIdx + minIdx] = fragColor;
                }

             
                return float4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
}
