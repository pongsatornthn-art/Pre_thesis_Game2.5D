using System.Collections;
using UnityEngine;

/// <summary>
/// [Claude 2026-11-28] ชั้นเสียงตอนอยู่ในโลก PTSD (เพิ่มจากเสียงบรรยากาศเดิมของ AmbientZone — ไม่ได้แทน)
///   เข้า PTSD → เสียงหูอื้อ (Tinnitus) ครั้งเดียว + เสียงสวด (Chant) วนเบา ๆ ค่อย ๆ ดังขึ้น
///   ระหว่างอยู่ → เสียงสุ่ม (กระซิบ ฯลฯ) ดังเป็นระยะรอบตัวผู้เล่น
///   ออก PTSD → เสียงสวดค่อย ๆ เงียบ
/// วางในซีน 1 ตัว · ต้องมี AudioSource (ใช้เล่นเสียงสวดแบบวน) · ส่งเข้ากลุ่ม SFX ของ AudioService
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class PtsdAudioLayer : MonoBehaviour
{
    [SerializeField] private AudioClip enterSting;
    [SerializeField, Range(0f, 1f)] private float stingVolume = 0.8f;
    [SerializeField] private AudioClip loopClip;
    [SerializeField, Range(0f, 1f)] private float loopVolume = 0.45f;
    [SerializeField, Min(0.1f)] private float fadeInSeconds = 3f;
    [SerializeField, Min(0.1f)] private float fadeOutSeconds = 1.5f;

    [Header("เสียงสุ่มระหว่างอยู่ในโลก PTSD")]
    [SerializeField] private AudioClip[] randomClips = new AudioClip[0];
    [SerializeField, Range(0f, 1f)] private float randomVolume = 0.6f;
    [Tooltip("สุ่มช่วงห่าง (วินาที) ระหว่างเสียงสุ่ม")]
    [SerializeField] private Vector2 randomInterval = new Vector2(12f, 25f);
    [Tooltip("เกิดห่างจากตัวผู้เล่นกี่เมตร (เสียงรอบทิศ)")]
    [SerializeField, Min(0f)] private float randomDistance = 4f;

    private float nextRandomAt;
    private bool inPtsd;

    private AudioSource loop;
    private Coroutine fade;

    private void Awake()
    {
        loop = GetComponent<AudioSource>();
        loop.clip = loopClip;
        loop.loop = true;
        loop.playOnAwake = false;
        loop.spatialBlend = 0f;
        loop.volume = 0f;
    }

    private void Start()
    {
        if (ServiceLocator.GetOptional<IAudioService>() is AudioService svc && svc.sfxMixerGroup != null)
            loop.outputAudioMixerGroup = svc.sfxMixerGroup;
        // เริ่มซีนมาในโลก PTSD อยู่แล้ว (โหลดเซฟ) → เปิดเสียงสวดเลย ไม่เล่นหูอื้อ
        if (PTSDManager.Instance != null && PTSDManager.Instance.currentMode != PTSDMode.None) { inPtsd = true; FadeTo(loopVolume, fadeInSeconds); }
    }

    private void OnEnable() => PTSDManager.OnPTSDStateChanged += Handle;
    private void OnDisable() => PTSDManager.OnPTSDStateChanged -= Handle;

    private void Update()
    {
        if (!inPtsd || randomClips == null || randomClips.Length == 0 || Time.timeScale == 0f) return;
        if (Time.time < nextRandomAt) return;
        nextRandomAt = Time.time + Random.Range(randomInterval.x, randomInterval.y);
        AudioClip c = randomClips[Random.Range(0, randomClips.Length)];
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Vector3 center = player != null ? player.transform.position : transform.position;
        Vector2 off = Random.insideUnitCircle.normalized * randomDistance;
        ServiceLocator.GetOptional<IAudioService>()?.PlaySFX(c, center + new Vector3(off.x, 1f, off.y), randomVolume);
    }

    private void Handle(bool active)
    {
        inPtsd = active;
        if (active) nextRandomAt = Time.time + Random.Range(randomInterval.x, randomInterval.y);
        if (active)
        {
            if (enterSting != null && !SaveRestoreScope.IsBusy)
            {
                Camera cam = Camera.main;
                ServiceLocator.GetOptional<IAudioService>()?.PlaySFX(enterSting, cam != null ? cam.transform.position : transform.position, stingVolume, false);
            }
            FadeTo(loopVolume, fadeInSeconds);
        }
        else FadeTo(0f, fadeOutSeconds);
    }

    private void FadeTo(float target, float seconds)
    {
        if (loopClip == null) return;
        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(Fade(target, seconds));
    }

    private IEnumerator Fade(float target, float seconds)
    {
        if (target > 0f && !loop.isPlaying) loop.Play();
        float start = loop.volume, t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            loop.volume = Mathf.Lerp(start, target, t / seconds);
            yield return null;
        }
        loop.volume = target;
        if (target <= 0f) loop.Stop();
        fade = null;
    }
}
