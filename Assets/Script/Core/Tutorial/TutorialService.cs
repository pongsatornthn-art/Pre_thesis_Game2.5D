using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [Claude 2026-11-28] ตัวตัดสินใจของระบบคำสอน (สเปก docs/TUTORIAL_SPEC.md)
///   • เคยขึ้นยัง → TutorialProgress (เก็บในเครื่อง แยกจากเซฟ — ตาย/โหลดเซฟแล้วไม่สอนซ้ำ · เริ่มเกมใหม่ = ล้าง)
///   • มาหลายอันพร้อมกัน → เข้าคิว ขึ้นทีละใบ (การ์ดแทรกมินิเกม ลัดคิวขึ้นก่อน)
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

    private readonly List<TutorialData> queue = new List<TutorialData>();
    private readonly List<TutorialData> seenCache = new List<TutorialData>();
    private ITutorialPresenter cachedPresenter;
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

        // เปิดซีนเกมโดยไม่ได้โหลดเซฟ = เริ่มเกมใหม่ (เมนูเริ่มเกมใหม่ / กด Play ใน Editor) → สอนใหม่ทั้งหมด
        // โหลดเซฟ / ตายแล้วย้อน / เล่นต่อ = SaveManager ตั้ง HasPendingRestore ก่อนเปิดซีน → ไม่ล้าง
        if (!SaveRestoreScope.HasPendingRestore) TutorialProgress.ClearAll();
    }

    private void OnDestroy()
    {
        if (ReferenceEquals(ServiceLocator.GetOptional<ITutorialService>(), this))
            ServiceLocator.Unregister<ITutorialService>();
    }

    private void OnEnable()
    {
        if (queue.Count > 0 && runner == null) runner = StartCoroutine(RunQueue());
    }

    private void OnDisable()
    {
        runner = null;      // coroutine หยุดเองตอนปิด object — เปิดใหม่ OnEnable ทำคิวต่อให้
        replaying = false;
    }

    public bool HasSeen(TutorialData tutorial)
    {
        return tutorial != null && TutorialProgress.Has(tutorial.tutorialId);
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

        // การ์ดแทรกมินิเกม (ยันประตู/โดนจับ) ต้องขึ้นตอนนี้เลย → ลัดคิว
        if (tutorial.interruptsMinigame) queue.Insert(0, tutorial);
        else queue.Add(tutorial);
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
            // การ์ดแทรกมินิเกม ใช้เงื่อนไขหลวมกว่า (มินิเกม/เวลาหยุดอยู่ก็ขึ้นได้ — การ์ดหยุดมินิเกมให้เอง)
            while (BusyFor(queue[0]) != null || IsShowing || replaying)
            {
                if (logWaitReason && Time.unscaledTime >= nextLog)
                {
                    Debug.Log($"[TutorialService] รอคิว '{queue[0].tutorialId}' — {BusyFor(queue[0]) ?? "การ์ดอื่นเปิดอยู่"}", this);
                    nextLog = Time.unscaledTime + 2f;
                }
                yield return null;
            }

            TutorialData next = queue[0];
            queue.RemoveAt(0);
            if (next == null || HasSeen(next)) continue;

            // จำทันทีที่ขึ้น (ไม่รอปิด) — ตายกลางการ์ด จะได้ไม่ขึ้นซ้ำ
            TutorialProgress.Mark(next.tutorialId);
            OnChanged?.Invoke();
            GameEventBus.Publish(new TutorialShownEvent(next));

            if (cachedPresenter != null) yield return cachedPresenter.Show(next);
            else Debug.Log($"[TutorialService] (ไม่มีการ์ด) คำสอน: {next.Title}", this);
        }
        runner = null;
    }

    private static string BusyFor(TutorialData t) =>
        t != null && t.interruptsMinigame ? TutorialGate.InterruptReason : TutorialGate.Reason;

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
