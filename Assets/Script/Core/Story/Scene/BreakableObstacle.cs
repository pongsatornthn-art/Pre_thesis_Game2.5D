using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// ของที่ "ทำลายได้" — ใช้ได้ 2 แบบ:
///   • กองของพังขวางทาง → ตั้ง Hit By = Melee (ต้องถือท่อตี · ยิงปืนใส่ไม่แตก)
///   • แม่กุญแจที่ประตู   → ตั้ง Hit By = Gun (ต้องยิง · เอาท่อตีไม่หลุด) + ลากประตูใส่ช่อง Unlock Door
///
/// รับดาเมจผ่าน IDamageable ตัวเดียวกับที่ท่อ/ปืนใช้ตีศัตรูอยู่แล้ว → ไม่ต้องแก้โค้ดอาวุธ
/// แยกว่าโดนอะไรจาก "ผู้เล่นถืออะไรอยู่" (PlayerCombat.HasRangedWeaponEquipped)
///
/// จำสถานะผ่านธงเรื่องราว (IStoryFlags ซึ่งเซฟอยู่แล้ว) → โหลดเซฟแล้วของที่พังไปแล้วไม่กลับมา
///
/// ⚠️ ท่อเช็ค "Collider บนตัวเดียวกับสคริปต์นี้" เท่านั้น (MeleeHitbox ใช้ GetComponent)
///    → ใส่ BoxCollider ครอบทั้งกองไว้ที่ตัวนี้ (Is Trigger = ไม่ติ๊ก) ลูกข้างในไม่ต้องมี Collider
/// [Claude 2026-11-07]
/// </summary>
public class BreakableObstacle : MonoBehaviour, IDamageable
{
    public enum HitBy { Melee, Gun, Any }

    [Header("ทำลายด้วยอะไร")]
    [Tooltip("Melee = ต้องถือท่อตี · Gun = ต้องยิง · Any = อะไรก็ได้")]
    public HitBy hitBy = HitBy.Melee;

    [Tooltip("ตีกี่ครั้งถึงแตก (ตีแบบกดค้างนับ 2 ครั้ง)")]
    [Min(1)] public int hitsToBreak = 3;

    [Header("ทุบด้วยปุ่ม E (ถือท่ออยู่ · ไม่เสียสตามินา) [นา 2026-11-28]")]
    [Tooltip("ผู้เล่นอยู่ใกล้กี่เมตร (วัดถึงขอบ Collider) ถึงขึ้นป้าย [E] ทุบ · ใช้กับ Hit By = Melee / Any")]
    public float smashRadius = 1.8f;

    [Tooltip("กด E ซ้ำได้ทุกกี่วินาที")]
    public float smashCooldown = 0.45f;

    [Tooltip("ท่าฟันเริ่มแล้วรอกี่วินาทีถึงนับเป็น 1 ครั้งที่โดน")]
    public float smashHitDelay = 0.2f;

    [Tooltip("คีย์ข้อความบนป้าย (LocalizationData.csv)")]
    public string smashPromptKey = "PROMPT_SMASH";

    [Tooltip("ป้ายลอยสูงจากตัวของกี่เมตร")]
    public float smashPromptHeight = 1.5f;

    [Header("จำสถานะ (เซฟ)")]
    [Tooltip("ธงที่ขึ้นเมื่อพังแล้ว — เว้นว่างได้ จะใช้ Save Key ข้างล่างแทน")]
    public StoryFlagId brokenFlag;

    [Tooltip("รหัสจำสถานะ (สร้างให้อัตโนมัติ ห้ามซ้ำกับชิ้นอื่น · ถ้า Duplicate มาให้กด ⋮ > Reset หรือลบทิ้งให้สร้างใหม่)")]
    public string saveKey;

    [Header("ตอนพัง")]
    [Tooltip("ของที่จะหายไปเมื่อพัง (เว้นว่าง = ตัวนี้เอง)")]
    public GameObject hideOnBreak;

    [Tooltip("ประตูที่ปลดล็อกเมื่อพัง (ใช้กับแม่กุญแจ)")]
    public DoorController unlockDoor;

    [Tooltip("เอฟเฟกต์ตอนแตก (ฝุ่น/เศษไม้) — ต้องวางเป็น 'พี่น้อง' ไม่ใช่ลูก ไม่งั้นหายไปพร้อมกัน")]
    public ParticleSystem breakFx;

    public AudioClip hitSfx;
    public AudioClip breakSfx;

    [Header("ตอนโดนตี")]
    [Tooltip("ตัวที่จะสั่นตอนโดนตี (เว้นว่าง = ไม่สั่น)")]
    public Transform shakeTarget;
    public float shakeAmount = 0.08f;
    public float shakeTime = 0.2f;

    [Tooltip("ตีด้วยของผิดประเภท (เช่น ยิงกองของพัง) → เล่นบทพูดใบ้นี้ครั้งเดียว")]
    public StorySequence wrongWeaponHint;

    [Header("เหตุการณ์")]
    public UnityEvent onHit;
    public UnityEvent onBroken;

    public bool IsBroken { get; private set; }

    private const float MinHitInterval = 0.3f;
    private float lastHitTime = -10f;
    private int hitsTaken;
    private bool hintPlayed;
    private PlayerCombat playerCombat;
    private Coroutine shakeRoutine;
    private float lastSmashTime = -10f;
    private Collider ownCollider;

    private void Reset()
    {
        saveKey = "break_" + System.Guid.NewGuid().ToString("N").Substring(0, 10);
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(saveKey)) Reset();
    }

    private void OnEnable()
    {
        StartCoroutine(SyncNextFrame());
    }

    // รอให้ระบบโหลดเซฟใส่ธงคืนครบก่อน (แบบเดียวกับ StoryFlagListener)
    private IEnumerator SyncNextFrame()
    {
        yield return null;
        while (SaveRestoreScope.IsBusy) yield return null;

        IStoryFlags flags = ServiceLocator.GetOptional<IStoryFlags>();
        if (flags != null && HasBrokenFlag(flags))
        {
            IsBroken = true;
            ApplyBroken(playEffects: false);
        }
    }

    // ===== กด E ทุบ [นา 2026-11-28] =====
    // เดิมตีแล้วเสียสตามินา (PlayerCombat) → ตอนนี้ถือท่อเดินเข้าใกล้ = ป้าย "[E] ทุบ" · กด E = ฟัน 1 ครั้ง ไม่เสียสตามินา
    // ไม่แตะ PlayerCombat: แค่สั่ง trigger "Attack" บน Animator เดียวกับที่ท่อใช้ (ดาเมจจริงมาจาก RegisterHit ของเราเอง ไม่ผ่าน hitbox)
    private void Update()
    {
        if (IsBroken || hitBy == HitBy.Gun) { HideSmashPrompt(); return; }

        IInteractPromptService prompt = ServiceLocator.GetOptional<IInteractPromptService>();
        if (playerCombat == null) playerCombat = FindFirstObjectByType<PlayerCombat>();
        if (playerCombat == null || !IsPlayerInRange() || InputBlocked()) { HideSmashPrompt(); return; }

        bool holdingMelee = Inventory.Instance != null && Inventory.Instance.currentEquippedItem != null
                            && Inventory.Instance.currentEquippedItem.itemType == ItemType.MeleeWeapon;

        if (!holdingMelee)
        {
            // ไม่ได้ถือท่อ: ไม่ขึ้นป้าย · กด E = เล่นบทพูดใบ้ (ถ้าตั้งไว้ · ครั้งเดียว)
            HideSmashPrompt();
            if (InteractInput.PickupPressed && !hintPlayed && wrongWeaponHint != null && StoryDirector.Instance != null)
            {
                hintPlayed = true;
                StoryDirector.Instance.Play(wrongWeaponHint);
            }
            return;
        }

        if (prompt != null) prompt.Show(this, transform, Vector3.up * smashPromptHeight, PromptKey.Pickup, smashPromptKey);

        if (InteractInput.PickupPressed && Time.time - lastSmashTime >= smashCooldown
            && (prompt == null || !prompt.IsOtherShown(this, PromptKey.Pickup)))
        {
            lastSmashTime = Time.time;
            if (playerCombat.animator != null) playerCombat.animator.SetTrigger("Attack");
            StartCoroutine(SmashHitRoutine());
        }
    }

    private IEnumerator SmashHitRoutine()
    {
        if (smashHitDelay > 0f) yield return new WaitForSeconds(smashHitDelay);
        RegisterHit(false);
    }

    private bool InputBlocked()
    {
        if (GameplayInputGate.Blocked) return true;
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return true;
        if (StoryDirector.Instance != null && StoryDirector.Instance.IsPlaying) return true;
        IDialogueService dialogue = ServiceLocator.GetOptional<IDialogueService>();
        return dialogue != null && dialogue.IsPlaying;
    }

    private bool IsPlayerInRange()
    {
        if (ownCollider == null) ownCollider = GetComponent<Collider>();
        Vector3 p = playerCombat.transform.position;
        Vector3 q = ownCollider != null ? ownCollider.ClosestPoint(p) : transform.position;
        p.y = 0f; q.y = 0f;
        return (p - q).sqrMagnitude <= smashRadius * smashRadius;
    }

    private void HideSmashPrompt()
    {
        ServiceLocator.GetOptional<IInteractPromptService>()?.Hide(this);
    }

    private void OnDisable()
    {
        HideSmashPrompt();
    }

    // ===== IDamageable =====
    public void TakeDamage(int damageAmount, float knockback, bool isHeavyAttack = false) => RegisterHit(isHeavyAttack);
    public void TakeDamage(int attackDamage, bool isHeavyAttack = false) => RegisterHit(isHeavyAttack);
    public void Die() => Break();

    private void RegisterHit(bool isHeavy)
    {
        if (IsBroken) return;

        // ท่อ 1 ครั้งบางทีส่งดาเมจเข้ามา 2 รอบ (hitbox เปิดค้างหลายเฟรม) → นับครั้งเดียวต่อการตี
        if (Time.time - lastHitTime < MinHitInterval) return;
        lastHitTime = Time.time;

        if (!AcceptsCurrentWeapon())
        {
            Shake();
            if (!hintPlayed && wrongWeaponHint != null && StoryDirector.Instance != null)
            {
                hintPlayed = true;
                StoryDirector.Instance.Play(wrongWeaponHint);
            }
            return;
        }

        hitsTaken += isHeavy ? 2 : 1;
        PlaySfx(hitSfx);
        Shake();
        onHit?.Invoke();

        if (hitsTaken >= hitsToBreak) Break();
    }

    private bool AcceptsCurrentWeapon()
    {
        if (hitBy == HitBy.Any) return true;

        if (playerCombat == null) playerCombat = FindFirstObjectByType<PlayerCombat>();
        bool holdingGun = playerCombat != null && playerCombat.HasRangedWeaponEquipped;

        return hitBy == HitBy.Gun ? holdingGun : !holdingGun;
    }

    /// <summary>ทำลายทันที (เรียกจาก UnityEvent ได้)</summary>
    public void Break()
    {
        if (IsBroken) return;
        IsBroken = true;

        IStoryFlags flags = ServiceLocator.GetOptional<IStoryFlags>();
        if (flags != null)
        {
            if (brokenFlag != null) flags.Set(brokenFlag);
            else if (!string.IsNullOrEmpty(saveKey)) flags.SetKey(saveKey);
        }

        ApplyBroken(playEffects: true);
        onBroken?.Invoke();
    }

    private void ApplyBroken(bool playEffects)
    {
        if (unlockDoor != null) unlockDoor.isUnlocked = true;

        if (playEffects)
        {
            PlaySfx(breakSfx);
            if (breakFx != null) breakFx.Play();
        }

        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        (hideOnBreak != null ? hideOnBreak : gameObject).SetActive(false);
    }

    private bool HasBrokenFlag(IStoryFlags flags)
    {
        if (brokenFlag != null) return flags.Has(brokenFlag);
        return !string.IsNullOrEmpty(saveKey) && flags.HasKey(saveKey);
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null) return;
        IAudioService audio = ServiceLocator.GetOptional<IAudioService>();
        if (audio != null) audio.PlaySFX(clip, transform.position);
    }

    private void Shake()
    {
        if (shakeTarget == null || !isActiveAndEnabled) return;
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeRoutine());
    }

    private Vector3 shakeBase;
    private bool shakeBaseSet;

    private IEnumerator ShakeRoutine()
    {
        if (!shakeBaseSet) { shakeBase = shakeTarget.localPosition; shakeBaseSet = true; }

        float t = 0f;
        while (t < shakeTime)
        {
            t += Time.deltaTime;
            float k = 1f - t / shakeTime;
            shakeTarget.localPosition = shakeBase + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * shakeAmount * k;
            yield return null;
        }
        shakeTarget.localPosition = shakeBase;
        shakeRoutine = null;
    }
}
