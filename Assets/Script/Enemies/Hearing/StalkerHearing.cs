using UnityEngine;

namespace Enemies.Hearing
{
    /// <summary>
    /// ระบบรับฟังเสียงปืนของ Stalker (แปะที่ Prefab ของ StalkerAI)
    /// สืบทอดจาก SoundListenerBase เพื่อรับฟัง GunshotEvent ผ่าน GameEventBus
    /// โดยไม่ต้องแก้ไขโค้ด StalkerAI.cs เดิมของเพื่อนร่วมทีม
    /// </summary>
    [RequireComponent(typeof(StalkerAI))]
    public class StalkerHearing : SoundListenerBase
    {
        [Tooltip("การอ้างอิงถึง StalkerAI บนตัวเดียวกัน (จะค้นหาให้อัตโนมัติหากเว้นว่าง)")]
        [SerializeField] private StalkerAI stalker;

        protected override float BaseHearRadius => stalker != null ? stalker.soundSenseRadius : 18f;
        protected override LayerMask ObstacleMask => stalker != null ? stalker.obstacleMask : (LayerMask)0;

        private void Awake()
        {
            if (stalker == null)
            {
                stalker = GetComponent<StalkerAI>();
            }
        }

        protected override void OnHeard(Vector3 sourcePos, float normalizedLoudness)
        {
            if (stalker == null) return;

            // ⚠️ ห้ามดึงผีออกจากสถานะที่กำลังทำอะไรสำคัญอยู่
            // ถ้าไม่กันไว้ ผู้เล่นที่โดนจับเล่นมินิเกมจะยิงปืน 1 นัดแล้วหลุดฟรีทันที (ช่องโหว่)
            // ส่วนตอนโดนสตันก็ต้องปล่อยให้นับเวลาสตันจนครบก่อน
            if (stalker.CurrentState is StalkerStateKill || stalker.CurrentState is StalkerStateStun) return;

            // กำลังไล่ล่าผู้เล่นอยู่แล้ว = รู้ตำแหน่งดีกว่าเสียงปืนอยู่แล้ว ไม่ต้องลดระดับลงไปเดินสำรวจ
            if (stalker.CurrentState is StalkerStateChase) return;

            // แปลงความดังสัมพัทธ์ (0..1) เป็นระดับความสงสัย (Suspicion)
            // ยิงจ่อตัวใกล้ๆ = ความสงสัย 100% (วิ่งมาตรวจทันที), ยิงขอบวง = ความสงสัยต่ำ (เดินมาดูช้าๆ)
            float minSuspicion = stalker.minShoutSuspicion;
            float maxSuspicion = stalker.maxShoutSuspicion;
            float suspicion = Mathf.Lerp(minSuspicion, maxSuspicion, normalizedLoudness);

            // เพิ่มค่าความตื่นตัว (Awareness) ในระบบสะสมของ Stalker
            if (stalker.Awareness != null)
            {
                stalker.Awareness.AddAwareness(suspicion);
            }

            // หมายเหตุ: เมธอด HandleMinionShout ใน StalkerAI ถูกประกาศเป็น private
            // จึงสั่งเปลี่ยนสถานะเข้าสู่ StalkerStateAlert โดยตรงผ่านเมธอดสาธารณะ ChangeState
            stalker.ChangeState(new StalkerStateAlert(sourcePos, suspicion));
        }
    }
}
