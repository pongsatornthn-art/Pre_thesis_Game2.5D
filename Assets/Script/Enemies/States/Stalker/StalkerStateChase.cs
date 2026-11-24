using UnityEngine;

public class StalkerStateChase : IStalkerState
{
    private float roarTimer = 0f;
    private bool hasRoared = false;
    private float roarDuration = 3f;
    private float stuckTimer = 0f;
    private float pathUpdateTimer = 0f;

    private float lostTimer = 0f;   // [2026-11-24] หลุดสายตามานานเท่าไหร่

    public void EnterState(StalkerAI ai)
    {
        roarTimer = 0f;
        hasRoared = false;
        lostTimer = 0f;
        roarDuration = 0f;   // [2026-11-24 นา] เดิมยืนคำรามอีก 3 วิ (ต่อจากกรีดร้องอีก 3 วิ) — ตอนนี้กรีดร้องสั้นๆ แล้ววิ่งเลย (เสียงคำรามยังเล่น)
        ai.lastContactTime = Time.time;
        if (ai.PlayerTransform != null && ai.ClearLineTo(ai.PlayerTransform.position)) ai.LastKnownPlayerPos = ai.PlayerTransform.position;

        // 🌟 ไอเดียที่ 4: บอสโผล่มาต้องยืนคำรามขู่ก่อน 3 วินาที ให้ผู้เล่นตั้งตัวทัน
        if (ai.Agent.isOnNavMesh) 
        {
            ai.Agent.ResetPath();
            ai.Agent.isStopped = true;
            ai.Agent.velocity = Vector3.zero;
        }

        ai.ShowDebugText("BOSS_ROAR!", Color.magenta);
        // [Claude 2026-11-12] เสียงคำรามตอนเริ่มไล่ (ไม่มีไฟล์ = เงียบ ไม่พัง)
        if (ai.chaseRoarClip != null) ServiceLocator.GetOptional<IAudioService>()?.PlaySFX(ai.chaseRoarClip, ai.transform.position, ai.screamVolume, false);
        Debug.Log($"<color=magenta>🦇 Stalker เห็นผู้เล่นแล้ว! ยืนคำรามขู่เป็นเวลา {roarDuration} วินาที!</color>");
    }

    public void UpdateState(StalkerAI ai)
    {
        if (!ai.Agent.isOnNavMesh) return;

        // 🌟 ช่วงคำรามขู่ (Roar Phase)
        if (!hasRoared)
        {
            roarTimer += Time.deltaTime;
            
            // ให้บอสหันหน้าตามผู้เล่นระหว่างคำรามเพื่อความหลอน
            if (ai.PlayerTransform != null)
            {
                Vector3 lookPos = ai.PlayerTransform.position;
                lookPos.y = ai.transform.position.y;
                ai.transform.LookAt(lookPos);
            }

            if (roarTimer >= roarDuration)
            {
                hasRoared = true;
                ai.Agent.isStopped = false;
                ai.Agent.speed = ai.chaseSpeed;
                ai.ShowDebugText("BOSS_CHASE", Color.red);
                if (ai.chaseRunLoop != null && ai.chaseRunLoop.clip != null && !ai.chaseRunLoop.isPlaying) ai.chaseRunLoop.Play();   // เสียงวิ่งไล่
                Debug.Log("<color=red>🦇 Stalker คำรามเสร็จแล้ว เริ่มวิ่งไล่ล่า!!</color>");
            }
            return; // ยังไม่ให้ขยับหรือเช็คอะไรทั้งสิ้นจนกว่าจะคำรามจบ
        }

        ai.UpdateDetection();
        // [2026-11-24 นา] เดิม: คลาดสายตาเฟรมเดียว (เกจหลุด 100) = เลิกไล่ทันที → ดูโง่ ผู้เล่นเลี้ยวมุมทีเดียวหลุด
        // ตอนนี้: หลุดสายตาแล้วยังวิ่งไปจุดที่เห็นล่าสุดต่อ (Lose Sight Grace) — ถึงจุดนั้นแล้วไม่เจอ/หมดเวลา ค่อยค้นหา
        if (ai.Awareness.IsActive) { lostTimer = 0f; ai.lastContactTime = Time.time; }
        else
        {
            lostTimer += Time.deltaTime;
            bool reachedLastSeen = !ai.Agent.pathPending && ai.Agent.remainingDistance <= 1f;
            if (lostTimer >= ai.loseSightGraceSeconds || (reachedLastSeen && lostTimer > 1f))
            {
                // [2026-11-21] ไปค้นที่จุดสุดท้ายที่เห็น (เดิมใช้ตำแหน่งจริงของผู้เล่น = รู้ทะลุกำแพง)
                ai.ChangeState(new StalkerStateAlert(ai.LastKnownPlayerPos));
                return;
            }
        }

        // อัปเดตปลายทางทุกๆ 0.2 วินาที แทนการอัปเดตทุกเฟรม (ป้องกัน NavMeshAgent เอ๋อเวลาวิ่งเร็วจัดและเลี้ยวไม่พ้นมุม)
        // [2026-11-21] เป้า = จุดสุดท้ายที่เห็น/ได้ยินผู้เล่น (UpdateDetection อัปเดตให้) ไม่ใช่ตำแหน่งจริงทะลุกำแพง
        pathUpdateTimer += Time.deltaTime;
        if (pathUpdateTimer >= 0.2f)
        {
            ai.Agent.SetDestination(ChaseTarget.OnNavMesh(ai.LastKnownPlayerPos)); // [Claude 2026-11-11] ปลายทางบนพื้นเดินได้เสมอ
            pathUpdateTimer = 0f;
        }

        // [2026-11-24 นา] ไปไม่ถึง (ของขวางทาง/ผู้เล่นอยู่อีกฝั่ง) — ถึงสุดทางแล้วเลิกดัน → ค้นหารอบๆ ตรงนั้นแทน
        if (StateChase.Unreachable(ai.Agent, ai.LastKnownPlayerPos) && ai.Agent.remainingDistance <= 0.8f)
        {
            ai.ChangeState(new StalkerStateAlert(ai.transform.position, 60f));
            return;
        }

        // Anti-Stuck: ถ้ามีเป้าหมายแต่ความเร็วเกือบ 0 (ติดกล่องที่ไม่ได้ Bake NavMesh)
        bool wantsToMove = ai.Agent.hasPath && !ai.Agent.pathPending && ai.Agent.remainingDistance > ai.Agent.stoppingDistance + 0.3f;
        if (wantsToMove && ai.Agent.velocity.sqrMagnitude < 0.1f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer > 1.5f)
            {
                // [2026-11-21] เดิมวาร์ปเข้าหาผู้เล่น 30% → บางทีวาร์ปข้ามกำแพงไปอีกห้อง ดูทะลุ
                // ตอนนี้: ขยับไปจุดเดินได้ข้างๆ ตัว ที่ไม่มีกำแพงคั่น (สุ่ม 6 ครั้ง) · หาไม่ได้ = ล้างทางคำนวณใหม่
                Debug.LogWarning("🦇 Stalker เดินติด → ขยับเลี่ยงข้างๆ");
                bool moved = false;
                for (int i = 0; i < 6 && !moved; i++)
                {
                    Vector2 c = Random.insideUnitCircle.normalized * Random.Range(0.8f, 1.6f);
                    Vector3 p = ai.transform.position + new Vector3(c.x, 0f, c.y);
                    if (UnityEngine.AI.NavMesh.SamplePosition(p, out UnityEngine.AI.NavMeshHit hit, 1f, UnityEngine.AI.NavMesh.AllAreas) && ai.ClearLineTo(hit.position))
                    {
                        ai.Agent.Warp(hit.position);
                        moved = true;
                    }
                }
                ai.Agent.ResetPath();
                pathUpdateTimer = 0.2f;   // หาทางใหม่เฟรมหน้า
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
        }

        // [2026-11-21] จับได้เฉพาะไม่มีกำแพงคั่น (เดิมยืนคนละฝั่งผนังก็จับได้)
        if (Vector3.Distance(ai.transform.position, ai.PlayerTransform.position) <= ai.killDistance && ai.ClearLineTo(ai.PlayerTransform.position))
        {
            ai.CatchPlayer(); // [แก้โดย Claude 2026-11-11] จุดจับเดียว — มีหน้ามือผีของปอใช้อันนั้น ไม่มีค่อยใช้กด E เดิม
        }
    }

    public void ExitState(StalkerAI ai)
    {
        if (ai.chaseRunLoop != null && ai.chaseRunLoop.isPlaying) ai.chaseRunLoop.Stop();   // [Claude 2026-11-12] เลิกไล่ = หยุดเสียงวิ่ง
    }
}
