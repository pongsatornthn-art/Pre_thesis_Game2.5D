using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ตารางสารบัญรวมเควสทั้งหมดในโปรเจกต์ (ScriptableObject)
/// ใช้แปลง questId กลับเป็น Asset QuestData ตอนโหลดเซฟเกม
/// 
/// เหตุผลที่ใช้ Catalog แทน Resources.LoadAll:
/// Asset ที่ไม่ได้อยู่ในโฟลเดอร์ Resources จะไม่ถูกค้นพบด้วย LoadAll ซึ่งเป็นบทเรียนจริงจากระบบสมุด
/// การใช้ Catalog เป็นหลักช่วยให้ Unity แพ็กไฟล์เข้า Build ได้อย่างแม่นยำ และยังคงมี Fallback ไปที่ Resources เผื่อไว้
/// </summary>
[CreateAssetMenu(fileName = "QuestCatalog", menuName = "Story/Quest Catalog", order = 3)]
public class QuestCatalog : ScriptableObject
{
    [Tooltip("ลาก Asset เควสทั้งหมดในเกมมาใส่ที่นี่")]
    [SerializeField] private List<QuestData> quests = new List<QuestData>();

    // แคช Resources เผื่อจำเป็นต้องค้นหาแบบ Fallback
    private QuestData[] cachedResourceQuests;

    public IReadOnlyList<QuestData> AllQuests => quests;

    /// <summary>ค้นหา QuestData จาก questId</summary>
    public QuestData FindQuest(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return null;

        // 1. ค้นหาใน Catalog ที่ลากใส่ไว้ก่อน
        if (quests != null)
        {
            for (int i = 0; i < quests.Count; i++)
            {
                if (quests[i] != null && quests[i].questId == questId)
                {
                    return quests[i];
                }
            }
        }

        // 2. หากไม่พบใน Catalog ให้ Fallback ไปค้นหาใน Resources พร้อมเตือนใน Console
        if (cachedResourceQuests == null)
        {
            cachedResourceQuests = Resources.LoadAll<QuestData>("");
        }

        if (cachedResourceQuests != null)
        {
            for (int i = 0; i < cachedResourceQuests.Length; i++)
            {
                if (cachedResourceQuests[i] != null && cachedResourceQuests[i].questId == questId)
                {
                    Debug.LogWarning($"[QuestCatalog] พบเควส '{questId}' ใน Resources แต่ไม่ได้ใส่ใน Catalog — ควรลากใส่ Catalog ให้เรียบร้อย");
                    return cachedResourceQuests[i];
                }
            }
        }

        Debug.LogError($"[QuestCatalog] ไม่พบเควส id '{questId}' ทั้งใน Catalog และ Resources");
        return null;
    }
}
