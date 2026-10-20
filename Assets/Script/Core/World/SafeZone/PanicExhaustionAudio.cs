using System.Collections;
using UnityEngine;

/// <summary>
/// ระบบเสียงบรรยากาศช่วงสภาวะตื่นตระหนก/หมดพลังใจ (Panic Exhaustion)
/// เล่นเสียงหายใจหอบ คลื่นวิทยุรบกวน และเสียงหัวใจเต้นวนซ้ำ พร้อมระบบ Fade เข้า-ออก
/// </summary>
public class PanicExhaustionAudio : MonoBehaviour
{
    [Header("คลิปเสียง (เว้นว่างได้)")]
    [Tooltip("เสียงหายใจหอบวนซ้ำ")]
    [SerializeField] private AudioClip breathingLoop;

    [Tooltip("เสียงคลื่นวิทยุรบกวน/เสียงวิ้งวนซ้ำ")]
    [SerializeField] private AudioClip radioStaticLoop;

    [Tooltip("เสียงหัวใจเต้นเร็ววนซ้ำ")]
    [SerializeField] private AudioClip heartbeatLoop;

    [Header("AudioSource ในฉาก (ห้าม AddComponent ตอนเล่น)")]
    [Tooltip("AudioSource สำหรับเสียงหายใจ")]
    [SerializeField] private AudioSource breathingSource;

    [Tooltip("AudioSource สำหรับเสียงคลื่นวิทยุ")]
    [SerializeField] private AudioSource radioStaticSource;

    [Tooltip("AudioSource สำหรับเสียงหัวใจ")]
    [SerializeField] private AudioSource heartbeatSource;

    [Header("การตั้งค่า")]
    [Tooltip("ระยะเวลาในการค่อยๆ เพิ่ม/ลดระดับเสียง (วินาที)")]
    [SerializeField, Min(0.1f)] private float fadeDuration = 0.5f;

    [Tooltip("ระดับเสียงเป้าหมายสูงสุด")]
    [SerializeField, Range(0f, 1f)] private float maxVolume = 1f;

    private Coroutine currentFadeRoutine;

    private void OnEnable()
    {
        GameEventBus.Subscribe<PanicExhaustionChangedEvent>(HandlePanicExhaustionChanged);
    }

    private void OnDisable()
    {
        GameEventBus.Unsubscribe<PanicExhaustionChangedEvent>(HandlePanicExhaustionChanged);
        StopAllAudioImmediate();
    }

    private void HandlePanicExhaustionChanged(PanicExhaustionChangedEvent e)
    {
        if (currentFadeRoutine != null)
            StopCoroutine(currentFadeRoutine);

        if (e.Active)
            currentFadeRoutine = StartCoroutine(FadeRoutine(true));
        else
            currentFadeRoutine = StartCoroutine(FadeRoutine(false));
    }

    private IEnumerator FadeRoutine(bool fadeIn)
    {
        if (fadeIn)
        {
            PrepareSource(breathingSource, breathingLoop);
            PrepareSource(radioStaticSource, radioStaticLoop);
            PrepareSource(heartbeatSource, heartbeatLoop);
        }

        float startVolumeBreathing = breathingSource != null ? breathingSource.volume : 0f;
        float startVolumeRadio = radioStaticSource != null ? radioStaticSource.volume : 0f;
        float startVolumeHeart = heartbeatSource != null ? heartbeatSource.volume : 0f;

        float targetVolume = fadeIn ? maxVolume : 0f;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);

            if (breathingSource != null && breathingLoop != null)
                breathingSource.volume = Mathf.Lerp(startVolumeBreathing, targetVolume, t);

            if (radioStaticSource != null && radioStaticLoop != null)
                radioStaticSource.volume = Mathf.Lerp(startVolumeRadio, targetVolume, t);

            if (heartbeatSource != null && heartbeatLoop != null)
                heartbeatSource.volume = Mathf.Lerp(startVolumeHeart, targetVolume, t);

            yield return null;
        }

        if (breathingSource != null && breathingLoop != null) breathingSource.volume = targetVolume;
        if (radioStaticSource != null && radioStaticLoop != null) radioStaticSource.volume = targetVolume;
        if (heartbeatSource != null && heartbeatLoop != null) heartbeatSource.volume = targetVolume;

        if (!fadeIn)
        {
            if (breathingSource != null) breathingSource.Stop();
            if (radioStaticSource != null) radioStaticSource.Stop();
            if (heartbeatSource != null) heartbeatSource.Stop();
        }

        currentFadeRoutine = null;
    }

    private void PrepareSource(AudioSource source, AudioClip clip)
    {
        if (source == null || clip == null) return;

        if (source.clip != clip) source.clip = clip;
        source.loop = true;
        if (!source.isPlaying)
        {
            source.volume = 0f;
            source.Play();
        }
    }

    private void StopAllAudioImmediate()
    {
        if (currentFadeRoutine != null)
        {
            StopCoroutine(currentFadeRoutine);
            currentFadeRoutine = null;
        }

        if (breathingSource != null) { breathingSource.volume = 0f; breathingSource.Stop(); }
        if (radioStaticSource != null) { radioStaticSource.volume = 0f; radioStaticSource.Stop(); }
        if (heartbeatSource != null) { heartbeatSource.volume = 0f; heartbeatSource.Stop(); }
    }
}
