using UnityEngine;

namespace Combat.Gun
{
    /// <summary>
    /// รับผิดชอบเรื่อง Feedback ของปืนทั้งหมด (เสียง, แสงแฟลชปากกระบอก, การสั่นหน้าจอ)
    /// แยกออกจาก GunfireController ตามหลัก Single Responsibility Principle (SRP)
    /// เพื่อให้การปรับแต่งเสียง/VFX ทำได้ที่ไฟล์เดียวโดยไม่ต้องยุ่งกับตรรกะการคำนวณการยิง
    /// </summary>
    public class GunFeedback : MonoBehaviour
    {
        [Header("Muzzle Flash")]
        [Tooltip("แสงวาบปากกระบอกปืน (Light) — เว้นว่าง = หาในลูกของผู้เล่น")]
        [SerializeField] private MuzzleFlashEffect muzzleFlash;

        [Tooltip("สไปรท์เปลวไฟปากกระบอกปืน — เว้นว่าง = หาในลูกของผู้เล่น")]
        [SerializeField] private MuzzleFlashSprite muzzleFlashSprite;

        private IAudioService audioService;

        private void Awake()
        {
            // แคช AudioService ไว้ตั้งแต่เริ่มเกม ห้ามค้นหาใหม่ทุกนัดที่ยิงเพื่อประสิทธิภาพ
            audioService = ServiceLocator.Get<IAudioService>();

            if (muzzleFlash == null)
            {
                muzzleFlash = GetComponentInChildren<MuzzleFlashEffect>();
            }

            if (muzzleFlashSprite == null)
            {
                // true = หาแม้ก้อนนั้นถูกปิดอยู่ (สไปรท์แฟลชซ่อนตัวเองไว้ตอนเริ่มเกม)
                muzzleFlashSprite = GetComponentInChildren<MuzzleFlashSprite>(true);
            }
        }

        private IAudioService GetAudio()
        {
            if (audioService == null)
            {
                audioService = ServiceLocator.Get<IAudioService>();
            }
            return audioService;
        }

        /// <summary>
        /// แสดงฟีดแบ็กเมื่อยิงปืน (เสียงปืน + แสงแฟลช + จอสั่น)
        /// </summary>
        public void PlayShot(RangedWeaponData gun, Vector3 muzzlePos)
        {
            if (gun == null) return;

            // 1. เล่นเสียงยิงปืนผ่าน IAudioService (ห้ามสร้าง AudioSource เอง)
            if (gun.fireSfx != null)
            {
                GetAudio()?.PlaySFX(gun.fireSfx, muzzlePos);
            }

            // 2. แสงวาบ + สไปรท์เปลวไฟที่ปลายกระบอกปืน (ทำงานคู่กัน)
            //    แสง = ทำให้ห้อง 3D สว่างวาบ · สไปรท์ = รูปเปลวไฟพิกเซลที่ตามองเห็น
            if (muzzleFlash != null) muzzleFlash.PlayFlash();
            if (muzzleFlashSprite != null) muzzleFlashSprite.Play();

            // 3. จอสั่นเบาๆ (จะทำงานร่วมกับ GunCameraShake ในเฟส 2)
            GunCameraShake shake = GunCameraShake.Instance;
            if (shake != null)
            {
                shake.Shake(0.05f, 0.3f);
            }
        }

        /// <summary>
        /// แสดงฟีดแบ็กเมื่อกระสุนหมดแล้วกดยิง (เสียงคลิกปืนเปล่า)
        /// </summary>
        public void PlayEmpty(RangedWeaponData gun, Vector3 muzzlePos)
        {
            if (gun == null) return;

            if (gun.emptySfx != null)
            {
                GetAudio()?.PlaySFX(gun.emptySfx, muzzlePos);
            }
            else
            {
                Debug.Log("<color=yellow>แชะ! กระสุนหมด (ยังไม่ได้ใส่ emptySfx ในอาวุธ)</color>");
            }
        }

        /// <summary>
        /// แสดงฟีดแบ็กเมื่อเริ่มรีโหลดกระสุน
        /// </summary>
        public void PlayReload(RangedWeaponData gun, Vector3 muzzlePos)
        {
            if (gun == null) return;

            if (gun.reloadSfx != null)
            {
                GetAudio()?.PlaySFX(gun.reloadSfx, muzzlePos);
            }
        }
    }
}
