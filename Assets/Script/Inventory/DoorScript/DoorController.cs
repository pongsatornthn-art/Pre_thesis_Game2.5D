using UnityEngine;
using TMPro; // 🌟 1. เรียกใช้ระบบ TextMeshPro
using System.Collections; // 🌟 2. เรียกใช้ระบบนับเวลา (Coroutine)

public class DoorController : MonoBehaviour
{
    [Header("Door Settings")]
    public string requiredDoorID;

    public string keyNameForPlayer;

    public bool isUnlocked = false;
    public bool isOpen = false;

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

    void Start()
    {
        animator = GetComponent<Animator>();

        // ตอนเริ่มเกม ให้ซ่อนข้อความแจ้งเตือนไว้ก่อน
        if (notificationText != null) notificationText.text = "";
    }

    void Update()
    {
        if (isPlayerNear && Input.GetKeyDown(KeyCode.E))
        {
            if (isUnlocked)
            {
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
        ShowNotification($"The door is locked! need <color=yellow>{keyNameForPlayer}</color>");
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
        if (other.CompareTag("Player"))
        {
            isPlayerNear = true;
            if (interactPrompt != null) interactPrompt.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = false;
            if (interactPrompt != null) interactPrompt.SetActive(false);

            // 🌟 ถ้าผู้เล่นเดินหนีออกจากประตู ให้ซ่อนข้อความขวาบนทันที
            if (notificationText != null) notificationText.text = "";
        }
    }
}