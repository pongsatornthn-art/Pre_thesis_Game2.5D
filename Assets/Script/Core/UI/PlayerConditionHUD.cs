using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// HUD แบบเกมสยองขวัญ (ไม่มีหลอดบนจอ) — งานเพลย์เทส 2026-11-11 ข้อ 8
/// เลือดลด  → จอค่อยๆ ซีดเทา + ขอบจอแดงเข้ม (Volume) · ต่ำมาก = เสียงหัวใจเต้น · เลือดไม่ฟื้นเอง ต้องใช้ยา
/// สตามิน่า → เสียงหอบ (ดังขึ้นตอนเหลือน้อย/หมด) · ตีตอนสตามิน่าไม่พอ = หอบ 1 ที
/// หลอดเลือด/สตามิน่าเดิม เก็บไว้ดูตอนเทส (ช่อง Show Debug Bars) — บิลด์จริงปิด
/// ทุกอย่างอยู่ในซีน (Volume / AudioSource) แก้ค่าได้ใน Inspector
/// [Claude 2026-11-12]
/// </summary>
public class PlayerConditionHUD : MonoBehaviour
{
    [SerializeField] private PlayerMovement player;

    [Header("เลือด → จอเทา")]
    [Tooltip("Volume (Global) ที่มีลดสี + ขอบจอแดง — น้ำหนักขึ้นตามเลือดที่หาย")]
    [SerializeField] private Volume lowHealthVolume;
    [Tooltip("เลือดต่ำกว่านี้ (สัดส่วน 0-1) จอเริ่มซีด")]
    [SerializeField, Range(0f, 1f)] private float greyStartRatio = 0.7f;
    [Tooltip("เลือดเหลือเท่านี้ = ซีดเต็มที่")]
    [SerializeField, Range(0f, 1f)] private float greyFullRatio = 0.15f;
    [SerializeField, Min(0.1f)] private float greyFadeSpeed = 2f;

    [Header("เลือดต่ำมาก → เสียงหัวใจ")]
    [SerializeField] private AudioSource heartbeat;
    [SerializeField, Range(0f, 1f)] private float heartbeatBelowRatio = 0.35f;
    [SerializeField, Range(0f, 1f)] private float heartbeatMaxVolume = 0.8f;

    [Header("สตามิน่า → เสียงหอบ")]
    [Tooltip("AudioSource เสียงหอบแบบวนลูป (ยังไม่มีไฟล์เสียง — ใส่ทีหลังได้)")]
    [SerializeField] private AudioSource panting;
    [Tooltip("สตามิน่าต่ำกว่านี้ เริ่มได้ยินเสียงหอบ")]
    [SerializeField, Range(0f, 1f)] private float pantBelowRatio = 0.35f;
    [SerializeField, Range(0f, 1f)] private float pantMaxVolume = 0.9f;
    [Tooltip("เสียงหอบสั้นตอนกดตีแต่สตามิน่าไม่พอ (เว้นว่างได้)")]
    [SerializeField] private AudioClip outOfStaminaGasp;

    [Header("หลอดเดิม (ไว้ดูตอนเทส)")]
    [Tooltip("ก้อนหลอดเลือด/สตามิน่าเดิม")]
    [SerializeField] private GameObject[] debugBars;
    [Tooltip("เปิด = โชว์หลอดไว้ดูตอนเทส · ปิด = ซ่อน (บิลด์จริง)")]
    [SerializeField] private bool showDebugBars = true;

    private float greyWeight;

    private void Awake()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.GetComponent<PlayerMovement>();
        }
        if (lowHealthVolume != null) lowHealthVolume.weight = 0f;
        ApplyDebugBars();
    }

    private void OnEnable() => PlayerCombat.OnOutOfStamina += HandleOutOfStamina;
    private void OnDisable() => PlayerCombat.OnOutOfStamina -= HandleOutOfStamina;

    private void OnValidate() => ApplyDebugBars();

    private void ApplyDebugBars()
    {
        if (debugBars == null) return;
        foreach (GameObject g in debugBars) if (g != null && g.activeSelf != showDebugBars) g.SetActive(showDebugBars);
    }

    private void Update()
    {
        if (player == null) return;

        // เลือด → จอเทา (ค่อยๆ เปลี่ยน ไม่กระตุก)
        float hp = player.HealthRatio;
        float target = Mathf.InverseLerp(greyStartRatio, greyFullRatio, hp);
        greyWeight = Mathf.MoveTowards(greyWeight, target, greyFadeSpeed * Time.unscaledDeltaTime);
        if (lowHealthVolume != null) lowHealthVolume.weight = greyWeight;

        bool paused = Time.timeScale == 0f;

        // หัวใจเต้น
        if (heartbeat != null)
        {
            float hv = hp > 0f && hp < heartbeatBelowRatio ? Mathf.Lerp(heartbeatMaxVolume, heartbeatMaxVolume * 0.3f, hp / heartbeatBelowRatio) : 0f;
            SetLoop(heartbeat, paused ? 0f : hv);
        }

        // หอบ
        if (panting != null)
        {
            float st = player.StaminaRatio;
            float pv = player.IsExhausted ? pantMaxVolume
                     : st < pantBelowRatio ? Mathf.Lerp(pantMaxVolume, 0f, st / pantBelowRatio) : 0f;
            SetLoop(panting, paused ? 0f : pv);
        }
    }

    private static void SetLoop(AudioSource src, float volume)
    {
        if (src.clip == null) return;
        src.volume = Mathf.MoveTowards(src.volume, volume, 1.5f * Time.unscaledDeltaTime);
        if (src.volume > 0.01f && !src.isPlaying) src.Play();
        else if (src.volume <= 0.01f && src.isPlaying) src.Stop();
    }

    private void HandleOutOfStamina()
    {
        if (outOfStaminaGasp == null) return;
        if (panting != null) panting.PlayOneShot(outOfStaminaGasp);
        else ServiceLocator.GetOptional<IAudioService>()?.PlaySFX(outOfStaminaGasp, transform.position, 1f, true);
    }
}
