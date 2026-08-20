using UnityEngine;
using TMPro; // เรียกใช้ระบบ TextMeshPro

public class PlayerInteraction : MonoBehaviour
{
    [Header("Pickup Settings")]
    public float interactRange = 3f; // ระยะที่ผู้เล่นจะเก็บของได้ (ปรับได้ตามใจชอบ)
    public LayerMask itemLayer;      // เลเยอร์ของไอเทม (ให้เลือก Layer "Item" ที่เราสร้างไว้)

    [Header("UI Elements")]
    public TextMeshProUGUI promptText; // ลาก PickupPrompt_Text มาใส่ช่องนี้

    private ItemPickup currentTargetItem; // เก็บข้อมูลไอเทมที่เมาส์กำลังชี้อยู่

    void Update()
    {
        CheckMouseHover();
        HandlePickup();
    }

    // ฟังก์ชันเช็กว่าเมาส์ชี้โดนไอเทมไหม
    void CheckMouseHover()
    {
        // ยิงเลเซอร์จากกล้อง ผ่านจุดที่เมาส์อยู่ ทะลุลงไปในฉาก
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // ถ้าเลเซอร์ชนวัตถุที่อยู่ใน itemLayer
        if (Physics.Raycast(ray, out hit, 100f, itemLayer))
        {
            // ดึงสคริปต์ ItemPickup จากของที่โดนชี้
            ItemPickup item = hit.collider.GetComponent<ItemPickup>();

            if (item != null)
            {
                // เช็กว่าตัวผู้เล่น ยืนอยู่ใกล้ไอเทมชิ้นนั้นพอไหม?
                float distance = Vector3.Distance(transform.position, item.transform.position);

                if (distance <= interactRange)
                {
                    currentTargetItem = item;
                    // แสดง UI และโชว์ชื่อไอเทม (ถ้าใน ItemData คุณพงศธรมีตัวแปรชื่อไอเทม)
                    promptText.text = "Press [E] to pickup";
                    promptText.gameObject.SetActive(true);
                    return; // จบการทำงานฟังก์ชันนี้แค่นี้
                }
            }
        }

        // ถ้าเมาส์ไม่ได้ชี้อะไร หรือยืนอยู่ไกลเกินไป ให้ซ่อน UI 
        ClearTarget();
    }

    // ฟังก์ชันรอกดปุ่ม E เพื่อเก็บของ
    void HandlePickup()
    {
        if (currentTargetItem != null && Input.GetKeyDown(KeyCode.E))
        {
            currentTargetItem.Pickup();
            ClearTarget();
        }
    }

    void ClearTarget()
    {
        currentTargetItem = null;
        if (promptText != null)
        {
            promptText.gameObject.SetActive(false);
        }
    }
}