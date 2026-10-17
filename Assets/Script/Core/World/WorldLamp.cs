using UnityEngine;

/// <summary>
/// ไฟ 1 ดวง — บอกว่าในโลก PTSD ดวงนี้เป็นยังไง (โลกปกติ: ไฟทุกดวงติดสว่าง ไม่กระพริบ)
///   Red     = ติดเป็นสีแดง (สีตั้งใน WorldLighting) · อาจโดนสุ่มกระพริบเป็นพักๆ
///   Flicker = แดง + ติดๆ ดับๆ ตลอด
///   Off     = ดับ
/// ไม่แปะ = นับเป็น Red · ความสว่าง/สี "ตั้งต้น" (โลกปกติ) คือค่าที่ตั้งใน Light ตอนเริ่มเกม
/// ตัวที่สั่งคือ WorldLighting (ตัวเดียวต่อซีน) — ตัวนี้แค่บอกนิสัยของดวงนี้
/// </summary>
[RequireComponent(typeof(Light))]
public class WorldLamp : MonoBehaviour
{
    public enum PtsdState { Red, Flicker, Off }

    [Tooltip("ในโลก PTSD ดวงนี้เป็นยังไง")]
    public PtsdState inPtsd = PtsdState.Red;

    [Tooltip("ก้อน/โมเดลโคมที่ควรสว่าง-มืด-เปลี่ยนสีตามไฟ (เช่น ก้อนเหลืองจำลอง) — เว้นว่างได้")]
    public Renderer glowVisual;
}
