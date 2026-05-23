Shader "OIT/WBOIT_Composite"
{
    Properties
    {
        //_AccumTex ("Accumulation", 2D) = "black" {}
        //_RevealTex ("Revealage", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings {
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
                //float4 screenPos : TEXCOORD1;
            };

            TEXTURE2D(_AccumTex); SAMPLER(sampler_AccumTex);
            TEXTURE2D(_RevealTex); SAMPLER(sampler_RevealTex);

            Varyings vert (Attributes i) {
                Varyings o;
                //o.positionCS = TransformObjectToHClip(i.positionOS);
                o.positionCS = float4(i.positionOS.x, i.positionOS.y, 0.0, 1.0);
                
                o.uv = i.uv;
                if (_ProjectionParams.x < 0.0)
                {
                    o.uv.y = 1.0 - o.uv.y;
                }
                //o.uv = i.uv;
                return o;
            }

            float4 frag (Varyings i) : SV_Target {
                
                float4 accum = SAMPLE_TEXTURE2D(_AccumTex,sampler_AccumTex, i.uv);
                float reveal = SAMPLE_TEXTURE2D(_RevealTex,sampler_RevealTex, i.uv).r;

                if (accum.a < 0.00001) 
                    discard;
                
                if (isinf(max(abs(accum.x), max(abs(accum.y), abs(accum.z))))) {
                    accum.rgb = float3(accum.a, accum.a, accum.a);
                }
                if (reveal >=1.0) {discard;}
                
                if (isinf(accum.a)) {
                    accum.a = 1000.0; 
                }
                
                float3 averageColor = accum.rgb / max(accum.a, 0.00001);
                averageColor = min(averageColor, 1.0);
                return float4(averageColor, 1.0 - reveal);
            }
            ENDHLSL
        }
    }
}