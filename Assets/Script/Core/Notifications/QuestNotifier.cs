using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ตัวตัดสินใจ "ป้ายแจ้งเตือน" — ฟังประกาศเควส/เซฟจาก GameEventBus → แปลภาษา → เข้าคิว → ส่งให้ตัวแสดงผลทีละป้าย + เล่นเสียง
///
/// แยก "ตัดสินใจ" (ตัวนี้) ออกจาก "หน้าตา" (INotificationPresenter) — อาร์ตเปลี่ยนกี่รอบไม่ต้องแตะไฟล์นี้
/// ยังไม่มีตัวแสดงผล → พิมพ์ลง Console แทน (เทสตรรกะได้เลยตอนนี้)
///
/// วางที่ [STORY] · ข้อความทั้งหมดเป็น key ใน LocalizationData.csv ({0} = ชื่อเควส/เป้าหมาย)
/// </summary>
public class QuestNotifier : MonoBehaviour
{
    [Header("ตัวแสดงผล (เว้นว่าง = พิมพ์ลง Console)")]
    [Tooltip("ลาก component ที่ implement INotificationPresenter มาใส่")]
    [SerializeField] private MonoBehaviour presenter;

    [Header("แจ้งเตือนอะไรบ้าง")]
    [SerializeField] private bool notifyQuestStarted = true;
    [SerializeField] private bool notifyObjectiveRevealed = true;
    [SerializeField] private bool notifyObjectiveProgress = true;
    [SerializeField] private bool notifyObjectiveCompleted = true;
    [SerializeField] private bool notifyQuestCompleted = true;
    [SerializeField] private bool notifySave = true;
    [SerializeField] private bool notifyMemoryUnlocked = true;

    [Header("เวลา (วินาทีจริง)")]
    [SerializeField, Min(0.5f)] private float holdSeconds = 2.5f;

    [Header("เสียง (เว้นว่าง = เสียงคลิกเมนูกลาง)")]
    [SerializeField] private AudioClip questStartedSound;
    [SerializeField] private AudioClip progressSound;
    [SerializeField] private AudioClip completedSound;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    [Header("Key ข้อความ")]
    [SerializeField] private string questStartedKey = "QUEST_TOAST_NEW";
    [SerializeField] private string objectiveRevealedKey = "QUEST_TOAST_NEW_OBJECTIVE";
    [SerializeField] private string objectiveCompletedKey = "QUEST_TOAST_OBJECTIVE_DONE";
    [SerializeField] private string questCompletedKey = "QUEST_TOAST_COMPLETED";
    [SerializeField] private string gameSavedKey = "SAVE_DONE";
    [SerializeField] private string memoryUnlockedKey = "MEMORY_TOAST_NEW";

    private readonly Queue<NotificationMessage> queue = new Queue<NotificationMessage>();
    private Coroutine runner;
    private INotificationPresenter cachedPresenter;

    private void Awake()
    {
        cachedPresenter = presenter as INotificationPresenter;
        if (presenter != null && cachedPresenter == null)
        {
            Debug.LogWarning($"[QuestNotifier] '{presenter.name}' ไม่ได้ implement INotificationPresenter — จะพิมพ์ลง Console แทน", this);
        }
    }

    private void OnEnable()
    {
        GameEventBus.Subscribe<QuestStartedEvent>(OnQuestStarted);
        GameEventBus.Subscribe<ObjectiveRevealedEvent>(OnObjectiveRevealed);
        GameEventBus.Subscribe<ObjectiveProgressEvent>(OnObjectiveProgress);
        GameEventBus.Subscribe<ObjectiveCompletedEvent>(OnObjectiveCompleted);
        GameEventBus.Subscribe<QuestCompletedEvent>(OnQuestCompleted);
        GameEventBus.Subscribe<GameSavedEvent>(OnGameSaved);
        GameEventBus.Subscribe<SaveBlockedEvent>(OnSaveBlocked);
        GameEventBus.Subscribe<MemoryUnlockedEvent>(OnMemoryUnlocked);
    }

    private void OnDisable()
    {
        GameEventBus.Unsubscribe<QuestStartedEvent>(OnQuestStarted);
        GameEventBus.Unsubscribe<ObjectiveRevealedEvent>(OnObjectiveRevealed);
        GameEventBus.Unsubscribe<ObjectiveProgressEvent>(OnObjectiveProgress);
        GameEventBus.Unsubscribe<ObjectiveCompletedEvent>(OnObjectiveCompleted);
        GameEventBus.Unsubscribe<QuestCompletedEvent>(OnQuestCompleted);
        GameEventBus.Unsubscribe<GameSavedEvent>(OnGameSaved);
        GameEventBus.Unsubscribe<SaveBlockedEvent>(OnSaveBlocked);
        GameEventBus.Unsubscribe<MemoryUnlockedEvent>(OnMemoryUnlocked);

        // เคลียร์คิวค้าง — กลับมาเปิดใหม่จะได้ไม่เด้งของเก่ารัวๆ
        queue.Clear();
        runner = null;
    }

    #region Event → ข้อความ

    private void OnQuestStarted(QuestStartedEvent e)
    {
        if (notifyQuestStarted) Enqueue(NotificationKind.QuestStarted, Format(questStartedKey, Text(e.Quest.titleKey)), questStartedSound);
    }

    private void OnObjectiveRevealed(ObjectiveRevealedEvent e)
    {
        if (notifyObjectiveRevealed) Enqueue(NotificationKind.ObjectiveRevealed, Format(objectiveRevealedKey, Text(e.Objective.descriptionKey)), questStartedSound);
    }

    private void OnObjectiveProgress(ObjectiveProgressEvent e)
    {
        // ครบแล้วไม่ต้องเด้งตัวเลข — ป้าย "สำเร็จ" จะตามมาเอง
        if (!notifyObjectiveProgress || e.Target <= 1 || e.Current >= e.Target) return;
        Enqueue(NotificationKind.ObjectiveProgress, $"{Text(e.Objective.descriptionKey)} ({e.Current}/{e.Target})", progressSound);
    }

    private void OnObjectiveCompleted(ObjectiveCompletedEvent e)
    {
        if (notifyObjectiveCompleted) Enqueue(NotificationKind.ObjectiveCompleted, Format(objectiveCompletedKey, Text(e.Objective.descriptionKey)), progressSound);
    }

    private void OnQuestCompleted(QuestCompletedEvent e)
    {
        if (notifyQuestCompleted) Enqueue(NotificationKind.QuestCompleted, Format(questCompletedKey, Text(e.Quest.titleKey)), completedSound);
    }

    private void OnGameSaved(GameSavedEvent e)
    {
        if (notifySave) Enqueue(NotificationKind.GameSaved, Text(gameSavedKey), null);
    }

    private void OnSaveBlocked(SaveBlockedEvent e)
    {
        // เซฟอัตโนมัติที่ถูกปฏิเสธไม่ต้องบอกผู้เล่น (ไม่ได้เป็นคนกดเอง)
        if (notifySave && e.Reason == SaveReason.Manual) Enqueue(NotificationKind.SaveBlocked, Text(e.ReasonKey), null);
    }

    private void OnMemoryUnlocked(MemoryUnlockedEvent e)
    {
        if (notifyMemoryUnlocked && e.Entry != null) Enqueue(NotificationKind.MemoryUnlocked, Format(memoryUnlockedKey, Text(e.Entry.titleKey)), completedSound);
    }

    #endregion

    #region Queue

    private void Enqueue(NotificationKind kind, string text, AudioClip sound)
    {
        if (string.IsNullOrEmpty(text)) return;

        queue.Enqueue(new NotificationMessage(kind, text, holdSeconds));
        PlaySound(sound);

        if (runner == null && isActiveAndEnabled) runner = StartCoroutine(RunQueue());
    }

    private IEnumerator RunQueue()
    {
        while (queue.Count > 0)
        {
            NotificationMessage msg = queue.Dequeue();

            if (cachedPresenter != null)
            {
                yield return cachedPresenter.Show(msg);
            }
            else
            {
                Debug.Log($"<color=#f0c040>🔔 [แจ้งเตือน:{msg.Kind}] {msg.Text}</color>");
                yield return new WaitForSecondsRealtime(0.1f);   // ไม่มีหน้าตา ไม่ต้องรอนาน
            }
        }
        runner = null;
    }

    private void PlaySound(AudioClip clip)
    {
        IAudioService audio = ServiceLocator.GetOptional<IAudioService>();
        if (audio == null) return;

        if (clip == null)
        {
            audio.PlayMenuSound(MenuSoundType.Click);
            return;
        }

        Vector3 pos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        audio.PlaySFX(clip, pos, volume, false);
    }

    #endregion

    #region Text

    private static string Text(string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;
        ILocalizationService loc = ServiceLocator.GetOptional<ILocalizationService>();
        return loc != null ? loc.GetText(key) : key;
    }

    private static string Format(string templateKey, string value)
    {
        string template = Text(templateKey);
        // ยังไม่ใส่ key ใน CSV (ได้ key กลับมา) หรือไม่มี {0} → ต่อท้ายเอง ไม่ให้ข้อความหาย
        return template.Contains("{0}") ? template.Replace("{0}", value) : $"{template} {value}".Trim();
    }

    #endregion
}
