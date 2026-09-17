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
        if (item == null)
        {
            Debug.LogWarning("เก็บไม่ได้! ยังไม่ได้ใส่ข้อมูลไอเทมในช่อง Item");
            return;
        }

        // 🌟 ให้ไอเทมเป็นคนตัดสินใจเองว่าจะเข้าคลังไหน (กระเป๋า / ของสำคัญ / สมุดเอกสาร)
        // ไม่ต้องมาเช็คชนิดตรงนี้ เพิ่มไอเทมชนิดใหม่ในอนาคตไม่ต้องกลับมาแก้ไฟล์นี้
        if (item.Collect(amount))
        {
            Debug.Log($"<color=green>เก็บ {item.itemName} สำเร็จ!</color>");
            Destroy(gameObject);
        }
        else
        {
            Debug.LogWarning($"เก็บ {item.itemName} ไม่ได้ (กระเป๋าเต็ม หรือยังไม่มีคลังปลายทางในซีน)");
        }
    }
}