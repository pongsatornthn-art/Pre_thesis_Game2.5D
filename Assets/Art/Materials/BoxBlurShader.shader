Shader "Custom/BoxBlurShader"
{
    Properties
    {
        [IntRange] _Blur ("Blur Size", Range(1, 6)) = 2
        _Scale ("Scale", Range(1, 5)) = 2
        _BlurAlpha ("Blur Alpha", Range(0, 1)) = 1
        
        // เพิ่มช่องให้เลือกปรับสีได้ (ค่าเริ่มต้นคือสีขาว = ไม่ย้อมสี)
        _Color ("Tint Color", Color) = (1, 1, 1, 1) 
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos  : TEXCOORD0;
            };

            TEXTURE2D(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            CBUFFER_START(UnityPerMaterial)
                float _Blur;
                float _Scale;
                float _BlurAlpha;
                half4 _Color; // ประกาศตัวแปรสีให้ระบบรู้จัก
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos  = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // เฟดจนใสแล้วไม่ต้องคำนวณเบลอ (ประหยัดเฟรมเรต)
                clip(_BlurAlpha - 0.003);

                float2 screenUV  = input.screenPos.xy / input.screenPos.w;
                float2 texelSize = _Scale / _ScreenParams.xy;

                int blurSize = max(1, (int)_Blur);
                half4 sum = 0;
                int count = 0;

                for (int i = -blurSize; i <= blurSize; i++)
                {
                    for (int j = -blurSize; j <= blurSize; j++)
                    {
                        float2 offset = float2(i, j) * texelSize;
                        sum += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV + offset);
                        count++;
                    }
                }

                half4 finalBlur = sum / count;
                
                // เอาภาพที่เบลอแล้ว มาคูณผสมกับสีที่เราตั้งค่าไว้ใน Material
                return half4(finalBlur.rgb * _Color.rgb, _BlurAlpha * _Color.a);
            }
            ENDHLSL
        }
    }
}