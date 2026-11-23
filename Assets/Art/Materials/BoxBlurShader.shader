Shader "Custom/BoxBlurShader"
{
    // [แก้ 2026-11-22] ขอบกล่องเบลอนุ่ม ไม่เห็นเป็นเหลี่ยม
    //   เดิม: กล่อง (Cube) เบลอข้างในทึบเท่ากันหมด → ขอบกล่องตัดคมเป็นเส้นตรงชัดๆ
    //   ใหม่: ความเข้มขึ้นกับ "ความหนาของกล่องที่สายตามองทะลุ" → ตรงขอบที่สายตาเฉียดผ่านนิดเดียว ค่อยๆ จางหาย
    //         + (เลือกได้) จางตรงที่กล่องจมพื้น/ชนวัตถุ (Soft Depth — ต้องเปิด Depth Texture ใน URP Asset)
    Properties
    {
        [IntRange] _Blur ("Blur Size", Range(1, 6)) = 2
        _Scale ("Scale", Range(1, 5)) = 2
        _BlurAlpha ("Blur Alpha", Range(0, 1)) = 1

        // เพิ่มช่องให้เลือกปรับสีได้ (ค่าเริ่มต้นคือสีขาว = ไม่ย้อมสี)
        _Color ("Tint Color", Color) = (1, 1, 1, 1)

        [Header(Soft Edges)]
        _EdgeFade ("Edge Fade (m) — ความหนาที่ถือว่าเข้มเต็ม ยิ่งมากขอบยิ่งนุ่ม", Range(0.01, 5)) = 0.8
        [Toggle(_SOFTDEPTH)] _UseSoftDepth ("Soft Depth — จางตรงที่ชนพื้น/วัตถุ (ต้องเปิด Depth Texture)", Float) = 0
        _SoftDistance ("Soft Depth Distance (m)", Range(0.01, 3)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back   // วาดเฉพาะด้านหน้าของกล่อง แล้วคำนวณความหนาเอง (เดิม Cull Off → ด้านหน้า+หลังซ้อนกัน 2 ชั้น)

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _SOFTDEPTH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #if defined(_SOFTDEPTH)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #endif

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos  : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
            };

            TEXTURE2D(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            CBUFFER_START(UnityPerMaterial)
                float _Blur;
                float _Scale;
                float _BlurAlpha;
                half4 _Color; // ประกาศตัวแปรสีให้ระบบรู้จัก
                float _EdgeFade;
                float _SoftDistance;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos  = ComputeScreenPos(output.positionCS);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            // ระยะที่สายตาเดินทางอยู่ "ข้างใน" กล่อง (หน่วยเมตรในโลก) นับจากจุดบนผิวด้านหน้า
            float ThicknessThroughBox(float3 posOS)
            {
                float3 camOS = TransformWorldToObject(_WorldSpaceCameraPos);
                float3 dir = posOS - camOS;
                float len = length(dir);
                if (len < 1e-5) return 0;
                dir /= len;

                // กล่อง Cube ของ Unity = -0.5 ถึง 0.5 ทุกแกน → หาจุดที่สายตาทะลุออกอีกฝั่ง
                // [แก้ 2026-11-23] เดิมใช้ sign(dir) → แกนไหนสายตาตั้งฉากพอดี (กลางจอ) sign = 0
                //   ทำให้คิดว่าทะลุออกทันที ความหนา = 0 → เห็นเป็นเส้นใสกลางจอ
                //   ตอนนี้: ทิศไม่มีวันเป็น 0 และแกนที่เกือบตั้งฉากจะได้ระยะไกลมาก (ไม่ใช่ระยะที่ใช้)
                float3 s = step(0.0, dir) * 2.0 - 1.0;   // +1 หรือ -1 เท่านั้น
                float3 safeD = abs(dir) < 1e-6 ? s * 1e-6 : dir;
                float3 tExit3 = (s * 0.5 - posOS) / safeD;
                float tExit = max(0, min(tExit3.x, min(tExit3.y, tExit3.z)));

                float3 entryWS = TransformObjectToWorld(posOS);
                float3 exitWS  = TransformObjectToWorld(posOS + dir * tExit);
                return distance(entryWS, exitWS);
            }

            half4 frag(Varyings input) : SV_Target
            {
                // เฟดจนใสแล้วไม่ต้องคำนวณเบลอ (ประหยัดเฟรมเรต)
                clip(_BlurAlpha - 0.003);

                // ขอบนุ่ม: ตรงขอบกล่องสายตาเฉียดผ่านบาง → จาง · กลางกล่องหนา → เต็ม
                float edge = saturate(ThicknessThroughBox(input.positionOS) / max(_EdgeFade, 0.001));
                edge = edge * edge * (3 - 2 * edge); // นุ่มทั้งต้นและปลาย

                float2 screenUV  = input.screenPos.xy / input.screenPos.w;

                #if defined(_SOFTDEPTH)
                    float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                    float fragEye  = input.screenPos.w;
                    edge *= saturate((sceneEye - fragEye) / max(_SoftDistance, 0.001));
                #endif

                float alpha = _BlurAlpha * _Color.a * edge;
                clip(alpha - 0.003);

                float2 texelSize = _Scale / _ScreenParams.xy;

                int blurSize = max(1, (int)_Blur);
                half4 sum = 0;
                int count = 0;

                for (int i = -blurSize; i <= blurSize; i++)
                {
                    for (int j = -blurSize; j <= blurSize; j++)
                    {
                        float2 offset = float2(i, j) * texelSize;
                        // clamp กันดึงสีจากนอกจอ (ขอบจอเป็นแถบแปลกๆ)
                        sum += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, saturate(screenUV + offset));
                        count++;
                    }
                }

                half4 finalBlur = sum / count;

                // เอาภาพที่เบลอแล้ว มาคูณผสมกับสีที่เราตั้งค่าไว้ใน Material
                return half4(finalBlur.rgb * _Color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}