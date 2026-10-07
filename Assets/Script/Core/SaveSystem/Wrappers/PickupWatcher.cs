using UnityEngine;

/// <summary>
/// ตัวเฝ้าไอเทมในแมพ — แปะคู่กับ ItemPickup (ของเพื่อน) ทุกชิ้นที่วางในแมพ
/// ItemPickup ทำลายตัวเองตอนถูกเก็บ → ตัวนี้ถูกทำลายตามมา → แจ้ง WorldPickupTracker ว่าชิ้นนี้ถูกเก็บแล้ว → ถูกเซฟ
///
/// วิธีแปะ: คลิกขวาที่ WorldPickupTracker (ใน [STORY]) → "แปะตัวเฝ้าให้ไอเทมทุกชิ้นในซีน"
/// (2026-10-07: เลิกแปะด้วยโค้ดตอนเล่น — ทุกอย่างต้องเห็นและแก้ได้ใน Editor)
///
/// รหัสถาวร สุ่มให้ตอนแปะ → ย้ายตำแหน่ง/เปลี่ยนชื่อไอเทมในแมพได้ เซฟเก่าไม่เพี้ยน
/// ⚠️ Duplicate ไอเทมที่มีตัวเฝ้าแล้ว รหัสจะซ้ำ → tracker เตือนใน Console → คลิกขวาที่ตัวเฝ้า "สุ่มรหัสใหม่"
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ItemPickup))]
public class PickupWatcher : MonoBehaviour
{
    [SerializeField, Tooltip("รหัสถาวร ระบบสุ่มให้ ห้ามแก้")]
    private string pickupId;

    private WorldPickupTracker tracker;
    private bool silent;

    public string PickupId => pickupId;

    internal void Bind(WorldPickupTracker owner) => tracker = owner;

    /// <summary>โหลดเซฟแล้วชิ้นนี้เคยถูกเก็บไปแล้ว → ลบออกจากแมพโดยไม่นับเป็นการเก็บใหม่</summary>
    internal void RemoveSilently()
    {
        silent = true;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (!silent && tracker != null) tracker.MarkCollected(pickupId);
    }

#if UNITY_EDITOR
    private void Reset() => RegenerateId();

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(pickupId)) RegenerateId();
    }

    [ContextMenu("สุ่มรหัสใหม่ (ใช้เมื่อ Duplicate แล้วรหัสซ้ำ)")]
    internal void RegenerateId()
    {
        pickupId = System.Guid.NewGuid().ToString("N");
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
