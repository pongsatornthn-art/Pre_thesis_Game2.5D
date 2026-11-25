using UnityEngine;

/// <summary>
/// [2026-11-21] มินิเกม "สะบัดหลุด" ตอนโดนมินเนียนตะครุบ (เอกสาร Minions หัวข้อ กลไกสะบัดตัว)
///
/// วางในซีน 1 ตัว (ซีนนี้อยู่ที่ UI_Manager/GrabStrugglePanel) · StateGrabScream เรียก Begin(ai) ตอนจับโดน
///
/// โดนจับ: ผู้เล่นขยับไม่ได้ + เสียเลือด grabDamage (30) ทันที + สตามิน่าหยุดฟื้น (ล็อกตัวผู้เล่น = PlayerMovement ไม่ฟื้นสตามิน่าเองอยู่แล้ว)
///         + มินเนียนกรีดร้องเรียกบอส (ShoutForStalker → บอสในรัศมีได้ยินแล้ววิ่งมา)
/// มินิเกม: ขีดวิ่งซ้าย→ขวา · คลิกซ้ายตอนขีดอยู่ในช่องเป้า
///   โดน  = สะบัดหลุด → มินเนียนกระเด็น + มึน 1.5 วิ + ท่านี้คูลดาวน์ 8 วิ
///   พลาด = เสียเลือด grabMissDamage (10) + ขีดเริ่มวิ่งใหม่ + ช่องเป้าย้ายที่
/// ยกเลิกเอง: ผู้เล่นตาย (เข้า Game Over ตามปกติ) · บอสมาจับต่อ (มินิเกมหน้ามือผีของปอ) · มินเนียนหายไป
/// </summary>
public class MinionGrabStruggle : MonoBehaviour
{
    public static MinionGrabStruggle Instance { get; private set; }

    /// <summary>กำลังโดนจับอยู่ (GameplayInputGate ใช้กันปุ่มอื่น เช่น คลิกซ้ายยิงปืน/ตี)</summary>
    public static bool AnyActive => Instance != null && Instance.active;

    [Header("UI (ก๊อปหน้าตาจากมินิเกมหายใจ)")]
    [Tooltip("แผงทั้งก้อน (ต้องมี UIPanelController + CanvasGroup)")]
    [SerializeField] private UIPanelController panel;
    [Tooltip("แถบพื้นหลัง — ความกว้างของตัวนี้ = ระยะที่ขีดวิ่ง")]
    [SerializeField] private RectTransform gaugeBackground;
    [Tooltip("ช่องเป้า (ลูกของแถบพื้นหลัง)")]
    [SerializeField] private RectTransform targetZone;
    [Tooltip("ขีดตัววิ่ง (ลูกของแถบพื้นหลัง)")]
    [SerializeField] private RectTransform cursor;
    [Tooltip("รูปขีดตัววิ่ง — กระพริบแดงตอนกดพลาด (เว้นว่างได้)")]
    [SerializeField] private UnityEngine.UI.Image cursorImage;

    [Header("ตำแหน่ง (2026-11-24 นา: ลอยเหนือหัวผู้เล่น)")]
    [Tooltip("ก้อน UI ที่จะย้ายไปลอยเหนือหัว (แถบ + ข้อความ) — เว้นว่าง = อยู่ที่เดิมบนจอ")]
    [SerializeField] private RectTransform followRoot;
    [Tooltip("สูงจากเท้าผู้เล่นกี่เมตร")]
    [SerializeField] private float headHeight = 2.3f;
    [Tooltip("เลื่อนบนจอเพิ่ม (พิกเซล) — ค่าบวก = ขึ้น")]
    [SerializeField] private Vector2 screenOffset = new Vector2(0f, 30f);

    [Header("ความยาก")]
    [Tooltip("ขีดวิ่งเร็วกี่ % ของแถบต่อวินาที (100 = วิ่งสุดแถบใน 1 วิ)")]
    [SerializeField] private float cursorSpeed = 85f;
    [Tooltip("ความกว้างช่องเป้า (% ของแถบ)")]
    [SerializeField, Range(5f, 60f)] private float zoneWidth = 18f;
    [Tooltip("ช่องเป้าสุ่มตำแหน่งกลางอยู่ในช่วงนี้ (% ของแถบ) — ไม่ให้อยู่ซ้ายสุดจนกดทันที")]
    [SerializeField] private Vector2 zoneCenterRange = new Vector2(35f, 85f);

    [Header("เสียง (เว้นว่างได้)")]
    [SerializeField] private AudioClip grabbedSfx;
    [SerializeField] private AudioClip missSfx;
    [SerializeField] private AudioClip breakFreeSfx;

    private bool active;
    private PTSDMonsterAI minion;
    private PlayerMovement player;
    private float value;          // 0..100 ตำแหน่งขีด
    private float zoneMin, zoneMax;
    private float missFlashUntil;
    private Color cursorBaseColor = Color.white;

    private void Awake()
    {
        Instance = this;
        if (cursorImage != null) cursorBaseColor = cursorImage.color;
    }

    private void Start()
    {
        if (panel != null) panel.HideImmediate();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable() => GameEventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);

    private void OnDisable()
    {
        GameEventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
        if (active) End(false);
    }

    /// <summary>เริ่มโดนจับ — คืน false ถ้าเริ่มไม่ได้ (มีคนจับอยู่แล้ว / หาผู้เล่นไม่เจอ)</summary>
    public bool Begin(PTSDMonsterAI by)
    {
        if (active || by == null) return false;
        player = by.Player != null ? by.Player : FindFirstObjectByType<PlayerMovement>();
        if (player == null || player.CurrentHealth <= 0) return false;

        active = true;
        minion = by;
        player.PushActionLock();                 // ยืนนิ่ง + สตามิน่าหยุดฟื้น (PlayerMovement ข้ามการฟื้นตอนล็อก)
        PlaySfx(grabbedSfx);
        by.ShoutForStalker();                    // GDD: โดนจับปุ๊บ กรีดร้องเรียกบอสทันที
        player.TakeDamage(by.grabDamage);        // GDD: เสีย 30 ทันที (ถ้าตายตรงนี้ OnPlayerDied จะปิดให้)

        if (!active) return true;                // ตายจากดาเมจจับแล้ว — ปิดไปแล้ว
        NewRound();
        if (panel != null) panel.Show();
        return true;
    }

    /// <summary>มินเนียนตัวนี้ออกจากท่าจับกลางคัน (ตาย/โดนเปลี่ยน state) → ปล่อยผู้เล่น</summary>
    public void CancelIfHeldBy(PTSDMonsterAI ai)
    {
        if (active && minion == ai) End(false, notifyMinion: false);
    }

    // [2026-11-24] ตามหัวผู้เล่นบนจอทุกเฟรม (หลังกล้องขยับเสร็จ)
    private void LateUpdate()
    {
        if (!active || followRoot == null || player == null) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 sp = cam.WorldToScreenPoint(player.transform.position + Vector3.up * headHeight);
        if (sp.z <= 0f) return;
        Vector2 screen = new Vector2(sp.x, sp.y) + screenOffset;

        RectTransform parent = followRoot.parent as RectTransform;
        Canvas canvas = followRoot.GetComponentInParent<Canvas>();
        Camera uiCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (parent != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCam, out Vector2 local))
        {
            // ยึดกึ่งกลาง → ตั้ง anchoredPosition เทียบกลาง parent ได้ตรงๆ
            followRoot.anchorMin = followRoot.anchorMax = new Vector2(0.5f, 0.5f);
            followRoot.anchoredPosition = local - parent.rect.center;
        }
    }

    private void Update()
    {
        if (!active) return;
        if (Time.timeScale == 0f) return;
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;

        // บอสมาถึงแล้วเข้ามินิเกมหน้ามือผี → ให้ของบอสคุมต่อ
        if (StalkerJumpscareUI.AnyActive || minion == null || !minion.isActiveAndEnabled) { End(false); return; }

        value += cursorSpeed * Time.deltaTime;
        if (value >= 100f) value = 0f;           // วิ่งสุดแถบแล้ววนใหม่ (ไม่เสียเลือด แต่เสียเวลาให้บอสมาใกล้)
        RefreshUI();

        if (Input.GetMouseButtonDown(0))
        {
            if (value >= zoneMin && value <= zoneMax) End(true);
            else Miss();
        }
    }

    private void Miss()
    {
        PlaySfx(missSfx);
        missFlashUntil = Time.time + 0.25f;
        player.TakeDamage(minion.grabMissDamage);   // GDD: พลาด = กระชาก 10
        if (!active) return;                         // ตายจากดาเมจ
        NewRound();
    }

    private void NewRound()
    {
        value = 0f;
        float center = Random.Range(zoneCenterRange.x, zoneCenterRange.y);
        zoneMin = Mathf.Clamp(center - zoneWidth * 0.5f, 0f, 100f);
        zoneMax = Mathf.Clamp(center + zoneWidth * 0.5f, 0f, 100f);
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (gaugeBackground == null) return;
        float w = gaugeBackground.rect.width;
        if (cursor != null)
            cursor.anchoredPosition = new Vector2(value / 100f * w - w * 0.5f, cursor.anchoredPosition.y);
        if (targetZone != null)
        {
            targetZone.sizeDelta = new Vector2(w * (zoneMax - zoneMin) / 100f, targetZone.sizeDelta.y);
            targetZone.anchoredPosition = new Vector2((zoneMin + zoneMax) * 0.5f / 100f * w - w * 0.5f, targetZone.anchoredPosition.y);
        }
        if (cursorImage != null)
            cursorImage.color = Time.time < missFlashUntil ? new Color(1f, 0.2f, 0.2f, cursorBaseColor.a) : cursorBaseColor;
    }

    private void OnPlayerDied(PlayerDiedEvent e)
    {
        if (active) End(false);
    }

    private void End(bool success, bool notifyMinion = true)
    {
        if (!active) return;
        active = false;                          // ตั้งก่อน → ai.OnGrabReleased → ExitState → CancelIfHeldBy ไม่วนกลับมา
        if (panel != null) panel.Hide();
        if (player != null) player.PopActionLock();
        if (success) PlaySfx(breakFreeSfx);

        PTSDMonsterAI m = minion;
        minion = null;
        if (notifyMinion && m != null && player != null)
            m.OnGrabReleased(success, m.transform.position - player.transform.position);
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null) return;
        IAudioService audio = ServiceLocator.GetOptional<IAudioService>();
        if (audio != null) audio.PlaySFX(clip, player != null ? player.transform.position : transform.position);
    }
}
