using UnityEngine;
using UnityEngine.Events;

public class ItemPickup : MonoBehaviour
{
    [Header("ข้อมูลไอเทม")]
    public ItemData item;
    public int amount = 1;

    [Header("Quest & Story Integration")]
    [Tooltip("ลากระบบปักธงเควส (เช่น StoryFlagSetter หรือ StoryCollectible) มาสั่งทำงานที่นี่")]
    public UnityEvent OnPickupSuccess;

    public void Pickup()
    {
        if (item == null)
        {
            Debug.LogWarning("เก็บไม่ได้! ยังไม่ได้ใส่ข้อมูลไอเทมในช่อง Item");
            return;
        }

        if (item.Collect(amount))
        {
            Debug.Log($"<color=green>เก็บ {item.itemName} สำเร็จ!</color>");
            OnPickupSuccess?.Invoke();

            Destroy(gameObject);
        }
        else
        {
            Debug.LogWarning($"เก็บ {item.itemName} ไม่ได้ (คลังปลายทางมีปัญหา)");
        }
    }
}