using System.Collections;
using UnityEngine;

/// <summary>
/// "ผังมอน" หนึ่งชุด — จุดเกิดมอน (MonsterSpawner ลูกทั้งหมด) + ห้องปลอดภัยของผังนี้
/// เกิดเฉพาะตอนเข้า PTSD ที่ "ตรงเงื่อนไข" ของผังนี้ ไม่ใช่เกิดทุกครั้งที่เข้า PTSD
///   • PTSD B (โต๊ะวาดรูป): Mode = TypeB + Required Flag = ธงเริ่มฉากโต๊ะ + Blocked By = ธงประกอบรูปเสร็จ
///   • PTSD A (จุดชมพู):   PtsdEncounterZone ตั้ง Arm Key ก่อนพาเข้า PTSD
///
/// เช็คเงื่อนไขจากธงเรื่องราว (เซฟอยู่ในสำเนาตอนเข้า PTSD) → ตายแล้วเกมย้อนกลับเข้า PTSD ใหม่ ผังเดิมก็เกิดใหม่ถูกชุด
/// จุดเกิดลูกต้องติ๊ก Spawn On Any Ptsd ออก (ไม่งั้นเกิดทุก PTSD เหมือนเดิม)
/// ทุกครั้งที่เกิด = เกิดครบจำนวน (มอนที่ตายรอบก่อนกลับมา — เจ้าของเลือก 2026-11-08)
/// [Claude 2026-11-08]
/// </summary>
public class PtsdMonsterLayout : MonoBehaviour
{
    public enum ModeFilter { Any, SurvivalA, NarrativeB }

    [Header("เกิดเมื่อ")]
    public ModeFilter mode = ModeFilter.Any;
    [Tooltip("ต้องมีธงนี้ (เว้นว่าง = ไม่เช็ค)")]
    public StoryFlagId requiredFlag;
    [Tooltip("มีธงนี้แล้ว = ไม่เกิดอีก (เว้นว่าง = ไม่เช็ค)")]
    public StoryFlagId blockedByFlag;
    [Tooltip("รหัสเปิดผัง — PtsdEncounterZone ตั้งให้ก่อนพาเข้า PTSD (เว้นว่าง = ไม่เช็ค)")]
    public string armKey;

    [Header("ของในผังนี้")]
    [Tooltip("โชว์เฉพาะตอนผังนี้ทำงาน เช่น ห้องเขียว (SafeZoneTrigger) — วางในซีนแบบปิดไว้")]
    public GameObject[] showWhileActive;

    public bool IsActive { get; private set; }

    private MonsterSpawner[] spawners;

    private void Awake()
    {
        spawners = GetComponentsInChildren<MonsterSpawner>(true);
        SetShown(false);
    }

    private void OnEnable() => PTSDManager.OnPTSDStateChanged += HandlePtsdChanged;
    private void OnDisable() => PTSDManager.OnPTSDStateChanged -= HandlePtsdChanged;

    private void HandlePtsdChanged(bool inPtsd)
    {
        if (!inPtsd) { Deactivate(); return; }
        // รอ 1 เฟรม: ตอนโหลดสำเนา (ตายแล้วย้อน) ธงอาจกำลังถูกใส่คืน
        StartCoroutine(ActivateIfMatches());
    }

    private IEnumerator ActivateIfMatches()
    {
        yield return null;
        while (SaveRestoreScope.IsBusy) yield return null;
        if (Matches()) Activate();
    }

    private bool Matches()
    {
        PTSDManager pm = PTSDManager.Instance;
        if (pm == null || pm.currentMode == PTSDMode.None) return false;
        if (mode == ModeFilter.SurvivalA && pm.currentMode != PTSDMode.Survival_TypeA) return false;
        if (mode == ModeFilter.NarrativeB && pm.currentMode != PTSDMode.Narrative_TypeB) return false;

        IStoryFlags flags = ServiceLocator.GetOptional<IStoryFlags>();
        if (flags == null) return requiredFlag == null && blockedByFlag == null && string.IsNullOrEmpty(armKey);
        if (requiredFlag != null && !flags.Has(requiredFlag)) return false;
        if (blockedByFlag != null && flags.Has(blockedByFlag)) return false;
        if (!string.IsNullOrEmpty(armKey) && !flags.HasKey(armKey)) return false;
        return true;
    }

    /// <summary>เกิดมอนทุกจุด + โชว์ห้องปลอดภัยของผังนี้</summary>
    public void Activate()
    {
        if (IsActive) return;
        IsActive = true;
        SetShown(true);
        foreach (var s in spawners)
        {
            if (s == null) continue;
            s.remainingCount = s.spawnCount;   // เกิดครบทุกรอบ
            s.SpawnNow();
        }
    }

    public void Deactivate()
    {
        StopAllCoroutines();
        if (!IsActive) return;
        IsActive = false;
        SetShown(false);
        foreach (var s in spawners) if (s != null) s.DespawnAll();
    }

    private void SetShown(bool on)
    {
        if (showWhileActive == null) return;
        foreach (var g in showWhileActive) if (g != null) g.SetActive(on);
    }
}
