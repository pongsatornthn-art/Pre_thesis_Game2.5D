using UnityEngine;

public enum MinigameResult
{
    None,       // ยังไม่จบ
    Success,    // ทำสำเร็จ
    Aborted     // ผู้เล่นออกกลางคัน
}

/// <summary>ประกาศใน GameEventBus เมื่อมินิเกมจบ — ระบบอื่น (ป้าย/สถิติ/เซฟอัตโนมัติ) มาฟังได้</summary>
public readonly struct MinigameFinishedEvent
{
    public readonly string MinigameId;
    public readonly MinigameResult Result;
    public MinigameFinishedEvent(string id, MinigameResult result) { MinigameId = id; Result = result; }
}

/// <summary>
/// คลาสแม่ของมินิเกมทุกตัว (ประกอบรูป / ไขรหัส / ต่อสายไฟ ในอนาคต) — Template Method
/// ฉากเนื้อเรื่องสั่งด้วย "มินิเกม/เล่นมินิเกม" ใส่ Scene Id · รู้จักแค่คลาสนี้ ไม่สนว่ามินิเกมอะไร
///
/// ทำให้มินิเกมลูกฟรี (STRUCTURE_CHECKLIST C):
///   แผงคำแนะนำ (UIPanelController + LocalizedText ในซีน) · ไม่รับปุ่มตอนหยุดเกม (IsInputBlocked)
///   เสียงเริ่ม/สำเร็จ ผ่าน IAudioService (+ PlaySfx ให้ลูกใช้) · ประกาศ MinigameFinishedEvent · ปุ่มออก (ถ้าอนุญาต)
/// มินิเกมลูกเขียนแค่: OnBegin (เตรียมด่าน) · OnEnd (เก็บกวาด) · เรียก Finish(Success) ตอนชนะ
/// เซฟ: ผลสำเร็จเก็บเป็นธง (PlayMinigameAction ปักให้) · เล่นค้างครึ่งทางแล้วโหลด = เริ่มใหม่
/// </summary>
public abstract class MinigameBase : SceneIdentified<MinigameBase>
{
    [Header("แผงระหว่างเล่น (ไม่บังคับ)")]
    [Tooltip("แผงคำแนะนำ เช่น 'คลิกเลือกชิ้น · R หมุน · คลิกช่องเพื่อวาง' (ข้อความใช้ LocalizedText)")]
    [SerializeField] private UIPanelController hintPanel;

    [Tooltip("UI ที่ซ่อนระหว่างเล่น เช่น แถบ Hotbar — ซ่อนแบบ 'โปร่งใส' ไม่ปิด object (สคริปต์ของมันยังทำงานปกติ ไม่หลุดค่า)\n" +
             "ตัวที่ใส่ต้องมี CanvasGroup (ไม่มี = เตือนใน Console แล้วไม่ซ่อน) · จบเกมคืนค่าเดิมทุกอย่าง")]
    [SerializeField] private GameObject[] hideWhilePlaying;

    [Header("ออกกลางคัน")]
    [Tooltip("ปุ่มออก (None = ออกไม่ได้ ต้องเล่นจนจบ) · ห้ามใช้ Esc (ชนเมนูหยุดเกม)")]
    [SerializeField] private KeyCode exitKey = KeyCode.None;

    [Header("เสียง (ผ่าน IAudioService)")]
    [SerializeField] private AudioClip startSound;
    [SerializeField] private AudioClip successSound;

    public bool IsRunning { get; private set; }
    public MinigameResult Result { get; private set; }

    /// <summary>มีมินิเกมเล่นอยู่สักตัวไหม — ระบบอื่น (เช่น ปุ่มเลข/ลูกกลิ้ง Hotbar) ใช้เช็คเพื่อไม่รับปุ่มระหว่างเล่น</summary>
    public static bool AnyRunning => runningCount > 0;
    private static int runningCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => runningCount = 0;   // กันค่าค้างข้ามรอบ Play

    private readonly System.Collections.Generic.List<(CanvasGroup group, float alpha, bool raycasts, bool interactable)> hiddenGroups = new();

    /// <summary>หยุดเกม = ห้ามรับปุ่ม (ลูกต้องเช็คก่อนรับ input เสมอ)</summary>
    protected bool IsInputBlocked => !IsRunning || (PauseManager.Instance != null && PauseManager.Instance.IsPaused);

    public void Begin()
    {
        if (IsRunning) return;

        IsRunning = true;
        runningCount++;
        Result = MinigameResult.None;
        if (hintPanel != null) hintPanel.Show();
        HideUI();
        PlaySfx(startSound);

        OnBegin();
    }

    /// <summary>ออกกลางคัน (ปุ่มออก หรือระบบสั่ง)</summary>
    public void Abort() => Finish(MinigameResult.Aborted);

    protected void Finish(MinigameResult result)
    {
        if (!IsRunning) return;

        IsRunning = false;
        runningCount = Mathf.Max(0, runningCount - 1);
        Result = result;
        if (hintPanel != null) hintPanel.Hide();
        RestoreUI();
        if (result == MinigameResult.Success) PlaySfx(successSound);

        OnEnd(result);
        Debug.Log($"<color=#7fd4ff>🧩 [Minigame] '{SceneId}' จบ: {result}</color>");
        GameEventBus.Publish(new MinigameFinishedEvent(SceneId, result));
    }

    // ซ่อนแบบไม่ปิด object — ปิด object จะทำให้สคริปต์ของคนอื่น (Hotbar/Docking) หยุดฟังประกาศและหลุดค่า
    private void HideUI()
    {
        hiddenGroups.Clear();
        if (hideWhilePlaying == null) return;

        foreach (GameObject go in hideWhilePlaying)
        {
            if (go == null) continue;
            if (go.TryGetComponent(out CanvasGroup cg))
            {
                hiddenGroups.Add((cg, cg.alpha, cg.blocksRaycasts, cg.interactable));
                cg.alpha = 0f;
                cg.blocksRaycasts = false;
                cg.interactable = false;
            }
            else
            {
                // ไม่ปิดภาพทีละตัวแทน — สคริปต์ช่องของ Hotbar เปิด/ปิดไอคอนเองตามของในช่อง ถ้าเราไปคืนค่าทับจะได้ช่องว่างโชว์กรอบขาว
                Debug.LogWarning($"[Minigame] '{SceneId}' ซ่อน '{go.name}' ไม่ได้ — ต้องแปะ CanvasGroup ที่ตัวนั้นก่อน (Add Component > Canvas Group)", go);
            }
        }
    }

    private void RestoreUI()
    {
        foreach (var h in hiddenGroups)
        {
            if (h.group == null) continue;
            h.group.alpha = h.alpha;
            h.group.blocksRaycasts = h.raycasts;
            h.group.interactable = h.interactable;
        }
        hiddenGroups.Clear();
    }

    protected virtual void OnDisable()
    {
        // object ถูกปิด/ลบกลางเกม = ออกกลางคัน — คืน UI/เมาส์/กล้อง/ตัวนับ ไม่ให้ Hotbar หายค้าง
        if (IsRunning) Finish(MinigameResult.Aborted);
    }

    protected virtual void Update()
    {
        if (IsInputBlocked) return;
        if (exitKey != KeyCode.None && Input.GetKeyDown(exitKey)) Abort();
    }

    /// <summary>เตรียมด่าน (รีเซ็ตชิ้น/สุ่ม ฯลฯ)</summary>
    protected abstract void OnBegin();

    /// <summary>เก็บกวาดตอนจบ ไม่ว่าชนะหรือออก</summary>
    protected virtual void OnEnd(MinigameResult result) { }

    protected void PlaySfx(AudioClip clip)
    {
        if (clip == null) return;
        IAudioService audio = ServiceLocator.GetOptional<IAudioService>();
        audio?.PlaySFX(clip, transform.position, 1f, false);
    }
}
