using UnityEngine;

/// <summary>
/// รับปุ่ม "ใช้" (E) กับ "ทิ้ง" (G) ของไอเทมที่ถืออยู่
///
/// ทำงานเฉพาะตอนเปิดสมุดอยู่ (ค่าเริ่มต้น) เพื่อไม่ให้ปุ่ม E ชนกับระบบอื่นที่ใช้ E เหมือนกัน
/// (เก็บของ / เปิดประตู / เปิดกล่อง / กดหนี jumpscare)
/// </summary>
public class ItemActionHandler : MonoBehaviour
{
    [Header("ปุ่ม")]
    [SerializeField] private KeyCode useKey = KeyCode.E;
    [SerializeField] private KeyCode dropKey = KeyCode.G;

    [Tooltip("ให้ทำงานเฉพาะตอนเปิดสมุดอยู่ (แนะนำเปิดไว้ กันปุ่ม E ชนกับระบบเก็บของ/เปิดประตู)")]
    [SerializeField] private bool onlyWhenJournalOpen = true;

    [Header("References")]
    [Tooltip("ก้อนผู้เล่นที่จะได้รับผลของไอเทม (เว้นว่าง = หาจาก PlayerMovement ในซีน)")]
    [SerializeField] private GameObject player;

    [Header("ทิ้งของ")]
    [Tooltip("prefab ของที่ตกพื้น (ต้องมี ItemPickup) — เว้นว่าง = ทิ้งแล้วหายเลย")]
    [SerializeField] private ItemPickup dropPrefab;
    [Tooltip("จุดที่ของจะตกลงพื้น (เว้นว่าง = ตรงตัวผู้เล่น)")]
    [SerializeField] private Transform dropPoint;
    [SerializeField] private float dropForwardOffset = 1f;

    private void Awake()
    {
        if (player == null)
        {
            PlayerMovement pm = Object.FindAnyObjectByType<PlayerMovement>();
            if (pm != null) player = pm.gameObject;
        }
    }

    private void Update()
    {
        if (onlyWhenJournalOpen && !JournalController.IsAnyOpen) return;

        if (Input.GetKeyDown(useKey)) UseEquipped();
        else if (Input.GetKeyDown(dropKey)) DropEquipped();
    }

    /// <summary>ใช้ไอเทมที่ถืออยู่ (ถ้ามันใช้ได้)</summary>
    public void UseEquipped()
    {
        ItemData item = GetEquipped();
        if (item == null) return;

        // ไม่ต้องเช็คว่าเป็นไอเทมชนิดไหน — ถามแค่ว่า "ใช้ได้ไหม"
        if (item is not IUsable usable)
        {
            Debug.Log($"<color=grey>{item.itemName} ใช้ไม่ได้</color>");
            return;
        }

        if (usable.Use(player))
        {
            Inventory.Instance.RemoveItem(item, 1);
        }
    }

    /// <summary>ทิ้งไอเทมที่ถืออยู่ 1 ชิ้น</summary>
    public void DropEquipped()
    {
        ItemData item = GetEquipped();
        if (item == null) return;

        // ของสำคัญ/เอกสาร ไม่ได้อยู่ในกระเป๋าอยู่แล้ว เลยทิ้งไม่ได้โดยธรรมชาติ
        SpawnDropped(item);
        Inventory.Instance.RemoveItem(item, 1);

        Debug.Log($"<color=grey>ทิ้ง {item.itemName}</color>");
    }

    private void SpawnDropped(ItemData item)
    {
        if (dropPrefab == null) return;

        Transform origin = dropPoint != null ? dropPoint : (player != null ? player.transform : transform);
        Vector3 pos = origin.position + origin.forward * dropForwardOffset;

        ItemPickup dropped = Instantiate(dropPrefab, pos, Quaternion.identity);
        dropped.item = item;
        dropped.amount = 1;
    }

    private ItemData GetEquipped()
    {
        if (Inventory.Instance == null) return null;
        return Inventory.Instance.currentEquippedItem;
    }
}
