using UnityEngine;

public class StalkerStateChase : IStalkerState
{
    public void EnterState(StalkerAI ai)
    {
        ai.Agent.speed = ai.chaseSpeed;
        if (ai.Agent.isOnNavMesh) ai.Agent.isStopped = false;
        ai.ShowDebugText("BOSS_CHASE", Color.red);
        Debug.Log("🦇 Stalker วิ่งไล่ฆ่าผู้เล่น!!");
    }

    public void UpdateState(StalkerAI ai)
    {
        if (!ai.Agent.isOnNavMesh) return;

        ai.UpdateDetection();
        if (!ai.Awareness.IsActive) 
        {
            // ถ้าหลุดสายตา ไปค้นหาตรงจุดที่คลาดกัน
            ai.ChangeState(new StalkerStateAlert(ai.PlayerTransform.position));
            return;
        }

        ai.Agent.SetDestination(ai.PlayerTransform.position);

        if (Vector3.Distance(ai.transform.position, ai.PlayerTransform.position) <= ai.killDistance)
        {
            ai.ChangeState(new StalkerStateKill());
        }
    }

    public void ExitState(StalkerAI ai) { }
}
