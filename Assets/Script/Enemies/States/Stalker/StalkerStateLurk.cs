using UnityEngine;

public class StalkerStateLurk : IStalkerState
{
    private float timer = 0f;
    private float moveTimer = 0f;

    public void EnterState(StalkerAI ai)
    {
        ai.Agent.speed = ai.searchSpeed;
        if (ai.Agent.isOnNavMesh) ai.Agent.isStopped = false;
        MoveToRandomPointNearPlayer(ai);
        ai.ShowDebugText("", Color.white); // ซ่อนข้อความตอนกำลังซุ่ม
        Debug.Log("🦇 Stalker กำลังเดินซุ่มคุมเชิงอยู่รอบๆ ผู้เล่น...");
    }

    public void UpdateState(StalkerAI ai)
    {
        if (!ai.Agent.isOnNavMesh) return;

        ai.UpdateDetection();

        if (ai.Awareness.IsActive) { ai.ChangeState(new StalkerStateScream()); return; }
        
        // เดินลาดตระเวนรอบๆ ผู้เล่นในเงามืด
        if (!ai.Agent.pathPending && ai.Agent.remainingDistance <= ai.Agent.stoppingDistance)
        {
            timer += Time.deltaTime;
            if (timer >= 3f) // ยืนดมกลิ่น 3 วิ แล้วเดินต่อ
            {
                MoveToRandomPointNearPlayer(ai);
                timer = 0f;
            }
        }

        // Anti Stuck
        if (ai.Agent.hasPath && ai.Agent.velocity.sqrMagnitude < 0.1f)
        {
            moveTimer += Time.deltaTime;
            if (moveTimer > 2f) { ai.Agent.ResetPath(); moveTimer = 0f; }
        }
        else moveTimer = 0f;
    }

    private void MoveToRandomPointNearPlayer(StalkerAI ai)
    {
        if (ai.PlayerTransform == null) return;
        
        // 🌟 สุ่มหาจุดสูงสุด 10 ครั้ง เพื่อหาจุดที่ "เดินไปถึงได้จริงๆ" (ไม่ออกนอกแมพ)
        for (int i = 0; i < 10; i++)
        {
            Vector2 randCircle = Random.insideUnitCircle.normalized * Random.Range(ai.lurkMinRadius, ai.lurkMaxRadius);
            Vector3 targetPoint = ai.PlayerTransform.position + new Vector3(randCircle.x, 0, randCircle.y);

            if (UnityEngine.AI.NavMesh.SamplePosition(targetPoint, out UnityEngine.AI.NavMeshHit hit, 10f, UnityEngine.AI.NavMesh.AllAreas))
            {
                // ตรวจสอบว่ามีเส้นทางเดินไปถึงจุดนั้นได้จริงๆ ใช่ไหม? (ไม่ติดกำแพง/ไม่อยู่นอกแมพ)
                UnityEngine.AI.NavMeshPath path = new UnityEngine.AI.NavMeshPath();
                ai.Agent.CalculatePath(hit.position, path);
                
                if (path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete)
                {
                    ai.Agent.SetDestination(hit.position);
                    return; // เจอจุดที่เดินได้แล้ว จบการทำงาน
                }
            }
        }
        
        // ถ้าสุ่ม 10 ครั้งแล้วไม่เจอที่เดินเลย (เช่น โดนต้อนเข้ามุม) ให้เดินไปหาผู้เล่นตรงๆ เลย
        ai.Agent.SetDestination(ai.PlayerTransform.position);
    }

    public void ExitState(StalkerAI ai) { }
}
