Shader "OIT/WBOIT"
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
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "WBOIT"
            Tags { "LightMode" = "WBOIT" }
            ZWrite Off
            Cull Off
            Blend 0 One One
            Blend 1 Zero OneMinusSrcColor
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

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
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 positionSS : TEXCOORD3;
            };
            
            struct FragOutput
            {
                float4 accumColor : SV_Target0;    // Accumulated Color
                float4 accumAlpha : SV_Target1; // Revealage
            };
            CBUFFER_START(UnityperMaterial)
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Alpha;
            float4  _BaseColor;
            float _Glossiness;
            float4 _SpecularColor;
            CBUFFER_END
            float CalculateWeight(float3 col,float fragDepth, float alpha)
            {
                return max(min(1.0, max(max(col.r, col.g), col.b) * alpha), alpha) * clamp(0.03 / (1e-5 + pow(fragDepth / 200, 4.0)), 1e-2, 3e3);
            }
            
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

            FragOutput frag (Varyings i) : SV_Target
            {
                FragOutput output;
                
                float4 texColor = tex2D(_MainTex, i.uv)*_BaseColor;
                float alpha = texColor.a * _Alpha;

                float3 normalWS = normalize(i.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(i.positionWS);

                float3 finalColor= CalculateLighting( texColor.rgb, viewDirWS,  normalWS,  _Glossiness,  _SpecularColor);
                
                float z = i.positionCS.w/ i.positionCS.z;
                
                float weight = CalculateWeight(finalColor, z, alpha);
                weight = min(weight, 10.0);
                output.accumColor= float4(finalColor.rgb * alpha, alpha) * weight;
                output.accumAlpha = alpha;
                
                return output;
            }
            ENDHLSL
        }
    }
}
