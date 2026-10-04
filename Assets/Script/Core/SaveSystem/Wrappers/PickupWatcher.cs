using UnityEngine;

/// <summary>
/// ตัวเฝ้าไอเทมในแมพ — WorldPickupTracker แปะให้เองตอนเริ่มซีน **ไม่ต้องแปะเอง**
/// ItemPickup ของเพื่อนทำลายตัวเองตอนถูกเก็บ → ตัวนี้ถูกทำลายตามมา → แจ้ง tracker ว่าชิ้นนี้หายไปแล้ว
/// </summary>
[DisallowMultipleComponent]
public class PickupWatcher : MonoBehaviour
{
    private WorldPickupTracker tracker;
    private string id;
    private bool silent;

    internal void Init(WorldPickupTracker owner, string pickupId)
    {
        tracker = owner;
        id = pickupId;
    }

    /// <summary>โหลดเซฟแล้วชิ้นนี้เคยถูกเก็บไปแล้ว → ลบออกจากแมพโดยไม่นับเป็นการเก็บใหม่</summary>
    internal void RemoveSilently()
    {
        silent = true;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (!silent && tracker != null) tracker.MarkCollected(id);
    }
}
