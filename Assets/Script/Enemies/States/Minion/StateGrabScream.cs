using UnityEngine;

/// <summary>
/// [2026-11-21] ท่า Grab & Scream ของมินเนียน (เอกสาร Minions หัวข้อ Grab&Scream)
///
/// เข้าเมื่อ: ไล่ถึงระยะตี + ผู้เล่นสตามิน่า ≤ 20% (PTSDMonsterAI.CanGrab — StateChase เป็นคนเลือก)
/// ลำดับ: ย่อตัว+ร้อง (ตัวสั่น/แดงเตือน · Super Armor) → พุ่งตะครุบ → โดนในกรวยหน้า = จับ (เปิดมินิเกมสะบัด MinionGrabStruggle)
///        พุ่งไม่โดน = ยืนเสียหลักสั้นๆ แล้วกลับไปไล่ (คูลดาวน์ grabWhiffCooldown)
/// จับแล้ว: มินเนียนนิ่งเกาะผู้เล่น จนมินิเกมจบ → MinionGrabStruggle เรียก ai.OnGrabReleased(...) เปลี่ยน state เอง
/// </summary>
public class StateGrabScream : IMonsterState
{
    private enum Phase { Crouch, Lunge, Held, Recover }
    private Phase phase;
    private float timer;
    private Vector3 lungeDir;
    private bool lunged;

    // ปรับจังหวะได้ตรงนี้
    private const float CrouchTime = 0.6f;     // ย่อตัวร้องก่อนพุ่ง (ให้ผู้เล่นมีเวลาเห็นแล้ว Dash หนี)
    private const float LungeTime = 0.25f;
    private const float RecoverTime = 1.2f;    // พุ่งพลาด ยืนเสียหลัก
    private const float LungeSpeed = 12f;
    private const float LungeDistance = 1.8f;
    private const float GrabReach = 1.3f;      // รัศมีจับรอบตัวมินเนียน
    private const float GrabConeHalfAngle = 65f; // กรวยด้านหน้า (องศาครึ่งหนึ่ง)

    public void EnterState(PTSDMonsterAI ai)
    {
        StopAgent(ai);
        phase = Phase.Crouch;
        timer = 0f;
        lunged = false;

        // พยายามแล้ว = คูลดาวน์สั้นก่อน (ถ้าจับสำเร็จ MinionGrabStruggle จะตั้งคูลดาวน์ยาวทับตอนปล่อย)
        ai.nextGrabTime = Time.time + ai.grabWhiffCooldown;

        ai.hasSuperArmor = true;           // GDD: Super Armor ตอนใช้ท่า Scream
        ai.BeginScreamTell();              // ตัวสั่น + แดง + เสียงสูดหายใจ
        ai.ShowDebugText("GRAB!", Color.magenta);

        lungeDir = ai.PlayerTransform.position - ai.transform.position;
        lungeDir.y = 0f;
        lungeDir = lungeDir.sqrMagnitude > 0.0001f ? lungeDir.normalized : ai.transform.forward;
    }

    public void UpdateState(PTSDMonsterAI ai)
    {
        timer += Time.deltaTime;

        switch (phase)
        {
            case Phase.Crouch:
                // ระหว่างย่อตัว ยังหันตามผู้เล่น (GDD: ค่า AG เต็ม → หันหน้าไปหาตัวเอก)
                Vector3 to = ai.PlayerTransform.position - ai.transform.position; to.y = 0f;
                if (to.sqrMagnitude > 0.0001f) lungeDir = to.normalized;
                if (timer >= CrouchTime) BeginLunge(ai);
                break;

            case Phase.Lunge:
                if (TryGrab(ai)) return;
                if (timer >= LungeTime)
                {
                    phase = Phase.Recover;
                    timer = 0f;
                    ai.hasSuperArmor = false;
                    ai.EndScreamTell();
                    StopAgent(ai);
                    ai.ShowDebugText("TIRED...", Color.gray);
                }
                break;

            case Phase.Held:
                // เกาะผู้เล่นนิ่งๆ รอมินิเกมจบ (MinionGrabStruggle จะเปลี่ยน state ให้)
                break;

            case Phase.Recover:
                if (timer >= RecoverTime) ai.ChangeState(new StateChase());
                break;
        }
    }

    private void BeginLunge(PTSDMonsterAI ai)
    {
        phase = Phase.Lunge;
        timer = 0f;
        if (ai.Agent.isOnNavMesh && !lunged)
        {
            lunged = true;
            ai.Agent.isStopped = false;
            ai.Agent.speed = LungeSpeed;
            ai.Agent.acceleration = 200f;
            ai.Agent.angularSpeed = 0f;
            ai.Agent.SetDestination(ai.transform.position + lungeDir * LungeDistance);
        }
    }

    // ผู้เล่นอยู่ในกรวยหน้า + ระยะจับ → เริ่มมินิเกมสะบัด
    private bool TryGrab(PTSDMonsterAI ai)
    {
        Vector3 d = ai.PlayerTransform.position - ai.transform.position; d.y = 0f;
        if (d.magnitude > GrabReach) return false;
        if (d.sqrMagnitude > 0.0001f && Vector3.Angle(lungeDir, d) > GrabConeHalfAngle) return false;

        MinionGrabStruggle struggle = MinionGrabStruggle.Instance;
        if (struggle == null || !struggle.Begin(ai)) return false;

        phase = Phase.Held;
        timer = 0f;
        StopAgent(ai);
        ai.ShowDebugText("GRABBING", Color.red);
        return true;
    }

    private static void StopAgent(PTSDMonsterAI ai)
    {
        if (!ai.Agent.isOnNavMesh) return;
        ai.Agent.ResetPath();
        ai.Agent.velocity = Vector3.zero;
        ai.Agent.isStopped = true;
    }

    public void ExitState(PTSDMonsterAI ai)
    {
        ai.EndScreamTell();
        ai.hasSuperArmor = false;

        // โดนเปลี่ยน state ระหว่างจับอยู่ (เช่น ตาย) → ปล่อยผู้เล่นด้วย
        if (phase == Phase.Held && MinionGrabStruggle.Instance != null)
            MinionGrabStruggle.Instance.CancelIfHeldBy(ai);

        if (ai.Agent.isOnNavMesh)
        {
            ai.Agent.ResetPath();
            ai.Agent.velocity = Vector3.zero;
            ai.Agent.speed = ai.chaseSpeed;
            ai.Agent.acceleration = 8f;
            ai.Agent.angularSpeed = 120f;
            ai.Agent.isStopped = false;
        }
    }
}
