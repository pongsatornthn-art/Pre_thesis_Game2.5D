using System;
using UnityEngine;

/// <summary>
/// UI หน้ากระเป๋า/คราฟต์
/// ⭐ ตัดการอ้างอิงตรงๆ ถึง ChestUI / CraftingManager ของเกมเดิมออก
/// เปลี่ยนเป็นยิง event แทน ระบบอื่นๆ ของเกมใหม่ (กล่อง, คราฟต์, ฯลฯ) ไป subscribe เอาเอง
/// ทำให้ไฟล์นี้ใช้ได้กับเกมใหม่โดยไม่ต้องมีคลาส ChestUI/CraftingManager อยู่จริง
/// </summary>
public class InventoryUI : MonoBehaviour
{
    public static InventoryUI Instance { get; private set; }

    [Header("UI References")]
    public GameObject inventoryPanel;
    public Transform hotbarGrid;
    public Transform backpackGrid;
    public GameObject craftingPanel;

    [Header("Selection Cursor")]
    public RectTransform selectionCursor;

    [Header("Hotbar (ต้องลากอ้างอิงจาก object เดียวกับ HotbarController)")]
    public HotbarController hotbarController;

    /// <summary>true = กำลังเปิดกระเป๋า, false = ปิด — ให้ระบบอื่น (กล่อง/คราฟต์/ฯลฯ) subscribe แทนการ hardcode</summary>
    public static event Action<bool> OnInventoryToggled;

    Inventory inventory;
    InventorySlotUI[] hotbarSlots;
    InventorySlotUI[] backpackSlots;

    void Awake()
    {
        if (Instance != null) return;
        Instance = this;
    }

    void Start()
    {
        inventory = Inventory.Instance;
        if (inventory != null) inventory.OnInventoryChanged += UpdateUI;

        hotbarSlots = hotbarGrid.GetComponentsInChildren<InventorySlotUI>();
        backpackSlots = backpackGrid.GetComponentsInChildren<InventorySlotUI>();

        if (hotbarController != null) hotbarController.OnHotbarSlotSelected += MoveSelectionCursor;

        UpdateUI();

        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        MoveSelectionCursor(0);
        SetMouseState(false);
    }

    void OnDestroy()
    {
        if (inventory != null) inventory.OnInventoryChanged -= UpdateUI;
        if (hotbarController != null) hotbarController.OnHotbarSlotSelected -= MoveSelectionCursor;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            ToggleInventory();
        }

        if (inventoryPanel != null && inventoryPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleInventory();
        }

        bool uiOpen = (inventoryPanel != null && inventoryPanel.activeSelf) ||
                      (craftingPanel != null && craftingPanel.activeSelf);

        Cursor.visible = true;
        Cursor.lockState = uiOpen ? CursorLockMode.None : CursorLockMode.Confined;
    }

    public void ToggleInventory()
    {
        if (inventoryPanel == null) return;

        bool isActive = !inventoryPanel.activeSelf;
        inventoryPanel.SetActive(isActive);
        if (craftingPanel != null) craftingPanel.SetActive(isActive);

        SetMouseState(isActive);

        // ระบบอื่นๆ ของเกมใหม่ (กล่อง, คราฟต์ ฯลฯ) ที่ต้องปิดตัวเองตอนกระเป๋าเปิด/ปิด ให้ subscribe event นี้เอง
        OnInventoryToggled?.Invoke(isActive);
    }

    void SetMouseState(bool isUIOpen)
    {
        Cursor.visible = true;
        Cursor.lockState = isUIOpen ? CursorLockMode.None : CursorLockMode.Confined;
    }

    void UpdateUI()
    {
        if (hotbarSlots != null)
        {
            for (int i = 0; i < hotbarSlots.Length; i++)
                hotbarSlots[i].ClearSlot();
        }

        if (backpackSlots != null)
        {
            for (int i = 0; i < backpackSlots.Length; i++)
                backpackSlots[i].ClearSlot();
        }

        if (inventory == null || inventory.items == null) return;
        for (int i = 0; i < hotbarSlots.Length; i++)
        {
            hotbarSlots[i].slotIndex = i;
            var item = i < inventory.items.Count ? inventory.items[i] : null;
            if (item != null) hotbarSlots[i].AddItem(item.itemData, item.amount, true);
        }

        for (int i = 0; i < backpackSlots.Length; i++)
        {
            int slotIdx = i + hotbarSlots.Length;
            backpackSlots[i].slotIndex = slotIdx;
            var item = slotIdx < inventory.items.Count ? inventory.items[slotIdx] : null;
            if (item != null) backpackSlots[i].AddItem(item.itemData, item.amount, false);
        }
    }

    void MoveSelectionCursor(int index)
    {
        if (selectionCursor == null || hotbarSlots == null) return;
        if (index < 0 || index >= hotbarSlots.Length) return;

        selectionCursor.position = hotbarSlots[index].transform.position;
    }
}