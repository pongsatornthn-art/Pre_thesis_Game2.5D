// [Map Builder] ลำแสงจากหน้าต่าง — โปร่ง บวกแสง (Additive) ไม่เขียน Depth
// ใช้กับ WindowLightRig (ส่งสี/ความทึบผ่าน MaterialPropertyBlock) · uv.x = ขวางลำแสง · uv.y = ตามยาว (0 = หน้าต่าง)
Shader "MapBuilder/LightShaft"
{
    Properties
    {
        _Color ("สี", Color) = (0.55, 0.68, 1, 1)
        _Opacity ("ความทึบ", Range(0, 1)) = 0.18
        _Soft ("ขอบนุ่ม", Range(0.01, 0.5)) = 0.3
        _Falloff ("จางตามยาว", Range(0.2, 4)) = 1.4
        _Noise ("ลายฝุ่นไหว", Range(0, 1)) = 0.35
        _NoiseSpeed ("ความเร็วฝุ่น", Range(0, 2)) = 0.15
        _RayFlicker ("เส้นแสงกระพริบ", Range(0, 1)) = 0
        _RaySpeed ("ความเร็วกระพริบ", Range(0, 3)) = 0.6
        [NoScaleOffset] _Cookie ("ลายหน้าต่าง (Volume)", 2D) = "white" {}
        _UseCookie ("ใช้ลายหน้าต่าง", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Shaft"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Opacity;
                half _Soft;
                half _Falloff;
                half _Noise;
                half _NoiseSpeed;
                half _RayFlicker;
                half _RaySpeed;
                half _UseCookie;
            CBUFFER_END
            TEXTURE2D(_Cookie); SAMPLER(sampler_Cookie);

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; float2 uv3 : TEXCOORD2; };
            struct Varyings  { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; float2 ray : TEXCOORD2; float2 cuv : TEXCOORD3; };

            Varyings vert (Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = i.uv;
                o.ray = i.uv2;
                o.cuv = i.uv3;   // x = เฟส · y = ความแรงของเส้น (ลำเดียว = 0, 1)
                return o;
            }

            // ค่าสุ่มนุ่มๆ ราคาถูก (value noise 2D)
            float hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i), b = hash21(i + float2(1, 0)), c = hash21(i + float2(0, 1)), d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            half4 frag (Varyings i) : SV_Target
            {
                float u = i.uv.x, v = i.uv.y;
                float edge = smoothstep(0.0, _Soft, u) * smoothstep(0.0, _Soft, 1.0 - u);   // ขอบข้างจาง
                float along = pow(saturate(1.0 - v), _Falloff) * smoothstep(0.0, 0.06, v); // จางไปทางปลาย + ไม่แข็งที่หน้าต่าง
                float n = vnoise(i.positionWS.xz * 1.7 + i.positionWS.y * 0.6 + _Time.y * _NoiseSpeed);
                float dust = lerp(1.0, 0.55 + 0.9 * n, _Noise);
                float strength = i.ray.y > 0.001 ? i.ray.y : 1.0;
                float flick = lerp(1.0, 0.55 + 0.45 * sin(_Time.y * _RaySpeed * 6.2831 + i.ray.x * 6.2831), _RayFlicker);
                float mask = 1.0;
                if (_UseCookie > 0.5)
                {
                    half4 ck = SAMPLE_TEXTURE2D(_Cookie, sampler_Cookie, i.cuv);
                    mask = ck.a; // cookie นำเข้าแบบ alpha จากสีเทา (ขาว = แสงผ่าน)
                    edge = smoothstep(0.0, 0.08, u) * smoothstep(0.0, 0.08, 1.0 - u); // ขอบใช้ลายแทน ไม่ต้องจางเยอะ
                }
                float a = edge * along * dust * _Opacity * strength * flick * mask;
                return half4(_Color.rgb * a, 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
