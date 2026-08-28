using UnityEngine;

public class StalkerStateChase : IStalkerState
{
    private float roarTimer = 0f;
    private bool hasRoared = false;
    private float roarDuration = 3f;
    private float stuckTimer = 0f;
    private float pathUpdateTimer = 0f;

    public void EnterState(StalkerAI ai)
    {
        roarTimer = 0f;
        hasRoared = false;

        // 🌟 ไอเดียที่ 4: บอสโผล่มาต้องยืนคำรามขู่ก่อน 3 วินาที ให้ผู้เล่นตั้งตัวทัน
        if (ai.Agent.isOnNavMesh) 
        {
            ai.Agent.ResetPath();
            ai.Agent.isStopped = true;
            ai.Agent.velocity = Vector3.zero;
        }

        ai.ShowDebugText("BOSS_ROAR!", Color.magenta);
        Debug.Log($"<color=magenta>🦇 Stalker เห็นผู้เล่นแล้ว! ยืนคำรามขู่เป็นเวลา {roarDuration} วินาที!</color>");
    }

    public void UpdateState(StalkerAI ai)
    {
        if (!ai.Agent.isOnNavMesh) return;

        // 🌟 ช่วงคำรามขู่ (Roar Phase)
        if (!hasRoared)
        {
            roarTimer += Time.deltaTime;
            
            // ให้บอสหันหน้าตามผู้เล่นระหว่างคำรามเพื่อความหลอน
            if (ai.PlayerTransform != null)
            {
                Vector3 lookPos = ai.PlayerTransform.position;
                lookPos.y = ai.transform.position.y;
                ai.transform.LookAt(lookPos);
            }

            if (roarTimer >= roarDuration)
            {
                hasRoared = true;
                ai.Agent.isStopped = false;
                ai.Agent.speed = ai.chaseSpeed;
                ai.ShowDebugText("BOSS_CHASE", Color.red);
                Debug.Log("<color=red>🦇 Stalker คำรามเสร็จแล้ว เริ่มวิ่งไล่ล่า!!</color>");
            }
            return; // ยังไม่ให้ขยับหรือเช็คอะไรทั้งสิ้นจนกว่าจะคำรามจบ
        }

        ai.UpdateDetection();
        if (!ai.Awareness.IsActive) 
        {
            // ถ้าหลุดสายตา ไปค้นหาตรงจุดที่คลาดกัน
            ai.ChangeState(new StalkerStateAlert(ai.PlayerTransform.position));
            return;
        }

        // อัปเดตปลายทางทุกๆ 0.2 วินาที แทนการอัปเดตทุกเฟรม (ป้องกัน NavMeshAgent เอ๋อเวลาวิ่งเร็วจัดและเลี้ยวไม่พ้นมุม)
        pathUpdateTimer += Time.deltaTime;
        if (pathUpdateTimer >= 0.2f)
        {
            ai.Agent.SetDestination(ai.PlayerTransform.position);
            pathUpdateTimer = 0f;
        }

        // Anti-Stuck: ถ้ามีเป้าหมายแต่ความเร็วเกือบ 0 (ติดกล่องที่ไม่ได้ Bake NavMesh)
        if (ai.Agent.hasPath && ai.Agent.velocity.sqrMagnitude < 0.1f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer > 3f)
            {
                Debug.LogWarning("🦇 Stalker เดินติดกำแพงนานเกิน 3 วินาที! ระบบกำลังวาร์ปแก้บัค...");
                // วาร์ปขยับเข้าหาผู้เล่นนิดนึงเพื่อหลุดจากจุดที่บัค
                Vector3 unstuckPos = Vector3.Lerp(ai.transform.position, ai.PlayerTransform.position, 0.3f);
                if (UnityEngine.AI.NavMesh.SamplePosition(unstuckPos, out UnityEngine.AI.NavMeshHit hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    ai.Agent.Warp(hit.position);
                }
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
        }

        if (Vector3.Distance(ai.transform.position, ai.PlayerTransform.position) <= ai.killDistance)
        {
            ai.ChangeState(new StalkerStateKill());
        }
    }

    public void ExitState(StalkerAI ai) { }
}
