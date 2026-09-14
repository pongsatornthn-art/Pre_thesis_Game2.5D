using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class PTSDVisualController : MonoBehaviour
{
    [Header("Post-Processing Core")]
    public Volume ptsdVolume;
    public float fadeSpeed = 1.5f;

    [Header("Pulse Settings (ชีพจรขอบจอ)")]
    [Tooltip("ความเร็วพื้นฐานการเต้นของขอบจอ")]
    public float basePulseSpeed = 4f;
    [Tooltip("ความแรงของการหด/ขยาย (ยิ่งมาก ขอบจอยิ่งกระเพื่อมแรง)")]
    public float pulseMagnitude = 0.15f;

    [Header("Audio")]
    public AudioSource ptsdAudio;
    public float maxAudioVolume = 1f;

    private Coroutine visualCoroutine;
    private float currentStressWeight = 0f;

    private Vignette vignette;
    private float initialVignetteIntensity;

    private void Start()
    {
        if (ptsdVolume != null && ptsdVolume.profile != null)
        {
            if (ptsdVolume.profile.TryGet(out vignette))
            {
                initialVignetteIntensity = vignette.intensity.value;
            }
        }
    }

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
        if (visualCoroutine != null) StopCoroutine(visualCoroutine);

        float targetWeight = isActive ? 1f : 0f;

        if (isActive && ptsdAudio != null && !ptsdAudio.isPlaying)
        {
            ptsdAudio.volume = 0f;
            ptsdAudio.Play();
        }

        visualCoroutine = StartCoroutine(FadePTSDVolume(targetWeight));
    }

    private IEnumerator FadePTSDVolume(float targetWeight)
    {
        while (!Mathf.Approximately(currentStressWeight, targetWeight))
        {
            currentStressWeight = Mathf.MoveTowards(currentStressWeight, targetWeight, fadeSpeed * Time.deltaTime);

            if (ptsdAudio != null)
            {
                ptsdAudio.volume = currentStressWeight * maxAudioVolume;
            }

            yield return null;
        }

        if (currentStressWeight == 0f && ptsdAudio != null)
        {
            ptsdAudio.Stop();
        }
    }

    private void Update()
    {
        if (ptsdVolume != null)
        {
            ptsdVolume.weight = currentStressWeight;

            if (vignette != null && currentStressWeight > 0f)
            {
                float currentPulseSpeed = basePulseSpeed * (1f + currentStressWeight);

                float pulse = Mathf.Sin(Time.time * currentPulseSpeed) * (pulseMagnitude * currentStressWeight);

                vignette.intensity.value = Mathf.Clamp(initialVignetteIntensity + pulse, 0f, 1f);
            }
        }
    }
}