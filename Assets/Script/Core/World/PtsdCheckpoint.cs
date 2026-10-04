using UnityEngine;

/// <summary>
/// จำ "จุดเริ่มใหม่" ของโลก PTSD — ถ่ายสำเนาสถานะทั้งเกม (ในหน่วยความจำ) ตอนก้าวเข้าโลก PTSD
/// ตายในโลก PTSD → นโยบาย RestartPtsdFromEntryDeath เอาสำเนานี้ไปโหลด = กลับไปตอนเพิ่งเข้า ของที่เก็บรอบนั้นหาย
///
/// ไม่เขียนลงไฟล์ — ออกเกมกลางโลก PTSD แล้วเข้าใหม่ = กลับไปเซฟล่าสุดในโลกปกติ (ตามกติกาห้ามเซฟใน PTSD)
/// วางที่ [STORY]
/// </summary>
public class PtsdCheckpoint : MonoBehaviour
{
    public static PtsdCheckpoint Instance { get; private set; }

    /// <summary>สำเนาตอนเข้าโลก PTSD ครั้งล่าสุด (null = ยังไม่เคยเข้า)</summary>
    public GameSaveData EntrySnapshot { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable() => GameEventBus.Subscribe<WorldModeChangedEvent>(OnWorldModeChanged);
    private void OnDisable() => GameEventBus.Unsubscribe<WorldModeChangedEvent>(OnWorldModeChanged);

    private void OnWorldModeChanged(WorldModeChangedEvent e)
    {
        if (e.EnteredPtsd)
        {
            // ประกาศนี้มาก่อนอนิเมชันสลับโลก — สถานะ ณ ตอนนี้คือ "ก่อนทำอะไรในโลก PTSD" พอดี
            // worldMode ในสำเนา = โหมด PTSD → โหลดสำเนาแล้ว SaveManager พากลับเข้าโลก PTSD เอง
            EntrySnapshot = ServiceLocator.TryGet(out SaveManager save) ? save.CaptureSnapshot() : null;
            if (EntrySnapshot == null) return;

            // เข้าโลกจากฉากเนื้อเรื่อง (EnterPtsdAction) → จำว่าฉากไหน คำสั่งที่เท่าไหร่
            // ตายแล้วเริ่มใหม่ จะเล่นฉากนั้นต่อจากคำสั่งนี้ → บทพูด/คำสั่งหลัง "เข้าโลก PTSD" ไม่หาย
            StoryDirector director = StoryDirector.Instance;
            if (director != null && director.CurrentSequence != null)
            {
                EntrySnapshot.resumeSequence = director.CurrentSequence;
                EntrySnapshot.resumeActionIndex = director.CurrentActionIndex;
            }

            Debug.Log("<color=magenta>📍 [PtsdCheckpoint] จำจุดเริ่มใหม่ของโลก PTSD แล้ว</color>");
        }
        else if (e.ExitedPtsd)
        {
            EntrySnapshot = null;
        }
    }
}
