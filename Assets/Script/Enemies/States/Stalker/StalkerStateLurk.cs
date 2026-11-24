using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// [2026-11-24 นา] "เดินล่า" — เดิมบอสวนรอบตัวผู้เล่นตลอด (รู้ตำแหน่งเราเสมอ ดูโง่+ตามติด)
/// ตอนนี้: เดินตรวจไปทั่วบ้านทีละจุด ยืนฟังสักพัก แล้วไปต่อ
///   ยิ่งไม่เจอผู้เล่นนาน (HuntHeat) ยิ่งเลือกเดินมาย่านที่ผู้เล่นอยู่บ่อยขึ้น = เหมือนมันได้กลิ่น
///   เพิ่งไล่/จับเสร็จ = เดินสุ่มทั่วบ้านก่อน ให้ผู้เล่นได้หายใจ
/// (ชื่อคลาสคงเดิม เพราะระบบอื่นเรียก StalkerStateLurk อยู่หลายที่)
/// </summary>
public class StalkerStateLurk : IStalkerState
{
    private static Bounds mapBounds;
    private static bool boundsReady;

    private float pauseUntil;
    private bool waiting;
    private float stuckTimer;

    public void EnterState(StalkerAI ai)
    {
        ai.Agent.speed = ai.searchSpeed;
        if (ai.Agent.isOnNavMesh) ai.Agent.isStopped = false;
        ai.ShowDebugText("", Color.white);
        waiting = false;
        PickNextPoint(ai);
    }

    public void UpdateState(StalkerAI ai)
    {
        if (!ai.Agent.isOnNavMesh) return;

        ai.UpdateDetection();
        if (ai.Awareness.IsActive) { ai.ChangeState(new StalkerStateScream()); return; }

        if (waiting)
        {
            if (Time.time >= pauseUntil) { waiting = false; PickNextPoint(ai); }
            return;
        }

        // ถึงจุดแล้ว → ยืนฟังสักพัก
        if (!ai.Agent.pathPending && ai.Agent.remainingDistance <= Mathf.Max(ai.Agent.stoppingDistance, 0.4f))
        {
            waiting = true;
            pauseUntil = Time.time + Random.Range(ai.roamPauseSeconds.x, ai.roamPauseSeconds.y);
            return;
        }

        // ติด → เปลี่ยนจุดใหม่
        if (ai.Agent.hasPath && ai.Agent.velocity.sqrMagnitude < 0.05f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer > 1.5f) { stuckTimer = 0f; PickNextPoint(ai); }
        }
        else stuckTimer = 0f;
    }

    private void PickNextPoint(StalkerAI ai)
    {
        bool hunt = ai.PlayerTransform != null && Random.value < Mathf.Lerp(ai.huntChance.x, ai.huntChance.y, ai.HuntHeat);
        for (int i = 0; i < 12; i++)
        {
            Vector3 target = hunt ? PointNearPlayer(ai) : RandomMapPoint();
            if (!NavMesh.SamplePosition(target, out NavMeshHit hit, 3f, NavMesh.AllAreas)) continue;
            if ((hit.position - ai.transform.position).sqrMagnitude < 9f) continue;   // ใกล้เกิน ไม่คุ้มเดิน
            NavMeshPath path = new NavMeshPath();
            if (!ai.Agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            ai.Agent.SetPath(path);
            return;
        }
        // หาไม่ได้ → ยืนรอแล้วลองใหม่
        waiting = true;
        pauseUntil = Time.time + 1f;
    }

    private static Vector3 PointNearPlayer(StalkerAI ai)
    {
        Vector2 c = Random.insideUnitCircle.normalized * Random.Range(ai.huntRadius.x, ai.huntRadius.y);
        return ai.PlayerTransform.position + new Vector3(c.x, 0f, c.y);
    }

    private static Vector3 RandomMapPoint()
    {
        if (!boundsReady)
        {
            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            if (tri.vertices.Length > 0)
            {
                mapBounds = new Bounds(tri.vertices[0], Vector3.zero);
                foreach (Vector3 v in tri.vertices) mapBounds.Encapsulate(v);
                boundsReady = true;
            }
        }
        return new Vector3(Random.Range(mapBounds.min.x, mapBounds.max.x), mapBounds.center.y, Random.Range(mapBounds.min.z, mapBounds.max.z));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { boundsReady = false; }

    public void ExitState(StalkerAI ai) { }
}
