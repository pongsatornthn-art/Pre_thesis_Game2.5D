using UnityEngine;

public class StateChase : IMonsterState
{
    private Vector3 leashCenter;
    private float repathTimer;

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
        
        // 🌟 แก้บักมอนสเตอร์โง่: 
        // ถ้าคลาดสายตาแป๊บเดียว (เดินหลบหลังกล่อง) เกจจะไม่ลดฮวบ ให้มันวิ่งไล่กวดต่อ (จู๊คได้มันส์ขึ้น)
        // จะเลิกวิ่งไล่ก็ต่อเมื่อเกจความสนใจลดลงต่ำกว่า 50% แล้วเท่านั้น
        if (ai.Awareness.currentAwareness < 50f) 
        {
            ai.ChangeState(new StateSearch(ai.PlayerTransform.position));
            return;
        }

        // [แก้โดย Claude 2026-11-11 งานข้อ 1] เดิมสั่งหาทางใหม่ทุกเฟรม + ใช้ตำแหน่งผู้เล่นตรงๆ
        // ผู้เล่นยืนตรงที่ไม่ใช่พื้นเดินได้ (ชิดโต๊ะ/ผนัง/ลัง) → ทางคำนวณไม่ทันเสร็จสักเฟรม (pathPending ค้าง) → มอนยืนนิ่ง "เอ๋อ"
        // ตอนนี้: หาทางใหม่ทุก 0.2 วิ (หรือทันทีถ้ายังไม่มีทาง) ไปยัง "จุดพื้นเดินได้ที่ใกล้ผู้เล่นที่สุด"
        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f || (!ai.Agent.hasPath && !ai.Agent.pathPending))
        {
            repathTimer = 0.2f;
            ai.Agent.SetDestination(ChaseTarget.OnNavMesh(ai.PlayerTransform.position));
        }

        float dist = Vector3.Distance(ai.transform.position, ai.PlayerTransform.position);
        if (dist <= ai.attackDistance)
        {
            ai.ChangeState(new StateAttack());
        }
    }

    public void ExitState(PTSDMonsterAI ai) { }
}
