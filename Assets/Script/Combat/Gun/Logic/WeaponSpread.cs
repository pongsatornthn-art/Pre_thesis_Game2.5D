using UnityEngine;

namespace Combat.Gun
{
    /// <summary>
    /// คำนวณขนาดกรวยกระสุน (Weapon Spread) และการสุ่มองศากระสุนเบี่ยงเบน
    ///
    /// ทำไมต้องเป็น Plain C# (ไม่ใช่ MonoBehaviour) และอยู่ในแอสเซมบลีแยก:
    /// 1. เป็นสูตรคณิตศาสตร์ล้วน เขียน EditMode Test ได้ 100% โดยไม่ต้องเปิดซีน
    /// 2. ไม่พึ่ง RangedWeaponData (ScriptableObject) แต่รับค่าผ่าน SpreadSettings แทน
    ///    ตามหลัก Dependency Inversion — ตรรกะบริสุทธิ์ไม่ควรผูกกับของฝั่ง Unity
    /// </summary>
    public class WeaponSpread
    {
        /// <summary>ขนาดกรวยกระสุนปัจจุบัน (องศา) — ยิ่งเยอะยิ่งบาน ไม่แม่น</summary>
        public float CurrentSpread { get; private set; }

        /// <summary>
        /// อัปเดตขนาดกรวยกระสุนในแต่ละเฟรม
        /// </summary>
        /// <param name="dt">delta time ของเฟรม</param>
        /// <param name="ctx">บริบทการเคลื่อนที่และการเล็งของผู้เล่น</param>
        /// <param name="s">ค่าตัวเลขของปืนที่ถืออยู่</param>
        public void Tick(float dt, SpreadContext ctx, SpreadSettings s)
        {
            float target;
            if (ctx.isMoving || ctx.isDashing)
            {
                // กำลังเคลื่อนที่: กรวยจะบานออกเต็มที่ตาม maxSpreadAngle
                target = s.maxSpreadAngle;
            }
            else if (ctx.isAimingAtEnemy)
            {
                // หยุดเดิน + เมาส์จ่อบนศัตรู: กรวยจะค่อยๆ หุบลงหา minSpreadAngle
                target = s.minSpreadAngle;
            }
            else
            {
                // ยืนนิ่งแต่ไม่ได้เล็งศัตรู: ค้างค่าเดิมไว้ ไม่หุบและไม่บาน (ตามข้อตกลง GDD ข้อ 3)
                return;
            }

            // เมื่อกดคลิกขวาค้าง (Steady Aim) กรวยเป้าหมายจะแคบลงกว่าเดิม
            if (ctx.isSteady)
            {
                target *= s.steadySpreadMultiplier;
            }

            // คำนวณอัตราความเร็วในการปรับขนาดกรวย (องศาต่อวินาที)
            float focusTime = Mathf.Max(s.focusTime, 0.001f);
            float rate = s.maxSpreadAngle / focusTime;

            // ถ้าคลิกขวาค้าง หุบเร็วขึ้นเป็น steadyFocusSpeedMultiplier เท่า (ปกติ 2 เท่า)
            if (ctx.isSteady)
            {
                rate *= s.steadyFocusSpeedMultiplier;
            }

            CurrentSpread = Mathf.MoveTowards(CurrentSpread, target, rate * dt);
        }

        /// <summary>
        /// ถูกเรียกทันทีที่ยิง 1 นัด: กรวยบานเพิ่มขึ้นจากแรงถีบ (Recoil)
        /// </summary>
        public void OnShotFired(SpreadSettings s)
        {
            CurrentSpread = Mathf.Min(s.maxSpreadAngle, CurrentSpread + s.recoilSpread);
        }

        /// <summary>
        /// ถูกเรียกเมื่อผู้เล่นโดนโจมตี: ตกใจ/เสียหลัก กรวยเด้งบานสุดทันที (GDD: Interrupt Condition)
        /// </summary>
        public void OnPlayerHit(SpreadSettings s)
        {
            CurrentSpread = s.maxSpreadAngle;
        }

        /// <summary>
        /// ถูกเรียกเมื่อสลับอาวุธ: ตั้งค่าเริ่มต้นของปืนกระบอกใหม่
        /// </summary>
        public void OnWeaponChanged(SpreadSettings s)
        {
            CurrentSpread = s.maxSpreadAngle;
        }

        /// <summary>
        /// สุ่มมุมเบี่ยงเบนของกระสุนรอบแกน Y (องศา)
        ///
        /// ทำไมใช้การสุ่มแบบผลรวม (Centered Distribution) แทน Random.Range(-s, s):
        /// การสุ่ม Random.Range ปกติมีความน่าจะเป็นแบนราบ (Flat) ทำให้กระสุนมีโอกาสเบี้ยวสุดขอบ
        /// เท่ากับโอกาสพุ่งตรงกลาง ผู้เล่นจะรู้สึกว่าปืนยิงมั่วไร้ทิศทาง
        /// การเอาตัวสุ่ม 2 ตัวมาเฉลี่ยกันจะทำให้ความน่าจะเป็นเกาะกลุ่มตรงกลาง (Bell Curve)
        /// กระสุนส่วนใหญ่จะพุ่งใกล้เป้าหมาย แต่ยังมีโอกาสหลุดขอบได้บ้างเมื่อกรวยบาน
        /// </summary>
        public float RollAngle()
        {
            float t = (Random.Range(-1f, 1f) + Random.Range(-1f, 1f)) * 0.5f;
            return t * CurrentSpread;
        }
    }
}
