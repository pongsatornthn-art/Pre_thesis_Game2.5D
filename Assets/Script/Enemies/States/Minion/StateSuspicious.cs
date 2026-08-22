using UnityEngine;

public class StateSuspicious : IMonsterState
{
    public void EnterState(PTSDMonsterAI ai)
    {
        if (ai.Agent.isOnNavMesh) 
        {
            ai.Agent.isStopped = true; // หยุดเดิน
            ai.Agent.ResetPath(); // ลบเส้นทางทิ้งเพื่อยืนนิ่งๆ
        }
        ai.ShowDebugText("MINION_SUSPICIOUS", Color.yellow);
        Debug.Log("🤔 มอนสเตอร์สงสัยบางอย่าง... เกจกำลังขึ้น!");
    }

    public void UpdateState(PTSDMonsterAI ai)
    {
        ai.UpdateDetection();

        // หันหน้ามองผู้เล่นตลอดเวลาที่สงสัย
        if (ai.PlayerTransform != null)
        {
            Vector3 lookPos = ai.PlayerTransform.position;
            lookPos.y = ai.transform.position.y;
            ai.transform.LookAt(lookPos);
        }

        // ถ้าเกจเต็ม 100
        if (ai.Awareness.IsActive)
        {
            ai.ChangeState(new StateChase());
        }
        // ถ้าเกจลดเหลือ 0 (คลาดสายตา)
        else if (!ai.Awareness.IsSuspicious)
        {
            Debug.Log("💨 สงสัยตาฝาด กลับไปเดินต่อ");
            ai.ReturnToDefaultPatrol();
        }
    }
    public void ExitState(PTSDMonsterAI ai) { }
}
