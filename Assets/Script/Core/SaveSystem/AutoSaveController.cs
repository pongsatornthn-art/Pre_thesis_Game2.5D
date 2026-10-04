using System.Collections;
using UnityEngine;

/// <summary>
/// เซฟอัตโนมัติ — ฟังประกาศใน GameEventBus แล้วขอเซฟเมื่อจังหวะเหมาะ
///   ✔ เควสจบ (ถ้าอยู่โลกปกติ)   ✔ ออกจากโลก PTSD
/// รอให้ฉากเนื้อเรื่องเล่นจบ + อนิเมชันสลับโลกจบก่อนค่อยเซฟ (ไม่งั้นเซฟกลางคัทซีน / ตำแหน่งผู้เล่นยังไม่วาร์ปกลับ)
/// ขอซ้อนกันหลายครั้งในช่วงสั้นๆ = เซฟครั้งเดียว
///
/// วางที่ [STORY] — ระบบเควส/เซฟไม่ต้องรู้ว่ามีตัวนี้ (ลบทิ้ง = ปิดเซฟอัตโนมัติ)
/// </summary>
public class AutoSaveController : MonoBehaviour
{
    [SerializeField] private bool saveOnQuestCompleted = true;
    [SerializeField] private bool saveOnExitPtsd = true;

    [Tooltip("รอหลังเกิดเหตุก่อนเซฟ (วินาทีจริง) — เผื่ออนิเมชันสลับโลก/วาร์ปผู้เล่นกลับ")]
    [SerializeField, Min(0f)] private float delaySeconds = 2f;

    [Tooltip("ถ้าฉากเนื้อเรื่องยังเล่นอยู่ รอได้นานสุดกี่วินาที ก่อนยอมแพ้รอบนี้")]
    [SerializeField, Min(1f)] private float maxWaitSeconds = 60f;

    private Coroutine pending;

    private void OnEnable()
    {
        GameEventBus.Subscribe<QuestCompletedEvent>(OnQuestCompleted);
        GameEventBus.Subscribe<WorldModeChangedEvent>(OnWorldModeChanged);
    }

    private void OnDisable()
    {
        GameEventBus.Unsubscribe<QuestCompletedEvent>(OnQuestCompleted);
        GameEventBus.Unsubscribe<WorldModeChangedEvent>(OnWorldModeChanged);
    }

    private void OnQuestCompleted(QuestCompletedEvent e)
    {
        if (saveOnQuestCompleted) RequestSave();
    }

    private void OnWorldModeChanged(WorldModeChangedEvent e)
    {
        if (saveOnExitPtsd && e.ExitedPtsd) RequestSave();
    }

    private void RequestSave()
    {
        if (pending == null) pending = StartCoroutine(SaveWhenSafe());
    }

    private IEnumerator SaveWhenSafe()
    {
        yield return new WaitForSecondsRealtime(delaySeconds);

        float waited = 0f;
        while (!IsSafeToSave())
        {
            if (waited >= maxWaitSeconds)
            {
                Debug.Log("[AutoSave] รอนานเกินไป ข้ามเซฟอัตโนมัติรอบนี้");
                pending = null;
                yield break;
            }
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        pending = null;

        // ยังอยู่ในโลก PTSD (เช่น เควสที่จบระหว่างอยู่ในโลก PTSD) → SaveManager จะปฏิเสธเอง
        // ไม่ต้องเช็คซ้ำที่นี่ (กติกาห้ามเซฟอยู่ที่ SaveManager ที่เดียว)
        if (ServiceLocator.TryGet(out SaveManager save)) save.TryAutoSave();
    }

    private static bool IsSafeToSave()
    {
        if (SaveRestoreScope.IsBusy) return false;
        if (StoryDirector.Instance != null && StoryDirector.Instance.IsPlaying) return false;
        if (Time.timeScale == 0f) return false;   // หยุดเกม / มินิเกมสตอล์กเกอร์
        return true;
    }
}
