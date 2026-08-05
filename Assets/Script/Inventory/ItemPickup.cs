using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    [Header("ข้อมูลไอเทม")]
    public ItemData item; // ลากไฟล์ ScriptableObject มาใส่ช่องนี้
    public int amount = 1; // จำนวนที่ได้ต่อการเก็บ 1 ครั้ง

    private bool playerInRange = false; // เช็กว่าผู้เล่นอยู่ในระยะหรือยัง

    void Update()
    {
        // ถ้าผู้เล่นอยู่ในระยะกรอบสีเขียว (Trigger) และกดปุ่ม E
        if (playerInRange && Input.GetKeyDown(KeyCode.E))
        {
            Pickup();
        }
    }

    // เมื่อมีวัตถุเดินเข้ามาในกรอบ Trigger
    void OnTriggerEnter(Collider other)
    {
        // เช็กว่าวัตถุที่เดินเข้ามามี Tag เป็น "Player" ใช่หรือไม่?
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            Debug.Log($"<color=yellow>กด E เพื่อเก็บ {item.name}</color>");
        }
    }

    // เมื่อวัตถุเดินออกจากกรอบ Trigger
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
        }
    }

    void Pickup()
    {
        // เช็กเพื่อความชัวร์ว่ามีระบบกระเป๋าและมีข้อมูลไอเทมอยู่จริงๆ
        if (Inventory.Instance != null && item != null)
        {
            // สั่งยัดของเข้ากระเป๋า
            Inventory.Instance.AddItem(item, amount);
            Debug.Log($"<color=green>เก็บ {item.name} สำเร็จ!</color>");

            // ทำลายไอเทมชิ้นนี้บนพื้นทิ้งไป
            Destroy(gameObject);
        }
        else
        {
            Debug.LogWarning("เก็บไม่ได้! หากระเป๋าไม่เจอ หรือยังไม่ได้ใส่ข้อมูล ItemData");
        }
    }
}