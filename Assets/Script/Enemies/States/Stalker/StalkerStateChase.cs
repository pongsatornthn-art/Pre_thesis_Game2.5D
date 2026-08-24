using UnityEngine;

public class StalkerStateChase : IStalkerState
{
    private float roarTimer = 0f;
    private bool hasRoared = false;
    private float roarDuration = 3f;

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

        ai.Agent.SetDestination(ai.PlayerTransform.position);

        if (Vector3.Distance(ai.transform.position, ai.PlayerTransform.position) <= ai.killDistance)
        {
            ai.ChangeState(new StalkerStateKill());
        }
    }

    public void ExitState(StalkerAI ai) { }
}
