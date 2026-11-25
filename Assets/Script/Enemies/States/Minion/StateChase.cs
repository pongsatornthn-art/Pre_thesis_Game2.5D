using UnityEngine;
using UnityEngine.AI;

public class StateChase : IMonsterState
{
    private Vector3 leashCenter;
    private float repathTimer;

    // [2026-11-21] กันติด — เดินไม่ขยับนานเกิน → ก้าวเลี่ยงไปข้างๆ สักพักแล้วค่อยหาทางใหม่
    private const float StuckSeconds = 1.2f;
    private const float SidestepSeconds = 0.6f;
    private float stuckTimer;
    private float sidestepUntil;

    public void EnterState(PTSDMonsterAI ai)
    {
        ai.Agent.speed = ai.chaseSpeed;
        if (ai.Agent.isOnNavMesh) ai.Agent.isStopped = false;
        leashCenter = ai.GetHomePosition();
        // เข้าโหมดไล่ตอนเห็นกันอยู่ = จำตำแหน่งตอนนี้ · มีกำแพงคั่น = ใช้จุดสุดท้ายที่จำไว้เดิม
        if (ai.ClearLineToPlayer()) ai.LastKnownPlayerPos = ai.PlayerTransform.position;
        stuckTimer = 0f;
        sidestepUntil = 0f;
        unreachableTimer = 0f;
        ai.ShowDebugText("MINION_CHASE", Color.red);
        Debug.Log("👁️‍🗨️ มอนสเตอร์เจอผู้เล่นแล้ว! วิ่งไล่สุดชีวิต!");
    }

    public void UpdateState(PTSDMonsterAI ai)
    {
        if (!ai.Agent.isOnNavMesh) return;

        float distFromHome = Vector3.Distance(ai.transform.position, leashCenter);
        if (distFromHome > ai.chaseLeashDistance)
        {
            Debug.Log("🛑 มอนสเตอร์วิ่งหลุดขอบเขต (Leash) เลิกตามแล้วเดินกลับบ้าน!");
            ai.Awareness.currentAwareness = 0f; // รีเซ็ตเกจ
            ai.ReturnToDefaultPatrol();
            return;
        }

        ai.UpdateDetection();

        // 🌟 แก้บักมอนสเตอร์โง่:
        // ถ้าคลาดสายตาแป๊บเดียว (เดินหลบหลังกล่อง) เกจจะไม่ลดฮวบ ให้มันวิ่งไล่กวดต่อ (จู๊คได้มันส์ขึ้น)
        // จะเลิกวิ่งไล่ก็ต่อเมื่อเกจความสนใจลดลงต่ำกว่า 50% แล้วเท่านั้น
        if (ai.Awareness.currentAwareness < 50f)
        {
            // [2026-11-21] ไปหาที่ "จุดสุดท้ายที่เห็น" ไม่ใช่ตำแหน่งจริงของผู้เล่น (เดิมรู้ทะลุกำแพง)
            ai.ChangeState(new StateSearch(ai.LastKnownPlayerPos));
            return;
        }

        bool clear = ai.ClearLineToPlayer();

        // [2026-11-21] กันติด: ก้าวเลี่ยงอยู่ = ปล่อยให้เดินไปจุดเลี่ยงก่อน
        if (Time.time < sidestepUntil) { CheckAttack(ai, clear); return; }
        TickStuck(ai);
        if (Time.time < sidestepUntil) return;

        // [แก้โดย Claude 2026-11-11 งานข้อ 1] หาทางใหม่ทุก 0.2 วิ ไปยัง "จุดพื้นเดินได้ที่ใกล้เป้าที่สุด"
        // [2026-11-21] เป้า = จุดสุดท้ายที่เห็นผู้เล่น (UpdateDetection อัปเดตให้ตอนไม่มีกำแพงคั่น) → หลบหลังกำแพง = มอนวิ่งไปจุดที่เห็นล่าสุด แล้วยืนงง
        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f || (!ai.Agent.hasPath && !ai.Agent.pathPending))
        {
            repathTimer = 0.2f;
            ai.Agent.SetDestination(ChaseTarget.OnNavMesh(ai.LastKnownPlayerPos));
        }

        // [2026-11-24 นา] ไปไม่ถึง (กล่อง/ของขวางทางตัดทางเดิน · ผู้เล่นอยู่อีกฝั่ง) — เดิมวิ่งไปสุดทางแล้วดันกล่องค้าง ดูโง่
        // ตอนนี้: ไปถึงสุดทางแล้วยืนจ้อง · ค้างนานเกิน = เลิกไล่ กลับไปเดินตรวจ
        if (Unreachable(ai.Agent, ai.LastKnownPlayerPos))
        {
            if (ai.Agent.remainingDistance <= 0.8f)
            {
                ai.Agent.isStopped = true;
                FacePlayer(ai);
                unreachableTimer += Time.deltaTime;
                if (unreachableTimer >= GiveUpSeconds)
                {
                    ai.Awareness.currentAwareness = 0f;
                    ai.ReturnToDefaultPatrol();
                }
                return;
            }
        }
        else unreachableTimer = 0f;
        if (ai.Agent.isStopped && Time.time >= sidestepUntil) ai.Agent.isStopped = false;

        CheckAttack(ai, clear);
    }

    private const float GiveUpSeconds = 2.5f;
    private float unreachableTimer;

    /// <summary>ทางที่หาได้ไปไม่ถึงเป้า (จบห่างเป้าเกิน 1.5 ม.)</summary>
    public static bool Unreachable(NavMeshAgent agent, Vector3 target)
    {
        if (agent.pathPending || agent.pathStatus != NavMeshPathStatus.PathPartial) return false;
        Vector3 d = agent.pathEndPosition - target; d.y = 0f;
        return d.sqrMagnitude > 1.5f * 1.5f;
    }

    private static void FacePlayer(PTSDMonsterAI ai)
    {
        if (ai.PlayerTransform == null) return;
        Vector3 dir = ai.PlayerTransform.position - ai.transform.position; dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f) ai.transform.rotation = Quaternion.Slerp(ai.transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 6f);
    }

    private void CheckAttack(PTSDMonsterAI ai, bool clear)
    {
        // [2026-11-21] ต้องไม่มีกำแพงคั่นถึงจะตี/ตะครุบ (เดิมผู้เล่นอยู่อีกห้องชิดผนัง มอนพุ่งใส่กำแพง)
        if (!clear) return;
        float dist = Vector3.Distance(ai.transform.position, ai.PlayerTransform.position);
        if (dist <= ai.attackDistance)
        {
            // [2026-11-21] Grab & Scream: ผู้เล่นสตามิน่าต่ำ (≤ 20%) → ตะครุบแทนตีปกติ
            if (ai.CanGrab()) ai.ChangeState(new StateGrabScream());
            else ai.ChangeState(new StateAttack());
        }
    }

    private void TickStuck(PTSDMonsterAI ai)
    {
        NavMeshAgent agent = ai.Agent;
        bool wantsToMove = agent.hasPath && !agent.pathPending && agent.remainingDistance > agent.stoppingDistance + 0.3f;
        if (!wantsToMove || agent.velocity.sqrMagnitude > 0.05f) { stuckTimer = 0f; return; }

        stuckTimer += Time.deltaTime;
        if (stuckTimer < StuckSeconds) return;
        stuckTimer = 0f;

        // หาจุดเดินได้ข้างๆ ตัว (สุ่มไม่เกิน 6 ครั้ง) แล้วเดินไปก่อนสักพัก
        for (int i = 0; i < 6; i++)
        {
            Vector2 c = Random.insideUnitCircle.normalized * Random.Range(1f, 2f);
            Vector3 p = ai.transform.position + new Vector3(c.x, 0f, c.y);
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 1f, NavMesh.AllAreas) && !ai.WallBetween(ai.transform.position + Vector3.up, hit.position + Vector3.up))
            {
                agent.ResetPath();
                agent.SetDestination(hit.position);
                sidestepUntil = Time.time + SidestepSeconds;
                repathTimer = 0f;
                return;
            }
        }
        agent.ResetPath();   // หาจุดเลี่ยงไม่ได้ = ล้างทางแล้วคำนวณใหม่รอบหน้า
        repathTimer = 0f;
    }

    public void ExitState(PTSDMonsterAI ai) { }
}
