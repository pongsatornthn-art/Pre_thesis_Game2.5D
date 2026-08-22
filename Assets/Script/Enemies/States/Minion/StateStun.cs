using UnityEngine;

public class StateStun : IMonsterState
{
    private float stunDuration;
    private float timer;

    public StateStun(float duration) { stunDuration = duration; }

    public void EnterState(PTSDMonsterAI ai)
    {
        if (ai.Agent.isOnNavMesh) 
        {
            ai.Agent.isStopped = true;
            ai.Agent.ResetPath();
        }
        timer = 0f;
        ai.ShowDebugText("MINION_STUN", Color.cyan);
    }

    public void UpdateState(PTSDMonsterAI ai)
    {
        timer += Time.deltaTime;
        if (timer >= stunDuration)
        {
            Debug.Log("💢 หายสตันปุ๊บ โกรธเกจ 100% ทันที!");
            ai.Awareness.ForceMaxAwareness();
            ai.ChangeState(new StateChase());
        }
    }

    public void ExitState(PTSDMonsterAI ai) 
    { 
        if (ai.Agent.isOnNavMesh) ai.Agent.isStopped = false; 
    }
}
