using UnityEngine;
using Combat.Gun;

namespace Enemies.Hearing
{
    /// <summary>
    /// คลาสฐานสำหรับสิ่งมีชีวิตหรือเอนทิตีที่สามารถได้ยินเสียงในเกม (Open/Closed Principle)
    /// คำนวณระยะทาง การลดทอนของเสียงเมื่อมีกำแพงกั้น และแปลงเป็นความดังสัมพัทธ์ (0..1)
    /// </summary>
    public abstract class SoundListenerBase : MonoBehaviour
    {
        /// <summary>รัศมีการได้ยินเสียงพื้นฐานของสิ่งมีชีวิตตัวนี้ (เมตร)</summary>
        protected abstract float BaseHearRadius { get; }

        /// <summary>LayerMask ของสิ่งกีดขวางที่สามารถกั้นเสียงได้ (เช่น กำแพง)</summary>
        protected abstract LayerMask ObstacleMask { get; }

        /// <summary>
        /// ถูกเรียกเมื่อได้ยินเสียงปืนในระยะทำการ
        /// </summary>
        /// <param name="sourcePos">ตำแหน่งจุดกำเนิดเสียง</param>
        /// <param name="normalizedLoudness">ความดังสัมพัทธ์ที่ได้ยิน (0.0 = ขอบวงพอดี, 1.0 = ยิงจ่อตัว)</param>
        protected abstract void OnHeard(Vector3 sourcePos, float normalizedLoudness);

        protected virtual void OnEnable()
        {
            // ลงทะเบียนรับอีเวนต์เสียงปืนจากระบบ GameEventBus
            GameEventBus.Subscribe<GunshotEvent>(OnGunshot);
        }

        protected virtual void OnDisable()
        {
            // ยกเลิกการลงทะเบียนเสมอเพื่อป้องกันปัญหา Memory Leak หรือการเรียกข้ามฉาก
            GameEventBus.Unsubscribe<GunshotEvent>(OnGunshot);
        }

        private void OnGunshot(GunshotEvent ev)
        {
            float radius = BaseHearRadius * ev.Loudness;
            if (radius <= 0.001f) return;

            // กำแพงกั้นเสียง: หากมีกำแพงหรือสิ่งกีดขวางขวางแนวเสียง รัศมีการได้ยินจะลดลงครึ่งหนึ่งตาม GDD
            Vector3 headPos = transform.position + Vector3.up;
            if (Physics.Linecast(ev.Position, headPos, ObstacleMask))
            {
                radius *= 0.5f;
            }

            float dist = Vector3.Distance(ev.Position, transform.position);
            if (dist > radius) return; // อยู่นอกรัศมีการได้ยิน = ไม่ได้ยินเสียง

            // คำนวณความดังสัมพัทธ์: 0 ที่ขอบวง, 1 ตอนจ่อตัว
            float normalizedLoudness = Mathf.Clamp01(1f - (dist / radius));
            OnHeard(ev.Position, normalizedLoudness);
        }
    }
}
