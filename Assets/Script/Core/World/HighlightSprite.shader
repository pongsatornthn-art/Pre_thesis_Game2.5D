// [ข้อ 6] ไฮไลต์ไอเทมแบบ Sprite — ขอบเรืองแสงรอบรูป (คำนวณจาก alpha ของสไปรต์) วาดทับทุกอย่างเสมอ (ZTest Always)
// ใช้คู่กับ ItemHighlight.cs (สร้างแผ่นวาดซ้อนให้เอง ไม่ต้องทำ Material)
// อยู่ในโฟลเดอร์ Resources เพื่อให้ติดไปกับตัวเกมตอน Build (Shader.Find หาเจอ)
Shader "Custom/HighlightSprite"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _Color ("Highlight Color", Color) = (1, 0.82, 0.35, 1)
        _Thickness ("Outline Thickness (texels)", Range(0, 16)) = 3
        _InnerGlow ("Inner Glow", Range(0, 1)) = 0.2
        _SpriteOnTop ("Draw Sprite On Top", Range(0, 1)) = 0
        _SpriteColor ("Sprite Tint", Color) = (1, 1, 1, 1)
        _PulseSpeed ("Pulse Speed", Float) = 3
        _PulseMin ("Pulse Min", Range(0, 1)) = 0.55
        _Fade ("Fade", Range(0, 1)) = 1
        _UVRect ("UV Rect (minU, minV, maxU, maxV)", Vector) = (0, 0, 1, 1)
        _TexelSz ("Texel Size (1/w, 1/h)", Vector) = (0.01, 0.01, 0, 0)
        _AlphaThreshold ("Alpha Threshold", Range(0, 1)) = 0.1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest (Always = ทับทุกอย่าง)", Float) = 8
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+100"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "HighlightSprite"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Thickness;
                half _InnerGlow;
                half _SpriteOnTop;
                half4 _SpriteColor;
                half _PulseSpeed;
                half _PulseMin;
                half _Fade;
                float4 _UVRect;
                float4 _TexelSz;
                half _AlphaThreshold;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            // นอกกรอบรูป (ส่วนที่ขยายเผื่อขอบ / รูปข้างๆ ใน atlas) = โปร่งใส
            half4 SampleSprite(float2 uv)
            {
                half inside = step(_UVRect.x, uv.x) * step(_UVRect.y, uv.y) * step(uv.x, _UVRect.z) * step(uv.y, _UVRect.w);
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * inside;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 o = _TexelSz.xy * _Thickness;
                float2 d = o * 0.7071;

                half n = 0;
                n = max(n, SampleSprite(i.uv + float2( o.x, 0)).a);
                n = max(n, SampleSprite(i.uv + float2(-o.x, 0)).a);
                n = max(n, SampleSprite(i.uv + float2(0,  o.y)).a);
                n = max(n, SampleSprite(i.uv + float2(0, -o.y)).a);
                n = max(n, SampleSprite(i.uv + float2( d.x,  d.y)).a);
                n = max(n, SampleSprite(i.uv + float2(-d.x,  d.y)).a);
                n = max(n, SampleSprite(i.uv + float2( d.x, -d.y)).a);
                n = max(n, SampleSprite(i.uv + float2(-d.x, -d.y)).a);

                half4 tex = SampleSprite(i.uv) * _SpriteColor;
                half selfMask = step(_AlphaThreshold, tex.a);
                half outline = saturate(step(_AlphaThreshold, n) - selfMask);

                half pulse = lerp(_PulseMin, 1.0, 0.5 + 0.5 * sin(_Time.y * _PulseSpeed));

                // ตัวรูปบนสุด (ถ้าเปิด) + เรืองแสงด้านใน + ขอบ
                half3 rgb = tex.rgb;
                half a = tex.a * _SpriteOnTop;

                half glowA = selfMask * _InnerGlow * pulse;
                rgb = lerp(rgb, _Color.rgb, saturate(glowA / max(a + glowA, 1e-4)));
                a = max(a, glowA);

                half lineA = outline * pulse;
                rgb = lerp(rgb, _Color.rgb, lineA);
                a = max(a, lineA);

                return half4(rgb, a * _Color.a * _Fade);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
