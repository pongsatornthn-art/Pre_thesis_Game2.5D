// [2026-11-21] หมอกเรี่ยพื้นแบบ "ผืนเดียว" — ไม่ใช่รูปควันหลายแผ่น
// · รูปร่างหมอกคำนวณจาก noise หลายชั้นในพิกัดโลก (ไม่มีรูป PNG → ไม่มีอาการรูปโดนบีบ/เบลอ) ก้อนใหญ่-เล็กปนกันเอง ไหลช้าๆ เปลี่ยนรูปตลอด
// · รับแสงไฟจริงในฉาก (ไฟแดงตกใส่หมอก = เรืองแดง · ที่มืด = หมอกหม่น)
// · ชนผนัง/ของ/ขาตัวละคร = จางนุ่มๆ (เทียบความลึกฉาก)
// · ผู้เล่นเดินผ่าน = ยุบเฉพาะจุดที่เดิน แล้วค่อยๆ ไหลกลับ (ตำแหน่งรอยเท้าส่งมาจาก MistTrailFeeder.cs ผ่าน _MistTrail)
// · ซ้อนหลายแผ่นที่ความสูงต่างกัน = มีความหนา (แผ่นสูงบางลงเอง ตาม _FogHeight)
Shader "PTSD/VolumeMist"
{
    Properties
    {
        _Color ("สีหมอก", Color) = (0.78, 0.82, 0.95, 1)
        _Density ("ความหนา", Range(0, 2)) = 0.85
        _Coverage ("ปริมาณ (มาก = หมอกน้อยลง มีช่องว่าง)", Range(0, 1)) = 0.42
        _Softness ("ความฟุ้งของขอบก้อน", Range(0.01, 1)) = 0.35
        _Scale ("ขนาดก้อนใหญ่ (เมตร)", Float) = 5
        _DetailScale ("ขนาดริ้วเล็ก (เมตร)", Float) = 1.3
        _Stretch ("ยืดตามแนว X (1 = ไม่ยืด)", Float) = 1.8
        _Flow ("ทิศ+ความเร็วไหล (xy = ก้อนใหญ่, zw = ริ้วเล็ก) เมตร/วิ", Vector) = (0.12, 0.02, -0.07, 0.05)
        _Warp ("ความบิดเบี้ยวของริ้ว", Range(0, 3)) = 1.2
        _FloorY ("ความสูงพื้น (โลก)", Float) = 0
        _FogHeight ("หมอกสูงสุด (เมตร เหนือพื้น)", Float) = 0.7
        _EdgeFade ("ขอบแผ่นจาง (เมตร)", Float) = 1.0
        _DepthFade ("ชนของ/ผนัง จางนุ่ม (เมตร)", Float) = 0.35
        _LightGain ("รับแสงไฟมากแค่ไหน", Range(0, 3)) = 1.0
        _Ambient ("แสงรอบตัว (กันหมอกดำในที่มืด)", Range(0, 2)) = 0.9
        _HoleRadius ("รัศมียุบตรงผู้เล่น (เมตร)", Float) = 0.75
        _HoleDepth ("ยุบลึกแค่ไหน (1 = หายหมด)", Range(0, 1)) = 0.9
        [Enum(Off,0,Quad,1,Noise,2,Depth,3,Density,4)] _DebugMode ("ดีบัก (ปิดไว้)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "Mist"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _LIGHT_LAYERS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Density, _Coverage, _Softness;
                float _Scale, _DetailScale, _Stretch;
                float4 _Flow;
                half _Warp;
                float _FloorY, _FogHeight, _EdgeFade, _DepthFade;
                half _LightGain, _Ambient;
                float _HoleRadius;
                half _HoleDepth;
                half _DebugMode;
            CBUFFER_END

            #define MIST_TRAIL_MAX 12
            float4 _MistTrail[MIST_TRAIL_MAX];   // xyz = ตำแหน่ง · w = ความแรง 0..1 (จาก MistTrailFeeder)
            float _MistTrailCount;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 edgeInfo   : TEXCOORD1;   // xy = ตำแหน่งบนแผ่น (-0.5..0.5) · zw = ขนาดแผ่นจริง (เมตร)
                float  eyeDepth   : TEXCOORD2;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.eyeDepth = -TransformWorldToView(o.positionWS).z;
                // Quad ของ Unity = -0.5..0.5 แกน X/Y (วางนอน: หมุน X 90) → ขนาดจริง = สเกลของ object
                float sx = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
                float sy = length(float3(UNITY_MATRIX_M[0].y, UNITY_MATRIX_M[1].y, UNITY_MATRIX_M[2].y));
                o.edgeInfo = float4(v.positionOS.xy, sx, sy);   // คิดระยะถึงขอบทีละพิกเซลใน frag (คิดที่มุมแล้วเฉลี่ย = 0 ทั้งแผ่น)
                return o;
            }

            // ---------- noise ----------
            float2 hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return -1.0 + 2.0 * frac(sin(p) * 43758.5453123);
            }
            float gnoise(float2 p)   // gradient noise -1..1
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = dot(hash2(i), f);
                float b = dot(hash2(i + float2(1, 0)), f - float2(1, 0));
                float c = dot(hash2(i + float2(0, 1)), f - float2(0, 1));
                float d = dot(hash2(i + float2(1, 1)), f - float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }
            float fbm(float2 p, int oct)
            {
                float s = 0, a = 0.5;
                for (int k = 0; k < oct; k++) { s += a * gnoise(p); p = mul(float2x2(1.6, 1.2, -1.2, 1.6), p); a *= 0.5; }
                return s;   // ≈ -0.5..0.5
            }

            half4 frag (Varyings i) : SV_Target
            {
                float3 wp = i.positionWS;
                float t = _Time.y;
                float2 xz = wp.xz * float2(1.0 / _Stretch, 1.0);
                float layer = wp.y * 3.7;   // แต่ละแผ่น (ความสูงต่างกัน) ได้ลายไม่ซ้ำกัน

                // ก้อนใหญ่ (ขนาดไม่เท่ากันเอง จาก fbm) + บิดด้วย noise อีกชั้น = ริ้วไหลแบบควันจริง
                float2 p = xz / _Scale + _Flow.xy * t / _Scale + layer;
                float2 warp = float2(fbm(p * 1.3 + 7.1 + t * 0.03, 3), fbm(p * 1.3 - 3.7 - t * 0.025, 3));
                float big = fbm(p + warp * _Warp, 5) + 0.5;

                float2 q = xz / _DetailScale + _Flow.zw * t / _DetailScale - layer;
                float detail = fbm(q + warp * 0.6, 4) + 0.5;

                float n = big * 0.75 + detail * 0.35;
                float dens = smoothstep(_Coverage, _Coverage + _Softness, n);

                // แผ่นสูง = บาง (มีความหนาแบบหมอกเรี่ยพื้น)
                float h = saturate((wp.y - _FloorY) / max(_FogHeight, 0.01));
                dens *= (1.0 - h) * (1.0 - h * 0.3);

                // ขอบแผ่นจาง (มองไม่เห็นว่าเป็นแผ่น)
                float2 edgeDist = (0.5 - abs(i.edgeInfo.xy)) * i.edgeInfo.zw;
                float2 ef = saturate(edgeDist / max(_EdgeFade, 0.01));
                dens *= smoothstep(0, 1, ef.x) * smoothstep(0, 1, ef.y);

                // ชนผนัง / ของ / ขาตัวละคร → จางนุ่ม
                float2 suv = i.positionCS.xy / _ScaledScreenParams.xy;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                dens *= saturate((sceneEye - i.eyeDepth) / max(_DepthFade, 0.001));

                // ยุบตรงผู้เล่น (เฉพาะจุดที่เดินผ่าน ขอบหลุมขยุกขยิกตามลายหมอก)
                float hole = 0;
                int cnt = (int)_MistTrailCount;
                for (int k = 0; k < MIST_TRAIL_MAX; k++)
                {
                    if (k >= cnt) break;
                    float4 tr = _MistTrail[k];
                    float d = distance(wp.xz, tr.xz) + (detail - 0.5) * 0.45;
                    float r = _HoleRadius * (0.6 + 0.4 * tr.w);
                    hole = max(hole, tr.w * (1.0 - smoothstep(r * 0.35, r, d)));
                }
                dens *= 1.0 - hole * _HoleDepth;

                dens = saturate(dens * _Density);
                if (_DebugMode > 0.5)
                {
                    if (_DebugMode < 1.5) return half4(1, 0, 0, 0.6);                       // 1 = แผ่นถูกวาดไหม
                    if (_DebugMode < 2.5) return half4(n, n, n, 1);                          // 2 = ลาย noise
                    if (_DebugMode < 3.5) return half4(saturate((sceneEye - i.eyeDepth)), ef.x, ef.y, 1);   // 3 = ความลึก/ขอบ
                    return half4(dens, dens, dens, 1);                                       // 4 = ความหนาสุดท้าย
                }
                if (dens < 0.003) discard;

                // แสง: ฟุ้งกระจาย (ไม่สนทิศ) = รอบตัว + แสงหลัก + ไฟทุกดวงในระยะ
                float3 light = SampleSH(half3(0, 1, 0)) * _Ambient;
                Light mainL = GetMainLight();
                light += mainL.color * 0.35;

                InputData inputData = (InputData)0;
                inputData.positionWS = wp;
                inputData.normalizedScreenSpaceUV = suv;
                #if defined(_ADDITIONAL_LIGHTS) || defined(_FORWARD_PLUS)
                uint pixelLightCount = GetAdditionalLightsCount();
                #if USE_FORWARD_PLUS
                for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    Light l = GetAdditionalLight(lightIndex, wp);
                    light += l.color * l.distanceAttenuation * 0.35;
                }
                #endif
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light l = GetAdditionalLight(lightIndex, wp);
                    light += l.color * l.distanceAttenuation * _LightGain;
                LIGHT_LOOP_END
                #endif

                half3 col = _Color.rgb * light;
                return half4(col, dens * _Color.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
