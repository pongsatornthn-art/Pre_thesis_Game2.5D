using UnityEngine;

public class StalkerStateStun : IStalkerState
{
    private float duration;
    private float timer;

    public StalkerStateStun(float dur) { duration = dur; }

    public void EnterState(StalkerAI ai)
    {
        if (ai.Agent.isOnNavMesh) ai.Agent.isStopped = true;
        timer = 0f;
        ai.ShowDebugText("BOSS_STUN", Color.cyan);
    }

    public void UpdateState(StalkerAI ai)
    {
        timer += Time.deltaTime;
        if (timer >= duration)
        {
            ai.ChangeState(new StalkerStateChase()); // โกรธ วิ่งตามต่อ
        }
    }
    public void ExitState(StalkerAI ai) { if (ai.Agent.isOnNavMesh) ai.Agent.isStopped = false; }
}
