using UnityEngine;

public class StateWaypoint : IMonsterState
{
    private float timer;
    private float stuckTimer;
    private int currentWaypointIndex = 0;

    public void EnterState(PTSDMonsterAI ai)
    {
        timer = (ai.assignedRoute != null) ? ai.assignedRoute.waitTimeAtPoint : 2f;
        stuckTimer = 0f;
        ai.Agent.speed = ai.patrolSpeed;
        if (ai.Agent.isOnNavMesh) ai.Agent.isStopped = false;
        ai.ShowDebugText("", Color.white); // ซ่อนข้อความตอนปกติ

        if (ai.assignedRoute != null && ai.assignedRoute.waypoints.Length > 0)
        {
            float minDist = float.MaxValue;
            for (int i = 0; i < ai.assignedRoute.waypoints.Length; i++)
            {
                float d = Vector3.Distance(ai.transform.position, ai.assignedRoute.waypoints[i].position);
                if (d < minDist)
                {
                    minDist = d;
                    currentWaypointIndex = i;
                }
            }
            if (ai.Agent.isOnNavMesh) ai.Agent.SetDestination(ai.assignedRoute.waypoints[currentWaypointIndex].position);
        }
    }

    public void UpdateState(PTSDMonsterAI ai)
    {
        if (!ai.Agent.isOnNavMesh) return;

        ai.UpdateDetection(); // 🌟 อัปเดตการสะสมเกจ

        if (ai.Awareness.IsActive) { ai.ChangeState(new StateChase()); return; }
        else if (ai.Awareness.IsSuspicious) { ai.ChangeState(new StateSuspicious()); return; }

        if (ai.assignedRoute == null || ai.assignedRoute.waypoints.Length == 0) return;

        if (ai.Agent.hasPath && ai.Agent.velocity.sqrMagnitude < 0.1f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer > 2f) { ai.Agent.ResetPath(); stuckTimer = 0f; }
        }
        else stuckTimer = 0f;

        if (!ai.Agent.pathPending && (ai.Agent.remainingDistance <= ai.Agent.stoppingDistance || !ai.Agent.hasPath))
        {
            timer += Time.deltaTime;
            if (timer >= ai.assignedRoute.waitTimeAtPoint)
            {
                currentWaypointIndex = (currentWaypointIndex + 1) % ai.assignedRoute.waypoints.Length;
                ai.Agent.SetDestination(ai.assignedRoute.waypoints[currentWaypointIndex].position);
                timer = 0;
            }
        }
    }
    public void ExitState(PTSDMonsterAI ai) { }
}
