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

    [Header("ออกกลางคัน")]
    [Tooltip("ปุ่มออก (None = ออกไม่ได้ ต้องเล่นจนจบ) · ห้ามใช้ Esc (ชนเมนูหยุดเกม)")]
    [SerializeField] private KeyCode exitKey = KeyCode.None;

    [Header("เสียง (ผ่าน IAudioService)")]
    [SerializeField] private AudioClip startSound;
    [SerializeField] private AudioClip successSound;

    public bool IsRunning { get; private set; }
    public MinigameResult Result { get; private set; }

    /// <summary>หยุดเกม = ห้ามรับปุ่ม (ลูกต้องเช็คก่อนรับ input เสมอ)</summary>
    protected bool IsInputBlocked => !IsRunning || (PauseManager.Instance != null && PauseManager.Instance.IsPaused);

    public void Begin()
    {
        if (IsRunning) return;

        IsRunning = true;
        Result = MinigameResult.None;
        if (hintPanel != null) hintPanel.Show();
        PlaySfx(startSound);

        OnBegin();
    }

    /// <summary>ออกกลางคัน (ปุ่มออก หรือระบบสั่ง)</summary>
    public void Abort() => Finish(MinigameResult.Aborted);

    protected void Finish(MinigameResult result)
    {
        if (!IsRunning) return;

        IsRunning = false;
        Result = result;
        if (hintPanel != null) hintPanel.Hide();
        if (result == MinigameResult.Success) PlaySfx(successSound);

        OnEnd(result);
        Debug.Log($"<color=#7fd4ff>🧩 [Minigame] '{SceneId}' จบ: {result}</color>");
        GameEventBus.Publish(new MinigameFinishedEvent(SceneId, result));
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
