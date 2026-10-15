using UnityEngine;

/// <summary>
/// ไฟ 1 ดวงที่ "เปิด/ปิดตามโลก" — แปะที่ไฟในแมพ (ตัวที่มี Light) แล้วเลือกช่อง Lit In
///   Both     = ติดทั้ง 2 โลก (โลก PTSD ความสว่างคูณตาม Lamp Multiplier ของบรรยากาศ PTSD)
///   RealOnly = ติดเฉพาะโลกปกติ — โลก PTSD ดับ (แบบเกมผี: ผีมาแล้วไฟดับเหลือไม่กี่ดวง)
///   PtsdOnly = ติดเฉพาะโลก PTSD (เช่น ไฟแดงหลอนๆ)
/// ไม่แปะ = นับเป็น Both · ความสว่าง/สี "ตั้งต้น" คือค่าที่ตั้งใน Light ตอนเริ่มเกม
/// ตัวที่สั่งคือ WorldLighting (ตัวเดียวต่อซีน) — ตัวนี้แค่บอกว่าดวงนี้ติดโลกไหน
/// </summary>
[RequireComponent(typeof(Light))]
public class WorldLamp : MonoBehaviour
{
    public enum LitIn { Both, RealOnly, PtsdOnly }

    [Tooltip("ไฟดวงนี้ติดโลกไหน")]
    public LitIn litIn = LitIn.Both;

    [Tooltip("ก้อน/โมเดลโคมที่ควรสว่าง-มืดตามไฟ (เช่น ก้อนเหลืองจำลอง) — ไฟดับแล้วซ่อน (เว้นว่างได้)")]
    public Renderer glowVisual;

    public bool IsLitIn(WorldMode mode) =>
        litIn == LitIn.Both ||
        (litIn == LitIn.RealOnly && mode == WorldMode.Real) ||
        (litIn == LitIn.PtsdOnly && mode != WorldMode.Real);
}
