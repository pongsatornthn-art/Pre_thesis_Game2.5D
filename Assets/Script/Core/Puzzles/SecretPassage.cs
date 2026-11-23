using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// [2026-11-22] ประตูลับ (เช่น ชั้นหนังสือเลื่อน/หมุนเปิด) — BookshelfPuzzle สั่ง Open() ตอนดึงหนังสือถูกเล่ม
/// วิธีใช้: แปะที่ชั้นหนังสือ (หรือ GameObject ที่จะขยับ) แล้วตั้งว่าจะเลื่อนไปทางไหน/หมุนกี่องศา
///   Blockers = กล่องชนที่กันทางเดินอยู่ (ปิดให้ตอนเปิด) · Enable On Open = ของที่จะโผล่ตอนเปิด (เช่น ทางเดินหลังชั้น)
///   เลือกชั้นแล้วดูในฉาก: เส้นสีเขียว = ตำแหน่งตอนเปิดเสร็จ
/// </summary>
public class SecretPassage : MonoBehaviour
{
    [Tooltip("ตัวที่ขยับ (เว้นว่าง = ตัวนี้)")]
    public Transform target;
    [Tooltip("เลื่อนไปเท่าไหร่ (เมตร · แกนของพ่อ) เช่น (1.2, 0, 0) = เลื่อนข้าง")]
    public Vector3 slideOffset = new Vector3(1.2f, 0f, 0f);
    [Tooltip("หมุนเปิดกี่องศา (รอบแกน Y) — 0 = ไม่หมุน · ใช้คู่กับการเลื่อนได้")]
    public float swingAngle = 0f;
    [Tooltip("เวลาเปิด (วินาที)")]
    [Min(0.05f)] public float openSeconds = 2.5f;
    [Tooltip("สั่นเบาๆ ระหว่างเลื่อน (เหมือนชั้นหนักๆ ครูดพื้น)")]
    public float rumble = 0.01f;

    [Header("ของที่เปลี่ยนตอนเปิด")]
    public Collider[] blockers;
    public GameObject[] enableOnOpen;
    public GameObject[] disableOnOpen;

    [Header("เสียง (เว้นว่างได้)")]
    public AudioSource audioSource;
    public AudioClip openSound;

    public UnityEvent onOpened;

    public bool IsOpen { get; private set; }

    private Vector3 closedPos;
    private Quaternion closedRot;

    private bool inited;

    private void Awake() => Init();

    private void Init()
    {
        if (inited) return;
        inited = true;
        if (target == null) target = transform;
        closedPos = target.localPosition;
        closedRot = target.localRotation;
    }

    public void Open()
    {
        Init();
        if (IsOpen) return;
        IsOpen = true;
        ApplyToggles();
        if (audioSource != null && openSound != null) audioSource.PlayOneShot(openSound);
        StartCoroutine(OpenRoutine());
    }

    /// <summary>โหลดเซฟ: เปิดค้างทันที ไม่เล่นท่า</summary>
    public void OpenInstant()
    {
        Init(); // โหลดเซฟอาจมาก่อน Awake
        IsOpen = true;
        ApplyToggles();
        Pose(1f, 0f);
        onOpened?.Invoke();
    }

    private void ApplyToggles()
    {
        if (blockers != null) foreach (Collider c in blockers) if (c != null) c.enabled = false;
        if (enableOnOpen != null) foreach (GameObject g in enableOnOpen) if (g != null) g.SetActive(true);
        if (disableOnOpen != null) foreach (GameObject g in disableOnOpen) if (g != null) g.SetActive(false);
    }

    private IEnumerator OpenRoutine()
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / openSeconds)
        {
            Pose(t * t * (3f - 2f * t), rumble);
            yield return null;
        }
        Pose(1f, 0f);
        onOpened?.Invoke();
    }

    private void Pose(float k, float shake)
    {
        Vector3 jitter = shake > 0f ? Random.insideUnitSphere * shake : Vector3.zero;
        jitter.y = 0f;
        target.localPosition = closedPos + slideOffset * k + jitter;
        target.localRotation = closedRot * Quaternion.Euler(0f, swingAngle * k, 0f);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Transform t = target != null ? target : transform;
        Vector3 from = t.position;
        Vector3 to = t.parent != null ? t.parent.TransformPoint((Application.isPlaying ? closedPos : t.localPosition) + slideOffset) : from + slideOffset;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(from, to);
        Gizmos.DrawWireSphere(to, 0.1f);
    }
#endif
}
