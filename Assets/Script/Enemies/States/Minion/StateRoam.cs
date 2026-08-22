using UnityEngine;

public class StateRoam : IMonsterState
{
    private float timer;
    private float stuckTimer;
    
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
            if (stuckTimer > 2f) { ai.Agent.ResetPath(); stuckTimer = 0f; }
        }
        else stuckTimer = 0f;

        if (!ai.Agent.pathPending && (ai.Agent.remainingDistance <= ai.Agent.stoppingDistance || !ai.Agent.hasPath))
        {
            timer += Time.deltaTime;
            if (timer >= ai.assignedZone.waitTimeAtPoint)
            {
                Vector3 randomPoint = ai.assignedZone.GetRandomPoint();
                if (UnityEngine.AI.NavMesh.SamplePosition(randomPoint, out UnityEngine.AI.NavMeshHit hit, 10f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    ai.Agent.SetDestination(hit.position);
                }
                timer = 0;
            }
        }
    }
    public void ExitState(PTSDMonsterAI ai) { }
}
