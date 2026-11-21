using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class PTSDVisualController : MonoBehaviour
{
    [Header("Post-Processing Core")]
    public Volume ptsdVolume;
    public float fadeSpeed = 2f;

    [Header("Pulse Effect (ขอบดำวูบวาบ)")]
    public bool enablePulse = true;
    [Tooltip("ความเร็วในการเต้นของขอบจอ")]
    public float pulseSpeed = 6f;
    [Tooltip("ความลึกของการหดตัว (0.2 = ขอบดำหดลง 20% แล้วกลับมาเต็ม)")]
    public float pulseAmount = 0.3f;

    [Header("Audio")]
    public AudioSource ptsdAudio;
    public float maxAudioVolume = 1f;

    private Coroutine visualCoroutine;
    private bool isPTSDActive = false;

    private void OnEnable()
    {
        PTSDManager.OnPTSDStateChanged += HandleStressVisuals;
    }

    private void OnDisable()
    {
        PTSDManager.OnPTSDStateChanged -= HandleStressVisuals;
    }

    private void HandleStressVisuals(bool isActive)
    {
        isPTSDActive = isActive;
        if (visualCoroutine != null) StopCoroutine(visualCoroutine);
        RestoreVignette();   // [2026-11-21] หยุดกลางจังหวะเต้น → คืนขอบดำเป็นค่าเดิมก่อน

        if (isActive && ptsdAudio != null && !ptsdAudio.isPlaying)
        {
            ptsdAudio.volume = 0f;
            ptsdAudio.Play();
        }

        visualCoroutine = StartCoroutine(FadeAndPulseRoutine(isActive ? 1f : 0f));
    }

    // [2026-11-21] ใช้ .profile (สำเนาตอนเล่น) ไม่ใช่ .sharedProfile → แก้ค่าระหว่างเล่นไม่ไปเขียนทับไฟล์ profile จริง
    private Vignette runtimeVignette;
    private float baseVignette;   // ค่าขอบดำตั้งต้น (จำครั้งเดียว — กันค่าไหลลงทุกรอบที่เข้า-ออก PTSD)
    private Vignette GetRuntimeVignette()
    {
        if (runtimeVignette == null && ptsdVolume != null && ptsdVolume.profile.TryGet(out runtimeVignette))
            baseVignette = runtimeVignette.intensity.value;
        return runtimeVignette;
    }

    private void RestoreVignette()
    {
        if (runtimeVignette != null) runtimeVignette.intensity.value = baseVignette;
    }

    private IEnumerator FadeAndPulseRoutine(float targetWeight)
    {
        float currentBaseWeight = ptsdVolume != null ? ptsdVolume.weight : 0f;

        // 1. ช่วงเฟดภาพเข้า / เฟดภาพออก
        while (!Mathf.Approximately(currentBaseWeight, targetWeight))
        {
            currentBaseWeight = Mathf.MoveTowards(currentBaseWeight, targetWeight, fadeSpeed * Time.deltaTime);

            if (ptsdVolume != null) ptsdVolume.weight = currentBaseWeight;
            if (ptsdAudio != null) ptsdAudio.volume = currentBaseWeight * maxAudioVolume;

            yield return null;
        }

        // 2. ช่วงจอมืดเต็มที่ -> เริ่มทำการวูบวาบ (Pulse)
        // [แก้ 2026-11-21 นา] เดิมเต้นด้วย ptsdVolume.weight → โทนสีทั้งจอ (เกรดสีแบบหนัง) เต้นตามไปด้วย
        //   ตอนนี้: weight ค้างที่ 1 (สีนิ่ง) แล้วเต้นเฉพาะความแรงขอบดำ (Vignette) แทน — ผลที่เห็นเหมือนเดิม
        if (isPTSDActive && enablePulse && ptsdVolume != null)
        {
            Vignette vignette = GetRuntimeVignette();

            while (isPTSDActive)
            {
                // สร้างคลื่นความถี่ขึ้นลงตามเวลา
                float wave = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f;
                if (vignette != null) vignette.intensity.value = baseVignette * (1f - wave * pulseAmount);
                yield return null;
            }

            RestoreVignette();   // คืนค่าเดิมก่อนเฟดออก
        }

        if (!isPTSDActive && ptsdAudio != null) ptsdAudio.Stop();
    }
}