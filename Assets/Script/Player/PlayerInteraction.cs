using UnityEngine;
using TMPro; // เรียกใช้ระบบ TextMeshPro

public class PlayerInteraction : MonoBehaviour
{
    [Header("Pickup Settings")]
    public float interactRange = 2f; // ระยะที่ผู้เล่นจะเก็บของได้ (ปรับได้ตามใจชอบ) · [2026-11-24] 3 → 2
    public LayerMask itemLayer;      // เลเยอร์ของไอเทม (ให้เลือก Layer "Item" ที่เราสร้างไว้)
    [Tooltip("[2026-11-24] ของที่มีอะไรเหล่านี้ขวางระหว่างตัวกับของ = เก็บไม่ได้ (กันเก็บทะลุห้อง) · Default + Wall")]
    public LayerMask blockedBy = (1 << 0) | (1 << 6);
    [Tooltip("ของที่ขวางต้องสูงอย่างน้อยกี่เมตรถึงนับเป็นกำแพง (กำแพง/ตู้สูง) — เก้าอี้/โต๊ะไม่นับ")]
    public float minBlockerHeight = 1.8f;

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
            if (dist >= bestDist || WallBetween(item, nearbyHits[i])) continue;   // [2026-11-24] มีกำแพงขวาง = อยู่อีกห้อง เก็บไม่ได้
            if (dist < bestDist) { bestDist = dist; nearest = item; nearestCol = nearbyHits[i]; }
        }

        if (currentTargetItem != null && currentTargetItem != nearest) worldPrompt?.Hide(this);
        currentTargetItem = nearest;
        if (promptText != null && promptText.gameObject.activeSelf) promptText.gameObject.SetActive(false);   // ป้ายกลางจอแบบเก่า — เลิกใช้

        if (nearest != null && worldPrompt != null)
        {
            float height = nearestCol != null ? nearestCol.bounds.max.y - nearest.transform.position.y + 0.35f : 0.8f;
            worldPrompt.Show(this, nearest.transform, Vector3.up * height, PromptKey.Pickup, "PROMPT_PICKUP");
        }
        else worldPrompt?.Hide(this);
    }

    // [2026-11-24] เส้นจากอกผู้เล่น → กลางตัวของ ชนอะไรระหว่างทางไหม (ข้ามตัวผู้เล่น/ตัวของ/ตัวที่เป็น trigger)
    private readonly RaycastHit[] wallHits = new RaycastHit[8];
    private bool WallBetween(ItemPickup item, Collider itemCol)
    {
        Vector3 from = transform.position + Vector3.up * 1f;
        Vector3 to = itemCol != null ? itemCol.bounds.center : item.transform.position;
        Vector3 dir = to - from;
        float len = dir.magnitude;
        if (len < 0.05f) return false;
        int n = Physics.RaycastNonAlloc(from, dir / len, wallHits, len, blockedBy, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Transform t = wallHits[i].collider.transform;
            if (t.root == transform.root || t.IsChildOf(item.transform) || item.transform.IsChildOf(t)) continue;
            if (wallHits[i].collider.GetComponentInParent<ItemPickup>() != null) continue;   // ของชิ้นอื่นวางซ้อน ไม่นับเป็นกำแพง
            if (wallHits[i].collider.bounds.size.y < minBlockerHeight) continue;           // เก้าอี้/โต๊ะเตี้ย ไม่นับ (ของบนโต๊ะต้องเก็บได้) — นับแค่กำแพง/ตู้สูง
            return true;
        }
        return false;
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
            IInteractPromptService worldPrompt = ServiceLocator.GetOptional<IInteractPromptService>();
            if (worldPrompt != null && worldPrompt.IsOtherShown(this, PromptKey.Pickup)) return;   // [Claude 2026-11-14] ป้ายโชว์ของชิ้นอื่น (เช่น ประตู) = E ครั้งนี้เป็นของชิ้นนั้น
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