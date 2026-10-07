using UnityEngine;

/// <summary>
/// เลือกว่าของชิ้นนี้โผล่ "โลกไหน" — แปะที่ของในแมพ (ชิ้นส่วนรูป / ป้าย / ศพ / ประตูลับ ฯลฯ) แล้วเลือกในช่อง Show In
/// ไม่ต้องย้ายของเข้าโฟลเดอร์ RealWorld_Env / MemoryWorld_Env ของปอ (ทำได้ทั้งสองแบบ เลือกแบบที่สะดวก)
///
/// สลับตอนฉากสลับจริง (จอมืดสุด) ไม่ใช่ตอนกดเข้าโลก → พร้อมกับฉากของปอพอดี
/// ใช้ร่วมกับ StoryCollectible ได้: ต้อง "อยู่ถูกโลก" และ "ยังไม่ถูกเก็บ" ถึงจะโผล่ (ScenePresence รวมผลให้)
/// ซ่อน = ปิดหน้าตา + ตัวชน (กด/ชนไม่ได้) · ตัว object ยังเปิดอยู่เพื่อคอยฟังการสลับโลก
/// </summary>
public class WorldPresence : MonoBehaviour, IPresenceGate
{
    public enum ShowIn
    {
        Both,              // ทั้งสองโลก
        RealOnly,          // โลกปกติเท่านั้น
        PtsdOnly,          // โลก PTSD ทุกแบบ
        PtsdSurvivalOnly,  // PTSD แบบ A (เอาชีวิตรอด)
        PtsdNarrativeOnly  // PTSD แบบ B (สืบเรื่อง)
    }

    [Tooltip("โผล่ในโลกไหน")]
    [SerializeField] private ShowIn showIn = ShowIn.PtsdOnly;

    public bool AllowVisible
    {
        get
        {
            IWorldModeService world = ServiceLocator.GetOptional<IWorldModeService>();
            WorldMode visible = world != null ? world.VisibleWorld : WorldMode.Real;   // ไม่มีบริการ = ถือว่าโลกปกติ

            switch (showIn)
            {
                case ShowIn.Both: return true;
                case ShowIn.RealOnly: return visible == WorldMode.Real;
                case ShowIn.PtsdOnly: return visible != WorldMode.Real;
                case ShowIn.PtsdSurvivalOnly: return visible == WorldMode.PtsdSurvival;
                case ShowIn.PtsdNarrativeOnly: return visible == WorldMode.PtsdNarrative;
                default: return true;
            }
        }
    }

    private void OnEnable()
    {
        GameEventBus.Subscribe<WorldVisualsSwappedEvent>(OnWorldSwapped);
        ScenePresence.Refresh(gameObject);
    }

    // บริการโลกลงทะเบียนตอน Awake ของ [STORY] — เผื่อ OnEnable ของชิ้นนี้มาก่อน คำนวณซ้ำอีกรอบ
    private void Start() => ScenePresence.Refresh(gameObject);

    private void OnDisable()
    {
        GameEventBus.Unsubscribe<WorldVisualsSwappedEvent>(OnWorldSwapped);
    }

    private void OnWorldSwapped(WorldVisualsSwappedEvent e) => ScenePresence.Refresh(gameObject);

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) ScenePresence.Refresh(gameObject);
    }
#endif
}
