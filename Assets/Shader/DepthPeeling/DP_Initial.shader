Shader "OIT/DP_Initial"
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
        Tags { "RenderType"="Opaque"  "RenderPipeline" = "UniversalPipeline" "LightMode" = "DP_Initial" }
        LOD 100
        
        Pass
        {
            Name "DP_Initial"
            Tags { "LightMode" = "DP_Peeling" }
            
            ZWrite on
            ZTest LEqual
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

            float4 frag (Varyings i) : SV_Target
            {
                
                float4 texColor = tex2D(_MainTex, i.uv)*_BaseColor;
                float alpha = texColor.a * _Alpha;

                float3 normalWS = normalize(i.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(i.positionWS);

                float3 finalColor= CalculateLighting( texColor.rgb, viewDirWS,  normalWS,  _Glossiness,  _SpecularColor);
    
                return float4(finalColor*alpha, alpha);
                                                                  
                
            }
            ENDHLSL
        }
    }
    FallBack "Diffuse"
}
