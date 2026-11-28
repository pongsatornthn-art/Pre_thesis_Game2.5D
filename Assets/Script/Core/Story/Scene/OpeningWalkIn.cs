using System.Collections;
using UnityEngine;

/// <summary>
/// [Claude 2026-11-28] ฉากเปิดชั่วคราว (ยังไม่มีฉากเปิดจริง — นาสั่ง):
///   เริ่มเกมใหม่ → ตัวละครโผล่ที่ขอบล่างของโถง → เดินเองขึ้นมาถึงจุดเกิดเดิม → ปักธง → การ์ดสอน "การควบคุมพื้นฐาน" เด้ง
///   (ตัวเฝ้า When_basics ใน [TUTORIAL] รอธง Done Flag นี้)
/// โหลดเซฟ / ตายแล้วย้อน / เคยเดินแล้ว (มีธงในเซฟ) = ข้าม ไม่เดินซ้ำ
///
/// วางในซีน: GameObject เปล่า + สคริปต์นี้ · ลูก StartPoint = จุดเริ่มเดิน · ปลายทาง = ตำแหน่งตัวละครในซีนตอนเริ่ม (ย้ายตัวละครในซีน = ย้ายปลายทาง)
/// ระหว่างเดิน: ผู้เล่นโดนล็อก (isStunned) · ท่าเดินสั่งผ่าน PlayerAnimationController.ScriptedMove
/// </summary>
public class OpeningWalkIn : MonoBehaviour
{
    [Tooltip("จุดเริ่มเดิน (ขอบล่างของโถง)")]
    [SerializeField] private Transform startPoint;

    [Tooltip("เดินเสร็จแล้วปักธงนี้ (การ์ดสอนพื้นฐานรอธงนี้) — มีธงแล้ว = ไม่เดินอีก")]
    [SerializeField] private StoryFlagId doneFlag;

    [Tooltip("ความเร็วเดิน (ม./วินาที) — เดินปกติในเกม ≈ 3")]
    [SerializeField, Min(0.5f)] private float walkSpeed = 2.2f;

    [Tooltip("ยืนนิ่งก่อนออกเดิน (วินาที) — ให้กล้อง/จอเข้าที่ก่อน")]
    [SerializeField, Min(0f)] private float delayBefore = 0.6f;

    [Tooltip("ยืนนิ่งหลังเดินถึงก่อนปล่อยให้บังคับ (วินาที)")]
    [SerializeField, Min(0f)] private float delayAfter = 0.3f;

    private IEnumerator Start()
    {
        // โหลดเซฟ = ตำแหน่งมาจากเซฟ ห้ามยุ่ง
        if (SaveRestoreScope.HasPendingRestore || SaveRestoreScope.IsBusy) yield break;
        yield return null;   // ให้ระบบอื่น Awake/Start ครบก่อน (StoryFlags, ผู้เล่น)

        IStoryFlags flags = ServiceLocator.GetOptional<IStoryFlags>();
        if (doneFlag != null && flags != null && flags.Has(doneFlag)) yield break;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        PlayerMovement movement = player != null ? player.GetComponent<PlayerMovement>() : null;
        if (player == null || movement == null || startPoint == null)
        {
            Debug.LogWarning("[OpeningWalkIn] ไม่เจอผู้เล่น / ยังไม่ได้ใส่ Start Point — ข้ามฉากเดิน", this);
            MarkDone(flags);
            yield break;
        }

        Rigidbody rb = player.GetComponent<Rigidbody>();
        Vector3 target = player.transform.position;
        Vector3 start = startPoint.position;
        start.y = target.y;

        Teleport(player.transform, rb, start);
        movement.isStunned = true;
        PlayerAnimationController.ScriptedMove = null;

        yield return new WaitForSeconds(delayBefore);

        Vector3 flat = target - start; flat.y = 0f;
        Vector2 dir = new Vector2(flat.x, flat.z).normalized;
        PlayerAnimationController.ScriptedMove = dir;

        // เดินตามฟิสิกส์ (MovePosition) — กล้องตามนุ่ม ๆ ชนอะไรก็ไม่ติด
        while (player != null && (player.transform.position - target).sqrMagnitude > 0.0025f)
        {
            yield return new WaitForFixedUpdate();
            Vector3 next = Vector3.MoveTowards(player.transform.position, target, walkSpeed * Time.fixedDeltaTime);
            if (rb != null) rb.MovePosition(next); else player.transform.position = next;
        }

        PlayerAnimationController.ScriptedMove = null;
        if (player != null) Teleport(player.transform, rb, target);

        yield return new WaitForSeconds(delayAfter);
        if (movement != null) movement.isStunned = false;
        MarkDone(flags);
    }

    private void OnDisable() => PlayerAnimationController.ScriptedMove = null;

    private void MarkDone(IStoryFlags flags)
    {
        if (doneFlag != null && flags != null) flags.Set(doneFlag);
    }

    private static void Teleport(Transform t, Rigidbody rb, Vector3 pos)
    {
        if (rb != null) { rb.position = pos; rb.linearVelocity = Vector3.zero; }
        t.position = pos;
        Physics.SyncTransforms();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (startPoint == null) return;
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(startPoint.position, 0.35f);
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) Gizmos.DrawLine(startPoint.position, p.transform.position);
    }
#endif
}
