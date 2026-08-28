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

        // เมื่อวิ่งมาถึงที่แล้ว ค้นหา 8 วิ ตาม GDD "Search_In_Area_Time = 8s"
        if (!ai.Agent.pathPending && ai.Agent.remainingDistance <= ai.Agent.stoppingDistance)
        {
            searchTimer += Time.deltaTime;
            if (searchTimer >= 8f)
            {
                Debug.Log("🦇 ตรวจสอบเสร็จแล้ว ไม่เจออะไร กลับไปซุ่มต่อ");
                ai.ChangeState(new StalkerStateLurk()); // กลับไปเดินรอบๆ ต่อ
            }
        }
    }
    public void ExitState(StalkerAI ai) { }
}
