using UnityEngine;

/// <summary>
/// [Claude 2026-11-28] ชุดไอคอนปุ่มขยับ 1 ชุด ที่ซ้อนบนคลิปในการ์ดคำสอน (เช่น "เมาส์ + ลูกศรซ้ายขวา")
/// วางเป็นลูกของกรอบคลิปในการ์ด · ตั้ง Overlay Id · ข้างในใส่รูปปุ่มแปะ KeyIconAnimator
/// หน้าไหนอยากโชว์ชุดนี้ → พิมพ์ Overlay Id ลงช่อง Overlay Ids ของหน้านั้นใน TutorialData
/// การ์ดเปิด/ปิดชุดให้เอง (ไม่ต้องตั้ง Active เอง)
/// </summary>
[DisallowMultipleComponent]
public class TutorialOverlay : MonoBehaviour
{
    [Tooltip("ชื่อชุด ห้ามซ้ำในการ์ดเดียวกัน เช่น mouse_look / mouse_right_hold / key_e / key_tab")]
    [SerializeField] private string overlayId;

    public string OverlayId => overlayId;
}
