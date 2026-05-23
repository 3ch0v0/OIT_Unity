Shader "OIT/DP_Peeling"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Alpha("Alpha", Range(0,1)) = 1.0
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _SpecularColor("Specular Color", Color) = (1,1,1,1)
        _Glossiness("Glossiness", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" "LightMode" = "DP_Peeling" }
        LOD 100

        Pass
        {
            Name "DP_Peeling"
            Tags { "LightMode" = "DP_Peeling" }
            ZTest LEqual
            ZWrite On
            Cull Off
            Blend Off
            
            HLSLPROGRAM
             #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "../Lighting.hlsl"
            #include "DPCommon.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS: NORMAL;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 positionSS : TEXCOORD3;
            };
             
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            TEXTURE2D(_PrevDepthTex);
            SAMPLER(sampler_PrevDepthTex);
            
            
            CBUFFER_START(UnityperMaterial)
            float _Alpha;
            float4  _BaseColor;
            float _Glossiness;
            float4 _SpecularColor;
            CBUFFER_END
            
            
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

            float4 frag(Varyings i): SV_Target
            {
                float4 texColor = tex2D(_MainTex, i.uv)*_BaseColor;
                float alpha = texColor.a * _Alpha;

                float3 normalWS = normalize(i.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(i.positionWS);

                float3 finalColor= CalculateLighting( texColor.rgb, viewDirWS,  normalWS,  _Glossiness,  _SpecularColor);
                float currDepth= i.positionCS.z;
                
                float2 ssUV = i.positionSS.xy/i.positionSS.w;
                float prevDepth = SAMPLE_TEXTURE2D(_PrevDepthTex, sampler_PrevDepthTex, ssUV).r;
                
                #if UNITY_REVERSED_Z
                    if (currDepth >= prevDepth-0.00001) 
                    {
                        discard;
                    }
                #endif
                
                #if (!UNITY_REVERSED_Z)
                 if (currDepth <= prevDepth+0.00001) 
                 {
                     discard;
                 }
                #endif
                
                return float4(finalColor*alpha,alpha);
                                                                  
                
            }
            ENDHLSL
        }
    }
    FallBack "Diffuse"
}
