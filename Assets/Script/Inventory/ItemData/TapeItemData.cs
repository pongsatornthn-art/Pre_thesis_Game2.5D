using UnityEngine;

/// <summary>
/// [Claude 2026-11-26] เทปเสียง 1 ม้วน — เก็บจากในฉากแล้วถึงจะเปิดฟังที่วิทยุได้
/// ไม่เข้ากระเป๋า (ไม่กินช่อง) แต่จำไว้ในความจำกลาง (StoryFlags) → เซฟ/โหลดแล้วยังอยู่
///
/// วิธีใช้:
///   1) Project คลิกขวา ▸ Create ▸ Inventory ▸ Items ▸ Tape → ใส่ชื่อ + ไฟล์เสียงในช่อง Clip
///   2) วางของในฉากที่มี ItemPickup แล้วลากไฟล์เทปนี้ใส่ช่อง Item (เหมือนเก็บของทั่วไป)
///   3) ที่วิทยุ (RadioTapeSystem) ลากไฟล์เทปใส่ช่อง Tape Items ตามลำดับปุ่ม
/// </summary>
[CreateAssetMenu(fileName = "New Tape", menuName = "Inventory/Items/Tape")]
public class TapeItemData : ItemData
{
    [Header("เทป")]
    [Tooltip("เสียงในเทป (วิทยุจะเล่นไฟล์นี้)")]
    public AudioClip clip;

    [Header("ซับไตเติล [Claude 2026-11-28]")]
    [Tooltip("ซับขึ้นตามเวลาเสียง (วินาที) · ข้อความเป็น key ใน LocalizationData.csv → เปลี่ยนภาษาได้\nแก้เวลาให้ตรงเสียงได้ที่นี่เลย · ว่าง = ไม่มีซับ\nบทต้นฉบับ: Google Doc ของทีม แท็บ 'Dialog เสียงวิทยุ'")]
    public System.Collections.Generic.List<TapeSubtitleLine> subtitles = new System.Collections.Generic.List<TapeSubtitleLine>();

    /// <summary>ซับบรรทัดที่ควรขึ้น ณ เวลานี้ของเสียง (null = ช่วงเงียบ/ไม่มีซับ)</summary>
    public TapeSubtitleLine LineAt(float time)
    {
        if (subtitles == null) return null;
        foreach (TapeSubtitleLine line in subtitles)
            if (line != null && time >= line.start && time < line.end) return line;
        return null;
    }

    public string CollectedKey => "tape:" + ItemId;

    private void Reset()
    {
        itemType = ItemType.General;
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
            Debug.LogError($"[TapeItemData] เก็บเทป '{name}' ไม่ได้ — ไม่มี StoryFlagService ([STORY]) ในซีน", this);
            return false;
        }
        flags.SetKey(CollectedKey);
        return true;
    }
}

/// <summary>[Claude 2026-11-28] ซับเทป 1 บรรทัด — เวลาเริ่ม/จบ (วินาทีในไฟล์เสียง) + key ข้อความ + key ชื่อคนพูด (ไม่บังคับ)</summary>
[System.Serializable]
public class TapeSubtitleLine
{
    [Min(0f)] public float start;
    [Min(0f)] public float end;
    [Tooltip("key ชื่อคนพูด เช่น TAPE_SPEAKER_ROWAN (เว้นว่าง = ไม่มีชื่อ เช่น เทปรายงาน)")]
    public string speakerKey;
    public string textKey;
}
