using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// [2026-11-27 ปอ] ระหว่างเล่นมินิเกมดันประตู (ทุกบาน ทั้งประตูครัว Tutorial และประตูที่โดนบอสไล่)
///   → มอนทุกตัว (มินเนียน PTSDMonsterAI + บอส StalkerAI) ยืนนิ่ง
/// เดิม: มอนยังเดินอยู่ระหว่างดันประตู ออกมาปุ๊บมันมายืนรอหน้าตัวผู้เล่นแล้ว
///
/// จบมินิเกม:
///   ชนะ / ยกเลิก → มอนลืมผู้เล่น (เกจรับรู้ = 0) กลับไปเดินลาดตระเวนตามเดิม + ยืนนิ่งต่ออีกนิด ให้ผู้เล่นตั้งตัว
///   แพ้          → ปล่อยมอนตามเดิม (ผู้เล่นตายอยู่แล้ว)
///
/// ไม่ต้องแปะที่ไหน — ทำงานเองตั้งแต่เริ่มเกม
/// ไม่ยุ่งกับตัวที่ระบบอื่นหยุดไว้แล้ว (บอสที่ PTSDDoorAmbush ย้ายไปหลังประตู · FirstChaseStalker ของประตูครัว)
/// </summary>
public static class EnemyFreezeDuringDoorStruggle
{
    /// <summary>หลังชนะ มอนยืนนิ่งต่ออีกกี่วินาที (ให้ผู้เล่นได้เดินออกห่าง)</summary>
    public static float postWinHoldSeconds = 1.5f;

    private struct Frozen
    {
        public Behaviour ai;
        public NavMeshAgent agent;
        public bool agentWasStopped;
    }

    private static readonly List<Frozen> frozen = new List<Frozen>();
    private static int releaseToken;   // เริ่มรอบใหม่ = ยกเลิกการปล่อยที่รออยู่

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        frozen.Clear();
        releaseToken = 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        DoorBarricadeStruggle.OnAnyStarted -= HandleStarted;
        DoorBarricadeStruggle.OnAnyStarted += HandleStarted;
        DoorBarricadeStruggle.OnAnyEnded -= HandleEnded;
        DoorBarricadeStruggle.OnAnyEnded += HandleEnded;
    }

    private static void HandleStarted(DoorBarricadeStruggle s)
    {
        releaseToken++;
        Release(true);    // เผื่อรอบก่อนยังยืนนิ่งรอปล่อยอยู่

        foreach (PTSDMonsterAI m in Object.FindObjectsByType<PTSDMonsterAI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            Freeze(m, m.Agent);
        foreach (StalkerAI b in Object.FindObjectsByType<StalkerAI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            Freeze(b, b.Agent);
    }

    private static void HandleEnded(DoorBarricadeStruggle s, DoorBarricadeStruggle.Result result)
    {
        if (frozen.Count == 0) return;
        if (result == DoorBarricadeStruggle.Result.Fail || postWinHoldSeconds <= 0f)
        {
            Release(result != DoorBarricadeStruggle.Result.Fail);
            return;
        }
        // ชนะ/ยกเลิก: ยืนนิ่งต่ออีกนิด แล้วค่อยปล่อย (ลืมผู้เล่นแล้ว)
        ReleaseLater(postWinHoldSeconds, ++releaseToken);
    }

    private static async void ReleaseLater(float seconds, int token)
    {
        try { await Awaitable.WaitForSecondsAsync(seconds); }
        catch (System.OperationCanceledException) { return; }   // ออกจาก Play Mode ระหว่างรอ
        if (token != releaseToken || DoorBarricadeStruggle.AnyActive) return;   // มีรอบใหม่เริ่มแล้ว
        Release(true);
    }

    private static void Freeze(Behaviour ai, NavMeshAgent agent)
    {
        if (ai == null || !ai.isActiveAndEnabled) return;   // ปิดอยู่แล้ว (ระบบอื่นหยุดไว้) → ไม่ยุ่ง

        bool wasStopped = false;
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            wasStopped = agent.isStopped;
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
        ai.enabled = false;   // หยุดสมองมอน (ไม่เดิน/ไม่ไล่/ไม่ตี)
        frozen.Add(new Frozen { ai = ai, agent = agent, agentWasStopped = wasStopped });
    }

    /// <param name="calm">true = ลืมผู้เล่น กลับไปเดินลาดตระเวน</param>
    private static void Release(bool calm)
    {
        foreach (Frozen f in frozen)
        {
            if (f.ai == null) continue;
            f.ai.enabled = true;
            if (f.agent != null && f.agent.isActiveAndEnabled && f.agent.isOnNavMesh)
            {
                f.agent.isStopped = f.agentWasStopped;
                if (calm) f.agent.ResetPath();
            }
            if (!calm || !f.ai.gameObject.activeInHierarchy) continue;

            if (f.ai is PTSDMonsterAI m)
            {
                if (m.Awareness != null) m.Awareness.ForceZeroAwareness();
                m.ReturnToDefaultPatrol();
            }
            else if (f.ai is StalkerAI b)
            {
                if (b.Awareness != null) b.Awareness.ForceZeroAwareness();
                b.lastContactTime = Time.time;   // เพิ่งเจอกัน → บอสเดินสุ่มทั่วบ้านก่อน
                b.ChangeState(new StalkerStateLurk());
            }
        }
        frozen.Clear();
    }
}
