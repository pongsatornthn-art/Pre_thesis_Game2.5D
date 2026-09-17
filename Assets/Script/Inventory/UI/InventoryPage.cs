using UnityEngine;

/// <summary>
/// หน้า "กระเป๋า" ในสมุด — เป็นแค่ตัวห่อ InventoryUI ของเดิม **ไม่ได้เขียนระบบกระเป๋าใหม่**
/// ระบบช่อง/ลากของ/hotbar ทั้งหมดยังเป็นของเดิมทุกอย่าง ไม่ต้องจัดซีนกระเป๋าใหม่
/// </summary>
public class InventoryPage : JournalPage
{
    [Header("Inventory (เว้นว่างได้ = หาเองจาก InventoryUI.Instance)")]
    [SerializeField] private InventoryUI inventoryUI;

    private bool disabledOwnInput;

    public override void Show()
    {
        EnsureInventoryUI();
        base.Show();

        if (inventoryUI != null && inventoryUI.inventoryPanel != null)
            inventoryUI.inventoryPanel.SetActive(true);
    }

    public override void Hide()
    {
        base.Hide();

        if (inventoryUI != null && inventoryUI.inventoryPanel != null)
            inventoryUI.inventoryPanel.SetActive(false);
    }

    public override void Refresh()
    {
        // ไม่ต้องทำอะไร — InventoryUI เดิมวาดช่องใหม่เองอยู่แล้วผ่าน event OnInventoryChanged
    }

    /// <summary>
    /// หา InventoryUI + สั่งให้มันเลิกดักปุ่มเอง
    /// เพราะของเดิมดักปุ่ม I กับ ESC อยู่ ถ้าปล่อยไว้จะชนกับ JournalController (กดทีสลับ 2 ครั้ง)
    /// </summary>
    private void EnsureInventoryUI()
    {
        if (inventoryUI == null) inventoryUI = InventoryUI.Instance;
        if (inventoryUI == null) inventoryUI = Object.FindAnyObjectByType<InventoryUI>();

        if (inventoryUI == null)
        {
            Debug.LogWarning("[InventoryPage] หา InventoryUI ในซีนไม่เจอ — หน้ากระเป๋าจะว่างเปล่า");
            return;
        }

        if (!disabledOwnInput)
        {
            inventoryUI.handleOwnInput = false;
            disabledOwnInput = true;
        }
    }
}
