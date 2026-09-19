using UnityEngine;
using UnityEngine.Rendering;
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

        if (isActive && ptsdAudio != null && !ptsdAudio.isPlaying)
        {
            ptsdAudio.volume = 0f;
            ptsdAudio.Play();
        }

        visualCoroutine = StartCoroutine(FadeAndPulseRoutine(isActive ? 1f : 0f));
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
        if (isPTSDActive && enablePulse && ptsdVolume != null)
        {
            while (isPTSDActive)
            {
                // สร้างคลื่นความถี่ขึ้นลงตามเวลา
                float wave = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f;
                ptsdVolume.weight = 1f - (wave * pulseAmount);
                yield return null;
            }
        }

        if (!isPTSDActive && ptsdAudio != null) ptsdAudio.Stop();
    }
}