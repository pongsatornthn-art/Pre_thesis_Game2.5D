using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// ของที่ "คลิกเมาส์ซ้ายเก็บ" ระหว่างอยู่มุม close-up FPS — เช่น กุญแจที่โผล่หลังรูปวาดบนขาตั้ง
/// (ตัวเก็บของปกติ ItemPickup ผูกกับสคริปต์ผู้เล่นที่ถูกปิดไว้ตอนอยู่มุม close-up · เจ้าของตกลง 2026-10-07: ต่อจากมินิเกมใช้เมาส์ ผู้เล่นคุ้นมืออยู่แล้ว)
///
/// วางในซีน: แปะที่ของชิ้นนั้น (ต้องมี Collider) · ใส่ไอเทมที่จะได้ · ใส่ธง "เก็บแล้ว"
///   ยังไม่ถึงเวลา (ธง Required ยังไม่ขึ้น) หรือเก็บไปแล้ว → ซ่อนหน้าตา+ตัวชน (ผ่าน ScenePresence รวมกับ WorldPresence ได้)
///   ถึงเวลาแล้ว + อยู่มุม close-up → เมาส์โผล่ · กล้องหยุดหัน · ชี้โดน = ไฮไลท์ · คลิกซ้าย = เก็บ
///   เก็บแล้ว: ItemData.Collect() ทุกชิ้น (คีย์ไอเทมเข้าหน้าคีย์ไอเทมเอง) · ปักธง · คืนเมาส์/กล้อง
/// ฉากเนื้อเรื่องรอได้ด้วยคำสั่ง "รอ/รอจนธงขึ้น" (ใส่ธง Picked Flag เดียวกัน)
/// เซฟ: จำผ่านธงในความจำกลาง — โหลดเซฟแล้วไม่โผล่ซ้ำ
/// </summary>
[RequireComponent(typeof(Collider))]
public class CloseUpClickPickup : MonoBehaviour, IPresenceGate
{
    [Header("เก็บได้เมื่อ")]
    [Tooltip("ธงนี้ขึ้นแล้วถึงโผล่และคลิกได้ เช่น Flag_PaintingRevealed (เว้นว่าง = ได้ตลอด)")]
    [SerializeField] private StoryFlagId requiredFlag;

    [Tooltip("Scene Id ของมุม close-up ที่ต้องเปิดอยู่ (เว้นว่าง = มุมไหนก็ได้)")]
    [SerializeField] private string closeUpId;

    [Header("ได้อะไร")]
    [Tooltip("ไอเทมที่ได้ (แต่ละชนิดไปคลังของตัวเอง: กุญแจ/ของสำคัญ → หน้าคีย์ไอเทม · เอกสาร → หน้าเอกสาร)")]
    [SerializeField] private List<ItemData> items = new List<ItemData>();

    [Tooltip("ธงที่ปักเมื่อเก็บแล้ว — บังคับ (ใช้จำว่าเก็บแล้ว + ให้ฉากรอ)")]
    [SerializeField] private StoryFlagId pickedFlag;

    [Header("หน้าตา / เสียง")]
    [Tooltip("ของที่โชว์ตอนเมาส์ชี้โดน เช่น กรอบเรืองแสง (เว้นว่างได้)")]
    [SerializeField] private GameObject hoverHighlight;
    [SerializeField] private AudioClip pickSound;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    [Tooltip("เรียกหลังเก็บ (เช่น เล่นเอฟเฟกต์) — ไม่ต้องใช้ซ่อนตัวเอง ระบบซ่อนให้แล้ว")]
    public UnityEvent onPicked;

    private IStoryFlags flags;
    private Collider[] myColliders;
    private bool armed;
    private CursorLockMode savedLock;
    private bool savedVisible;
    private StoryCloseUpView armedView;

    // ---------------- โผล่ / ไม่โผล่ ----------------

    public bool AllowVisible => IsReady && !IsPicked;

    private bool IsReady => requiredFlag == null || (flags != null && flags.Has(requiredFlag));
    private bool IsPicked => pickedFlag != null && flags != null && flags.Has(pickedFlag);

    private void Awake()
    {
        myColliders = GetComponentsInChildren<Collider>(true);
        if (hoverHighlight != null) hoverHighlight.SetActive(false);
        if (pickedFlag == null) Debug.LogWarning($"[CloseUpClickPickup] '{name}' ยังไม่ได้ใส่ Picked Flag — เก็บแล้วโหลดเซฟจะโผล่ซ้ำ และฉากจะรอไม่ได้", this);
        if (items.Count == 0) Debug.LogWarning($"[CloseUpClickPickup] '{name}' ยังไม่ได้ใส่ไอเทมที่จะได้", this);
    }

    private void OnEnable()
    {
        Subscribe();
        Sync();
    }

    // ซีนเพิ่งเปิด: อาจ OnEnable ก่อนความจำกลางลงทะเบียน → ลองอีกรอบตอน Start
    private void Start()
    {
        Subscribe();
        Sync();
    }

    private void OnDisable()
    {
        Disarm();
        if (flags != null) flags.OnChanged -= Sync;
        flags = null;
    }

    private void Subscribe()
    {
        if (flags != null) return;
        flags = ServiceLocator.GetOptional<IStoryFlags>();
        if (flags != null) flags.OnChanged += Sync;
    }

    private void Sync() => ScenePresence.Refresh(gameObject);

    // ---------------- คลิกเก็บ ----------------

    private void Update()
    {
        bool shouldArm = AllowVisible && ScenePresence.IsAllowed(gameObject) && ViewIsActive();
        if (shouldArm != armed)
        {
            if (shouldArm) Arm();
            else Disarm();
        }
        if (!armed) return;
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;

        bool hovering = IsMouseOver();
        if (hoverHighlight != null && hoverHighlight.activeSelf != hovering) hoverHighlight.SetActive(hovering);

        if (hovering && Input.GetMouseButtonDown(0)) Pick();
    }

    private bool ViewIsActive()
    {
        StoryCloseUpView view = StoryCloseUpView.Active;
        if (view == null) return false;
        return string.IsNullOrEmpty(closeUpId) || view.SceneId == closeUpId;
    }

    private bool IsMouseOver()
    {
        Camera cam = armedView != null ? armedView.ViewCamera : null;
        if (cam == null) return false;

        // ยิงทะลุทุกอย่าง — กันโซนกด F (trigger) / ตัวขาตั้ง บังเรย์
        foreach (RaycastHit hit in Physics.RaycastAll(cam.ScreenPointToRay(Input.mousePosition), 20f, ~0, QueryTriggerInteraction.Collide))
        {
            foreach (Collider c in myColliders) if (hit.collider == c) return true;
        }
        return false;
    }

    private void Pick()
    {
        foreach (ItemData item in items)
        {
            if (item == null) continue;
            if (!item.Collect()) Debug.LogWarning($"[CloseUpClickPickup] เก็บ '{item.name}' ไม่สำเร็จ (คลังปลายทางมีปัญหา — ดู error ก่อนหน้า)", this);
        }

        PlaySfx(pickSound);
        if (pickedFlag != null) flags?.Set(pickedFlag);   // → Sync ซ่อนตัวเอง · ฉากที่รอธงนี้เดินต่อ
        Disarm();
        Sync();
        onPicked?.Invoke();
    }

    // เมาส์โผล่ + กล้องหยุดหัน ระหว่างรอคลิก — คืนค่าเดิมตอนเลิก (ไม่แย่ง Cursor กับระบบอื่น)
    private void Arm()
    {
        armed = true;
        armedView = StoryCloseUpView.Active;
        savedLock = Cursor.lockState;
        savedVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (armedView != null) armedView.SetLookEnabled(false);
    }

    private void Disarm()
    {
        if (!armed) return;
        armed = false;
        if (hoverHighlight != null) hoverHighlight.SetActive(false);
        Cursor.lockState = savedLock;
        Cursor.visible = savedVisible;
        if (armedView != null && StoryCloseUpView.Active == armedView) armedView.SetLookEnabled(true);
        armedView = null;
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null) return;
        ServiceLocator.GetOptional<IAudioService>()?.PlaySFX(clip, transform.position, volume, false);
    }
}
