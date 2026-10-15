// "เพดานดำ" ทับทั้งจอ — ใช้กับ Full Screen Pass Renderer Feature ของ URP
// ทุกพิกเซล: หาตำแหน่งในโลกจาก depth → ดูแผนที่สายตา (_LOSMask) → ที่ตัวละครมองไม่เห็น = มืด
// ไม่มี LineOfSightDarkness ทำงานในซีน (_LOSEnabled = 0) → ภาพเดิมทุกอย่าง
Shader "Hidden/LOS/Darkness"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "LOSDarkness"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // depth ที่ LineOfSightDarkness วาดเอง (รวมของโปร่งใส) — ไม่ใช้ _CameraDepthTexture เพราะไม่มีของโปร่งใส
            TEXTURE2D_X_FLOAT(_LOSDepthTexture);
            TEXTURE2D(_LOSMask);
            SAMPLER(sampler_LOSMask);
            float4 _LOSMaskRect;   // xy = มุมซ้ายล่าง (x, z) · zw = 1/ขนาด
            float _LOSEnabled;
            float _LOSDarkness;    // 1 = ดำสนิท
            float _LOSDebug;
            float _LOSFacePush;    // เมตร — หน้ากำแพง/ด้านข้างของ ดูที่จุดตรงหน้ามันแทน

            float3 WorldAt(float2 uv)
            {
                float rawDepth = SAMPLE_TEXTURE2D_X(_LOSDepthTexture, sampler_PointClamp, uv).r;
                #if UNITY_REVERSED_Z
                    float depth = rawDepth;
                #else
                    float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawDepth);
                #endif
                return ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (_LOSEnabled < 0.5) return col;

                float3 world = WorldAt(uv);

                // ทิศที่ผิวหันไป (จาก depth รอบๆ — เลือกข้างที่ต่างน้อยกว่า กันขอบวัตถุเพี้ยน)
                float2 px = 1.0 / _ScaledScreenParams.xy;
                float3 l = WorldAt(uv - float2(px.x, 0)), r = WorldAt(uv + float2(px.x, 0));
                float3 d = WorldAt(uv - float2(0, px.y)), u = WorldAt(uv + float2(0, px.y));
                float3 dx = distance(r, world) < distance(world, l) ? r - world : world - l;
                float3 dy = distance(u, world) < distance(world, d) ? u - world : world - d;
                float3 n = normalize(cross(dx, dy));
                if (dot(n, _WorldSpaceCameraPos - world) < 0) n = -n;

                // ผิวตั้ง (หน้ากำแพง/ด้านข้างของ/ตัวละคร): เห็นได้ถ้า "พื้นที่ตรงหน้ามัน" มองเห็น
                // → หน้ากำแพงฝั่งเราสว่างทั้งแผ่น (ไม่มีเงาเส้นดำจากกรอบประตูที่ยื่นออกมา) · พื้น/ด้านบนของ ใช้จุดตัวเองตามเดิม
                float2 xz = world.xz;
                float2 nh = n.xz;
                if (abs(n.y) < 0.5 && dot(nh, nh) > 1e-4) xz += normalize(nh) * _LOSFacePush;

                float2 m = (xz - _LOSMaskRect.xy) * _LOSMaskRect.zw;
                half seen = 0;
                if (all(m >= 0) && all(m <= 1)) seen = SAMPLE_TEXTURE2D(_LOSMask, sampler_LOSMask, m).r;
                // ขอบที่เบลอ: เอาแค่ครึ่งใน (ฝั่งที่มองเห็น) — พ้นเส้นสายตาปุ๊บดำสนิท ไม่ฟุ้งไปโดนของที่ชิดกำแพงฝั่งโน้น
                seen = saturate(seen * 2 - 1);

                // ดีบัก: แดง = ตำแหน่ง x ในแผนที่ · เขียว = ตำแหน่ง z · น้ำเงิน = มองเห็น
                if (_LOSDebug > 0.5) return half4(saturate(m.x), saturate(m.y), seen, 1);

                half dark = (1 - saturate(seen)) * _LOSDarkness;
                col.rgb *= 1 - dark;
                return col;
            }
            ENDHLSL
        }
    }
}
