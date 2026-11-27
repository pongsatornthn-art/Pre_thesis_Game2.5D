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

    [Header("[2026-11-27] ใช้ยาจากมือ (ไม่ต้องเปิดสมุด)")]
    [Tooltip("ถือยาอยู่ (เลือกจากฮอตบาร์) แล้วคลิกซ้าย = ใช้เลย")]
    [SerializeField] private bool clickToUseHeldItem = true;
    [Tooltip("ปุ่มลัดใช้ยาที่ถืออยู่ (None = ปิด)")]
    [SerializeField] private KeyCode quickUseKey = KeyCode.H;

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
        // [2026-11-27] ไม่ได้เปิดสมุด: ถือยาอยู่ → คลิกซ้าย / ปุ่มลัด = ใช้
        if (!JournalController.IsAnyOpen && !GameplayInputGate.Blocked && !PauseOpen())
        {
            bool click = clickToUseHeldItem && Input.GetMouseButtonDown(0) && !PointerOverUI();
            bool key = quickUseKey != KeyCode.None && Input.GetKeyDown(quickUseKey);
            if ((click || key) && Inventory.Instance != null && Inventory.Instance.currentEquippedItem is ConsumableItemData)
            {
                UseEquipped();
                return;
            }
        }

        if (onlyWhenJournalOpen && !JournalController.IsAnyOpen) return;

        if (Input.GetKeyDown(useKey)) UseEquipped();
        else if (Input.GetKeyDown(dropKey)) DropEquipped();
    }

    private static bool PauseOpen() => Time.timeScale == 0f || (PauseManager.Instance != null && PauseManager.Instance.IsPaused);

    private static bool PointerOverUI() =>
        UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

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

    /// <summary>
    /// หาไอเทมที่ปุ่ม E/G จะทำงานด้วย
    ///
    /// - เปิดหน้ากระเป๋าในสมุดอยู่ + เลือกช่องไว้ → ทำกับ "ของในช่องที่เลือก"
    /// - นอกนั้น → ทำกับ "ของที่ถืออยู่ในมือ" (hotbar) เหมือนเดิม
    ///
    /// เหตุผล: ตอนเปิดสมุด สายตาผู้เล่นอยู่ที่ช่องที่ไฮไลท์
    /// ถ้า E ไปใช้ของที่ถือในมือแทนจะงงมาก (แนวเดียวกับ Resident Evil / Silent Hill)
    /// </summary>
    private ItemData GetEquipped()
    {
        if (Inventory.Instance == null) return null;

        // เปิดหน้ากระเป๋าอยู่ = ยึดช่องที่ไฮไลท์เสมอ
        // ถ้าไฮไลท์ช่องว่างอยู่ก็ให้เป็น null ไปเลย (ไม่ตกไปใช้ของในมือ ซึ่งจะงงมาก)
        if (BagSelection.IsBagPageOpen) return BagSelection.SelectedItem;

        return Inventory.Instance.currentEquippedItem;
    }
}
