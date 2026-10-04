using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

/// <summary>
/// หน้าเควสในสมุดบันทึก (หน้าที่ 4 ของสมุด · ปุ่มลัด 'M')
/// สืบทอดจาก JournalPage เพื่อให้ JournalController สลับแท็บได้อัตโนมัติตามหลัก OCP
///
/// แบบสมุดจดจริง: ทุกเควสที่เคยได้รับเรียงตามลำดับที่ได้รับ
/// เสร็จแล้วขีดฆ่าอยู่ที่เดิม ไม่ย้าย · เป้าหมายแบบนับโชว์ (2/4)
///
/// ⚠️ ช่วง 1: วาดแบบเรียบง่ายด้วย prefab แถวเดียว — หน้าตาจริง (กระดาษ/ลายมือ) ทำในช่วง 2 (QUEST_SAVE_SPEC.md)
/// </summary>
public class QuestPage : JournalPage
{
    [Header("รายการในสมุด")]
    [Tooltip("ที่วางแถวทั้งหมด (ควรมี Vertical Layout Group)")]
    [SerializeField] private Transform entriesContainer;

    [Tooltip("แถวหัวข้อเควส")]
    [SerializeField] private QuestEntryUI questTitlePrefab;

    [Tooltip("แถวเป้าหมาย (ย่อหน้าเข้าไปเล็กน้อย)")]
    [SerializeField] private QuestEntryUI objectivePrefab;

    [Header("กรณีไม่มีเควส")]
    [SerializeField] private GameObject emptyMessage;
    [SerializeField] private TMP_Text emptyMessageText;

    private readonly List<QuestEntryUI> spawned = new List<QuestEntryUI>();

    private IQuestService questService;
    private ILocalizationService locService;

    private void Reset()
    {
        // ตั้งค่าเริ่มต้นของหน้านี้ใน Inspector อัตโนมัติ (PageId = "quest", ShortcutKey = M)
        FieldInfo idField = typeof(JournalPage).GetField("pageId", BindingFlags.NonPublic | BindingFlags.Instance);
        idField?.SetValue(this, "quest");

        FieldInfo keyField = typeof(JournalPage).GetField("shortcutKey", BindingFlags.NonPublic | BindingFlags.Instance);
        keyField?.SetValue(this, KeyCode.M);
    }

    private void OnEnable()
    {
        questService = ServiceLocator.Get<IQuestService>();
        if (questService != null) questService.OnChanged += Refresh;

        locService = ServiceLocator.Get<ILocalizationService>();
        if (locService != null) locService.OnLanguageChanged += Refresh;
    }

    private void OnDisable()
    {
        if (questService != null) questService.OnChanged -= Refresh;
        if (locService != null) locService.OnLanguageChanged -= Refresh;
    }

    public override void Refresh()
    {
        if (questService == null) questService = ServiceLocator.Get<IQuestService>();
        if (locService == null) locService = ServiceLocator.Get<ILocalizationService>();

        ClearSpawned();

        IReadOnlyList<QuestData> journal = questService?.Journal;
        bool hasAny = journal != null && journal.Count > 0;

        if (emptyMessage != null) emptyMessage.SetActive(!hasAny);
        if (!hasAny)
        {
            if (emptyMessageText != null && locService != null)
            {
                emptyMessageText.text = locService.GetText("QUEST_EMPTY");
                emptyMessageText.font = locService.GetFont(FontCategory.Default);
            }
            return;
        }

        if (entriesContainer == null) return;

        // เรียงตามลำดับที่ได้รับ — ไม่ย้ายเควสที่จบแล้วลงล่าง (เหมือนจดสมุดจริง)
        for (int i = 0; i < journal.Count; i++)
        {
            QuestData quest = journal[i];
            if (quest == null) continue;

            bool questDone = questService.GetState(quest) == QuestState.Completed;
            Spawn(questTitlePrefab, GetText(quest.titleKey), questDone, FontCategory.Header);

            if (quest.objectives == null) continue;
            for (int j = 0; j < quest.objectives.Count; j++)
            {
                QuestObjective obj = quest.objectives[j];
                if (obj == null || !questService.IsObjectiveVisible(quest, obj)) continue;

                string text = GetText(obj.descriptionKey);
                if (questService.TryGetObjectiveProgress(quest, obj, out int current, out int target) && target > 1)
                {
                    text += $" ({current}/{target})";
                }

                Spawn(objectivePrefab, text, questService.IsObjectiveDone(quest, obj), FontCategory.Default);
            }
        }
    }

    private void Spawn(QuestEntryUI prefab, string text, bool isDone, FontCategory fontCategory)
    {
        if (prefab == null) return;

        QuestEntryUI entry = Instantiate(prefab, entriesContainer);
        entry.Setup(text, isDone, locService?.GetFont(fontCategory));
        spawned.Add(entry);
    }

    private string GetText(string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;
        return locService != null ? locService.GetText(key) : key;
    }

    private void ClearSpawned()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null) Destroy(spawned[i].gameObject);
        }
        spawned.Clear();
    }
}
