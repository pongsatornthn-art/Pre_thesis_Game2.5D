using System.Collections;
using UnityEngine;

/// <summary>
/// สมุดเซฟบนโต๊ะ (งานข้อ 8) — เล็งสมุดแล้วคลิก (MiniGameInteractable → Begin)
/// อนิเมชั่น: กล้องเลื่อนเข้าใกล้สมุด → ปกสมุดกางออก → ปากกาขยับเขียน → หน้าจอเลือกช่องเซฟขึ้น
/// เลือกเสร็จ/ยกเลิก → ปากกากลับที่ → ปกปิด → กล้องถอยกลับ เล็งต่อได้
///
/// ของในซีนเป็นกล่องชั่วคราว — ได้โมเดลจริงแล้วลากใส่ช่อง Cover Pivot / Pen แทนได้เลย
/// (Cover Pivot = จุดหมุนที่ขอบสันสมุด หมุนรอบแกน Z ของตัวเอง)
/// ใช้เวลาจริง (unscaled) ทั้งหมด เพราะหน้าจอเลือกช่องหยุดเวลาเกม
/// [Claude 2026-11-11]
/// </summary>
[DefaultExecutionOrder(-40)]
public class DeskSaveNotebook : MonoBehaviour
{
    [Header("ต่อกับ")]
    [SerializeField] private SavePoint savePoint;
    [SerializeField] private FPSCameraController deskCamera;

    [Header("กล้อง")]
    [Tooltip("ตำแหน่ง+มุมกล้องตอนก้มดูสมุด")]
    [SerializeField] private Transform closeUpPoint;
    [SerializeField, Min(0.05f)] private float cameraMoveSeconds = 0.6f;

    [Header("สมุด")]
    [SerializeField] private Transform coverPivot;
    [Tooltip("มุมกางปก (องศา รอบแกน Z ของ Cover Pivot)")]
    [SerializeField] private float coverOpenAngle = 175f;
    [SerializeField, Min(0.05f)] private float coverSeconds = 0.45f;

    [Header("ปากกา")]
    [SerializeField] private Transform pen;
    [Tooltip("ตำแหน่ง+มุมปากกาตอนเขียน (บนหน้ากระดาษ)")]
    [SerializeField] private Transform penWritePoint;
    [SerializeField, Min(0f)] private float writeSeconds = 1.0f;
    [Tooltip("ขนาดการขยับมือตอนเขียน (เมตร)")]
    [SerializeField] private float scribbleSize = 0.025f;

    [Header("สอนเซฟ")]
    [Tooltip("ปักธงนี้ตอนคลิกสมุดครั้งแรก (= ผู้เล่นรู้จักจุดเซฟแล้ว) — ประตูออกห้องทำงานปลดล็อกด้วยธงนี้")]
    [SerializeField] private StoryFlagId usedFlag;

    [Header("เสียง (เว้นว่างได้)")]
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip writeSound;

    public static bool IsBusy { get; private set; }

    private Quaternion coverClosedRot;
    private Vector3 penHomePos;
    private Quaternion penHomeRot;

    private void Awake()
    {
        if (coverPivot != null) coverClosedRot = coverPivot.localRotation;
        if (pen != null)
        {
            penHomePos = pen.position;
            penHomeRot = pen.rotation;
        }
    }

    private void OnDisable()
    {
        if (IsBusy)
        {
            IsBusy = false;
            if (deskCamera != null) deskCamera.InputBlocked = false;
        }
    }

    /// <summary>ลากเข้าช่อง On Interact ของ MiniGameInteractable บนสมุด</summary>
    public void Begin()
    {
        if (IsBusy || SaveSlotPickerUI.IsOpen || savePoint == null) return;
        if (usedFlag != null) ServiceLocator.GetOptional<IStoryFlags>()?.Set(usedFlag);
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        IsBusy = true;
        if (deskCamera != null) deskCamera.InputBlocked = true;

        Transform cam = deskCamera != null ? deskCamera.transform : null;
        Vector3 camHomeLocalPos = cam != null ? cam.localPosition : Vector3.zero;
        Quaternion camHomeLocalRot = cam != null ? cam.localRotation : Quaternion.identity;
        Vector3 camHomeWorldPos = cam != null ? cam.position : Vector3.zero;
        Quaternion camHomeWorldRot = cam != null ? cam.rotation : Quaternion.identity;

        // 1. กล้องเข้าใกล้ + ปกกาง (พร้อมกัน)
        PlaySound(openSound);
        float t = 0f;
        float dur = Mathf.Max(cameraMoveSeconds, coverSeconds);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float kc = Smooth(t / cameraMoveSeconds);
            if (cam != null && closeUpPoint != null)
            {
                cam.position = Vector3.Lerp(camHomeWorldPos, closeUpPoint.position, kc);
                cam.rotation = Quaternion.Slerp(camHomeWorldRot, closeUpPoint.rotation, kc);
            }
            SetCover(Smooth(t / coverSeconds));
            yield return null;
        }

        // 2. ปากกาไปที่หน้ากระดาษ แล้วขยับเขียน
        if (pen != null && penWritePoint != null)
        {
            yield return MovePen(penHomePos, penHomeRot, penWritePoint.position, penWritePoint.rotation, 0.3f);
            PlaySound(writeSound);
            t = 0f;
            while (t < writeSeconds)
            {
                t += Time.unscaledDeltaTime;
                // ขยับเป็นวงเล็กๆ ไล่ไปทางขวา เหมือนเขียนหนังสือ
                float a = t * 22f;
                Vector3 offset = penWritePoint.right * (Mathf.Sin(a) * scribbleSize * 0.4f + t / writeSeconds * scribbleSize * 2f)
                               + penWritePoint.forward * (Mathf.Cos(a * 0.5f) * scribbleSize * 0.3f);
                pen.position = penWritePoint.position + offset;
                yield return null;
            }
        }
        else if (writeSeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(writeSeconds);
        }

        // 3. หน้าจอเลือกช่องเซฟ — รอจนผู้เล่นเลือก/ยกเลิก
        bool finished = false;
        savePoint.SaveFromDesk(_ => finished = true);
        while (!finished) yield return null;

        // 4. ย้อนกลับทุกอย่าง
        if (pen != null && penWritePoint != null)
            yield return MovePen(pen.position, pen.rotation, penHomePos, penHomeRot, 0.3f);

        Vector3 fromPos = cam != null ? cam.position : Vector3.zero;
        Quaternion fromRot = cam != null ? cam.rotation : Quaternion.identity;
        Transform parent = cam != null ? cam.parent : null;
        t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float kc = Smooth(t / cameraMoveSeconds);
            if (cam != null)
            {
                // กลับไปตำแหน่งเดิมเทียบกับตัวผู้เล่น (เผื่อตัวผู้เล่นขยับ)
                Vector3 homePos = parent != null ? parent.TransformPoint(camHomeLocalPos) : camHomeLocalPos;
                Quaternion homeRot = parent != null ? parent.rotation * camHomeLocalRot : camHomeLocalRot;
                cam.position = Vector3.Lerp(fromPos, homePos, kc);
                cam.rotation = Quaternion.Slerp(fromRot, homeRot, kc);
            }
            SetCover(1f - Smooth(t / coverSeconds));
            yield return null;
        }
        if (cam != null)
        {
            cam.localPosition = camHomeLocalPos;
            cam.localRotation = camHomeLocalRot;
        }
        SetCover(0f);

        if (deskCamera != null) deskCamera.InputBlocked = false;
        IsBusy = false;
    }

    private IEnumerator MovePen(Vector3 fromPos, Quaternion fromRot, Vector3 toPos, Quaternion toRot, float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Smooth(t / seconds);
            pen.SetPositionAndRotation(Vector3.Lerp(fromPos, toPos, k), Quaternion.Slerp(fromRot, toRot, k));
            yield return null;
        }
        pen.SetPositionAndRotation(toPos, toRot);
    }

    private void SetCover(float open01)
    {
        if (coverPivot == null) return;
        coverPivot.localRotation = coverClosedRot * Quaternion.Euler(0f, 0f, coverOpenAngle * Mathf.Clamp01(open01));
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null) return;
        ServiceLocator.GetOptional<IAudioService>()?.PlaySFX(clip, transform.position, 1f, false);
    }

    private static float Smooth(float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x));
}
