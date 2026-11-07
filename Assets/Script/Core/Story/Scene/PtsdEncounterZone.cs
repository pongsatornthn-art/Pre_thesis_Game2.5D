using System.Collections;
using UnityEngine;

/// <summary>
/// จุดชมพู "PTSD A (Random)" — เดินชนแล้วเข้า PTSD แบบเอาตัวรอด พร้อมผังมอน/ห้องเขียวของจุดนี้
/// เกิด**ครั้งเดียว**: ออกจาก PTSD สำเร็จ (ห้องเขียว + มินิเกมหายใจ) = หายถาวร (จำในเซฟ)
/// ตายกลางทาง = เกมย้อนกลับไปตอนเพิ่งเข้า (ระบบตายเดิม) → จุดยังไม่นับว่าผ่าน
///
/// ตั้งในซีน: กล่อง Collider (Is Trigger) เท่าพื้นที่ชมพู + ลาก PtsdMonsterLayout ของจุดนี้ใส่ช่อง Layout
///   (Layout ช่อง Arm Key ปล่อยว่างได้ — จุดนี้ใส่ให้เอง)
/// เปิดใช้เมื่อมี Required Flag (เช่น ธงประกอบรูปเสร็จ = จบ PTSD B แล้ว) → ไม่มีทางซ้อนกับตอนหนีบอสครั้งแรก
/// [Claude 2026-11-08]
/// </summary>
[RequireComponent(typeof(Collider))]
public class PtsdEncounterZone : MonoBehaviour
{
    [Tooltip("ผังมอน + ห้องเขียวของจุดนี้")]
    public PtsdMonsterLayout layout;

    [Tooltip("ทำงานเมื่อมีธงนี้แล้ว (เช่น Flag_PaintingAssembled)")]
    public StoryFlagId requiredFlag;

    [Tooltip("ของที่โชว์ตอนจุดยังใช้งานได้ (เช่น รอยแดงบนพื้น) — ผ่านแล้วซ่อน · เว้นว่างได้")]
    public GameObject visual;

    [Tooltip("รหัสจำสถานะ (สร้างให้อัตโนมัติ ห้ามซ้ำ)")]
    public string saveKey;

    private string ArmKey => saveKey + "_armed";
    private string ClearedKey => saveKey + "_cleared";

    private bool running;

    private void Reset()
    {
        saveKey = "enc_" + System.Guid.NewGuid().ToString("N").Substring(0, 10);
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(saveKey)) saveKey = "enc_" + System.Guid.NewGuid().ToString("N").Substring(0, 10);
        if (layout != null) layout.armKey = ArmKey;
    }

    private void OnEnable()
    {
        PTSDManager.OnPTSDStateChanged += HandlePtsdChanged;
        StartCoroutine(SyncNextFrame());
    }

    private void OnDisable() => PTSDManager.OnPTSDStateChanged -= HandlePtsdChanged;

    private IEnumerator SyncNextFrame()
    {
        yield return null;
        while (SaveRestoreScope.IsBusy) yield return null;

        IStoryFlags flags = ServiceLocator.GetOptional<IStoryFlags>();
        if (flags == null) yield break;
        if (layout != null) layout.armKey = ArmKey;

        // ตายแล้วเกมย้อนกลับเข้า PTSD ของจุดนี้ → ยังถือว่ากำลังเล่นจุดนี้อยู่
        running = flags.HasKey(ArmKey);
        RefreshVisual(flags);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (running || !other.CompareTag("Player")) return;

        PTSDManager pm = PTSDManager.Instance;
        if (pm == null || pm.currentMode != PTSDMode.None) return;

        IStoryFlags flags = ServiceLocator.GetOptional<IStoryFlags>();
        if (flags == null || flags.HasKey(ClearedKey)) return;
        if (requiredFlag != null && !flags.Has(requiredFlag)) return;

        // ตั้งรหัสเปิดผังก่อนเข้า PTSD → อยู่ในสำเนาจุดเริ่มใหม่ (ตายแล้วย้อนกลับก็รู้ว่าผังไหน)
        if (layout != null) layout.armKey = ArmKey;
        flags.SetKey(ArmKey);
        running = true;
        pm.TriggerPTSDSurvival();
    }

    private void HandlePtsdChanged(bool inPtsd)
    {
        if (inPtsd || !running) return;

        // ออกจาก PTSD ได้ (ห้องเขียว + หายใจ) → ผ่านจุดนี้ถาวร
        running = false;
        IStoryFlags flags = ServiceLocator.GetOptional<IStoryFlags>();
        if (flags != null)
        {
            flags.ClearKey(ArmKey);
            flags.SetKey(ClearedKey);
            RefreshVisual(flags);
        }
    }

    private void RefreshVisual(IStoryFlags flags)
    {
        bool cleared = flags.HasKey(ClearedKey);
        GetComponent<Collider>().enabled = !cleared;
        if (visual != null) visual.SetActive(!cleared);
    }
}
