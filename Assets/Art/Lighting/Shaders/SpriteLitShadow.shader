// สไปรท์/ไทล์แมพที่ "รับแสง + รับเงา + สร้างเงา" ได้ ในโลก 3D (URP Forward / Forward+)
// ใช้กับ: พื้น Tilemap (Wrap = 0) · ตัวละครสไปรท์ (Wrap ~0.5 ให้ด้านที่หันหนีไฟไม่ดำสนิท)
// รูปมาจาก SpriteRenderer / TilemapRenderer ผ่าน _MainTex เอง · สีของ SpriteRenderer = vertex color
// ขอบรูปใช้ตัดทิ้ง (Alpha Cutoff) ไม่ใช่โปร่งแสง — ถึงสร้างเงาเป็นรูปทรงตัวละครได้
Shader "Custom/SpriteLitShadow"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite", 2D) = "white" {}
        [MainColor] _Color ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _Wrap ("Wrap Lighting (0 = พื้น, 0.5 = ตัวละคร)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Color;
            half _Cutoff;
            half _Wrap;
        CBUFFER_END

        // สไปรท์/ไทล์ไม่มี normal ในโมเดล → ใช้ "ด้านหน้าของแผ่น" (แกน -Z ของ object) แทน
        float3 SpriteNormalWS()
        {
            return normalize(TransformObjectToWorldDir(float3(0, 0, -1)));
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                half4 color : COLOR;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normalWS = SpriteNormalWS();
                o.color = v.color * _Color;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half3 Shade(Light light, float3 n)
            {
                half ndl = dot(n, light.direction);
                half wrapped = saturate((ndl + _Wrap) / (1.0 + _Wrap));
                return light.color * (wrapped * light.distanceAttenuation * light.shadowAttenuation);
            }

            half4 frag(Varyings i, bool isFront : SV_IsFrontFace) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                clip(col.a - _Cutoff);

                float3 n = isFront ? i.normalWS : -i.normalWS;   // มองจากด้านหลังแผ่นก็ยังสว่างถูกด้าน

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                half3 lighting = Shade(GetMainLight(shadowCoord), n);

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);

                // Forward+ ไม่เปิด _ADDITIONAL_LIGHTS — ต้องเช็ค USE_FORWARD_PLUS ด้วย ไม่งั้นไฟจุด/ไฟห้องไม่ติดพื้น
                #if defined(_ADDITIONAL_LIGHTS) || USE_FORWARD_PLUS
                uint pixelLightCount = GetAdditionalLightsCount();

                #if USE_FORWARD_PLUS
                for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    lighting += Shade(GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1)), n);
                }
                #endif

                LIGHT_LOOP_BEGIN(pixelLightCount)
                    lighting += Shade(GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1)), n);
                LIGHT_LOOP_END
                #endif

                half3 ambient = SampleSH(n);
                half3 rgb = col.rgb * (lighting + ambient);
                rgb = MixFog(rgb, i.fogFactor);
                return half4(rgb, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"   // LerpWhiteTo ที่ Shadows.hlsl ต้องใช้
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half alpha : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = SpriteNormalWS();
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDir = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
                #if UNITY_REVERSED_Z
                    o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.alpha = v.color.a * _Color.a;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a * i.alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half alpha : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.alpha = v.color.a * _Color.a;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a * i.alpha - _Cutoff);
                return i.positionCS.z;
            }
            ENDHLSL
        }
    }
}
