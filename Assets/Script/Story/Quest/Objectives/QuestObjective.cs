using System;
using UnityEngine;

/// <summary>
/// คลาสแม่ของ "เป้าหมาย" 1 ข้อในเควส — ทุกชนิดเป้าหมายสืบทอดจากตัวนี้
///
/// เพิ่มชนิดใหม่ (เช่น ฆ่าผี N ตัว / ยืนในโซนครบ X วินาที):
///   สร้างคลาสลูก [Serializable] แล้ว override IsMet() → โผล่ในเมนูเลือกชนิดใน Inspector เอง
///   ไม่ต้องแก้ QuestService / QuestData / หน้าสมุด (Open-Closed)
///
/// QuestService ไม่รู้จักคลาสลูกตัวไหนเลย ถามผ่าน IsMet / TryGetProgress เท่านั้น (Liskov)
/// </summary>
[Serializable]
public abstract class QuestObjective
{
    [SerializeField, HideInInspector]
    internal string objectiveId;   // รหัสถาวร QuestData สุ่มให้เองใน OnValidate · ระบบเซฟใช้จำว่าข้อไหนขีดฆ่าแล้ว

    [Tooltip("คีย์ข้อความในสมุด (LocalizationData.csv) เช่น QUEST_FIND_KEY_OBJ1")]
    public string descriptionKey;

    [Header("ลำดับ (เว้นว่าง = ทำข้อนี้เมื่อไหร่ก็ได้)")]
    [Tooltip("ข้อนี้ทำได้เมื่อเงื่อนไขนี้ครบ เช่น 'ส่งเควส' ทำได้เมื่อเก็บชิ้นส่วนครบ")]
    [SerializeReference, SubclassPicker]
    public IStoryCondition requires;

    [Tooltip("ซ่อนข้อนี้ในสมุดจนกว่าเงื่อนไขด้านบนจะครบ (กันสปอยล์)")]
    public bool hiddenUntilAvailable;

    [Tooltip("สำรองไว้สำหรับเป้าหมายจับเวลา (ช่อง 'ระยะเวลา' ในการ์ด Figma) — ตอนนี้ยังไม่ทำงาน · 0 = ไม่จำกัด")]
    [Min(0f)] public float timeLimitSeconds;

    public string ObjectiveId => objectiveId;

    /// <summary>ข้อนี้เริ่มทำได้หรือยัง (ผ่านเงื่อนไข requires)</summary>
    public bool IsAvailable(StoryContext ctx) => requires == null || requires.IsMet(ctx);

    /// <summary>เงื่อนไขของข้อนี้ครบหรือยัง — QuestService จะ "ล็อก" ว่าเสร็จทันทีที่ครบครั้งแรก</summary>
    public abstract bool IsMet(StoryContext ctx);

    /// <summary>
    /// ข้อที่นับได้ (เช่น 2/4) ให้คืน true พร้อมตัวเลข — สมุดจะโชว์ตัวเลขเมื่อ target มากกว่า 1
    /// ข้อธรรมดาไม่ต้อง override
    /// </summary>
    public virtual bool TryGetProgress(StoryContext ctx, out int current, out int target)
    {
        current = 0;
        target = 0;
        return false;
    }

    internal void RegenerateId() => objectiveId = Guid.NewGuid().ToString("N");
}
