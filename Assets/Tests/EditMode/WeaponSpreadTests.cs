using NUnit.Framework;
using UnityEngine;
using Combat.Gun;

namespace Tests.EditMode
{
    /// <summary>
    /// ชุดทดสอบ EditMode สำหรับคลาส WeaponSpread (ข้อ 14)
    /// ตรวจสอบความถูกต้องของสูตรกรวยกระสุนตามตารางการตัดสินใจใน GDD โดยไม่ต้องเปิดเกม
    ///
    /// หมายเหตุ: ใช้ SpreadSettings (struct ตัวเลขล้วน) แทน RangedWeaponData
    /// เพราะ ScriptableObject อยู่คนละแอสเซมบลี ไฟล์เทสจะมองไม่เห็น
    /// (ของเดิมเขียนด้วย RangedWeaponData ทำให้เทสไม่ถูกคอมไพล์เลยสักเคส)
    /// </summary>
    public class WeaponSpreadTests
    {
        private SpreadSettings gun;
        private WeaponSpread spread;

        [SetUp]
        public void Setup()
        {
            spread = new WeaponSpread();
            gun = new SpreadSettings
            {
                maxSpreadAngle = 15f,
                minSpreadAngle = 0f,
                focusTime = 1.5f,
                recoilSpread = 5f,
                steadySpreadMultiplier = 0.35f,
                steadyFocusSpeedMultiplier = 2f
            };
        }

        // เคสที่ 1: เดินอยู่ -> กรวยวิ่งเข้าหา maxSpreadAngle (15 องศา)
        [Test]
        public void Test_1_Moving_ExpandsToMaxSpread()
        {
            spread.OnWeaponChanged(gun);
            SpreadContext aimingCtx = new SpreadContext { isMoving = false, isAimingAtEnemy = true };
            spread.Tick(2.0f, aimingCtx, gun);
            Assert.AreEqual(0f, spread.CurrentSpread, 0.01f);

            // เริ่มเดิน -> กรวยต้องขยายเข้าหา maxSpreadAngle
            SpreadContext movingCtx = new SpreadContext { isMoving = true, isAimingAtEnemy = false };
            spread.Tick(0.5f, movingCtx, gun);
            Assert.Greater(spread.CurrentSpread, 0f);

            spread.Tick(2.0f, movingCtx, gun);
            Assert.AreEqual(gun.maxSpreadAngle, spread.CurrentSpread, 0.01f);
        }

        // เคสที่ 2: เดิน + steady (คลิกขวา) -> เข้าหา maxSpreadAngle * 0.35 (~5.25 องศา)
        [Test]
        public void Test_2_MovingAndSteady_CapsAtReducedSpread()
        {
            spread.OnWeaponChanged(gun);
            SpreadContext steadyMoveCtx = new SpreadContext { isMoving = true, isSteady = true, isAimingAtEnemy = false };

            float expectedTarget = gun.maxSpreadAngle * gun.steadySpreadMultiplier;
            spread.Tick(2.0f, steadyMoveCtx, gun);
            Assert.AreEqual(expectedTarget, spread.CurrentSpread, 0.05f);
        }

        // เคสที่ 3: ยืนนิ่ง + เล็งโดนศัตรู ครบ focusTime -> ได้ minSpreadAngle (0 องศา)
        [Test]
        public void Test_3_StandingAndAimingAtEnemy_ReachesZeroSpread()
        {
            spread.OnWeaponChanged(gun);
            SpreadContext focusCtx = new SpreadContext { isMoving = false, isSteady = false, isAimingAtEnemy = true };

            // ติ๊กจนครบเวลา focusTime (1.5 วิ) -> ต้องหุบเหลือ 0 องศาเป๊ะตาม GDD
            spread.Tick(gun.focusTime + 0.1f, focusCtx, gun);
            Assert.AreEqual(gun.minSpreadAngle, spread.CurrentSpread, 0.01f);
        }

        // เคสที่ 4: ยืนนิ่ง + ไม่ได้เล็งศัตรู -> ค่าต้องไม่เปลี่ยนเลย (ค้างที่เดิม)
        [Test]
        public void Test_4_StandingWithoutAimingAtEnemy_FreezesSpread()
        {
            spread.OnWeaponChanged(gun);
            SpreadContext focusCtx = new SpreadContext { isMoving = false, isAimingAtEnemy = true };
            spread.Tick(0.5f, focusCtx, gun);
            float spreadBefore = spread.CurrentSpread;
            Assert.Less(spreadBefore, 15f);
            Assert.Greater(spreadBefore, 0f);

            // ยืนนิ่งแต่ไม่ได้เล็งศัตรู -> ค่าต้องหยุดค้างที่เดิม ไม่ขยับเลย
            SpreadContext notAimingCtx = new SpreadContext { isMoving = false, isAimingAtEnemy = false };
            spread.Tick(1.0f, notAimingCtx, gun);
            Assert.AreEqual(spreadBefore, spread.CurrentSpread, 0.0001f);
        }

        // เคสที่ 5: OnShotFired -> กรวยเพิ่มขึ้นทีละ recoilSpread แต่ไม่เกิน maxSpreadAngle แม้ยิงรัว 100 นัด
        [Test]
        public void Test_5_OnShotFired_IncreasesSpread_ClampedAtMax()
        {
            SpreadContext focusCtx = new SpreadContext { isMoving = false, isAimingAtEnemy = true };
            spread.Tick(2.0f, focusCtx, gun);
            Assert.AreEqual(0f, spread.CurrentSpread, 0.01f);

            // ยิง 1 นัด -> กรวยเด้งขึ้นเท่ากับ recoilSpread (5 องศา)
            spread.OnShotFired(gun);
            Assert.AreEqual(gun.recoilSpread, spread.CurrentSpread, 0.01f);

            // ยิงรัว 100 นัด -> ต้องไม่ทะลุเพดาน maxSpreadAngle (15 องศา)
            for (int i = 0; i < 100; i++) spread.OnShotFired(gun);
            Assert.AreEqual(gun.maxSpreadAngle, spread.CurrentSpread, 0.01f);
        }

        // เคสที่ 6: RollAngle() -> สุ่ม 10,000 ครั้ง ค่าต้องอยู่ในช่วง [-CurrentSpread, CurrentSpread]
        // และค่าเฉลี่ยของ |มุม| ต้องน้อยกว่า 0.5 * CurrentSpread (พิสูจน์การกระจายแบบ Bell curve เกาะกลาง)
        [Test]
        public void Test_6_RollAngle_BellCurveDistribution()
        {
            spread.OnWeaponChanged(gun);
            float currentSpread = spread.CurrentSpread;
            int samples = 10000;
            float sumAbsolute = 0f;

            for (int i = 0; i < samples; i++)
            {
                float angle = spread.RollAngle();
                Assert.GreaterOrEqual(angle, -currentSpread);
                Assert.LessOrEqual(angle, currentSpread);
                sumAbsolute += Mathf.Abs(angle);
            }

            float averageAbsolute = sumAbsolute / samples;
            float uniformExpectedAverage = currentSpread * 0.5f;

            Assert.Less(averageAbsolute, uniformExpectedAverage,
                $"ค่าเฉลี่ย |มุม| ({averageAbsolute:F2}) ควรน้อยกว่าครึ่งหนึ่งของกรวย ({uniformExpectedAverage:F2}) เพื่อยืนยันว่ากระสุนเกาะกลางจริง");
        }

        // เคสที่ 7 (เพิ่มโดย Claude): โดนศัตรูตี -> กรวยเด้งบานสุดทันที ตาม GDD Interrupt Condition
        [Test]
        public void Test_7_OnPlayerHit_JumpsToMaxSpread()
        {
            SpreadContext focusCtx = new SpreadContext { isMoving = false, isAimingAtEnemy = true };
            spread.Tick(2.0f, focusCtx, gun);
            Assert.AreEqual(0f, spread.CurrentSpread, 0.01f);

            spread.OnPlayerHit(gun);
            Assert.AreEqual(gun.maxSpreadAngle, spread.CurrentSpread, 0.01f);
        }

        // เคสที่ 8 (เพิ่มโดย Claude): ยืนนิ่ง + เล็งศัตรู + คลิกขวา -> ต้องหุบเร็วกว่าไม่คลิกขวา
        [Test]
        public void Test_8_SteadyAim_FocusesFasterThanNormal()
        {
            WeaponSpread normal = new WeaponSpread();
            WeaponSpread steady = new WeaponSpread();
            normal.OnWeaponChanged(gun);
            steady.OnWeaponChanged(gun);

            SpreadContext normalCtx = new SpreadContext { isMoving = false, isAimingAtEnemy = true, isSteady = false };
            SpreadContext steadyCtx = new SpreadContext { isMoving = false, isAimingAtEnemy = true, isSteady = true };

            // ติ๊กเวลาเท่ากันครึ่งหนึ่งของ focusTime
            float halfFocus = gun.focusTime * 0.5f;
            normal.Tick(halfFocus, normalCtx, gun);
            steady.Tick(halfFocus, steadyCtx, gun);

            Assert.Less(steady.CurrentSpread, normal.CurrentSpread,
                "กดคลิกขวาค้างต้องหุบกรวยได้เร็วกว่าการยืนเฉย ๆ");
        }
    }
}
