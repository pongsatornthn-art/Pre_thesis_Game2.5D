using UnityEngine;

public class StateChase : IMonsterState
{
    private Vector3 leashCenter;

    public void EnterState(PTSDMonsterAI ai)
    {
        ai.Agent.speed = ai.chaseSpeed;
        if (ai.Agent.isOnNavMesh) ai.Agent.isStopped = false;
        leashCenter = ai.GetHomePosition();
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
        
        // ถ้าคลาดสายตา (เกจเริ่มลดจาก 100) เปลี่ยนเป็นเดินค้นหา
        if (!ai.Awareness.IsActive) 
        {
            ai.ChangeState(new StateSearch(ai.PlayerTransform.position));
            return;
        }

        ai.Agent.SetDestination(ai.PlayerTransform.position);

        float dist = Vector3.Distance(ai.transform.position, ai.PlayerTransform.position);
        if (dist <= ai.attackDistance)
        {
            ai.ChangeState(new StateAttack());
        }
    }

    public void ExitState(PTSDMonsterAI ai) { }
}
