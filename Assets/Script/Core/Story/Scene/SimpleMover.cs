using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// ตัวเคลื่อนที่สารพัดประโยชน์ — ประตูเลื่อน / กำแพงขยับ / ของตก / ผีเดินผ่าน / ลิฟต์
/// แปะที่วัตถุในซีนแล้วลาก Empty Object ใส่ช่อง Waypoints ได้เลย
///
/// ทำไมถึงเป็น MonoBehaviour ไม่ใช่ IStoryAction:
/// ScriptableObject (ไฟล์ asset) ชี้ไปหาของในซีนไม่ได้ ลาก Transform ใส่แล้วจะเด้งกลับเป็น None
/// ตรรกะที่ต้องรู้จักตำแหน่งในซีน จึงต้องอยู่ในซีนเสมอ
/// ฝั่งเนื้อเรื่องสั่งงานผ่าน "ธง" แทน โดยไม่ต้องรู้จักวัตถุชิ้นนี้เลย (ดู StoryFlagListener)
///
/// สคริปต์นี้ไม่รู้จักระบบเนื้อเรื่องเลย — ใช้กับอะไรก็ได้ที่แค่ต้องการให้ของเคลื่อนที่
/// </summary>
public class SimpleMover : MonoBehaviour
{
    public enum MoveMode
    {
        Once,       // เดินถึงปลายทางแล้วหยุด
        Loop,       // วนกลับไปจุดแรกแล้วเดินใหม่เรื่อยๆ
        PingPong    // เดินไป-กลับสลับไปมา
    }

    [Header("เส้นทาง — ลาก Empty Object ในซีนใส่ได้เลย")]
    [Tooltip("จุดปลายทางเรียงตามลำดับ (จุดเริ่มต้น = ตำแหน่งที่วัตถุนี้ยืนอยู่ตอนเริ่มเกม)")]
    public Transform[] waypoints;

    [Header("การเคลื่อนที่")]
    [Tooltip("ความเร็ว (หน่วยต่อวินาที)")]
    public float speed = 2f;

    public MoveMode mode = MoveMode.Once;

    [Tooltip("ความนุ่มตอนออกตัว/หยุด — เส้นตรง = ความเร็วคงที่ตลอด")]
    public AnimationCurve easing = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Tooltip("หันหน้าไปทางที่เดิน (ใช้กับตัวละคร ไม่ควรเปิดกับประตู)")]
    public bool faceMoveDirection = false;

    [Tooltip("เคลื่อนที่ต่อแม้เกมถูกหยุด เช่น ตอนเปิดสมุด — ปกติปิดไว้")]
    public bool ignoreTimeScale = false;

    [Header("เริ่มเมื่อไหร่")]
    [Tooltip("เริ่มเดินทันทีที่เกมเริ่ม (ปกติปิดไว้ แล้วให้ StoryFlagListener เป็นคนสั่ง)")]
    public bool playOnStart = false;

    [Header("เหตุการณ์")]
    [Tooltip("ยิงเมื่อเดินครบเส้นทาง 1 รอบ — ลาก StoryFlagSetter.Set มาใส่เพื่อปักธงบอกเนื้อเรื่องว่าเสร็จแล้ว")]
    public UnityEvent onArrived;

    private Vector3 startPos;
    private Quaternion startRot;
    private Coroutine routine;

    public bool IsMoving => routine != null;

    private void Awake()
    {
        startPos = transform.position;
        startRot = transform.rotation;
    }

    private void Start()
    {
        if (playOnStart) Play();
    }

    /// <summary>เริ่มเดินไปตามเส้นทาง</summary>
    public void Play()
    {
        StopMoving();
        routine = StartCoroutine(RunRoutine(false));
    }

    /// <summary>เดินย้อนกลับไปจุดเริ่มต้น (เช่น ปิดประตู)</summary>
    public void PlayReverse()
    {
        StopMoving();
        routine = StartCoroutine(RunRoutine(true));
    }

    public void StopMoving()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
    }

    /// <summary>
    /// วาร์ปไปจุดปลายทางทันทีโดยไม่เล่นอนิเมชัน
    /// ใช้ตอนโหลดเซฟ — ประตูที่เปิดไว้แล้วต้องเปิดค้างอยู่ ไม่ใช่ค่อยๆ เปิดใหม่ให้ผู้เล่นเห็น
    /// </summary>
    public void JumpToEnd()
    {
        StopMoving();
        Vector3[] route = BuildRoute(false);
        if (route.Length > 0) transform.position = route[route.Length - 1];
    }

    /// <summary>กลับไปตำแหน่งตั้งต้นทันที</summary>
    public void ResetToStart()
    {
        StopMoving();
        transform.position = startPos;
        transform.rotation = startRot;
    }

    private Vector3[] BuildRoute(bool reverse)
    {
        List<Vector3> points = new List<Vector3>();
        if (waypoints != null)
        {
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] != null) points.Add(waypoints[i].position);
            }
        }

        if (!reverse) return points.ToArray();

        // ขากลับ: ไล่จุดย้อนจากท้ายมาหน้า (ข้ามจุดสุดท้ายที่ยืนอยู่แล้ว) แล้วปิดท้ายด้วยจุดตั้งต้น
        List<Vector3> back = new List<Vector3>();
        for (int i = points.Count - 2; i >= 0; i--) back.Add(points[i]);
        back.Add(startPos);
        return back.ToArray();
    }

    private IEnumerator RunRoutine(bool reverse)
    {
        while (true)
        {
            yield return MoveThrough(BuildRoute(reverse));
            onArrived?.Invoke();

            if (mode == MoveMode.Once) break;
            if (mode == MoveMode.PingPong) reverse = !reverse;
        }
        routine = null;
    }

    private IEnumerator MoveThrough(Vector3[] route)
    {
        for (int i = 0; i < route.Length; i++)
        {
            Vector3 from = transform.position;
            Vector3 to = route[i];
            float distance = Vector3.Distance(from, to);

            if (distance < 0.001f || speed <= 0f)
            {
                transform.position = to;
                continue;
            }

            if (faceMoveDirection)
            {
                Vector3 dir = to - from;
                dir.y = 0f; // ล็อกแกน Y กันตัวละครก้มหน้าคว่ำ
                if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir);
            }

            float duration = distance / speed;
            float t = 0f;

            while (t < 1f)
            {
                float delta = ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
                t += delta / duration;
                transform.position = Vector3.Lerp(from, to, easing.Evaluate(Mathf.Clamp01(t)));
                yield return null;
            }

            transform.position = to;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        Gizmos.color = Color.cyan;
        Vector3 prev = Application.isPlaying ? startPos : transform.position;

        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;
            Gizmos.DrawLine(prev, waypoints[i].position);
            Gizmos.DrawWireSphere(waypoints[i].position, 0.15f);
            prev = waypoints[i].position;
        }
    }
}
