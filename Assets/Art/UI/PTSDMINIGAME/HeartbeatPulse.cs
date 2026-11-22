using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-11-22] ทำให้รูปเต้นตุบๆ แบบหัวใจ (ตุบแรง-ตุบเบา แล้วพัก) — ใช้กับปอดในแถบ ECG
/// วิธีใช้: แปะที่ ECG_Lungs แล้วลาก ECG_LungGlow ใส่ช่อง Glow Image (ไม่ใส่ก็ได้) — เต้นเองตลอดที่รูปเปิดอยู่
/// ไม่ขึ้นกับสคริปต์มินิเกมตัวไหน · ใช้เวลาจริง (เกมหยุดเวลาอยู่ก็ยังเต้น)
/// อยากให้เร็ว/ช้าตามสถานการณ์ → สคริปต์อื่นสั่ง SetStress(0–1) ได้ (0 = สงบ · 1 = ตื่นตระหนก)
/// </summary>
[DisallowMultipleComponent]
public class HeartbeatPulse : MonoBehaviour
{
    [Tooltip("วงแสงหลังปอด (ECG_LungGlow) — สว่างวูบตามจังหวะ · เว้นว่างได้")]
    public Image glowImage;

    [Header("จังหวะ")]
    [Tooltip("ครั้งต่อนาที ตอนสงบ (Stress = 0)")]
    public float calmBPM = 60f;
    [Tooltip("ครั้งต่อนาที ตอนตื่นตระหนก (Stress = 1)")]
    public float panicBPM = 120f;
    [Tooltip("ระดับความเครียดเริ่มต้น (0 = สงบ · 1 = ตื่นตระหนก)")]
    [Range(0f, 1f)] public float stress = 0.7f;

    [Header("ท่าเต้น")]
    [Tooltip("ขยายตอนเต้นกี่ % (0.12 = ใหญ่ขึ้น 12%)")]
    [Range(0f, 0.4f)] public float beatScale = 0.12f;
    [Range(0f, 1f)] public float glowMinAlpha = 0.35f;
    [Range(0f, 1f)] public float glowMaxAlpha = 1f;

    private Vector3 baseScale;
    private float phase;
    private float stressSmoothed;

    private void Awake()
    {
        baseScale = transform.localScale;
        stressSmoothed = stress;
    }

    private void OnDisable()
    {
        transform.localScale = baseScale; // ปิดแผง → คืนขนาดเดิม ไม่ค้างตอนพองอยู่
    }

    /// <summary>ตั้งระดับความเครียด 0–1 (เปลี่ยนความเร็วเต้นแบบนุ่มๆ ไม่กระตุก)</summary>
    public void SetStress(float value) => stress = Mathf.Clamp01(value);

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        stressSmoothed = Mathf.MoveTowards(stressSmoothed, stress, dt * 0.8f);
        float bpm = Mathf.Lerp(calmBPM, panicBPM, stressSmoothed);

        phase = (phase + dt * bpm / 60f) % 1f;
        float beat = Curve(phase);

        transform.localScale = baseScale * (1f + beat * beatScale);
        if (glowImage != null)
        {
            Color c = glowImage.color;
            c.a = Mathf.Lerp(glowMinAlpha, glowMaxAlpha, beat);
            glowImage.color = c;
        }
    }

    /// <summary>ตุบแรง (ที่ 0) + ตุบเบา (ที่ 0.2) แล้วนิ่ง — คืนค่า 0–1</summary>
    private static float Curve(float p)
    {
        float lub = Mathf.Exp(-Mathf.Pow(p / 0.06f, 2f));
        float dub = 0.6f * Mathf.Exp(-Mathf.Pow((p - 0.2f) / 0.06f, 2f));
        float wrap = Mathf.Exp(-Mathf.Pow((p - 1f) / 0.06f, 2f));
        return Mathf.Clamp01(lub + dub + wrap);
    }
}
