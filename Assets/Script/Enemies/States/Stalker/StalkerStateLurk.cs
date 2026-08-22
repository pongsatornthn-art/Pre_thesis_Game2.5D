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

        if (ai.Awareness.IsActive) { ai.ChangeState(new StalkerStateChase()); return; }
        
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
        
        // สุ่มจุดเดินรอบๆ ผู้เล่น โดยอิงจากค่า Lurk Radius ที่ตั้งไว้ใน Inspector
        Vector2 randCircle = Random.insideUnitCircle.normalized * Random.Range(ai.lurkMinRadius, ai.lurkMaxRadius);
        Vector3 targetPoint = ai.PlayerTransform.position + new Vector3(randCircle.x, 0, randCircle.y);

        if (UnityEngine.AI.NavMesh.SamplePosition(targetPoint, out UnityEngine.AI.NavMeshHit hit, 10f, UnityEngine.AI.NavMesh.AllAreas))
        {
            ai.Agent.SetDestination(hit.position);
        }
    }

    public void ExitState(StalkerAI ai) { }
}
