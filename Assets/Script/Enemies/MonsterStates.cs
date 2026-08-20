using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Interface หลักสำหรับทุกสถานะของมอนสเตอร์ (State Pattern)
/// </summary>
public interface IMonsterState
{
    void EnterState(PTSDMonsterAI ai);
    void UpdateState(PTSDMonsterAI ai);
    void ExitState(PTSDMonsterAI ai);
}

// ==========================================
// 1. สถานะยืนเฝ้าที่ (Stationary)
// ==========================================
public class StateStationary : IMonsterState
{
    private float timer;
    private float waitTime = 3f;
    private float moveRadius = 2f;
    private Vector3 startPos;

    public void EnterState(PTSDMonsterAI ai)
    {
        startPos = ai.transform.position;
        timer = waitTime;
        ai.Agent.speed = ai.patrolSpeed;
        if (ai.Agent.isOnNavMesh) ai.Agent.isStopped = false;
    }

    public void UpdateState(PTSDMonsterAI ai)
    {
        if (!ai.Agent.isOnNavMesh) return; // ป้องกัน Error ถ้าตัวลอยอยู่

        if (ai.CheckDetection())
        {
            ai.ChangeState(new StateChase());
            return;
        }

        if (!ai.Agent.pathPending && ai.Agent.remainingDistance <= ai.Agent.stoppingDistance)
        {
            timer += Time.deltaTime;
            if (timer >= waitTime)
            {
                // ขยับตัวนิดหน่อยรอบๆ จุดเกิด
                Vector3 randomDir = Random.insideUnitSphere * moveRadius;
                randomDir += startPos;
                if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, moveRadius, 1))
                {
                    ai.Agent.SetDestination(hit.position);
                }
                timer = 0;
            }
        }
    }

    public void ExitState(PTSDMonsterAI ai) { }
}

// ==========================================
// 2. สถานะเดินสุ่ม (Roam Zone)
// ==========================================
public class StateRoam : IMonsterState
{
    private float timer;
    
    public void EnterState(PTSDMonsterAI ai)
    {
        timer = (ai.assignedZone != null) ? ai.assignedZone.waitTimeAtPoint : 2f;
        ai.Agent.speed = ai.patrolSpeed;
        if (ai.Agent.isOnNavMesh) ai.Agent.isStopped = false;
    }

    public void UpdateState(PTSDMonsterAI ai)
    {
        if (!ai.Agent.isOnNavMesh) return; // ป้องกัน Error

        if (ai.CheckDetection())
        {
            ai.ChangeState(new StateChase());
            return;
        }

        if (ai.assignedZone == null) return; // ถ้าไม่มีกล่องข้อมูลให้อยู่นิ่งๆ

        if (!ai.Agent.pathPending && ai.Agent.remainingDistance <= ai.Agent.stoppingDistance)
        {
            timer += Time.deltaTime;
            if (timer >= ai.assignedZone.waitTimeAtPoint)
            {
                // เดินสุ่มในระยะ Zone
                Vector3 randomDir = Random.insideUnitSphere * ai.assignedZone.roamRadius;
                randomDir += ai.assignedZone.transform.position; // อิงจากจุดศูนย์กลาง Zone
                
                if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, ai.assignedZone.roamRadius, 1))
                {
                    ai.Agent.SetDestination(hit.position);
                }
                timer = 0;
            }
        }
    }

    public void ExitState(PTSDMonsterAI ai) { }
}

// ==========================================
// 3. สถานะเดินตามจุด (Waypoints)
// ==========================================
public class StateWaypoint : IMonsterState
{
    private float timer;
    private int currentWaypointIndex = 0;

    public void EnterState(PTSDMonsterAI ai)
    {
        timer = (ai.assignedRoute != null) ? ai.assignedRoute.waitTimeAtPoint : 2f;
        ai.Agent.speed = ai.patrolSpeed;
        if (ai.Agent.isOnNavMesh) ai.Agent.isStopped = false;
    }

    public void UpdateState(PTSDMonsterAI ai)
    {
        if (!ai.Agent.isOnNavMesh) return; // ป้องกัน Error

        if (ai.CheckDetection())
        {
            ai.ChangeState(new StateChase());
            return;
        }

        if (ai.assignedRoute == null || ai.assignedRoute.waypoints.Length == 0) return;

        if (!ai.Agent.pathPending && ai.Agent.remainingDistance <= ai.Agent.stoppingDistance)
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

// ==========================================
// 4. สถานะวิ่งไล่ล่า (Chase)
// ==========================================
public class StateChase : IMonsterState
{
    public void EnterState(PTSDMonsterAI ai)
    {
        ai.Agent.speed = ai.chaseSpeed;
        if (ai.Agent.isOnNavMesh) ai.Agent.isStopped = false;
        Debug.Log("👁️‍🗨️ มอนสเตอร์เจอผู้เล่นแล้ว! เริ่มวิ่งไล่!");
    }

    public void UpdateState(PTSDMonsterAI ai)
    {
        if (!ai.Agent.isOnNavMesh) return; // ป้องกัน Error

        // 1. เช็คระยะห่างจาก "บ้าน" (Leash System)
        float distFromHome = Vector3.Distance(ai.transform.position, ai.GetHomePosition());
        if (distFromHome > ai.chaseLeashDistance)
        {
            Debug.Log("🛑 มอนสเตอร์วิ่งหลุดขอบเขต (Leash) เลิกตามแล้วเดินกลับบ้าน!");
            ai.ReturnToDefaultPatrol();
            return;
        }

        // 2. เช็คว่าผู้เล่นหนีพ้นระยะสายตา/เสียง หรือเปล่า
        if (!ai.CheckDetection()) 
        {
            ai.ReturnToDefaultPatrol();
            return;
        }

        ai.Agent.SetDestination(ai.PlayerTransform.position);

        // เช็คว่าถึงระยะโจมตีหรือยัง
        float dist = Vector3.Distance(ai.transform.position, ai.PlayerTransform.position);
        if (dist <= ai.attackDistance)
        {
            ai.ChangeState(new StateAttack());
        }
    }

    public void ExitState(PTSDMonsterAI ai) { }
}

// ==========================================
// 5. สถานะโจมตี (Attack)
// ==========================================
public class StateAttack : IMonsterState
{
    private float lastAttackTime;

    public void EnterState(PTSDMonsterAI ai)
    {
        if (ai.Agent.isOnNavMesh) ai.Agent.isStopped = true; // หยุดเดินตอนตี
    }

    public void UpdateState(PTSDMonsterAI ai)
    {
        if (!ai.Agent.isOnNavMesh) return; // ป้องกัน Error

        // ตีผู้เล่นเป็นจังหวะ
        if (Time.time >= lastAttackTime + ai.attackCooldown)
        {
            ai.AttackPlayer();
            lastAttackTime = Time.time;
        }

        // ถ้าผู้เล่นถอยหนีออกนอกระยะตี ให้กลับไปวิ่งไล่ต่อ
        float dist = Vector3.Distance(ai.transform.position, ai.PlayerTransform.position);
        if (dist > ai.attackDistance)
        {
            ai.ChangeState(new StateChase());
        }
    }

    public void ExitState(PTSDMonsterAI ai)
    {
        if (ai.Agent.isOnNavMesh) ai.Agent.isStopped = false; // กลับมาเดินได้
    }
}
