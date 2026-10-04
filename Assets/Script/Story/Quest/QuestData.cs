using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ข้อมูลเควส 1 ภารกิจ (ScriptableObject) — สร้างจาก Create > Story > Quest
///
/// ไม่มีลำดับตายตัว: เควสหลายอันทำพร้อมกันได้ ผู้เล่นวิ่งทั่วแมพ
/// อยากให้เควสนี้ต้องรออันอื่นก่อน → ตั้ง "Available When" (เช่น Quest Completed ของอีกเควส)
/// อยากให้ข้อไหนในเควสต้องรอ → ตั้ง "Requires" ของข้อนั้น
/// → level designer จัดลำดับเองใน Inspector ทั้งหมด (สเปก: QUEST_SAVE_SPEC.md หัวข้อ 3)
/// </summary>
[CreateAssetMenu(fileName = "Quest_", menuName = "Story/Quest", order = 2)]
public class QuestData : ScriptableObject
{
    [Tooltip("รหัสถาวรของเควส ห้ามแก้หลังเริ่มใช้งาน เพราะระบบเซฟใช้อ้างอิง")]
    public string questId;

    [Tooltip("คีย์ชื่อเควสใน LocalizationData.csv")]
    public string titleKey;

    [Tooltip("คีย์คำอธิบายเควสใน LocalizationData.csv (เว้นว่างได้)")]
    public string descriptionKey;

    [Header("เริ่มเควสเมื่อไหร่")]
    [Tooltip("✔ = เริ่มเองอัตโนมัติเมื่อเงื่อนไขด้านล่างครบ (ไม่มีเงื่อนไข = เริ่มทันทีตอนเข้าเกม)\n" +
             "✘ = เริ่มเฉพาะตอนฉากเนื้อเรื่องสั่ง GiveQuestAction")]
    public bool autoStart;

    [Tooltip("เงื่อนไขก่อนเริ่ม (prerequisite) — ใช้คู่กับ Auto Start เช่น ต้องจบเควส A ก่อน / ต้องมีธง X")]
    [SerializeReference, SubclassPicker]
    public IStoryCondition availableWhen;

    [Header("เป้าหมาย (ทำข้อไหนก่อนก็ได้ ถ้าไม่ได้ตั้ง Requires)")]
    [SerializeReference, SubclassPicker]
    public List<QuestObjective> objectives = new List<QuestObjective>();

    [Header("เมื่อเควสจบ")]
    [Tooltip("ฉากที่จะเล่นตอนทำครบทุกข้อ เช่น ส่งเควส → Cutscene → ออกจาก PTSD (เว้นว่างได้)")]
    public StorySequence onCompleted;

#if UNITY_EDITOR
    /// <summary>
    /// สุ่มรหัสถาวรให้เป้าหมายที่ยังไม่มี และแก้รหัสซ้ำ
    /// รหัสซ้ำเกิดได้เมื่อกด Duplicate ข้อใน Inspector — ถ้าไม่แก้ ขีดฆ่าข้อหนึ่งอีกข้อจะขีดตามด้วย
    /// </summary>
    private void OnValidate()
    {
        if (objectives == null) return;

        HashSet<string> seenIds = new HashSet<string>();
        HashSet<QuestObjective> seenRefs = new HashSet<QuestObjective>();
        bool dirty = false;

        for (int i = 0; i < objectives.Count; i++)
        {
            QuestObjective obj = objectives[i];
            if (obj == null) continue;

            // Unity บางเวอร์ชันกด + ในลิสต์แล้วได้ "ตัวเดียวกัน" มา 2 ช่อง → แก้ช่องหนึ่งอีกช่องเปลี่ยนตาม
            if (!seenRefs.Add(obj))
            {
                Debug.LogWarning($"[QuestData] '{name}' เป้าหมายข้อที่ {i + 1} ชี้ไปที่ตัวเดียวกับข้ออื่น — ล้างช่องนี้แล้ว กรุณาเลือกชนิดใหม่", this);
                objectives[i] = null;
                dirty = true;
                continue;
            }

            if (string.IsNullOrEmpty(obj.objectiveId) || !seenIds.Add(obj.objectiveId))
            {
                obj.RegenerateId();
                seenIds.Add(obj.objectiveId);
                dirty = true;
            }
        }

        if (dirty) UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
