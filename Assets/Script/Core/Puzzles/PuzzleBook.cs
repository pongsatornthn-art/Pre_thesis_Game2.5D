using System.Collections;
using UnityEngine;

/// <summary>
/// [2026-11-22] หนังสือ 1 เล่มในปริศนาห้องหนังสือ — คลิกแล้วดึงออกมา
/// วิธีใช้: แปะที่หนังสือแต่ละเล่มที่ให้คลิกได้ (ต้องมี Collider — ไม่มีจะใส่ BoxCollider ให้เอง)
///   ติ๊ก Is Correct ที่เล่นที่ถูกเล่มเดียว · เล่มอื่นเป็นตัวหลอก
///   ชี้เมาส์ = เลื่อนออกมานิดนึง (+ เรืองแสง ถ้ามี ItemHighlight แบบ Manual)
///   คลิก = ดึงออกมาเต็ม → ถูก: ค้างไว้ · ผิด: เลื่อนกลับที่เดิม
/// ทิศดึง: ช่อง Pull Direction (แกนของตัวหนังสือเอง) — ลองกด Play แล้วชี้ดู ถ้าหนังสือจมเข้าชั้น ให้กลับทิศ
/// </summary>
[DisallowMultipleComponent]
public class PuzzleBook : MonoBehaviour
{
    [Tooltip("เล่มที่ถูก (ดึงแล้วประตูลับเปิด)")]
    public bool isCorrect = false;

    [Header("ท่าดึง")]
    [Tooltip("ทิศดึงออก — แกนของตัวหนังสือเอง (ลูกศรสีฟ้าตอนเลือกหนังสือ = Forward)")]
    public Vector3 pullDirection = Vector3.forward;
    [Tooltip("ดึงออกมากี่เมตร")]
    [Min(0f)] public float pullDistance = 0.12f;
    [Tooltip("ตอนชี้เมาส์ เลื่อนออกมากี่ % ของระยะดึง")]
    [Range(0f, 1f)] public float hoverAmount = 0.2f;
    [Tooltip("เอียงหัวหนังสือตอนดึง (องศา) — เหมือนเอานิ้วเกี่ยวสันออกมา")]
    public float pullTilt = 12f;
    [Tooltip("แกนที่เอียง (แกนของตัวหนังสือ)")]
    public Vector3 tiltAxis = Vector3.right;
    [Tooltip("เวลาดึง/คืน (วินาที)")]
    [Min(0.05f)] public float pullSeconds = 0.35f;
    [Tooltip("เล่มผิด: ค้างไว้กี่วินาทีก่อนเลื่อนกลับ")]
    [Min(0f)] public float wrongHoldSeconds = 0.6f;

    [Header("เสียง (เว้นว่างได้)")]
    public AudioClip pullSound;
    public AudioClip returnSound;

    public bool IsPulled { get; private set; }
    public bool IsBusy => anim != null;

    private Vector3 restPos;
    private Quaternion restRot;
    private float amount;   // 0 = อยู่ในชั้น · 1 = ดึงออกเต็ม
    private float target;
    private bool hovered;
    private Coroutine anim;
    private ItemHighlight highlight;

    private bool inited;

    private void Awake() => Init();

    private void Init()
    {
        if (inited) return;
        inited = true;
        restPos = transform.localPosition;
        restRot = transform.localRotation;
        if (GetComponentInChildren<Collider>() == null) gameObject.AddComponent<BoxCollider>();
        highlight = GetComponent<ItemHighlight>();
    }

    private void Update()
    {
        if (anim != null || IsPulled) return;
        // ชี้เมาส์ → เลื่อนออกมานิดเดียว (นุ่มๆ)
        target = hovered ? hoverAmount : 0f;
        if (!Mathf.Approximately(amount, target))
        {
            amount = Mathf.MoveTowards(amount, target, Time.unscaledDeltaTime * 3f);
            Apply();
        }
    }

    public void SetHovered(bool on)
    {
        if (hovered == on) return;
        hovered = on;
        if (highlight != null) highlight.SetHighlighted(on && !IsPulled);
    }

    /// <summary>ดึงออก — คืนค่าว่าเป็นเล่มที่ถูกไหม (เล่มผิดจะเลื่อนกลับเอง)</summary>
    public bool Pull(AudioSource audio)
    {
        if (anim != null || IsPulled) return false;
        if (highlight != null) highlight.SetHighlighted(false);
        if (audio != null && pullSound != null) audio.PlayOneShot(pullSound);
        anim = StartCoroutine(PullRoutine(audio));
        return isCorrect;
    }

    /// <summary>โหลดเซฟ: เล่มที่ถูกถูกดึงไปแล้ว → วางท่าดึงออกทันที</summary>
    public void SetPulledInstant()
    {
        Init(); // โหลดเซฟอาจมาก่อน Awake
        if (anim != null) { StopCoroutine(anim); anim = null; }
        IsPulled = true;
        amount = 1f;
        Apply();
    }

    private IEnumerator PullRoutine(AudioSource audio)
    {
        yield return Animate(1f);
        if (isCorrect)
        {
            IsPulled = true;
        }
        else
        {
            yield return new WaitForSecondsRealtime(wrongHoldSeconds);
            if (audio != null && returnSound != null) audio.PlayOneShot(returnSound);
            yield return Animate(0f);
        }
        anim = null;
    }

    private IEnumerator Animate(float to)
    {
        float from = amount;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / pullSeconds)
        {
            float k = t * t * (3f - 2f * t);
            amount = Mathf.Lerp(from, to, k);
            Apply();
            yield return null;
        }
        amount = to;
        Apply();
    }

    private void Apply()
    {
        Vector3 dirLocal = restRot * pullDirection.normalized; // ทิศในพิกัดของพ่อ (ชั้นหนังสือ)
        transform.localPosition = restPos + dirLocal * (pullDistance * amount);
        transform.localRotation = restRot * Quaternion.AngleAxis(pullTilt * amount, tiltAxis);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isCorrect ? Color.green : Color.yellow;
        Vector3 d = transform.TransformDirection(pullDirection.normalized) * Mathf.Max(pullDistance, 0.05f);
        Gizmos.DrawLine(transform.position, transform.position + d * 2f);
        Gizmos.DrawWireSphere(transform.position + d * 2f, 0.02f);
    }
#endif
}
