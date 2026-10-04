using System;
using UnityEngine;

/// <summary>
/// ตัวแปลง PTSDManager (ของเพื่อน) → IWorldModeService (ของเรา)
/// ฟังประกาศ static OnPTSDStateChanged ที่เพื่อนมีอยู่แล้ว และเรียก API สาธารณะของเขา — **ไม่แก้ไฟล์เพื่อน**
///
/// วางที่ [STORY] ก้อนเดียวกับบริการเนื้อเรื่องอื่น
/// </summary>
public class PtsdWorldModeAdapter : MonoBehaviour, IWorldModeService
{
    private WorldMode mode = WorldMode.Real;

    public WorldMode Mode => mode;
    public bool IsInPtsd => mode != WorldMode.Real;
    public event Action<WorldMode, WorldMode> OnModeChanged;

    private void Awake()
    {
        ServiceLocator.Register<IWorldModeService>(this);
        PTSDManager.OnPTSDStateChanged += HandlePtsdStateChanged;
    }

    private void Start()
    {
        // เผื่อเริ่มซีนมาอยู่ในโหมด PTSD อยู่แล้ว (ตั้งค่าไว้ใน Inspector ของ PTSDManager)
        if (PTSDManager.Instance != null) SetMode(Map(PTSDManager.Instance.currentMode));
    }

    private void OnDestroy()
    {
        PTSDManager.OnPTSDStateChanged -= HandlePtsdStateChanged;
        ServiceLocator.Unregister<IWorldModeService>();
    }

    public void EnterPtsd(WorldMode target)
    {
        PTSDManager ptsd = PTSDManager.Instance;
        if (ptsd == null)
        {
            Debug.LogWarning("[WorldMode] ไม่พบ PTSDManager ในซีน — เข้าโลก PTSD ไม่ได้");
            return;
        }

        switch (target)
        {
            case WorldMode.PtsdSurvival: ptsd.TriggerPTSDSurvival(); break;
            case WorldMode.PtsdNarrative: ptsd.TriggerPTSDNarrative(); break;
            default: Debug.LogWarning("[WorldMode] EnterPtsd ต้องระบุ PtsdSurvival หรือ PtsdNarrative"); break;
        }
    }

    public void ExitPtsd()
    {
        if (PTSDManager.Instance != null) PTSDManager.Instance.ExitPTSD();
    }

    // PTSDManager ยิงตัวนี้ "ก่อน" เริ่มอนิเมชันสลับโลก และตั้ง currentMode ไว้แล้ว
    private void HandlePtsdStateChanged(bool _)
    {
        if (PTSDManager.Instance != null) SetMode(Map(PTSDManager.Instance.currentMode));
    }

    private void SetMode(WorldMode next)
    {
        if (next == mode) return;

        WorldMode previous = mode;
        mode = next;
        Debug.Log($"<color=magenta>🌀 [WorldMode] {previous} → {next}</color>");

        OnModeChanged?.Invoke(previous, next);
        GameEventBus.Publish(new WorldModeChangedEvent(previous, next));
    }

    private static WorldMode Map(PTSDMode ptsdMode)
    {
        switch (ptsdMode)
        {
            case PTSDMode.Survival_TypeA: return WorldMode.PtsdSurvival;
            case PTSDMode.Narrative_TypeB: return WorldMode.PtsdNarrative;
            default: return WorldMode.Real;
        }
    }
}
