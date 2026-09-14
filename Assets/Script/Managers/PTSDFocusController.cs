using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class PTSDFocusController : MonoBehaviour
{
    [Header("Post-Processing")]
    public Volume ptsdVolume;
    [Tooltip("ความเร็วในการเฟดภาพเบลอ")]
    public float transitionSpeed = 10f;

    [Header("Blur Settings (ตั้งค่าความเบลอ)")]
    [Tooltip("ค่าปกติ (ไม่เบลอ)")]
    public float normalFocalLength = 1f;
    [Tooltip("ค่าตอนเพ่งสมาธิ (ยิ่งเยอะฉากหลังยิ่งเบลอจัด)")]
    public float activeFocalLength = 60f;
    [Tooltip("ระยะห่างของจุดที่ชัดที่สุด (เช่น ระยะมือที่ถือปืน)")]
    public float focusDistance = 0.5f;

    private DepthOfField dof;
    private bool isFocusing = false;
    private Coroutine blurCoroutine;

    private void Start()
    {
        if (ptsdVolume != null && ptsdVolume.profile != null)
        {
            ptsdVolume.profile.TryGet(out dof);

            if (dof != null)
            {
                dof.focalLength.value = normalFocalLength;
                dof.focusDistance.value = focusDistance;
            }
        }
    }

    /// <summary>
    /// ฟังก์ชันสาธารณะสำหรับให้ระบบอื่น (เช่น เล็งปืน หรือ มินิเกม) สั่งเปิด/ปิดเบลอ
    /// </summary>
    /// <param name="focus">true = เริ่มเบลอฉากหลัง, false = กลับมาปกตื</param>
    public void SetFocusState(bool focus)
    {
        if (dof == null || isFocusing == focus) return;

        isFocusing = focus;

        if (blurCoroutine != null) StopCoroutine(blurCoroutine);
        blurCoroutine = StartCoroutine(AnimateBlur(focus ? activeFocalLength : normalFocalLength));
    }

    private IEnumerator AnimateBlur(float targetFocalLength)
    {
        dof.active = true;

        while (!Mathf.Approximately(dof.focalLength.value, targetFocalLength))
        {
            dof.focalLength.value = Mathf.MoveTowards(dof.focalLength.value, targetFocalLength, transitionSpeed * Time.deltaTime * 50f);
            yield return null;
        }

        if (!isFocusing)
        {
            dof.active = false;
        }
    }
}