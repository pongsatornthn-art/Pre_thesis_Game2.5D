// เงาจางๆ ใต้เท้าตัวละคร (วงรีดำค่อยๆ จางออก) — ใช้กับ Quad นอนราบใต้เท้า
// ไม่ต้องใช้รูป: คำนวณวงจาก UV เอง · ปรับความเข้มที่ Color (ช่อง A) · ปรับความนุ่มขอบที่ Softness
// คิวอยู่ก่อนช่วงโปร่งใส (AlphaTest+10) — กล่องเบลอห้องจับภาพฉากก่อนช่วงโปร่งใส ถ้าเป็นคิวโปร่งใสปกติเงาจะหายตอนอยู่หลังกล่องเบลอ
Shader "Custom/BlobShadow"
{
    Properties
    {
        _Color ("Color (A = ความเข้มเงา)", Color) = (0, 0, 0, 0.55)
        _Softness ("Softness (0 = ขอบคม, 1 = จางทั้งวง)", Range(0.01, 1)) = 0.8
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "AlphaTest+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha, Zero One   // ไม่แตะช่อง alpha ของภาพ — ถ้าเขียนทับ เอฟเฟกต์จอทีหลังอ่านผิด เงากลายเป็นวงสีขาว
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Softness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float r = length(i.uv * 2.0 - 1.0);                       // 0 = กลางวง · 1 = ขอบ
                half a = 1.0 - smoothstep(1.0 - _Softness, 1.0, r);
                return half4(_Color.rgb, _Color.a * a);
            }
            ENDHLSL
        }
    }
}
