using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// หน้าแผนที่ในสมุด (งานข้อ 4) — กด M เปิด · ESC / M ปิด (สมุดจัดการให้)
/// มีแผนที่แล้ว (ธง Flag_MansionMapTaken หรือมีไอเทมแผนที่ในกระเป๋า) = โชว์รูปผังบ้าน
/// ยังไม่มี = โชว์ข้อความ "ยังไม่มีแผนที่"
/// รูปตอนนี้ = ผังพิมพ์เขียวที่วาดจากผนังในซีน (Art/UI/Map/MansionMap.png) — มีรูปจริงแล้วลากใส่ช่อง Map Image แทนได้เลย
/// [Claude 2026-11-11]
/// </summary>
public class MapPage : JournalPage
{
    [Header("Map")]
    [Tooltip("รูปแผนที่")]
    [SerializeField] private Image mapImage;
    [Tooltip("ข้อความตอนยังไม่มีแผนที่ (ใส่ LocalizedText key MAP_NONE)")]
    [SerializeField] private GameObject noMapMessage;

    [Header("มีแผนที่เมื่อ (อย่างใดอย่างหนึ่ง)")]
    [SerializeField] private StoryFlagId hasMapFlag;
    [SerializeField] private ItemData mapItem;

    private void Reset()
    {
        // ค่าเริ่มต้นใน Inspector: PageId = "map", ShortcutKey = M (เหมือนวิธีของ QuestPage)
        var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        typeof(JournalPage).GetField("pageId", bf)?.SetValue(this, "map");
        typeof(JournalPage).GetField("shortcutKey", bf)?.SetValue(this, KeyCode.M);
    }

    public override void Refresh()
    {
        bool has = HasMap();
        if (mapImage != null) mapImage.gameObject.SetActive(has);
        if (noMapMessage != null) noMapMessage.SetActive(!has);
    }

    private bool HasMap()
    {
        IStoryFlags flags = ServiceLocator.GetOptional<IStoryFlags>();
        if (hasMapFlag != null && flags != null && flags.Has(hasMapFlag)) return true;
        if (mapItem != null && Inventory.Instance != null && Inventory.Instance.GetItemCount(mapItem) > 0) return true;
        return false;
    }
}
