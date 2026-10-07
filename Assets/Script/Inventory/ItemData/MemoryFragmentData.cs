using UnityEngine;

/// <summary>
/// ไอเทม "เศษ/ชิ้นส่วน" ที่เก็บแล้ว **ไม่โชว์ในคลังไหนเลย** (ไม่เข้ากระเป๋า ไม่เข้าคีย์ไอเทม) — เช่น เศษรูปวาด · Create > Inventory/Items/Memory Fragment
/// วางในแมพด้วย ItemPickup (+ PickupWatcher) แบบไอเทมอื่น · เก็บแล้วระบบแค่ "จำไว้" ในความจำกลาง (ธงไม่มีไฟล์ — เซฟ/โหลดได้)
///
/// ใครอยากรู้ว่ามีหรือยัง ถามผ่าน ItemOwnership.Has(ชิ้นนี้) — มินิเกมประกอบรูป (ช่องขวา) / เงื่อนไข "มีไอเทมครบชุด" / เควส x/N ใช้ได้ทันที
/// เจ้าของตกลง 2026-10-07: ยุบคลังความทรงจำ (หน้า L) ทิ้ง — เศษไม่ต้องโชว์ ของที่ประกอบเสร็จค่อยเข้าคีย์ไอเทม
/// </summary>
[CreateAssetMenu(fileName = "Fragment_", menuName = "Inventory/Items/Memory Fragment")]
public class MemoryFragmentData : ItemData
{
    [Tooltip("เพิ่มตัวนับนี้ +1 ตอนเก็บครั้งแรกด้วย (ไม่บังคับ) — เช่น Counter_PicturePieces ให้เควสแบบนับ (x/N) เดินต่อได้")]
    public StoryCounterId alsoAddCounter;

    /// <summary>ชื่อในความจำกลาง — ใช้รหัสไอเทม (ItemId) ไม่ซ้ำกันอยู่แล้ว</summary>
    public string CollectedKey => "fragment:" + ItemId;

    private void Reset()
    {
        itemType = ItemType.Memory;
        isStackable = false;
    }

    public bool IsCollected()
    {
        IStoryFlags flags = ServiceLocator.GetOptional<IStoryFlags>();
        return flags != null && flags.HasKey(CollectedKey);
    }

    public override bool Collect(int amount = 1)
    {
        IStoryFlags flags = ServiceLocator.GetOptional<IStoryFlags>();
        if (flags == null)
        {
            Debug.LogError($"[MemoryFragmentData] เก็บ '{name}' ไม่ได้ — ไม่มีความจำกลาง (StoryFlagService ที่ [STORY]) ในซีน", this);
            return false;
        }

        bool firstTime = !flags.HasKey(CollectedKey);
        flags.SetKey(CollectedKey);
        if (firstTime && alsoAddCounter != null) ServiceLocator.GetOptional<IStoryCounters>()?.Add(alsoAddCounter, 1);
        return true;
    }
}
