using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

/// <summary>
/// หน้าแสดงเควสในสมุดบันทึก (หน้าที่ 4 ของสมุด · ปุ่มลัด 'M')
/// สืบทอดจาก JournalPage เพื่อให้ JournalController สลับแท็บได้อัตโนมัติตามหลัก OCP
/// แสดงเควสปัจจุบัน (พร้อมขีดฆ่าเป้าหมายที่สำเร็จแล้ว) และรายการเควสทั้งหมดที่ทำเสร็จแล้ว
/// </summary>
public class QuestPage : JournalPage
{
    [Header("เควสปัจจุบัน (Active Quest)")]
    [SerializeField] private GameObject activeSection;
    [SerializeField] private TMP_Text questTitleText;
    [SerializeField] private TMP_Text questDescText;
    [SerializeField] private Transform objectivesContainer;
    [SerializeField] private QuestEntryUI objectivePrefab;

    [Header("เควสที่ทำเสร็จแล้ว (Completed Quests)")]
    [SerializeField] private GameObject completedSection;
    [SerializeField] private Transform completedContainer;
    [SerializeField] private QuestEntryUI completedEntryPrefab;

    [Header("กรณีไม่มีเควส")]
    [SerializeField] private GameObject emptyMessage;
    [SerializeField] private TMP_Text emptyMessageText;

    private readonly List<QuestEntryUI> spawnedObjectives = new List<QuestEntryUI>();
    private readonly List<QuestEntryUI> spawnedCompleted = new List<QuestEntryUI>();

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

        QuestData active = questService?.ActiveQuest;
        IReadOnlyList<QuestData> completed = questService?.CompletedQuests;
        bool hasCompleted = completed != null && completed.Count > 0;

        // 1. วาดส่วนเควสปัจจุบัน
        if (active != null)
        {
            if (activeSection != null) activeSection.SetActive(true);
            if (emptyMessage != null) emptyMessage.SetActive(false);

            if (questTitleText != null)
            {
                questTitleText.text = locService != null ? locService.GetText(active.titleKey) : active.titleKey;
                if (locService != null) questTitleText.font = locService.GetFont(FontCategory.Header);
            }

            if (questDescText != null)
            {
                questDescText.text = locService != null ? locService.GetText(active.descriptionKey) : active.descriptionKey;
                if (locService != null) questDescText.font = locService.GetFont(FontCategory.Default);
            }

            DrawObjectives(active);
        }
        else
        {
            if (activeSection != null) activeSection.SetActive(false);
            if (emptyMessage != null)
            {
                emptyMessage.SetActive(!hasCompleted);
                if (emptyMessageText != null && locService != null)
                {
                    emptyMessageText.text = locService.GetText("QUEST_EMPTY");
                    emptyMessageText.font = locService.GetFont(FontCategory.Default);
                }
            }
        }

        // 2. วาดส่วนเควสที่ทำเสร็จแล้ว
        if (completedSection != null) completedSection.SetActive(hasCompleted);
        if (hasCompleted)
        {
            DrawCompletedQuests(completed);
        }
    }

    private void DrawObjectives(QuestData active)
    {
        if (active.objectives == null || objectivePrefab == null || objectivesContainer == null) return;

        bool previousDone = true;
        for (int i = 0; i < active.objectives.Length; i++)
        {
            QuestObjective obj = active.objectives[i];
            if (obj == null) continue;

            // หากเลือกซ่อนไว้จนกว่าข้อก่อนหน้าจะเสร็จ และข้อก่อนหน้ายังไม่เสร็จ ให้หยุดวาดข้อนี้และข้อถัดไป
            if (obj.hiddenUntilPrevious && !previousDone) break;

            bool isDone = questService != null && questService.IsObjectiveDone(obj);
            string text = locService != null ? locService.GetText(obj.descriptionKey) : obj.descriptionKey;
            TMP_FontAsset font = locService?.GetFont(FontCategory.Default);

            QuestEntryUI entry = Instantiate(objectivePrefab, objectivesContainer);
            entry.Setup(text, isDone, font);
            spawnedObjectives.Add(entry);

            previousDone = isDone;
        }
    }

    private void DrawCompletedQuests(IReadOnlyList<QuestData> completed)
    {
        if (completedEntryPrefab == null || completedContainer == null) return;

        for (int i = 0; i < completed.Count; i++)
        {
            QuestData q = completed[i];
            if (q == null) continue;

            string title = locService != null ? locService.GetText(q.titleKey) : q.titleKey;
            TMP_FontAsset font = locService?.GetFont(FontCategory.Default);

            QuestEntryUI entry = Instantiate(completedEntryPrefab, completedContainer);
            entry.Setup(title, true, font); // เควสที่จบแล้วขีดฆ่าทั้งอันตามข้อตกลง
            spawnedCompleted.Add(entry);
        }
    }

    private void ClearSpawned()
    {
        for (int i = 0; i < spawnedObjectives.Count; i++)
        {
            if (spawnedObjectives[i] != null) Destroy(spawnedObjectives[i].gameObject);
        }
        spawnedObjectives.Clear();

        for (int i = 0; i < spawnedCompleted.Count; i++)
        {
            if (spawnedCompleted[i] != null) Destroy(spawnedCompleted[i].gameObject);
        }
        spawnedCompleted.Clear();
    }
}
