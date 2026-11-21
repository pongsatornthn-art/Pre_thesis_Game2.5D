using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// [แก้ 2026-11-20] ทำให้ UI แบบ World Space (หลอดเลือด/Stamina/กระสุนเหนือหัว) วาดทับตัวละครและฉากเสมอ
/// ไม่จมเข้าไปในตัวละครหรือของ 3D
/// วิธีใช้: แปะที่ GameObject ที่มี Canvas (เช่น Player/AimPivot/Canvas (1)) แล้วกด Play — ไม่ต้องตั้งค่าอื่น
///  1) ดัน Order in Layer ของ Canvas ให้สูงกว่าตัวละคร (GFX ใช้ 20)
///  2) สั่งให้ทุกรูป/ตัวหนังสือใน Canvas ไม่ต้องเช็คความลึก (ZTest Always) → ของ 3D บังไม่ได้
/// </summary>
[RequireComponent(typeof(Canvas))]
public class WorldHudAlwaysOnTop : MonoBehaviour
{
    [Tooltip("ลำดับการวาด ต้องมากกว่า Order in Layer ของตัวละคร (GFX = 20)")]
    public int sortingOrder = 100;

    [Tooltip("ให้ของ 3D (กำแพง โต๊ะ) บังหลอดไม่ได้ด้วย")]
    public bool ignoreDepth = true;

    [Tooltip("เช็คซ้ำทุกกี่วินาที เผื่อมีรูป/ข้อความเพิ่มเข้ามาทีหลัง (0 = ทำครั้งเดียวตอนเริ่ม)")]
    public float refreshInterval = 1f;

    private static readonly int ZTestId = Shader.PropertyToID("unity_GUIZTestMode");
    private Material imageMat;
    private float nextRefresh;

    private void Start()
    {
        Canvas c = GetComponent<Canvas>();
        c.sortingOrder = sortingOrder;
        Apply();
    }

    private void Update()
    {
        if (refreshInterval <= 0f || Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + refreshInterval;
        Apply();
    }

    private void Apply()
    {
        if (!ignoreDepth) return;

        foreach (Graphic g in GetComponentsInChildren<Graphic>(true))
        {
            if (g is TMP_Text tmp)
            {
                // fontMaterial = สำเนาเฉพาะตัวนี้ ไม่กระทบตัวหนังสือที่อื่นในเกม
                Material m = tmp.fontMaterial;
                if (m != null && m.GetInt(ZTestId) != (int)CompareFunction.Always)
                    m.SetInt(ZTestId, (int)CompareFunction.Always);
                continue;
            }

            // รูปที่ใช้วัสดุพื้นฐานของ UI → เปลี่ยนเป็นวัสดุแบบไม่เช็คความลึก (ใช้ร่วมกันทั้ง Canvas)
            if (g.material == null || g.material == g.defaultMaterial)
            {
                if (imageMat == null)
                {
                    imageMat = new Material(Graphic.defaultGraphicMaterial) { name = "UI_AlwaysOnTop" };
                    imageMat.SetInt(ZTestId, (int)CompareFunction.Always);
                }
                g.material = imageMat;
            }
            else if (g.material.GetInt(ZTestId) != (int)CompareFunction.Always)
            {
                // วัสดุที่ตั้งเองไว้ → ทำสำเนาก่อนแก้ ไม่ไปแก้ไฟล์วัสดุต้นฉบับ
                Material copy = new Material(g.material);
                copy.SetInt(ZTestId, (int)CompareFunction.Always);
                g.material = copy;
            }
        }
    }

    private void OnDestroy()
    {
        if (imageMat != null) Destroy(imageMat);
    }
}
