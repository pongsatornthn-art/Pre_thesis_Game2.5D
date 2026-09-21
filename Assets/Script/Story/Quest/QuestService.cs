using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ระบบบริการเควสส่วนกลาง (Quest Service)
/// คอยติดตามสถานะธง (IStoryFlags) เพื่อประเมินว่าเป้าหมายเควสสำเร็จหรือยัง
/// จัดการการเริ่มเควสถัดไปแบบอัตโนมัติ และบันทึก/โหลดสถานะผ่าน ISaveable
/// </summary>
public class QuestService : MonoBehaviour, IQuestService, ISaveable
{
    [Header("Catalog สำหรับค้นหาเควสตอนโหลดเซฟ")]
    [SerializeField] private QuestCatalog catalog;

    private QuestData activeQuest;
    private readonly List<QuestData> completedQuests = new List<QuestData>();
    private IStoryFlags flagsService;

    public QuestData ActiveQuest => activeQuest;
    public IReadOnlyList<QuestData> CompletedQuests => completedQuests;
    public event Action OnChanged;

    [Serializable]
    private struct QuestSaveData
    {
        public string activeQuestId;
        public List<string> completedQuestIds;
    }

    private void Awake() => ServiceLocator.Register<IQuestService>(this);

    private void Start()
    {
        flagsService = ServiceLocator.Get<IStoryFlags>();
        if (flagsService != null)
        {
            flagsService.OnChanged += HandleFlagsChanged;
            HandleFlagsChanged(); // ตรวจสอบสถานะทันทีเมื่อเริ่มเกม
        }
    }

    private void OnDestroy()
    {
        if (flagsService != null) flagsService.OnChanged -= HandleFlagsChanged;
        ServiceLocator.Unregister<IQuestService>();
    }

    public void StartQuest(QuestData quest)
    {
        if (quest == null) return;

        // กันเควสที่ทำจบไปแล้วถูกเริ่มซ้ำ (เช่น เหยียบทริกเกอร์เดิมอีกรอบ หรือโหลดเซฟแล้วเดินซ้ำทาง)
        if (completedQuests.Contains(quest)) return;

        if (activeQuest == quest) return;

        if (activeQuest != null)
        {
            Debug.LogWarning($"[QuestService] มีเควส '{activeQuest.questId}' อยู่แล้ว — เริ่มเควสใหม่ '{quest.questId}' ทับของเดิม");
        }

        activeQuest = quest;
        Debug.Log($"<color=green>📜 [QuestService] เริ่มเควส: {quest.questId}</color>");
        OnChanged?.Invoke();
        HandleFlagsChanged(); // ตรวจสอบทันทีเผื่อเงื่อนไขครบแล้ว
    }

    public bool IsObjectiveDone(QuestObjective objective)
    {
        if (objective == null || objective.completedWhenFlagSet == null) return false;
        if (flagsService == null) flagsService = ServiceLocator.Get<IStoryFlags>();
        return flagsService != null && flagsService.Has(objective.completedWhenFlagSet);
    }

    private void HandleFlagsChanged()
    {
        if (activeQuest == null) return;

        // เควสที่ยังไม่ได้ใส่เป้าหมาย ห้ามนับว่าสำเร็จ
        // ไม่งั้นพอสร้าง QuestData ใหม่แล้วยังไม่ทันกรอก objectives มันจะจบทันที
        // แล้วลาม StartQuest ตัวถัดไปเป็นทอดๆ จนเนื้อเรื่องวิ่งรวดเดียวจบ
        if (activeQuest.objectives == null || activeQuest.objectives.Length == 0)
        {
            OnChanged?.Invoke();
            return;
        }

        bool allDone = true;
        for (int i = 0; i < activeQuest.objectives.Length; i++)
        {
            if (!IsObjectiveDone(activeQuest.objectives[i]))
            {
                allDone = false;
                break;
            }
        }

        if (allDone) CompleteCurrentQuest();
        else OnChanged?.Invoke();
    }

    private void CompleteCurrentQuest()
    {
        if (activeQuest == null) return;
        QuestData finished = activeQuest;
        if (!completedQuests.Contains(finished)) completedQuests.Add(finished);
        activeQuest = null;

        Debug.Log($"<color=green>🏆 [QuestService] เควสสำเร็จ: {finished.questId}</color>");
        OnChanged?.Invoke();

        if (finished.nextQuest != null) StartQuest(finished.nextQuest);
    }

    #region ISaveable Implementation

    public string CaptureState()
    {
        QuestSaveData data = new QuestSaveData
        {
            activeQuestId = activeQuest != null ? activeQuest.questId : string.Empty,
            completedQuestIds = new List<string>()
        };

        for (int i = 0; i < completedQuests.Count; i++)
        {
            if (completedQuests[i] != null && !string.IsNullOrEmpty(completedQuests[i].questId))
            {
                data.completedQuestIds.Add(completedQuests[i].questId);
            }
        }
        return JsonUtility.ToJson(data);
    }

    public void RestoreState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson)) return;
        QuestSaveData data = JsonUtility.FromJson<QuestSaveData>(stateJson);
        activeQuest = null;
        completedQuests.Clear();

        if (catalog == null) catalog = Resources.Load<QuestCatalog>("QuestCatalog");

        if (catalog != null)
        {
            if (!string.IsNullOrEmpty(data.activeQuestId))
                activeQuest = catalog.FindQuest(data.activeQuestId);

            if (data.completedQuestIds != null)
            {
                for (int i = 0; i < data.completedQuestIds.Count; i++)
                {
                    QuestData q = catalog.FindQuest(data.completedQuestIds[i]);
                    if (q != null && !completedQuests.Contains(q)) completedQuests.Add(q);
                }
            }
        }
        OnChanged?.Invoke();

        // ประเมินธงใหม่หลังโหลดเซฟ เผื่อเควสที่กู้คืนมามีเงื่อนไขครบอยู่แล้ว
        // ถ้าไม่เรียก เควสจะค้างอยู่ในหน้าสมุดตลอดไปทั้งที่ทำเสร็จแล้ว
        HandleFlagsChanged();
    }

    #endregion
}
