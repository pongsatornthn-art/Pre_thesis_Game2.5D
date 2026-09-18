using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

namespace Combat.Gun
{
    /// <summary>
    /// ระบบเขย่าหน้าจอสั้นๆ เมื่อยิงปืน (GDD: สั่นเบาๆ 0.05 วินาที)
    /// สร้างแยกออกมาเพื่อไม่ให้กระทบกับ PTSDCameraShake ของเพื่อนร่วมทีม
    /// </summary>
    public class GunCameraShake : MonoBehaviour
    {
        public static GunCameraShake Instance { get; private set; }

        [Header("Cinemachine Camera")]
        [SerializeField] private CinemachineCamera vCam;

        private CinemachineBasicMultiChannelPerlin noiseProfile;
        private Coroutine shakeCoroutine;

        /// <summary>แรงสั่นที่สคริปต์นี้ใส่เข้าไปอยู่ตอนนี้ (ไว้ถอนคืนให้ตรงส่วน)</summary>
        private float ownAmplitude;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            if (vCam == null)
            {
                vCam = FindFirstObjectByType<CinemachineCamera>();
            }

            if (vCam != null)
            {
                noiseProfile = vCam.GetComponent<CinemachineBasicMultiChannelPerlin>();
            }
        }

        /// <summary>
        /// สั่งให้หน้าจอสั่นตามระยะเวลาและความแรงที่กำหนด
        /// </summary>
        public void Shake(float duration = 0.05f, float amplitude = 0.3f)
        {
            if (noiseProfile == null)
            {
                if (vCam != null)
                {
                    noiseProfile = vCam.GetComponent<CinemachineBasicMultiChannelPerlin>();
                }
                if (noiseProfile == null) return;
            }

            // ยิงรัว ๆ: ยกเลิกรอบเก่าแล้วต้อง "ถอนแรงสั่นที่เราเคยใส่ไว้" ออกก่อนเสมอ
            // ไม่งั้นค่าจะสะสมขึ้นเรื่อย ๆ จนจอสั่นค้างไม่มีวันหยุด
            if (shakeCoroutine != null)
            {
                StopCoroutine(shakeCoroutine);
                RemoveOwnAmplitude();
            }

            shakeCoroutine = StartCoroutine(ShakeRoutine(duration, amplitude));
        }

        /// <summary>ถอนเฉพาะแรงสั่นที่สคริปต์นี้ใส่เข้าไป ไม่ไปยุ่งกับของระบบอื่น</summary>
        private void RemoveOwnAmplitude()
        {
            if (noiseProfile == null || ownAmplitude <= 0f) return;
            noiseProfile.AmplitudeGain = Mathf.Max(0f, noiseProfile.AmplitudeGain - ownAmplitude);
            ownAmplitude = 0f;
        }

        private IEnumerator ShakeRoutine(float duration, float amplitude)
        {
            // ⚠️ สำคัญ: ต้องจำค่าเดิมไว้แล้วคืนค่าเดิม ห้ามยัด 0
            // เพราะ PTSDCameraShake ของเพื่อนร่วมทีมเขียนค่า AmplitudeGain ตัวเดียวกันนี้
            // และมันเซ็ตค่าแค่ตอนเปลี่ยนสถานะ PTSD เท่านั้น
            // ถ้าเรายัด 0 ทับ = ยิงปืน 1 นัดตอนอยู่ในโลก PTSD แล้วจอหยุดสั่นถาวร
            // บวกทับของเดิมเพื่อให้สั่นแรงขึ้นชั่วขณะ ไม่ใช่ไปแทนที่ของระบบอื่น
            ownAmplitude = amplitude;
            noiseProfile.AmplitudeGain += amplitude;

            // ใช้เวลาจริง เพราะจังหวะยิงอาจอยู่ในช่วงที่เกมหยุดเวลา (เปิดสมุด/Pause)
            yield return new WaitForSecondsRealtime(duration);

            // ถอนเฉพาะส่วนของเรา ค่าที่ระบบอื่นตั้งไว้ยังอยู่ครบ
            RemoveOwnAmplitude();
            shakeCoroutine = null;
        }
    }
}
