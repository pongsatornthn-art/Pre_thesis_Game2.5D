using UnityEngine;

/// <summary>
/// ตัวคุมกลางของ "ช่องไอเทมที่ถูกเลือก" ในหน้ากระเป๋าของสมุด
///
/// หน้าที่:
/// 1. จำว่าตอนนี้เลือกช่องไหนอยู่ แล้วสั่งเปิด/ปิดกรอบไฮไลท์ให้ถูกช่อง
/// 2. รับ input 3 ทาง — คลิกเมาส์ (BagSlotVisual ส่งมา) · ปุ่มเลข 1-6 · สกรอลล์เมาส์
/// 3. ส่งไอเทมที่เลือกไปให้กระดาษฝั่งขวาแสดงผล
/// 4. เปิดให้ระบบอื่นรู้ว่ากำลังเล็งไอเทมชิ้นไหนอยู่ (ItemActionHandler ใช้ตัดสินว่า E/G ทำกับอะไร)
///
/// แปะไว้ที่ก้อน Page_Bag — พอสลับไปหน้าอื่นก้อนนี้ถูกปิด Update ก็หยุดเอง
/// ปุ่มเลข/สกรอลล์จึงไม่ทำงานตอนอยู่หน้าอื่นโดยไม่ต้องเช็คอะไรเพิ่ม
/// </summary>
public class BagSelection : MonoBehaviour
{
    [Header("References")]
    [Tooltip("ก้อนที่รวมช่องไอเทมทั้งหมด (เว้นว่าง = หาในลูกของก้อนนี้)")]
    [SerializeField] private Transform slotsParent;

    [Tooltip("กระดาษรายละเอียดฝั่งขวา (เว้นว่างได้ถ้ายังไม่ทำ)")]
    [SerializeField] private BagDetailPanel detailPanel;

    [Header("Options")]
    [Tooltip("เปิดหน้ากระเป๋าแล้วเลือกช่องแรกที่มีของให้อัตโนมัติ")]
    [SerializeField] private bool autoSelectOnOpen = true;

    [Tooltip("ปุ่มเลขปุ่มแรก (1) — ช่องถัดไปจะไล่ต่อเอง")]
    [SerializeField] private KeyCode firstNumberKey = KeyCode.Alpha1;

    [Tooltip("ใช้สกรอลล์เมาส์เลื่อนช่องที่เลือกได้")]
    [SerializeField] private bool useScrollWheel = true;

    private BagSlotVisual[] slots;
    private int selectedIndex = -1;

    /// <summary>ไอเทมในช่องที่กำลังเลือกอยู่ (null = ช่องว่าง หรือยังไม่ได้เลือก)</summary>
    public static ItemData SelectedItem { get; private set; }

    /// <summary>หน้ากระเป๋าเปิดอยู่ไหม — ให้ ItemActionHandler รู้ว่าควรทำกับช่องที่เลือก ไม่ใช่ของในมือ</summary>
    public static bool IsBagPageOpen { get; private set; }

    private void Awake()
    {
        Transform root = slotsParent != null ? slotsParent : transform;
        slots = root.GetComponentsInChildren<BagSlotVisual>(true);

        foreach (BagSlotVisual s in slots)
        {
            if (s != null) s.Bind(this);
        }
    }

    private void OnEnable()
    {
        IsBagPageOpen = true;
        if (Inventory.Instance != null) Inventory.Instance.OnInventoryChanged += Refresh;

        // เปิดหน้ามาใหม่ = เริ่มจากช่องแรกที่มีของ
        selectedIndex = -1;
        Refresh();
    }

    private void OnDisable()
    {
        IsBagPageOpen = false;
        if (Inventory.Instance != null) Inventory.Instance.OnInventoryChanged -= Refresh;

        ClearSelection();
    }

    private void Update()
    {
        if (slots == null || slots.Length == 0) return;

        // 1) ปุ่มเลขตามหมายเลขที่พิมพ์อยู่บนกรอบช่อง
        for (int i = 0; i < slots.Length; i++)
        {
            if (Input.GetKeyDown(firstNumberKey + i))
            {
                SelectIndex(i);
                return;
            }
        }

        // 2) สกรอลล์เมาส์เลื่อนทีละช่อง (วนกลับเมื่อสุดปลาย)
        if (!useScrollWheel) return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) < 0.01f) return;

        int start = selectedIndex < 0 ? 0 : selectedIndex;
        int step = scroll > 0f ? -1 : 1;
        int next = (start + step + slots.Length) % slots.Length;

        SelectIndex(next);
    }

    /// <summary>เลือกช่องตามหมายเลข (0 = ช่องที่พิมพ์เลข 1)</summary>
    public void SelectIndex(int index)
    {
        if (slots == null || index < 0 || index >= slots.Length) return;

        selectedIndex = index;
        ApplySelection();
    }

    /// <summary>เลือกช่องนี้ (BagSlotVisual เรียกตอนถูกคลิก)</summary>
    public void SelectSlot(BagSlotVisual slot)
    {
        if (slots == null || slot == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == slot)
            {
                SelectIndex(i);
                return;
            }
        }
    }

    /// <summary>วาดกรอบไฮไลท์และหน้าขวาใหม่ตาม selectedIndex ปัจจุบัน</summary>
    private void ApplySelection()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null) slots[i].SetSelected(i == selectedIndex);
        }

        BagSlotVisual current = (selectedIndex >= 0 && selectedIndex < slots.Length) ? slots[selectedIndex] : null;
        SelectedItem = current != null ? current.Item : null;

        // ช่องว่าง = ส่ง null ไป กระดาษฝั่งขวาจะซ่อนตัวเอง (กรอบไฮไลท์ยังอยู่ที่ช่องนั้น)
        if (detailPanel != null) detailPanel.Show(SelectedItem);
    }

    /// <summary>
    /// เรียกทุกครั้งที่ของในกระเป๋าเปลี่ยน (ใช้ยา / ทิ้งของ / ลากสลับช่อง / เก็บของใหม่)
    /// </summary>
    private void Refresh()
    {
        if (slots == null || slots.Length == 0) return;

        // ยังไม่เคยเลือกอะไร → เลือกช่องแรกที่มีของให้ (หน้าขวาจะได้ไม่โล่งตอนเปิดมาครั้งแรก)
        if (selectedIndex < 0 && autoSelectOnOpen)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].HasItem)
                {
                    selectedIndex = i;
                    break;
                }
            }
        }

        // เลือกช่องเดิมค้างไว้เสมอ ไม่เด้งไปช่องอื่นเอง (ผู้เล่นจะได้ไม่งงว่ากรอบวิ่งไปไหน)
        ApplySelection();
    }

    private void ClearSelection()
    {
        if (slots != null)
        {
            foreach (BagSlotVisual s in slots)
            {
                if (s != null) s.SetSelected(false);
            }
        }

        selectedIndex = -1;
        SelectedItem = null;
        if (detailPanel != null) detailPanel.Clear();
    }
}
