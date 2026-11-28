using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [Claude 2026-11-28] ตัวตัดสินใจของระบบคำสอน (สเปก docs/TUTORIAL_SPEC.md)
///   • เคยขึ้นยัง → จำเป็นธงในความจำกลาง (StoryFlags key "tut:&lt;id&gt;") = ติดไปกับเซฟเอง ไม่ต้องมี ISaveable
///   • มาหลายอันพร้อมกัน → เข้าคิว ขึ้นทีละใบ
///   • ยังไม่ว่าง (บทพูด/หยุดเกม/สลับโลก ฯลฯ ดู TutorialGate) → รอ
/// หน้าตาการ์ดอยู่อีกตัว (ITutorialPresenter → TutorialCardPresenter)
///
/// วางในซีน: ก้อนเดียวกับการ์ด (prefab [TUTORIAL]) · ลากไฟล์ TutorialData ทุกอันใส่ Catalog
/// </summary>
public class TutorialService : MonoBehaviour, ITutorialService
{
    [Tooltip("ลาก component ที่ implement ITutorialPresenter (TutorialCardPresenter) มาใส่")]
    [SerializeField] private MonoBehaviour presenter;

    [Tooltip("คำสอนทั้งหมดในเกม — ลำดับในนี้ = ลำดับในแท็บ Tutorials ของสมุด · เรียกด้วยรหัสได้เฉพาะที่อยู่ในนี้")]
    [SerializeField] private List<TutorialData> catalog = new List<TutorialData>();

    [Tooltip("พิมพ์ลง Console ว่าการ์ดรอคิวเพราะอะไร (ไว้ดีบัก)")]
    [SerializeField] private bool logWaitReason;

    public event Action OnChanged;

    private readonly Queue<TutorialData> queue = new Queue<TutorialData>();
    private readonly List<TutorialData> seenCache = new List<TutorialData>();
    private ITutorialPresenter cachedPresenter;
    private IStoryFlags flags;
    private Coroutine runner;
    private bool replaying;

    public bool IsShowing => cachedPresenter != null && cachedPresenter.IsOpen;

    public IReadOnlyList<TutorialData> SeenForJournal
    {
        get
        {
            seenCache.Clear();
            foreach (TutorialData t in catalog)
                if (t != null && t.showInJournal && HasSeen(t)) seenCache.Add(t);
            return seenCache;
        }
    }

    private void Awake()
    {
        ServiceLocator.Register<ITutorialService>(this);

        cachedPresenter = presenter as ITutorialPresenter;
        if (presenter == null)
            Debug.LogWarning("[TutorialService] ยังไม่ได้ลากการ์ด (TutorialCardPresenter) ใส่ช่อง Presenter — คำสอนจะขึ้นไม่ได้", this);
        else if (cachedPresenter == null)
            Debug.LogWarning($"[TutorialService] '{presenter.name}' ไม่ได้ implement ITutorialPresenter", this);

        WarnDuplicateIds();
    }

    private void OnDestroy()
    {
        if (ReferenceEquals(ServiceLocator.GetOptional<ITutorialService>(), this))
            ServiceLocator.Unregister<ITutorialService>();
    }

    private void OnEnable()
    {
        flags = ServiceLocator.GetOptional<IStoryFlags>();
        if (flags != null) flags.OnChanged += RaiseChanged;
        if (queue.Count > 0 && runner == null) runner = StartCoroutine(RunQueue());
    }

    private void OnDisable()
    {
        if (flags != null) flags.OnChanged -= RaiseChanged;
        runner = null;      // coroutine หยุดเองตอนปิด object — เปิดใหม่ OnEnable ทำคิวต่อให้
        replaying = false;
    }

    private void RaiseChanged() => OnChanged?.Invoke();

    private IStoryFlags Flags
    {
        get
        {
            if (flags == null)
            {
                flags = ServiceLocator.GetOptional<IStoryFlags>();
                if (flags != null && isActiveAndEnabled) flags.OnChanged += RaiseChanged;
            }
            return flags;
        }
    }

    public bool HasSeen(TutorialData tutorial)
    {
        if (tutorial == null || string.IsNullOrEmpty(tutorial.tutorialId)) return false;
        IStoryFlags f = Flags;
        return f != null && f.HasKey(tutorial.SeenKey);
    }

    public bool Request(string tutorialId)
    {
        TutorialData found = catalog.Find(t => t != null && t.tutorialId == tutorialId);
        if (found == null)
        {
            Debug.LogWarning($"[TutorialService] ไม่เจอคำสอนรหัส '{tutorialId}' ใน Catalog", this);
            return false;
        }
        Request(found);
        return true;
    }

    public void Request(TutorialData tutorial)
    {
        if (tutorial == null) return;
        if (string.IsNullOrEmpty(tutorial.tutorialId))
        {
            Debug.LogWarning($"[TutorialService] '{tutorial.name}' ไม่มี Tutorial Id — ข้าม", tutorial);
            return;
        }
        if (HasSeen(tutorial) || queue.Contains(tutorial)) return;

        if (Flags == null)
            Debug.LogWarning("[TutorialService] ไม่มี StoryFlagService ในซีน — คำสอนจะจำไม่ได้ว่าเคยขึ้น (ขึ้นซ้ำทุกครั้ง)", this);

        queue.Enqueue(tutorial);
        if (runner == null && isActiveAndEnabled) runner = StartCoroutine(RunQueue());
    }

    public void Replay(TutorialData tutorial)
    {
        if (tutorial == null || cachedPresenter == null || IsShowing) return;
        StartCoroutine(RunReplay(tutorial));
    }

    public void CloseCurrent() => cachedPresenter?.Close();

    private IEnumerator RunReplay(TutorialData tutorial)
    {
        replaying = true;
        yield return cachedPresenter.Show(tutorial);
        replaying = false;
    }

    private IEnumerator RunQueue()
    {
        float nextLog = 0f;
        while (queue.Count > 0)
        {
            // รอจังหวะว่าง + ไม่มีการ์ดอื่นค้าง (รวมการ์ดที่เปิดดูซ้ำจากสมุด)
            while (TutorialGate.IsBusy || IsShowing || replaying)
            {
                if (logWaitReason && Time.unscaledTime >= nextLog)
                {
                    Debug.Log($"[TutorialService] รอคิว '{queue.Peek().tutorialId}' — {TutorialGate.Reason ?? "การ์ดอื่นเปิดอยู่"}", this);
                    nextLog = Time.unscaledTime + 2f;
                }
                yield return null;
            }

            TutorialData next = queue.Dequeue();
            if (next == null || HasSeen(next)) continue;   // ระหว่างรอ อาจโหลดเซฟที่เคยเห็นแล้ว

            // จำทันทีที่ขึ้น (ไม่รอปิด) — เซฟกลางการ์ด/ตายกลางการ์ด จะได้ไม่ขึ้นซ้ำ
            Flags?.SetKey(next.SeenKey);
            GameEventBus.Publish(new TutorialShownEvent(next));

            if (cachedPresenter != null) yield return cachedPresenter.Show(next);
            else Debug.Log($"[TutorialService] (ไม่มีการ์ด) คำสอน: {next.Title}", this);
        }
        runner = null;
    }

    private void WarnDuplicateIds()
    {
        var ids = new HashSet<string>();
        foreach (TutorialData t in catalog)
        {
            if (t == null || string.IsNullOrEmpty(t.tutorialId)) continue;
            if (!ids.Add(t.tutorialId))
                Debug.LogWarning($"[TutorialService] รหัสคำสอนซ้ำ '{t.tutorialId}' ({t.name}) — ขึ้นอันเดียวแล้วอีกอันจะไม่ขึ้นเลย", t);
        }
    }
}
