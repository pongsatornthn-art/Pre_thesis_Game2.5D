using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// คัทซีน Timeline ที่ฉากเนื้อเรื่องสั่งเล่นได้ — แปะคู่กับ PlayableDirector ที่วางในซีน แล้วตั้ง Scene Id
/// ฉากสั่งด้วย "คัทซีน/เล่นคัทซีน" ใส่ชื่อเดียวกัน · Timeline ทำ/แก้ในหน้าต่าง Timeline ตามปกติ
///
/// ⚠️ ปิด "Play On Awake" ของ PlayableDirector ไว้ ไม่งั้นคัทซีนเล่นเองตอนเริ่มซีน
/// คัทซีนที่เล่นตอนเดินชนกล่อง ใช้ AutoTriggerTimeline ของปอได้ตามเดิม ไม่ต้องแปะตัวนี้
/// </summary>
[RequireComponent(typeof(PlayableDirector))]
public class StoryCutscene : CutsceneBase
{
    [Tooltip("ปุ่มข้ามคัทซีน (None = ข้ามไม่ได้)")]
    [SerializeField] private KeyCode skipKey = KeyCode.None;

    private PlayableDirector director;

    private PlayableDirector Director => director != null ? director : director = GetComponent<PlayableDirector>();

    protected override void Awake()
    {
        base.Awake();
        if (Director.playOnAwake)
        {
            Debug.LogWarning($"[StoryCutscene] '{name}' เปิด Play On Awake ไว้ — คัทซีนจะเล่นเองตอนเริ่มซีน ควรปิด", this);
        }
    }

    private void Update()
    {
        if (skipKey == KeyCode.None || !IsPlaying) return;
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;
        if (Input.GetKeyDown(skipKey)) Skip();
    }

    public override void Play()
    {
        Director.time = 0;
        Director.Play();
    }

    public override bool IsPlaying => Director.state == PlayState.Playing;

    public override void Skip()
    {
        // กระโดดไปเฟรมสุดท้าย ให้ทุกแถบ (กล้อง/เปิดปิดของ/สัญญาณ) อยู่สภาพจบ แล้วหยุด
        Director.time = Director.duration;
        Director.Evaluate();
        Director.Stop();
    }
}
