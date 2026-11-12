using UnityEngine;

public class StateRoam : IMonsterState
{
    private float timer;
    private float stuckTimer;
    private readonly UnityEngine.AI.NavMeshPath roamPath = new UnityEngine.AI.NavMeshPath();

    public void EnterState(PTSDMonsterAI ai)
    {
        timer = (ai.assignedZone != null) ? ai.assignedZone.waitTimeAtPoint : 2f;
        stuckTimer = 0f;
        ai.Agent.speed = ai.patrolSpeed;
        if (ai.Agent.isOnNavMesh) ai.Agent.isStopped = false;
        ai.ShowDebugText("", Color.white); // ซ่อนข้อความตอนปกติ
    }

    public void UpdateState(PTSDMonsterAI ai)
    {
        if (!ai.Agent.isOnNavMesh) return;

        ai.UpdateDetection(); // 🌟 อัปเดตการสะสมเกจ

        if (ai.Awareness.IsActive) { ai.ChangeState(new StateChase()); return; }
        else if (ai.Awareness.IsSuspicious) { ai.ChangeState(new StateSuspicious()); return; }

        if (ai.assignedZone == null) return;

        if (ai.Agent.hasPath && ai.Agent.velocity.sqrMagnitude < 0.1f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer > 1f) 
            { 
                Debug.Log("⚠️ มอนสเตอร์เดินติด! สั่งเปลี่ยนจุดหมายใหม่ทันที");
                ai.Agent.ResetPath(); 
                stuckTimer = 0f; 
                timer = ai.assignedZone.waitTimeAtPoint; // บังคับให้หาจุดใหม่ทันที ไม่ต้องรอ
            }
        }
        else stuckTimer = 0f;

        // เผื่อระยะคลาดเคลื่อน (Tolerance) ให้มัน ถ้าจุดหมายอยู่ชิดกำแพงเกินไปจะได้ไม่ต้องพยายามเดินเบียดให้ถึง 0 เป๊ะๆ
        float arriveTolerance = Mathf.Max(ai.Agent.stoppingDistance, 0.8f);

        if (!ai.Agent.pathPending && (ai.Agent.remainingDistance <= arriveTolerance || !ai.Agent.hasPath))
        {
            timer += Time.deltaTime;
            if (timer >= ai.assignedZone.waitTimeAtPoint)
            {
                // [Claude 2026-11-12] เดิมสุ่มความสูงด้วย + หาพื้นเดินได้ไกลถึง 10 ม. → บางทีได้จุดบนโต๊ะ (NavMesh มีเกาะบนโต๊ะ) เดินไปไม่ถึง ยืนเอ๋อ
                // ตอนนี้: สุ่มที่ระดับพื้นเดียวกับมอน · หาพื้นใกล้ๆ · ต้องมีทางเดินไปถึงจริง (ไม่ได้ = ลองใหม่เฟรมหน้า)
                Vector3 randomPoint = ai.assignedZone.GetRandomPoint();
                randomPoint.y = ai.transform.position.y;
                if (UnityEngine.AI.NavMesh.SamplePosition(randomPoint, out UnityEngine.AI.NavMeshHit hit, 1.5f, UnityEngine.AI.NavMesh.AllAreas)
                    && Mathf.Abs(hit.position.y - ai.transform.position.y) < 0.5f
                    && ai.Agent.CalculatePath(hit.position, roamPath)
                    && roamPath.status == UnityEngine.AI.NavMeshPathStatus.PathComplete)
                {
                    ai.Agent.SetPath(roamPath);
                    timer = 0;
                }
            }
        }
    }
    public void ExitState(PTSDMonsterAI ai) { }
}
