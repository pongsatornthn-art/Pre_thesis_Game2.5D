using UnityEngine;

/// <summary>
/// คลาสแม่ของ "ของในซีนที่เดินไปใกล้แล้วกดปุ่มโต้ตอบ (F)" — กดสำรวจ / ชิ้นส่วนที่เก็บได้ / จุดส่งเควส / จุดเซฟ
/// รวมเรื่องที่ทุกตัวต้องทำเหมือนกันไว้ที่เดียว: ตรวจระยะ · ปุ่ม · ป้าย "กด E" · เงื่อนไข · ไม่รับปุ่มตอนหยุดเกม/ตอนฉากเล่น
/// คลาสลูกเขียนแค่ "กดแล้วเกิดอะไร" (Template Method)
///
/// ต้องมี Collider ที่ติ๊ก Is Trigger ไว้เป็นระยะกด
/// </summary>
[RequireComponent(typeof(Collider))]
public abstract class StoryInteractableBase : MonoBehaviour
{
    [Header("การกด (ปุ่มกลาง InteractInput — ตอนนี้ F)")]
    [Tooltip("UI ป้าย 'กด F' (เว้นว่างได้)")]
    [SerializeField] private GameObject interactPrompt;

    [Tooltip("เงื่อนไขที่ต้องครบก่อนถึงจะกดได้ (เว้นว่าง = กดได้เสมอ)")]
    [SerializeReference, SubclassPicker] private IStoryCondition interactableWhen;

    private bool isPlayerNear;
    private GameObject nearPlayer;

    protected GameObject NearPlayer => nearPlayer;

    protected virtual void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    protected virtual void Awake()
    {
        // กันตั้งผิด: ลาก object ตัวเอง (หรือ object แม่) มาใส่ช่องป้าย → ซ่อนป้าย = ซ่อนตัวเองหายไปถาวร
        // (2026-10-06 เจอจริงที่ Key_Test)
        if (interactPrompt != null && transform.IsChildOf(interactPrompt.transform))
        {
            Debug.LogWarning($"[{GetType().Name}] '{name}' ช่อง Interact Prompt ชี้มาที่ตัวเอง — ต้องใส่ UI ป้าย 'กด F' แยกต่างหาก (หรือเว้นว่าง) · ปิดการใช้ป้ายให้ก่อน", this);
            interactPrompt = null;
        }
    }

    protected virtual void OnDisable()
    {
        isPlayerNear = false;
        SetPrompt(false);
    }

    protected virtual void Update()
    {
        if (!isPlayerNear) return;

        bool canInteract = CanInteractNow();
        SetPrompt(canInteract);

        if (canInteract && InteractInput.Pressed)
        {
            SetPrompt(false);
            OnInteract(StoryContext.Create(this, nearPlayer));
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        isPlayerNear = true;
        nearPlayer = other.gameObject;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        isPlayerNear = false;
        SetPrompt(false);
    }

    private bool CanInteractNow()
    {
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return false;
        if (StoryDirector.Instance != null && StoryDirector.Instance.IsPlaying) return false;   // กันกดซ้อนระหว่างฉากเล่น
        if (!IsInteractable()) return false;
        return interactableWhen == null || interactableWhen.IsMet(StoryContext.Create(this, nearPlayer));
    }

    private void SetPrompt(bool visible)
    {
        if (interactPrompt != null && interactPrompt.activeSelf != visible) interactPrompt.SetActive(visible);
    }

    /// <summary>คลาสลูกบอกว่าตอนนี้ยังกดได้ไหม (เช่น ส่งไปแล้ว = กดไม่ได้)</summary>
    protected virtual bool IsInteractable() => true;

    /// <summary>กด E แล้วเกิดอะไร</summary>
    protected abstract void OnInteract(StoryContext ctx);

    /// <summary>เล่นฉากผ่าน StoryDirector (เว้นว่างได้)</summary>
    protected static void PlaySequence(StorySequence sequence)
    {
        if (sequence == null) return;
        StoryDirector director = StoryDirector.Instance;
        if (director != null) director.Play(sequence);
        else Debug.LogWarning("[StoryInteractable] ไม่พบ StoryDirector ในซีน — ฉากไม่เล่น");
    }
}
