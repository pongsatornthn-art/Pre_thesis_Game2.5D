// แผนที่ "มองเห็น/มองไม่เห็น" มุมบน (ใช้กับ LineOfSightDarkness)
// Pass 0 = ระบายพื้นที่ที่มองเห็นเป็นสีขาว · Pass 1 = ผสมกับเฟรมก่อน (ความมืดค่อยๆ ไหลตาม) + เบลอขอบให้นุ่ม
// Pass 2 = เขียน depth แทนวัสดุโปร่งใส (ดูข้างล่าง)
Shader "Hidden/LOS/Mask"
{
    Properties
    {
        _MainTex ("Current", 2D) = "black" {}
        _PrevTex ("Previous", 2D) = "black" {}
        _Blend ("Blend", Float) = 0.1
        _BlurTexel ("Blur Texel", Vector) = (0.002, 0.002, 0, 0)
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "Fill"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 vert(float4 v : POSITION) : SV_POSITION { return mul(UNITY_MATRIX_VP, v); }
            half4 frag() : SV_Target { return 1; }
            ENDHLSL
        }

        Pass
        {
            Name "Accumulate"
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            sampler2D _PrevTex;
            float _Blend;
            float4 _BlurTexel;

            half4 frag(v2f_img i) : SV_Target
            {
                float2 d = _BlurTexel.xy;
                half cur = tex2D(_MainTex, i.uv).r * 0.36;
                cur += (tex2D(_MainTex, i.uv + float2(d.x, 0)).r + tex2D(_MainTex, i.uv - float2(d.x, 0)).r
                      + tex2D(_MainTex, i.uv + float2(0, d.y)).r + tex2D(_MainTex, i.uv - float2(0, d.y)).r) * 0.12;
                cur += (tex2D(_MainTex, i.uv + d).r + tex2D(_MainTex, i.uv - d).r
                      + tex2D(_MainTex, i.uv + float2(d.x, -d.y)).r + tex2D(_MainTex, i.uv + float2(-d.x, d.y)).r) * 0.04;
                half prev = tex2D(_PrevTex, i.uv).r;
                return lerp(prev, cur, _Blend);
            }
            ENDHLSL
        }

        // Pass 2 = เขียน depth แทนชิ้นส่วนแมพที่วัสดุโปร่งใส (LOSDepthPass วาดทับวัสดุเดิม)
        // วัสดุโปร่งใสของ URP ปิด pass DepthOnly ของตัวเองไว้ → ต้องวาดด้วยอันนี้แทน · ทึบทั้งชิ้น
        Pass
        {
            Name "LOSDepth"
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 vert(float4 v : POSITION) : SV_POSITION { return UnityObjectToClipPos(v); }
            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
