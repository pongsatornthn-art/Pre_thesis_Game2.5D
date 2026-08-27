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
        if (Inventory.Instance == null) return;

        bool hasKey = false;
        ItemData keyToUse = null;

        foreach (var slot in Inventory.Instance.items)
        {
            if (slot != null && slot.itemData != null && slot.itemData is KeyItemData keyData)
            {
                if (keyData.targetDoorID == requiredDoorID)
                {
                    hasKey = true;
                    keyToUse = keyData;
                    break;
                }
            }
        }

        if (hasKey)
        {
            isUnlocked = true;
            ToggleDoor();

            KeyItemData key = keyToUse as KeyItemData;
            if (key.consumeOnUse)
            {
                Inventory.Instance.RemoveItem(keyToUse, 1);
            }
        }
        else
        {
            // 🌟 ถ้าไขไม่ได้ ให้สั่งโชว์ข้อความ!
            ShowNotification($"The door is locked! need <color=yellow>{keyNameForPlayer}</color>");
        }
    }

    private void ToggleDoor()
    {
        isOpen = !isOpen;
        if (animator != null) animator.SetBool("IsOpen", isOpen);
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