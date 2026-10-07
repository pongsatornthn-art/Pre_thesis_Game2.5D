using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ช่องของสำคัญ 1 ช่องในหน้า K — วางในซีนไว้ตายตัว (ไม่ใช้โค้ดสร้าง) · หน้าตาก๊อปจากช่องกระเป๋า
/// ว่าง = โชว์แค่กรอบเปล่า · มีของ = โชว์ไอคอน · คลิก = บอกหน้า K ให้โชว์รายละเอียดชิ้นนี้
/// เสียง hover/คลิก: แปะ UIButtonFX ที่ช่องนี้
/// </summary>
public class KeyItemSlot : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image icon;
    [Tooltip("กรอบไฮไลท์ตอนถูกเลือก")]
    [SerializeField] private GameObject selectedFrame;

    private KeyItemPage owner;

    public KeyItemData Item { get; private set; }

    internal void Bind(KeyItemPage page) => owner = page;

    internal void Set(KeyItemData item)
    {
        Item = item;
        if (icon != null)
        {
            icon.sprite = item != null ? item.icon : null;
            icon.enabled = item != null && item.icon != null;
        }
        if (item == null) SetSelected(false);
    }

    internal void SetSelected(bool selected)
    {
        if (selectedFrame != null) selectedFrame.SetActive(selected);
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left && Item != null && owner != null) owner.Select(Item);
    }
}
