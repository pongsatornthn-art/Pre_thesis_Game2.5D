using UnityEngine;


public class StalkerStateKill : IStalkerState
{
    private float timer;

    public void EnterState(StalkerAI ai)
    {
        if (ai.Agent.isOnNavMesh) ai.Agent.isStopped = true;
        ai.ShowDebugText("BOSS_KILL", Color.red);
        Debug.Log("<color=red>💀 JUMPSCARE! STALKER INSTANT KILL PLAYER!</color>");
        
        PlayerMovement p = ai.PlayerTransform.GetComponent<PlayerMovement>();
        if (p != null) p.TakeDamage(9999); // ดาเมจ 999 ฆ่าทันที
        
        timer = 0f;
    }

    public void UpdateState(StalkerAI ai) 
    { 
        // เผื่อตอนเทสผู้เล่นไม่มีเลือด หรือเป็นอมตะ ให้บอสกลับไปไล่ล่าต่อ ไม่ยืนเอ๋อค้าง
        timer += Time.deltaTime;
        if (timer > 2f)
        {
            ai.ChangeState(new StalkerStateChase());
        }
    }
    public void ExitState(StalkerAI ai) { }
}
