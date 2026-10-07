/// <summary>
/// คลาสแม่ของคัทซีนทุกแบบ — คำสั่ง "คัทซีน/เล่นคัทซีน" รู้จักแค่ตัวนี้ (ไม่สนว่าเป็น Timeline หรือภาพทีละหน้า)
///   StoryCutscene = Timeline (กล้องเคลื่อน / อนิเมชัน / เสียงตามเวลา)
///   ComicCutscene = ภาพทีละหน้าแบบการ์ตูน กดเปิดหน้าถัดไป
/// คัทซีนแบบใหม่ในอนาคต = สร้างคลาสลูกใหม่ · คำสั่งในฉากไม่ต้องแก้ (Open-Closed)
/// </summary>
public abstract class CutsceneBase : SceneIdentified<CutsceneBase>
{
    public abstract void Play();
    public abstract bool IsPlaying { get; }

    /// <summary>ข้ามไปจบทันที</summary>
    public abstract void Skip();

    // เครื่องมือทดสอบ: ตอนกด Play อยู่ คลิกขวาที่หัวคอมโพเนนต์นี้ใน Inspector → เล่นคัทซีนทันที (ไม่ต้องเดินไปถึงจุดในเนื้อเรื่อง)
    [UnityEngine.ContextMenu("▶ ทดสอบเล่น (ต้องกด Play ก่อน)")]
    private void TestPlayFromInspector()
    {
        if (!UnityEngine.Application.isPlaying)
        {
            UnityEngine.Debug.LogWarning($"[{GetType().Name}] กด Play ก่อน แล้วค่อยคลิกขวา → ทดสอบเล่น", this);
            return;
        }
        Play();
    }
}
