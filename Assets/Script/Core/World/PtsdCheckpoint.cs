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

    // [Claude 2026-11-12 ต้าตกลง] ยาที่กินไประหว่างอยู่ใน PTSD "ไม่คืน" ตอนตายแล้วเริ่มใหม่ — ตายมีต้นทุน
    // static เพราะซีนถูกโหลดใหม่ตอนเริ่มใหม่ (ตัวนี้ตายไปพร้อมซีนเก่า)
    private static readonly System.Collections.Generic.Dictionary<ItemData, int> consumedInPtsd = new System.Collections.Generic.Dictionary<ItemData, int>();
    private static bool trackingConsumption;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { consumedInPtsd.Clear(); trackingConsumption = false; }

    private void OnEnable()
    {
        GameEventBus.Subscribe<WorldModeChangedEvent>(OnWorldModeChanged);
        GameEventBus.Subscribe<ConsumableUsedEvent>(OnConsumableUsed);
        GameEventBus.Subscribe<GameLoadedEvent>(OnGameLoaded);
    }

    private void OnDisable()
    {
        GameEventBus.Unsubscribe<WorldModeChangedEvent>(OnWorldModeChanged);
        GameEventBus.Unsubscribe<ConsumableUsedEvent>(OnConsumableUsed);
        GameEventBus.Unsubscribe<GameLoadedEvent>(OnGameLoaded);
    }

    private void OnConsumableUsed(ConsumableUsedEvent e)
    {
        if (!trackingConsumption || e.Item == null) return;
        consumedInPtsd.TryGetValue(e.Item, out int n);
        consumedInPtsd[e.Item] = n + 1;
    }

    private void OnGameLoaded(GameLoadedEvent e)
    {
        if (!e.FromCheckpoint) { consumedInPtsd.Clear(); trackingConsumption = false; return; }

        // กลับมาจุดเริ่ม PTSD แล้ว (ของคืนตามสำเนา) → หักยาที่กินไปแล้วออก
        if (Inventory.Instance != null)
        {
            foreach (var kv in consumedInPtsd)
            {
                int have = Inventory.Instance.GetItemCount(kv.Key);
                int take = Mathf.Min(have, kv.Value);
                if (take > 0) Inventory.Instance.RemoveItem(kv.Key, take);
                if (take > 0) Debug.Log($"<color=orange>[PtsdCheckpoint] ตายแล้วเริ่มใหม่ — {kv.Key.itemName} ที่กินไป {take} ชิ้นไม่คืน</color>");
            }
        }
        // ยังอยู่ใน PTSD รอบใหม่ — ยาที่กินรอบนี้ก็นับต่อ (ยังไม่ล้าง)
        trackingConsumption = true;
    }

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

            consumedInPtsd.Clear();
            trackingConsumption = true;
            Debug.Log("<color=magenta>📍 [PtsdCheckpoint] จำจุดเริ่มใหม่ของโลก PTSD แล้ว</color>");
        }
        else if (e.ExitedPtsd)
        {
            EntrySnapshot = null;
            consumedInPtsd.Clear();
            trackingConsumption = false;
        }
    }
}
