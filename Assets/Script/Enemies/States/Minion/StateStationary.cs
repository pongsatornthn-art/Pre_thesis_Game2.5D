using UnityEngine;

public class StateStationary : IMonsterState
{
    private float timer;
    private float stuckTimer;
    private float waitTime = 3f;
    private float moveRadius = 2f;
    private Vector3 startPos;

    public void EnterState(PTSDMonsterAI ai)
    {
        startPos = ai.transform.position;
        timer = waitTime;
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

        if (ai.Agent.hasPath && ai.Agent.velocity.sqrMagnitude < 0.1f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer > 2f) { ai.Agent.ResetPath(); stuckTimer = 0f; }
        }
        else stuckTimer = 0f;

        if (!ai.Agent.pathPending && (ai.Agent.remainingDistance <= ai.Agent.stoppingDistance || !ai.Agent.hasPath))
        {
            timer += Time.deltaTime;
            if (timer >= waitTime)
            {
                Vector3 randomDir = Random.insideUnitSphere * moveRadius;
                randomDir += startPos;
                if (UnityEngine.AI.NavMesh.SamplePosition(randomDir, out UnityEngine.AI.NavMeshHit hit, moveRadius, UnityEngine.AI.NavMesh.AllAreas))
                {
                    ai.Agent.SetDestination(hit.position);
                }
                timer = 0;
            }
        }
    }
    public void ExitState(PTSDMonsterAI ai) { }
}
