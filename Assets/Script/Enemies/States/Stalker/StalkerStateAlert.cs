using UnityEngine;

public class StalkerStateAlert : IStalkerState
{
    private Vector3 alertPos;
    private float searchTimer;
    private float suspicionLevel;

    public StalkerStateAlert(Vector3 pos, float suspicion = 100f) 
    { 
        alertPos = pos; 
        suspicionLevel = suspicion;
    }

    public void EnterState(StalkerAI ai)
    {
        // คำนวณความเร็วในการเดินไปสำรวจตามค่าความสงสัย (suspicionLevel 0-100)
        // 100 = วิ่งหน้าตั้ง (chaseSpeed)
        // 20 = เดินย่องสำรวจ (searchSpeed)
        ai.Agent.speed = Mathf.Lerp(ai.searchSpeed, ai.chaseSpeed, suspicionLevel / 100f); 
        if (ai.Agent.isOnNavMesh) 
        {
            ai.Agent.isStopped = false;
            // สุ่มจุดใกล้ๆ จุดที่เกิดเสียงในรัศมี 2 เมตร จะได้ไม่วิ่งไปทับร่างมินเนียนเป๊ะๆ
            Vector2 randCircle = Random.insideUnitCircle * 2f;
            Vector3 targetPoint = alertPos + new Vector3(randCircle.x, 0, randCircle.y);
            
            if (UnityEngine.AI.NavMesh.SamplePosition(targetPoint, out UnityEngine.AI.NavMeshHit hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                ai.Agent.SetDestination(hit.position);
            }
            else
            {
                ai.Agent.SetDestination(alertPos);
            }
        }
        searchTimer = 0f;
        ai.ShowDebugText("BOSS_ALERT", new Color(1f, 0.5f, 0f));
    }

    public void UpdateState(StalkerAI ai)
    {
        if (!ai.Agent.isOnNavMesh) return;

        ai.UpdateDetection();
        if (ai.Awareness.IsActive) { ai.ChangeState(new StalkerStateScream()); return; }

        // [2026-11-24 นา] ถึงจุดแล้ว: เดิมยืนนิ่ง 8 วิแล้วเลิก → ตอนนี้เดินค้นห้องรอบๆ (Search Radius) จนครบ Search Seconds
        if (arrived) searchTimer += Time.deltaTime;
        if (!ai.Agent.pathPending && ai.Agent.remainingDistance <= Mathf.Max(ai.Agent.stoppingDistance, 0.5f))
        {
            arrived = true;
            if (Time.time >= nextPeekAt)
            {
                nextPeekAt = Time.time + Random.Range(1.2f, 2.5f);
                PeekAround(ai);
            }
        }
        if (searchTimer >= ai.searchSeconds)
        {
            Debug.Log("🦇 ตรวจสอบเสร็จแล้ว ไม่เจออะไร กลับไปเดินล่าต่อ");
            ai.ChangeState(new StalkerStateLurk());
        }
    }

    private bool arrived;
    private float nextPeekAt;

    // เดินไปส่องจุดใกล้ๆ จุดที่คลาดกัน (เข้าห้องข้างๆ / มุมห้อง) — ต้องเดินถึงได้จริง
    private void PeekAround(StalkerAI ai)
    {
        ai.Agent.speed = Mathf.Lerp(ai.searchSpeed, ai.chaseSpeed, 0.4f);
        for (int i = 0; i < 8; i++)
        {
            Vector2 c = Random.insideUnitCircle * ai.searchRadius;
            if (!UnityEngine.AI.NavMesh.SamplePosition(alertPos + new Vector3(c.x, 0f, c.y), out UnityEngine.AI.NavMeshHit hit, 2f, UnityEngine.AI.NavMesh.AllAreas)) continue;
            var path = new UnityEngine.AI.NavMeshPath();
            if (ai.Agent.CalculatePath(hit.position, path) && path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete) { ai.Agent.SetPath(path); return; }
        }
    }
    public void ExitState(StalkerAI ai) { }
}
