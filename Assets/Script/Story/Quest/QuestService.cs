using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ระบบบริการเควสส่วนกลาง (Quest Service) — แบบสมุดบันทึก หลายเควสพร้อมกัน
///
/// หน้าที่เดียว: ดูความจำกลาง (ธง / ตัวนับ / ไอเทม) แล้วตัดสินว่า
///   เควสไหนควรเริ่ม · ข้อไหนโผล่ · ข้อไหนขีดฆ่า · เควสไหนจบ
/// แล้วประกาศผ่าน GameEventBus — ไม่รู้ว่ามี UI / เสียง / ป้ายแจ้งเตือนอยู่ (SRP + DIP)
///
/// กติกาสำคัญ:
/// - เป้าหมายที่สำเร็จแล้ว "ล็อก" ไว้เลย — เอากุญแจไปใช้จนหายจากตัว ข้อ "เก็บกุญแจ" ก็ยังขีดฆ่าอยู่
/// - สมุดเรียงตามลำดับที่ได้รับ เควสจบแล้วอยู่ตำแหน่งเดิม (ลำดับนี้ถูกเซฟ)
/// - ตอนโหลดเซฟคำนวณใหม่แบบเงียบ ไม่ยิงประกาศ
///
/// สเปก: docs/QUEST_SAVE_SPEC.md
/// </summary>
public class QuestService : MonoBehaviour, IQuestService, ISaveable, ISaveRestoreOrder, ISaveRestoreListener
{
    // โหลดหลังความจำกลาง (-100) แต่ก่อนของในซีน
    public int RestoreOrder => -50;

    [Header("สารบัญเควสทั้งหมด (ใช้หาเควสที่เริ่มเอง + หาเควสตอนโหลดเซฟ)")]
    [Tooltip("เว้นว่าง = หา Resources/QuestCatalog เอง")]
    [SerializeField] private QuestCatalog catalog;

    [Tooltip("กันลูปไม่รู้จบ — เควสจบแล้วไปปักธงที่ทำให้เควสอื่นจบต่อเป็นทอดๆ เกินจำนวนนี้ถือว่าผิดปกติ")]
    [SerializeField] private int maxEvaluationPasses = 16;

    /// <summary>สถานะของเควส 1 อันในสมุด</summary>
    private class QuestRecord
    {
        public QuestData quest;
        public QuestState state;
        public readonly HashSet<string> doneObjectives = new HashSet<string>();
        public readonly HashSet<string> revealedObjectives = new HashSet<string>();
        public readonly Dictionary<string, int> lastProgress = new Dictionary<string, int>(); // รันไทม์อย่างเดียว ไว้ดูว่าตัวเลขเปลี่ยนไหม
    }

    private readonly List<QuestRecord> records = new List<QuestRecord>();
    private readonly List<QuestData> journal = new List<QuestData>();
    private readonly Dictionary<QuestData, QuestRecord> recordByQuest = new Dictionary<QuestData, QuestRecord>();

    private IStoryFlags flags;
    private IStoryCounters counters;
    private IKeyItemHolder keyItems;
    private IDocumentLog documents;
    private Inventory subscribedInventory;
    private GameObject cachedPlayer;

    private bool isEvaluating;
    private bool needsAnotherPass;
    private bool changedThisEvaluation;

    public IReadOnlyList<QuestData> Journal => journal;
    public event Action OnChanged;

    #region Save Data

    [Serializable]
    private class QuestEntrySave
    {
        public string questId;
        public int state;
        public List<string> doneObjectives = new List<string>();
        public List<string> revealedObjectives = new List<string>();
    }

    [Serializable]
    private class QuestSaveData
    {
        public int version = 2;
        public List<QuestEntrySave> journal = new List<QuestEntrySave>();
    }

    #endregion

    #region Lifecycle

    private void Awake()
    {
        ServiceLocator.Register<IQuestService>(this);
        if (catalog == null) catalog = Resources.Load<QuestCatalog>("QuestCatalog");
    }

    private void Start()
    {
        // ฟังทุกแหล่งที่ทำให้เป้าหมายเปลี่ยนได้ — ทุกแหล่งไปจบที่ HandleWorldChanged ที่เดียว
        flags = ServiceLocator.GetOptional<IStoryFlags>();
        counters = ServiceLocator.GetOptional<IStoryCounters>();
        keyItems = ServiceLocator.GetOptional<IKeyItemHolder>();
        documents = ServiceLocator.GetOptional<IDocumentLog>();
        subscribedInventory = Inventory.Instance;

        if (flags != null) flags.OnChanged += HandleWorldChanged;
        // StoryFlagService เป็นทั้งธงและตัวนับ (event ตัวเดียวกัน) — ห้ามสมัครซ้ำไม่งั้นคำนวณ 2 รอบ
        if (counters != null && !ReferenceEquals(counters, flags)) counters.OnChanged += HandleWorldChanged;
        if (keyItems != null) keyItems.OnChanged += HandleWorldChanged;
        if (documents != null) documents.OnChanged += HandleWorldChanged;
        if (subscribedInventory != null) subscribedInventory.OnInventoryChanged += HandleWorldChanged;

        // [2026-10-08] เป้าหมายที่มีเงื่อนไข "อยู่โลกไหน" ต้องคิดใหม่ตอนสลับโลกด้วย
        // (เคยพัง: ตัวนับครบตอนเงื่อนไขยังไม่ผ่าน → สลับโลกแล้วไม่มีใครสั่งคิดใหม่ เป้าหมายค้างตลอดไป)
        subscribedWorld = ServiceLocator.GetOptional<IWorldModeService>();
        if (subscribedWorld != null) subscribedWorld.OnModeChanged += HandleWorldModeChanged;

        if (flags == null)
        {
            Debug.LogWarning("[QuestService] ไม่พบ IStoryFlags (StoryFlagService) — เป้าหมายแบบธง/ตัวนับจะไม่มีวันสำเร็จ");
        }

        // กำลังโหลดเซฟ → ไม่ต้องคำนวณตอนนี้ (จะคำนวณแบบเงียบใน OnRestoreCompleted)
        // ไม่งั้นเควสที่เริ่มเองจะยิงป้าย "เควสใหม่" ก่อนเซฟถูกใส่คืน
        if (!SaveRestoreScope.IsBusy) Evaluate(silent: false);
    }

    private void OnDestroy()
    {
        if (flags != null) flags.OnChanged -= HandleWorldChanged;
        if (counters != null && !ReferenceEquals(counters, flags)) counters.OnChanged -= HandleWorldChanged;
        if (keyItems != null) keyItems.OnChanged -= HandleWorldChanged;
        if (documents != null) documents.OnChanged -= HandleWorldChanged;
        if (subscribedInventory != null) subscribedInventory.OnInventoryChanged -= HandleWorldChanged;
        if (subscribedWorld != null) subscribedWorld.OnModeChanged -= HandleWorldModeChanged;
        ServiceLocator.Unregister<IQuestService>();
    }

    private IWorldModeService subscribedWorld;

    private void HandleWorldModeChanged(WorldMode from, WorldMode to) => HandleWorldChanged();

    private void HandleWorldChanged()
    {
        // ระหว่างโหลดเซฟ ธง/ตัวนับ/กระเป๋าถูกใส่คืนทีละชิ้น — ห้ามคำนวณกลางทาง (จะยิงป้ายแจ้งเตือนผิดๆ)
        if (SaveRestoreScope.IsBusy) return;
        Evaluate(silent: false);
    }

    /// <summary>SaveManager เรียกหลังใส่ความจำคืนครบทุกชิ้น</summary>
    public void OnRestoreCompleted() => Evaluate(silent: true);

    #endregion

    #region IQuestService

    public QuestState GetState(QuestData quest)
    {
        return quest != null && recordByQuest.TryGetValue(quest, out QuestRecord r) ? r.state : QuestState.NotStarted;
    }

    public bool IsObjectiveDone(QuestData quest, QuestObjective objective)
    {
        return objective != null && TryGetRecord(quest, out QuestRecord r) && r.doneObjectives.Contains(objective.ObjectiveId);
    }

    public bool IsObjectiveVisible(QuestData quest, QuestObjective objective)
    {
        if (objective == null || !TryGetRecord(quest, out QuestRecord r)) return false;
        if (!objective.hiddenUntilAvailable) return true;
        return r.revealedObjectives.Contains(objective.ObjectiveId) || r.doneObjectives.Contains(objective.ObjectiveId);
    }

    public bool TryGetObjectiveProgress(QuestData quest, QuestObjective objective, out int current, out int target)
    {
        current = 0;
        target = 0;
        if (objective == null || !objective.TryGetProgress(BuildContext(), out current, out target)) return false;

        // ข้อที่ล็อกว่าเสร็จแล้ว โชว์เต็มเสมอ แม้ตัวนับจริงจะลดลงทีหลัง
        if (IsObjectiveDone(quest, objective)) current = target;
        return true;
    }

    public void StartQuest(QuestData quest)
    {
        if (quest == null) return;

        // เคยได้รับแล้ว (ทำอยู่หรือจบแล้ว) — เหยียบทริกเกอร์ซ้ำ / โหลดเซฟแล้วเดินทางเดิม ต้องไม่เริ่มใหม่
        if (recordByQuest.ContainsKey(quest)) return;

        AddRecord(quest, silent: false);
        Evaluate(silent: false);
    }

    #endregion

    #region Evaluation

    /// <summary>
    /// คำนวณทั้งสมุดใหม่ — ปลอดภัยต่อการถูกเรียกซ้อน
    /// (เควสจบ → ฉากปักธง → OnChanged → เรียกตัวเองอีก) จะถูกต่อคิวเป็นรอบถัดไปแทนการซ้อน
    /// </summary>
    private void Evaluate(bool silent)
    {
        if (isEvaluating)
        {
            needsAnotherPass = true;
            return;
        }

        isEvaluating = true;
        int pass = 0;

        try
        {
            do
            {
                needsAnotherPass = false;
                EvaluateOnce(silent);
                pass++;
            }
            while (needsAnotherPass && pass < maxEvaluationPasses);

            if (needsAnotherPass)
            {
                Debug.LogError($"[QuestService] คำนวณเควสวนเกิน {maxEvaluationPasses} รอบ — น่าจะมีเควส 2 อันที่ทำให้กันและกันเปลี่ยนวนไปมา");
            }
        }
        finally
        {
            isEvaluating = false;
        }

        // ไม่รีเซ็ตตอนเริ่ม — StartQuest จดเควสก่อนเรียก Evaluate ต้องให้การเปลี่ยนนั้นถูกแจ้งด้วย
        if (changedThisEvaluation)
        {
            changedThisEvaluation = false;
            OnChanged?.Invoke();
        }
    }

    private void EvaluateOnce(bool silent)
    {
        StoryContext ctx = BuildContext();

        // 1. เควสที่ตั้ง Auto Start และเงื่อนไขก่อนเริ่มครบแล้ว
        if (catalog != null && catalog.AllQuests != null)
        {
            IReadOnlyList<QuestData> all = catalog.AllQuests;
            for (int i = 0; i < all.Count; i++)
            {
                QuestData q = all[i];
                if (q == null || !q.autoStart || recordByQuest.ContainsKey(q)) continue;
                if (q.availableWhen == null || q.availableWhen.IsMet(ctx))
                {
                    AddRecord(q, silent);
                }
            }
        }

        // 2. ไล่เควสที่กำลังทำ (ใช้ index เพราะลิสต์อาจยาวขึ้นระหว่างลูป)
        for (int i = 0; i < records.Count; i++)
        {
            if (records[i].state == QuestState.Active) EvaluateRecord(records[i], ctx, silent);
        }
    }

    private void EvaluateRecord(QuestRecord record, StoryContext ctx, bool silent)
    {
        List<QuestObjective> objectives = record.quest.objectives;
        bool hasAnyObjective = false;
        bool allDone = true;

        if (objectives != null)
        {
            for (int i = 0; i < objectives.Count; i++)
            {
                QuestObjective obj = objectives[i];
                if (obj == null) continue;   // ช่องที่ยังไม่ได้เลือกชนิด ไม่นับ
                hasAnyObjective = true;

                if (!EvaluateObjective(record, obj, ctx, silent)) allDone = false;
            }
        }

        // เควสที่ยังไม่ได้ใส่เป้าหมาย ห้ามนับว่าสำเร็จ
        // ไม่งั้นพอสร้าง QuestData ใหม่แล้วยังไม่ทันกรอก มันจะจบทันทีแล้วลามไปเควสอื่นที่รอเควสนี้
        if (hasAnyObjective && allDone) CompleteRecord(record, silent);
    }

    /// <returns>true = ข้อนี้เสร็จแล้ว</returns>
    private bool EvaluateObjective(QuestRecord record, QuestObjective obj, StoryContext ctx, bool silent)
    {
        string id = obj.ObjectiveId;
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning($"[QuestService] เควส '{record.quest.questId}' มีเป้าหมายที่ไม่มีรหัส — เปิด asset เควสใน Inspector 1 ครั้งให้ระบบสุ่มรหัสให้", record.quest);
            return false;
        }

        if (record.doneObjectives.Contains(id)) return true;
        if (!obj.IsAvailable(ctx)) return false;

        // โผล่ครั้งแรก (เฉพาะข้อที่ตั้งซ่อนไว้ถึงจะนับว่า "โผล่")
        if (record.revealedObjectives.Add(id))
        {
            changedThisEvaluation = true;
            if (!silent && obj.hiddenUntilAvailable) GameEventBus.Publish(new ObjectiveRevealedEvent(record.quest, obj));
        }

        // ตัวเลขเปลี่ยน
        if (obj.TryGetProgress(ctx, out int current, out int target))
        {
            bool hadValue = record.lastProgress.TryGetValue(id, out int previous);
            if (!hadValue || previous != current)
            {
                record.lastProgress[id] = current;
                changedThisEvaluation = true;
                // ครั้งแรกที่เห็นตัวเลขไม่ประกาศ (เพิ่งจดลงสมุด ป้าย QuestStarted ครอบคลุมแล้ว)
                if (!silent && hadValue) GameEventBus.Publish(new ObjectiveProgressEvent(record.quest, obj, current, target));
            }
        }

        if (!obj.IsMet(ctx)) return false;

        record.doneObjectives.Add(id);
        changedThisEvaluation = true;
        if (!silent) GameEventBus.Publish(new ObjectiveCompletedEvent(record.quest, obj));
        return true;
    }

    private void AddRecord(QuestData quest, bool silent)
    {
        QuestRecord record = new QuestRecord { quest = quest, state = QuestState.Active };
        records.Add(record);
        journal.Add(quest);
        recordByQuest[quest] = record;
        changedThisEvaluation = true;
        needsAnotherPass = true;   // ข้อในเควสใหม่อาจครบอยู่แล้ว ต้องไล่อีกรอบ

        Debug.Log($"<color=green>📜 [QuestService] จดเควสใหม่: {quest.questId}</color>");
        if (!silent) GameEventBus.Publish(new QuestStartedEvent(quest));
    }

    private void CompleteRecord(QuestRecord record, bool silent)
    {
        record.state = QuestState.Completed;
        changedThisEvaluation = true;
        needsAnotherPass = true;   // เควสที่รอ "เควสนี้จบ" (Available When) ต้องได้เริ่มในรอบเดียวกัน

        Debug.Log($"<color=green>🏆 [QuestService] เควสสำเร็จ: {record.quest.questId}</color>");
        if (silent) return;

        GameEventBus.Publish(new QuestCompletedEvent(record.quest));

        if (record.quest.onCompleted != null)
        {
            StoryDirector director = StoryDirector.Instance;
            if (director != null) director.Play(record.quest.onCompleted);
            else Debug.LogWarning($"[QuestService] เควส '{record.quest.questId}' มีฉากตอนจบ แต่ไม่พบ StoryDirector ในซีน");
        }
    }

    private StoryContext BuildContext()
    {
        if (cachedPlayer == null) cachedPlayer = GameObject.FindGameObjectWithTag("Player");

        return new StoryContext
        {
            Player = cachedPlayer,
            Flags = flags ?? ServiceLocator.GetOptional<IStoryFlags>(),
            Counters = counters ?? ServiceLocator.GetOptional<IStoryCounters>(),
            Quests = this,
            Runner = this
        };
    }

    private bool TryGetRecord(QuestData quest, out QuestRecord record)
    {
        record = null;
        return quest != null && recordByQuest.TryGetValue(quest, out record);
    }

    #endregion

    #region ISaveable Implementation

    public string CaptureState()
    {
        QuestSaveData data = new QuestSaveData();

        for (int i = 0; i < records.Count; i++)
        {
            QuestRecord r = records[i];
            if (r.quest == null || string.IsNullOrEmpty(r.quest.questId))
            {
                Debug.LogWarning("[QuestService] พบเควสที่ไม่มี questId — จะไม่ถูกเซฟ", r.quest);
                continue;
            }

            data.journal.Add(new QuestEntrySave
            {
                questId = r.quest.questId,
                state = (int)r.state,
                doneObjectives = new List<string>(r.doneObjectives),
                revealedObjectives = new List<string>(r.revealedObjectives)
            });
        }
        return JsonUtility.ToJson(data);
    }

    public void RestoreState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson)) return;

        QuestSaveData data = JsonUtility.FromJson<QuestSaveData>(stateJson);
        records.Clear();
        journal.Clear();
        recordByQuest.Clear();

        if (catalog == null) catalog = Resources.Load<QuestCatalog>("QuestCatalog");

        if (data?.journal != null)
        {
            if (catalog == null)
            {
                Debug.LogError("[QuestService] โหลดเควสไม่ได้ — ไม่มี QuestCatalog (ใส่ในช่อง Catalog หรือวางไว้ที่ Resources/QuestCatalog)");
            }
            else
            {
                foreach (QuestEntrySave entry in data.journal)
                {
                    QuestData q = catalog.FindQuest(entry.questId);
                    if (q == null || recordByQuest.ContainsKey(q)) continue;

                    QuestRecord r = new QuestRecord { quest = q, state = (QuestState)entry.state };
                    if (entry.doneObjectives != null) r.doneObjectives.UnionWith(entry.doneObjectives);
                    if (entry.revealedObjectives != null) r.revealedObjectives.UnionWith(entry.revealedObjectives);

                    records.Add(r);
                    journal.Add(q);
                    recordByQuest[q] = r;
                }
            }
        }

        // คำนวณใหม่แบบเงียบ เผื่อธงที่โหลดมาทำให้ข้อไหนครบแล้ว — ไม่งั้นเควสจะค้างในสมุดทั้งที่ทำเสร็จ
        changedThisEvaluation = true;   // สมุดถูกแทนทั้งเล่ม ต้องวาดใหม่แน่นอน

        // ถ้าโหลดผ่าน SaveManager จะคำนวณทีเดียวใน OnRestoreCompleted หลังทุกชิ้นใส่คืนครบ
        // ถ้าถูกเรียกตรงๆ (เทส) คำนวณเลยแบบเงียบ — ไม่งั้นเควสที่ธงครบแล้วจะค้างในสมุด
        if (!SaveRestoreScope.IsRestoring) Evaluate(silent: true);
    }

    #endregion
}
