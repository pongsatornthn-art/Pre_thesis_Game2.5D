using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    [Header("ข้อมูลไอเทม")]
    public ItemData item;
    public int amount = 1;

    // เปลี่ยนจาก void Pickup() เป็น public void Pickup() 
    // เพื่อให้ PlayerInteraction มาสั่งทำงานได้
    public void Pickup()
    {
        if (Inventory.Instance != null && item != null)
        {
            Inventory.Instance.AddItem(item, amount);
            Debug.Log($"<color=green>เก็บ {item.name} สำเร็จ!</color>");
            Destroy(gameObject);
        }
        else
        {
            Debug.LogWarning("เก็บไม่ได้! หากระเป๋าไม่เจอ หรือยังไม่ได้ใส่ข้อมูล");
        }
    }
}