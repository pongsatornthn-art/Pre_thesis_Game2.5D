using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Combat.Gun;

/// <summary>
/// [2026-11-25 นา ข้อ 14] บอกผู้เล่นทุกครั้งที่โดนตี (เดิมโดนตีแล้วเงียบ — มีแค่จอซีดตอนเลือดน้อยของ PlayerConditionHUD)
///   ก. ขอบจอแดงวาบ   — Volume (Global) ที่มี Vignette แดง · น้ำหนักพุ่งขึ้นแล้วค่อยๆ หาย
///   ข. กล้องสั่นสั้นๆ   — ใช้ GunCameraShake ตัวเดิม (ถอนแรงสั่นของตัวเองคืน ไม่ทับระบบสั่นอื่น)
///   ค. ตัวละครกระพริบแดง — ย้อมสี SpriteRenderer ของผู้เล่นชั่วครู่แล้วคืนสีเดิม
/// ฟัง PlayerDamagedEvent อย่างเดียว — ไม่ต้องแก้ไฟล์ผู้เล่นของปอ
///
/// วางในซีน: GameObject "PlayerHitFeedback" + Volume (Is Global ✔ · Priority 60 · Weight 0 · Profile Settings/HitFlashVolumeProfile)
/// + สคริปต์นี้ · ช่อง Player Sprites = SpriteRenderer ตัวผู้เล่น (เว้นว่าง = หาจากตัว Player เอง)
/// </summary>
public class PlayerHitFeedback : MonoBehaviour
{
    [Header("ก. ขอบจอแดง")]
    [SerializeField] private Volume hitVolume;
    [Tooltip("ขอบแดงค้างเต็มกี่วิ ก่อนเริ่มจาง")]
    [SerializeField, Min(0f)] private float flashHold = 0.05f;
    [Tooltip("จางหายภายในกี่วิ")]
    [SerializeField, Min(0.01f)] private float flashFade = 0.35f;

    [Header("ข. กล้องสั่น")]
    [SerializeField] private bool shakeCamera = true;
    [SerializeField, Min(0f)] private float shakeSeconds = 0.18f;
    [Tooltip("แรงสั่น (ยิงปืน = 0.3)")]
    [SerializeField, Min(0f)] private float shakeAmplitude = 1.2f;

    [Header("ค. ตัวกระพริบแดง")]
    [Tooltip("เว้นว่าง = หา SpriteRenderer ทั้งหมดใต้ตัว Player เอง")]
    [SerializeField] private SpriteRenderer[] playerSprites;
    [SerializeField] private Color flashColor = new Color(1f, 0.25f, 0.25f, 1f);
    [SerializeField, Min(0.01f)] private float tintSeconds = 0.25f;

    private Coroutine volumeRoutine;
    private Coroutine tintRoutine;
    private Color[] originalColors;

    private void Awake()
    {
        if (hitVolume == null) hitVolume = GetComponent<Volume>();
        if (hitVolume != null) hitVolume.weight = 0f;
    }

    private void OnEnable() => GameEventBus.Subscribe<PlayerDamagedEvent>(OnDamaged);
    private void OnDisable()
    {
        GameEventBus.Unsubscribe<PlayerDamagedEvent>(OnDamaged);
        RestoreColors();
        if (hitVolume != null) hitVolume.weight = 0f;
    }

    private void OnDamaged(PlayerDamagedEvent e)
    {
        if (e.Damage <= 0) return;

        if (hitVolume != null)
        {
            if (volumeRoutine != null) StopCoroutine(volumeRoutine);
            volumeRoutine = StartCoroutine(FlashVolume());
        }

        if (shakeCamera && GunCameraShake.Instance != null)
            GunCameraShake.Instance.Shake(shakeSeconds, shakeAmplitude);

        if (FindSprites())
        {
            if (tintRoutine != null) { StopCoroutine(tintRoutine); RestoreColors(); }
            tintRoutine = StartCoroutine(TintSprites());
        }
    }

    private IEnumerator FlashVolume()
    {
        hitVolume.weight = 1f;
        if (flashHold > 0f) yield return new WaitForSecondsRealtime(flashHold);
        float t = 0f;
        while (t < flashFade)
        {
            t += Time.unscaledDeltaTime;
            hitVolume.weight = 1f - Mathf.Clamp01(t / flashFade);
            yield return null;
        }
        hitVolume.weight = 0f;
        volumeRoutine = null;
    }

    private IEnumerator TintSprites()
    {
        originalColors = new Color[playerSprites.Length];
        for (int i = 0; i < playerSprites.Length; i++)
            if (playerSprites[i] != null) originalColors[i] = playerSprites[i].color;

        float t = 0f;
        while (t < tintSeconds)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Clamp01(t / tintSeconds);   // แดงสุดตอนโดน แล้วค่อยๆ คืน
            for (int i = 0; i < playerSprites.Length; i++)
                if (playerSprites[i] != null)
                    playerSprites[i].color = Color.Lerp(originalColors[i], originalColors[i] * flashColor, k);
            yield return null;
        }
        RestoreColors();
        tintRoutine = null;
    }

    private void RestoreColors()
    {
        if (originalColors == null || playerSprites == null) return;
        for (int i = 0; i < playerSprites.Length && i < originalColors.Length; i++)
            if (playerSprites[i] != null) playerSprites[i].color = originalColors[i];
        originalColors = null;
    }

    private bool FindSprites()
    {
        if (playerSprites != null && playerSprites.Length > 0) return true;
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) return false;
        playerSprites = p.GetComponentsInChildren<SpriteRenderer>(true);
        return playerSprites.Length > 0;
    }
}
