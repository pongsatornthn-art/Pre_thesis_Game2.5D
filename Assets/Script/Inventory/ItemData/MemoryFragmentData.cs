using UnityEngine;

/// <summary>
/// ไอเทม "ชิ้นส่วนความทรงจำ" — เช่น เศษรูปวาด · Create > Inventory/Items/Memory Fragment
/// วางในแมพด้วย ItemPickup (+ PickupWatcher) แบบไอเทมอื่น · เก็บแล้ว**ไม่เข้ากระเป๋า/คลังกุญแจ**
/// แต่ปลดช่องในคลังความทรงจำ (หน้า L) ของตัวเอง — แบบเดียวกับ KeyItemData → คลังกุญแจ (ItemData.Collect ตัดสินใจเอง)
///
/// มินิเกมประกอบรูป / เป้าหมายเควส "เก็บไอเทมให้ครบชุด" นับชิ้นนี้ได้ผ่าน ItemOwnership
/// </summary>
[CreateAssetMenu(fileName = "Fragment_", menuName = "Inventory/Items/Memory Fragment")]
public class MemoryFragmentData : ItemData
{
    [Header("คลังความทรงจำ")]
    [Tooltip("ช่องที่ชิ้นนี้ปลด (รูป/ชื่อ/เรื่องย่อในหน้า L มาจากช่องนี้)")]
    public MemoryEntryData entry;

    [Tooltip("เพิ่มตัวนับนี้ +1 ตอนเก็บด้วย (ไม่บังคับ) — เช่น Counter_PicturePieces ให้เควสแบบนับ (x/N) ของปอเดินต่อได้")]
    public StoryCounterId alsoAddCounter;

    private void Reset()
    {
        itemType = ItemType.Memory;
        isStackable = false;
    }

    public override bool Collect(int amount = 1)
    {
        if (entry == null)
        {
            Debug.LogError($"[MemoryFragmentData] '{name}' ยังไม่ได้ใส่ช่อง Entry — เก็บแล้วจะไม่มีอะไรโผล่ในคลังความทรงจำ", this);
            return false;
        }

        IMemoryArchive archive = ServiceLocator.GetOptional<IMemoryArchive>();
        if (archive == null)
        {
            Debug.LogError($"[MemoryFragmentData] เก็บ '{name}' ไม่ได้ — ไม่มี MemoryArchive ในซีน (แปะที่ [STORY])", this);
            return false;
        }

        bool firstTime = !archive.IsUnlocked(entry);
        archive.Unlock(entry);
        if (firstTime && alsoAddCounter != null) ServiceLocator.GetOptional<IStoryCounters>()?.Add(alsoAddCounter, 1);
        return true;
    }
}
