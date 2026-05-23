Shader "OIT/PPLL_Build"
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
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        
        ZTest LEqual
        ZWrite Off
        Cull Off
        ColorMask 0

        Pass
        {
            Tags { "LightMode" = "Abuffer" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #pragma require randomwrite

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "PPLLCommon.hlsl"
            #include "../Lighting.hlsl"

            RWStructuredBuffer<PPLLLinkedListNode> fragLinkedBuffer : register(u1);
            RWByteAddressBuffer startOffetBuffer : register(u2);
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainColor;
                float _Alpha;
                float4 _BaseColor;
                float _Glossiness;
                float4 _SpecularColor;
            CBUFFER_END
            
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
            
            [earlydepthstencil]

            Varyings vert(Attributes i)
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

            void frag(Varyings input,uint uSampleIdx : SV_SampleIndex) 
            {
                float4 texColor = tex2D(_MainTex, input.uv)*_BaseColor;
                float alpha = texColor.a * _Alpha;
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                float3 finalColor= CalculateLighting( texColor.rgb, viewDirWS,  normalWS,  _Glossiness,  _SpecularColor);
                float4 fragColor = float4(finalColor, alpha);
                
                if(fragColor.a <= 0.001) discard;
                
                uint uPixelCount = fragLinkedBuffer.IncrementCounter();
                
                uint2 pixelCoord = uint2(input.positionCS.xy);
                uint uStartOffsetAddress = 4 * (pixelCoord.y * (uint)_ScreenParams.x + pixelCoord.x);
                
                uint uOldStartOffset;
                startOffetBuffer.InterlockedExchange(uStartOffsetAddress, uPixelCount, uOldStartOffset);
                
                PPLLLinkedListNode Element;
                Element.pixelColor = PackRGBA(fragColor);
                Element.uDepthSampleIdx = PackDepthSampleIdx(ABufferLinear01Depth(input.positionCS.z, _ZBufferParams), uSampleIdx);
                Element.next = uOldStartOffset;
                
                fragLinkedBuffer[uPixelCount] = Element;
            }
            ENDHLSL
        }
    }
}