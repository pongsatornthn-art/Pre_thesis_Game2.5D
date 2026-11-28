using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [Claude 2026-11-28] ไอคอนปุ่มที่ขยับบนการ์ดคำสอน (แบบเกม AAA) — แปะที่รูปปุ่ม [E] / เมาส์ / ลูกศร
/// ใช้เวลาจริง (unscaled) → ขยับได้แม้เกมหยุดอยู่
///   ชีพจร   = ขยาย-หดเบา ๆ (ปุ่มที่ต้องกด)
///   กดลง    = จมลงเป็นจังหวะ + มืดลงนิดนึง (คลิก/กดปุ่ม)
///   ค้างกด  = จมลงแล้วค้างไว้ครึ่งรอบ (ค้างคลิกขวาเล็ง)
///   เลื่อนซ้ายขวา / ขึ้นลง = ไปกลับ (ลูกศรเมาส์ "ขยับไปมา")
///   กะพริบ  = จาง-ชัด
/// </summary>
[DisallowMultipleComponent]
public class KeyIconAnimator : MonoBehaviour
{
    public enum Motion { Pulse, Press, Hold, SlideHorizontal, SlideVertical, Blink }

    [SerializeField] private Motion motion = Motion.Pulse;

    [Tooltip("ใช้กี่วินาทีต่อ 1 รอบ")]
    [SerializeField, Min(0.1f)] private float period = 1f;

    [Tooltip("ความแรง: ชีพจร = ขยายกี่เท่า (0.12 = 12%) · กดลง/เลื่อน = กี่พิกเซล · กะพริบ = จางเหลือเท่าไหร่ (0–1)")]
    [SerializeField] private float amount = 0.12f;

    [Tooltip("เลื่อนจังหวะ (0–1) — ไอคอนหลายตัวจะได้ไม่ขยับพร้อมกันเป๊ะ")]
    [SerializeField, Range(0f, 1f)] private float phaseOffset;

    private RectTransform rect;
    private Graphic graphic;
    private Vector2 basePos;
    private Vector3 baseScale;
    private Color baseColor;
    private float startTime;

    private void Awake()
    {
        rect = transform as RectTransform;
        graphic = GetComponent<Graphic>();
        if (rect != null) basePos = rect.anchoredPosition;
        baseScale = transform.localScale;
        if (graphic != null) baseColor = graphic.color;
    }

    private void OnEnable() => startTime = Time.unscaledTime;

    private void OnDisable() => ResetPose();

    private void ResetPose()
    {
        if (rect != null) rect.anchoredPosition = basePos;
        transform.localScale = baseScale;
        if (graphic != null) graphic.color = baseColor;
    }

    private void Update()
    {
        float t = ((Time.unscaledTime - startTime) / period + phaseOffset) % 1f;
        float wave = 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f);   // 0 → 1 → 0 นุ่ม ๆ

        switch (motion)
        {
            case Motion.Pulse:
                transform.localScale = baseScale * (1f + amount * wave);
                break;

            case Motion.Press:
            {
                float down = t < 0.15f ? t / 0.15f : t < 0.3f ? 1f - (t - 0.15f) / 0.15f : 0f;   // กดเร็ว ปล่อยเร็ว แล้วพัก
                SetOffset(new Vector2(0f, -amount * down));
                Tint(1f - 0.25f * down);
                break;
            }

            case Motion.Hold:
            {
                float down = t < 0.1f ? t / 0.1f : t < 0.6f ? 1f : t < 0.7f ? 1f - (t - 0.6f) / 0.1f : 0f;
                SetOffset(new Vector2(0f, -amount * down));
                Tint(1f - 0.25f * down);
                break;
            }

            case Motion.SlideHorizontal:
                SetOffset(new Vector2(Mathf.Sin(t * Mathf.PI * 2f) * amount, 0f));
                break;

            case Motion.SlideVertical:
                SetOffset(new Vector2(0f, Mathf.Sin(t * Mathf.PI * 2f) * amount));
                break;

            case Motion.Blink:
                if (graphic != null)
                {
                    Color c = baseColor;
                    c.a = baseColor.a * Mathf.Lerp(1f, Mathf.Clamp01(amount), wave);
                    graphic.color = c;
                }
                break;
        }
    }

    private void SetOffset(Vector2 offset)
    {
        if (rect != null) rect.anchoredPosition = basePos + offset;
    }

    private void Tint(float brightness)
    {
        if (graphic == null) return;
        graphic.color = new Color(baseColor.r * brightness, baseColor.g * brightness, baseColor.b * brightness, baseColor.a);
    }
}
