using UnityEngine;

public class StateAttack : IMonsterState
{
    private float timer;

    public void EnterState(PTSDMonsterAI ai)
    {
        if (ai.Agent.isOnNavMesh) 
        {
            ai.Agent.isStopped = true;
            ai.Agent.ResetPath(); // ล็อกขาให้นิ่งตอนตี
        }
        timer = 0f; 

        // 🌟 สุ่มกาชาการโจมตีตาม GDD (70% ตีปกติ / 30% จับ Root และเรียก Stalker)
        int rand = Random.Range(1, 101);
        if (rand <= 30)
        {
            ai.ShowDebugText("MINION_CALL_BOSS", Color.magenta);
            ai.ShoutForStalker(); // โอกาส 30% ตะโกนเรียกบอส
        }
        else
        {
            ai.ShowDebugText("MINION_ATTACK", Color.red);
            Debug.Log($"<color=orange>{ai.gameObject.name} โจมตีปกติ {ai.attackDamage} ดาเมจ!</color>");
            PlayerMovement p = ai.PlayerTransform.GetComponent<PlayerMovement>();
            if (p != null) p.TakeDamage(ai.attackDamage);
        }
    }

    public void UpdateState(PTSDMonsterAI ai)
    {
        timer += Time.deltaTime;
        if (timer >= ai.attackCooldown)
        {
            ai.ChangeState(new StateChase()); // ตีเสร็จกลับไปวิ่งไล่ใหม่
        }
    }

    public void ExitState(PTSDMonsterAI ai) { }
}
