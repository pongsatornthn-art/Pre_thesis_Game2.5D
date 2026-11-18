// [ข้อ 6] ไฮไลต์วัตถุ 3D (MeshRenderer) — เรืองแสงทับตัววัตถุ + ขอบรอบนอก วาดทับทุกอย่างเสมอ (ZTest Always)
// ItemHighlight.cs ใช้ shader นี้ 2 ชั้น:
//   ชั้น Fill (คิว 3100) = ทาสีเรืองบนตัววัตถุ + เขียน Stencil
//   ชั้น Ring (คิว 3101) = วัตถุขยายใหญ่ขึ้นนิด วาดเฉพาะส่วนที่ Stencil ไม่ได้เขียน → เหลือแต่ "ขอบ"
// อยู่ในโฟลเดอร์ Resources เพื่อให้ติดไปกับตัวเกมตอน Build
Shader "Custom/HighlightMesh"
{
    Properties
    {
        [HDR] _Color ("Highlight Color", Color) = (1, 0.82, 0.35, 1)
        _FillAlpha ("Fill Alpha", Range(0, 1)) = 0.2
        _RimAlpha ("Rim Alpha", Range(0, 1)) = 0.6
        _RimPower ("Rim Power", Range(0.5, 8)) = 2
        _Expand ("Expand (scale)", Range(0, 0.5)) = 0
        _Width ("Expand along normal (m)", Range(0, 0.2)) = 0
        _Center ("Object-space center", Vector) = (0, 0, 0, 0)
        _PulseSpeed ("Pulse Speed", Float) = 3
        _PulseMin ("Pulse Min", Range(0, 1)) = 0.55
        _Fade ("Fade", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comp", Float) = 8
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilPass ("Stencil Pass", Float) = 2
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
        }

        Pass
        {
            Name "HighlightMesh"
            Tags { "LightMode" = "UniversalForward" }

            Stencil
            {
                Ref 64
                ReadMask 64
                WriteMask 64
                Comp [_StencilComp]
                Pass [_StencilPass]
            }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _FillAlpha;
                half _RimAlpha;
                half _RimPower;
                float _Expand;
                float _Width;
                float4 _Center;
                half _PulseSpeed;
                half _PulseMin;
                half _Fade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 p = v.positionOS.xyz;
                p = _Center.xyz + (p - _Center.xyz) * (1.0 + _Expand) + v.normalOS * _Width;
                float3 posWS = TransformObjectToWorld(p);
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewDirWS = GetWorldSpaceViewDir(posWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                half3 v = normalize(i.viewDirWS);
                half rim = pow(1.0 - saturate(abs(dot(n, v))), _RimPower);
                half pulse = lerp(_PulseMin, 1.0, 0.5 + 0.5 * sin(_Time.y * _PulseSpeed));
                half a = saturate(_FillAlpha + rim * _RimAlpha) * pulse;
                return half4(_Color.rgb, a * _Color.a * _Fade);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
