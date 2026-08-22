using UnityEngine;

public class StateSearch : IMonsterState
{
    private Vector3 lastKnownPos;
    private float searchTimer;

    public StateSearch(Vector3 pos) { lastKnownPos = pos; }

    public void EnterState(PTSDMonsterAI ai)
    {
        ai.Agent.speed = ai.patrolSpeed; 
        if (ai.Agent.isOnNavMesh) 
        {
            ai.Agent.isStopped = false;
            ai.Agent.SetDestination(lastKnownPos);
        }
        searchTimer = 0f;
        ai.ShowDebugText("MINION_SEARCH", new Color(1f, 0.5f, 0f)); // ส้ม
        Debug.Log("🔍 มอนสเตอร์กำลังเดินไปค้นหาที่จุดล่าสุด...");
    }

    public void UpdateState(PTSDMonsterAI ai)
    {
        if (!ai.Agent.isOnNavMesh) return;

        ai.UpdateDetection();
        if (ai.Awareness.IsActive)
        {
            ai.ChangeState(new StateChase()); 
            return;
        }
        else if (ai.Awareness.IsSuspicious) // เพิ่งหันมาเห็นแว้บๆ ตอนกำลังหา
        {
            ai.ChangeState(new StateSuspicious());
            return;
        }

        if (!ai.Agent.pathPending && ai.Agent.remainingDistance <= ai.Agent.stoppingDistance)
        {
            searchTimer += Time.deltaTime; 
            if (searchTimer > 3f) 
            {
                Debug.Log("🤷‍♂️ หาไม่เจอจริงๆ เลิกหาละ กลับไปเดินตามเดิม");
                ai.Awareness.currentAwareness = 0f;
                ai.ReturnToDefaultPatrol();
            }
        }
    }
    public void ExitState(PTSDMonsterAI ai) { }
}
