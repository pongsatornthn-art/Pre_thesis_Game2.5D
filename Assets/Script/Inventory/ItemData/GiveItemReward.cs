using UnityEngine;

public class GiveItemReward : MonoBehaviour
{
    [Header("ไอเทมที่จะแจกผู้เล่น")]
    public ItemData itemToGive;
    public int amount = 1;

    // ฟังก์ชันนี้จะถูกเรียกตอนส่งเควสสำเร็จ
    public void GiveRewardToPlayer()
    {
        if (itemToGive == null)
        {
            Debug.LogWarning("ยังไม่ได้ใส่ไอเทมที่จะแจก!");
            return;
        }

        if (Inventory.Instance != null)
        {
            // สั่งเรียกใช้ AddItem จากสคริปต์ Inventory ของคุณพงศธร
            bool success = Inventory.Instance.AddItem(itemToGive, amount);

            if (success)
            {
                Debug.Log($"<color=green>ได้รับ {itemToGive.itemName} เข้ากระเป๋าแล้ว!</color>");
            }
        }
        else
        {
            Debug.LogError("หา Inventory ของผู้เล่นไม่เจอในฉาก!");
        }
    }
}