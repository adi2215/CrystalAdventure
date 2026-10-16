Shader "Custom/RadialBlur"
{
    Properties
    {
        _Intensity("Intensity", Range(0,1)) = 0.2
        _Center("Center", Vector) = (0.5, 0.5, 0, 0)
        _MainTex("MainTex", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalRenderPipeline" }
        Pass
        {
            Name "RadialBlur"
            ZWrite Off
            Cull Off
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            // Подключаем URP библиотеку (исправляет ошибку TEXTURE2D)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float _Intensity;
            float2 _Center;

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS);
                o.uv = v.uv;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float2 dir = i.uv - _Center;
                float4 col = 0;

                const int samples = 8;

                for (int s = 0; s < samples; s++)
                {
                    float scale = 1.0 - (s / (float)samples) * _Intensity;
                    float2 uv = _Center + dir * scale;
                    col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                }

                return col / samples;
            }

            ENDHLSL
        }
    }
}
