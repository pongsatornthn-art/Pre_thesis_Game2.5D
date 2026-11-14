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
        CheckNearbyItem();   // [Claude 2026-11-14] เดิม CheckMouseHover (ต้องเอาเมาส์ชี้) → เจ้าของสั่งเปลี่ยนเป็นเดินใกล้ + ป้ายลอยเหนือของ
        HandlePickup();
    }

    // [Claude 2026-11-14] หาไอเทมที่ใกล้ตัวที่สุดในระยะ interactRange → ขึ้นป้าย "[E] เก็บ" ลอยเหนือของ (WorldInteractPrompt)
    private readonly Collider[] nearbyHits = new Collider[16];

    void CheckNearbyItem()
    {
        IInteractPromptService worldPrompt = ServiceLocator.GetOptional<IInteractPromptService>();
        ItemPickup nearest = null;
        float bestDist = float.MaxValue;
        Collider nearestCol = null;

        int count = Physics.OverlapSphereNonAlloc(transform.position, interactRange, nearbyHits, itemLayer, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            ItemPickup item = nearbyHits[i].GetComponentInParent<ItemPickup>();
            if (item == null || !item.isActiveAndEnabled) continue;
            Vector3 d = item.transform.position - transform.position;
            d.y = 0f;
            float dist = d.sqrMagnitude;
            if (dist < bestDist) { bestDist = dist; nearest = item; nearestCol = nearbyHits[i]; }
        }

        if (currentTargetItem != null && currentTargetItem != nearest) worldPrompt?.Hide(this);
        currentTargetItem = nearest;
        if (promptText != null && promptText.gameObject.activeSelf) promptText.gameObject.SetActive(false);   // ป้ายกลางจอแบบเก่า — เลิกใช้

        if (nearest != null && worldPrompt != null)
        {
            float height = nearestCol != null ? nearestCol.bounds.max.y - nearest.transform.position.y + 0.35f : 0.8f;
            worldPrompt.Show(this, nearest.transform, height, PromptKey.Pickup, "PROMPT_PICKUP");
        }
        else worldPrompt?.Hide(this);
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
                    // แสดง UI และโชว์ชื่อไอเทม (ถ้าลืมใส่ Text ใน Inspector จะได้ไม่พัง)
                    if (promptText != null)
                    {
                        promptText.text = $"Press [{InteractInput.PickupKeyName}] to pickup";
                        promptText.gameObject.SetActive(true);
                    }
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
        if (currentTargetItem != null && InteractInput.PickupPressed) // [แก้โดย Claude 2026-10-07] กลับเป็น E ตามเดิม (ปุ่มเก็บของ)
        {
            if (GameplayInputGate.Blocked || (PauseManager.Instance != null && PauseManager.Instance.IsPaused)) return;   // [Claude 2026-11-14] ตอนป้ายซ่อน ห้ามเก็บ
            currentTargetItem.Pickup();
            ClearTarget();
        }
    }

    void OnDisable() => ClearTarget();   // [Claude 2026-11-14] ปิดสคริปต์ (เช่น มุม close-up) = เก็บป้ายลอย

    void ClearTarget()
    {
        ServiceLocator.GetOptional<IInteractPromptService>()?.Hide(this);   // [Claude 2026-11-14]
        currentTargetItem = null;
        if (promptText != null)
        {
            promptText.gameObject.SetActive(false);
        }
    }
}