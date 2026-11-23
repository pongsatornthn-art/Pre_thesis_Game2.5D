using UnityEngine;
using TMPro; // 🌟 1. เรียกใช้ระบบ TextMeshPro
using System.Collections; // 🌟 2. เรียกใช้ระบบนับเวลา (Coroutine)

public class DoorController : MonoBehaviour
{
    [Header("Door Settings")]
    public string requiredDoorID;

    public string keyNameForPlayer;

    [Tooltip("[Claude 2026-11-14] คีย์บทพูดตอนกดแล้วประตูล็อก (LocalizationData.csv) · เว้นว่าง = DOOR_LOCKED \"ประตูล็อกอยู่ ต้องหากุญแจก่อน\"")]
    public string lockedTextKey;

    public bool isUnlocked = false;
    public bool isOpen = false;

    [Header("กันกด E รัว (แก้ 2026-11-22)")]
    [Tooltip("ต้องรอนานเท่านี้ (วินาที) ก่อนกด E เปิด/ปิดซ้ำได้ — และต้องรอให้ท่าเปิด/ปิดเล่นจบก่อนด้วย\nเดิม: กด E รัว ท่าประตูกระตุกกลับไปเริ่มใหม่")]
    [Min(0f)] public float toggleCooldown = 0.4f;

    [Header("UI & Effects")]
    public GameObject interactPrompt;

    // 🌟 ช่องใส่ Text ขวาบน
    public TextMeshProUGUI notificationText;

    // 🌟 เพิ่มช่องใส่ Box Collider ตัวทึบ (ตัวกันเดินทะลุ)
    [Header("Physics")]
    public BoxCollider solidCollider;

    private bool isPlayerNear = false;
    private Animator animator;
    private Coroutine notificationCoroutine; // ตัวเก็บค่าเวลานับถอยหลัง
    private float nextToggleTime;            // [แก้ 2026-11-22] กดได้อีกครั้งเมื่อไหร่

    void Start()
    {
        animator = GetComponent<Animator>();

        // ตอนเริ่มเกม ให้ซ่อนข้อความแจ้งเตือนไว้ก่อน
        if (notificationText != null) notificationText.text = "";
    }

    void Update()
    {
        // [Claude 2026-11-14] ป้ายลอยโชว์ของชิ้นอื่นอยู่ (เช่น ไอเทมใกล้กว่า) = E ครั้งนี้ไม่ใช่ของประตู
        if (isPlayerNear && Input.GetKeyDown(KeyCode.E) && !InteractPromptMarker.OtherPromptShown(gameObject))
        {
            if (isUnlocked)
            {
                // [แก้ 2026-11-22] ท่าเปิด/ปิดยังเล่นไม่จบ หรือเพิ่งกดไป → ไม่รับ (กันท่ากระตุกรีเซ็ต)
                if (Time.time < nextToggleTime || IsDoorAnimating()) return;
                nextToggleTime = Time.time + toggleCooldown;
                ToggleDoor();
            }
            else
            {
                TryUnlockDoor();
            }
        }
    }

    private void TryUnlockDoor()
    {
        // 🌟 กุญแจย้ายไปอยู่ "คลังของสำคัญ" แยกจากกระเป๋าแล้ว (ไม่กินช่อง ทิ้งไม่ได้)
        // Consume() จะเช็คให้ว่ามีกุญแจตรงรหัสไหม และหักออกให้เองถ้ากุญแจตั้ง consumeOnUse ไว้
        IKeyItemHolder keys = ServiceLocator.Get<IKeyItemHolder>();

        if (keys != null && keys.Consume(requiredDoorID))
        {
            isUnlocked = true;
            ToggleDoor();
            return;
        }

        // ไม่มีกุญแจ (หรือยังไม่มี KeyItemHolder ในซีน)
        // [Claude 2026-11-14] เดิมขึ้นข้อความอังกฤษมุมจอ → เจ้าของสั่ง: ตัวละครพูดเป็นบทพูด + ผ่านระบบแปลภาษา
        IDialogueService dialogue = ServiceLocator.GetOptional<IDialogueService>();
        if (dialogue != null)
        {
            if (!dialogue.IsPlaying)
                StartCoroutine(dialogue.PlaySimple(string.IsNullOrEmpty(lockedTextKey) ? "DOOR_LOCKED" : lockedTextKey, 2.5f));
            return;
        }
        ShowNotification($"The door is locked! need <color=yellow>{keyNameForPlayer}</color>");   // สำรอง: ซีนที่ไม่มีระบบบทพูด
    }

    /// <summary>[แก้ 2026-11-22] ประตูกำลังเล่นท่าเปิด/ปิดอยู่ไหม (กำลังเปลี่ยนท่า หรือท่าที่เล่นอยู่ยังไม่จบรอบ)</summary>
    private bool IsDoorAnimating()
    {
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return false;
        if (animator.IsInTransition(0)) return true;
        AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(0);
        return !st.loop && st.normalizedTime < 1f;
    }

    private void ToggleDoor()
    {
        isOpen = !isOpen;
        if (animator != null) animator.SetBool("IsOpen", isOpen);

        // 🌟 ปิด/เปิด กล่องฟิสิกส์ทึบ ตามสถานะประตู
        // ถ้า isOpen เป็น true (เปิด) -> enabled จะเป็น false (เดินทะลุได้)
        if (solidCollider != null)
        {
            solidCollider.enabled = !isOpen;
        }
    }

    // 🌟 ระบบโชว์ข้อความ 2.5 วินาที แล้วลบทิ้ง
    private void ShowNotification(string message)
    {
        if (notificationText == null) return;

        // ถ้ามีข้อความเก่าค้างอยู่ ให้รีเซ็ตเวลานับใหม่
        if (notificationCoroutine != null) StopCoroutine(notificationCoroutine);

        notificationCoroutine = StartCoroutine(ClearMessageAfterDelay(message, 2.5f));
    }

    private IEnumerator ClearMessageAfterDelay(string message, float delay)
    {
        notificationText.text = message;
        yield return new WaitForSeconds(delay); // รอเวลา
        notificationText.text = ""; // เคลียร์ข้อความทิ้ง
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[DoorController] OnTriggerEnter โดนวัตถุ: {other.name} | Tag: {other.tag}");
        if (other.CompareTag("Player"))
        {
            isPlayerNear = true;
            if (interactPrompt != null) interactPrompt.SetActive(true);
            Debug.Log("[DoorController] ตรวจพบ Player อยู่ใกล้ประตูแล้ว");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Debug.Log($"[DoorController] OnTriggerExit หลุดจากวัตถุ: {other.name}");
        if (other.CompareTag("Player"))
        {
            isPlayerNear = false;
            if (interactPrompt != null) interactPrompt.SetActive(false);
            if (notificationText != null) notificationText.text = "";
        }
    }
}