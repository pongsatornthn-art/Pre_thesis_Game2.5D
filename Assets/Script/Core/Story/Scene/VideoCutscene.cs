using System.Collections;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// [2026-11-24 นา] คัทซีนแบบ "ไฟล์วิดีโอ" (.mp4) — ใช้แทนคัทซีนต่อรูปได้ (สั่งเล่นด้วย Scene Id เหมือนคัทซีนแบบอื่น)
///
/// วิธีทำ (ของอยู่ในซีนทั้งหมด):
///   1. ใน Canvas สร้าง object แม่ "Video_PaintingMemory" แปะ CanvasGroup + UIPanelController + สคริปต์นี้ · ตั้ง Scene Id
///   2. ลูก = RawImage เต็มจอ (พื้นดำ) · ลาก Render Texture ใส่ช่อง Texture ของ RawImage
///   3. แปะ VideoPlayer (ตัวเดียวกันหรือลูก) → Render Mode = Render Texture · Target Texture = Render Texture อันเดียวกัน · ไม่ติ๊ก Play On Awake
///   4. ลากไฟล์วิดีโอใส่ช่อง Clip ของสคริปต์นี้ · ฉากเนื้อเรื่อง "เล่นคัทซีน" ใส่ Scene Id เดียวกัน
/// ปุ่มข้าม = จบทันที · กดหยุดเกม = วิดีโอหยุดตาม · จบเองเมื่อวิดีโอจบ
/// </summary>
public class VideoCutscene : CutsceneBase
{
    [Header("วิดีโอ")]
    [Tooltip("ไฟล์วิดีโอ (.mp4) — เว้นว่าง = ใช้ Clip ที่ตั้งใน VideoPlayer")]
    [SerializeField] private VideoClip clip;
    [Tooltip("ตัวเล่นวิดีโอในซีน")]
    [SerializeField] private VideoPlayer player;

    [Header("หน้าตา")]
    [Tooltip("ตัวเปิด/ปิดทั้งชุด (fade เหมือน UI อื่น) — เว้นว่าง = ใช้ CanvasGroup ของ object นี้")]
    [SerializeField] private UIPanelController panel;

    [Header("ปุ่ม")]
    [Tooltip("ข้ามทั้งชุด (None = ข้ามไม่ได้) · ห้ามใช้ Esc (ชนเมนูหยุดเกม)")]
    [SerializeField] private KeyCode skipKey = KeyCode.Tab;
    [Tooltip("ต้องกดค้างกี่วิถึงข้าม (กันกดพลาด) — 0 = กดทีเดียวข้าม")]
    [SerializeField, Min(0f)] private float holdToSkipSeconds = 0.6f;

    private Coroutine running;
    private bool skipRequested;
    private float skipHeld;
    private bool started;   // เริ่มเล่นจริงแล้ว (ก่อนนั้น Update ห้ามสั่ง Play เอง)

    public override bool IsPlaying => running != null;

    protected override void Awake()
    {
        base.Awake();
        if (player == null) player = GetComponentInChildren<VideoPlayer>(true);
        if (player != null) { player.playOnAwake = false; player.isLooping = false; }
        // แผงอยู่ก้อนเดียวกับสคริปต์ = ห้าม HideImmediate (ปิดก้อนแล้วเริ่ม coroutine ไม่ได้) → ซ่อนด้วย alpha
        if (panel != null && panel.gameObject != gameObject) panel.HideImmediate();
        else SetSelfAlpha(false);
    }

    private void Update()
    {
        if (running == null || player == null) return;

        bool paused = PauseManager.Instance != null && PauseManager.Instance.IsPaused;
        if (paused && player.isPlaying) player.Pause();
        else if (!paused && started && !player.isPlaying && player.isPrepared && !skipRequested) player.Play();
        if (paused) return;

        if (skipKey == KeyCode.None) return;
        if (Input.GetKey(skipKey))
        {
            skipHeld += Time.unscaledDeltaTime;
            if (skipHeld >= holdToSkipSeconds) skipRequested = true;
        }
        else skipHeld = 0f;
    }

    public override void Play()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (!gameObject.activeInHierarchy || player == null)
        {
            Debug.LogWarning($"[VideoCutscene] '{name}' เล่นไม่ได้ — {(player == null ? "ยังไม่ได้ใส่ VideoPlayer" : "อยู่ใต้ object ที่ปิดอยู่")}", this);
            return;
        }
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(PlayRoutine());
    }

    public override void Skip() => skipRequested = true;

    private IEnumerator PlayRoutine()
    {
        skipRequested = false;
        skipHeld = 0f;
        started = false;
        if (clip != null) player.clip = clip;
        if (player.clip == null && string.IsNullOrEmpty(player.url))
        {
            Debug.LogWarning($"[VideoCutscene] '{name}' ยังไม่ได้ใส่ไฟล์วิดีโอ — ข้าม", this);
            running = null;
            yield break;
        }

        player.Prepare();
        while (!player.isPrepared && !skipRequested) yield return null;

        if (panel != null) panel.Show(); else SetSelfAlpha(true);
        if (!skipRequested) { player.Play(); started = true; }

        // จบเมื่อ: เล่นถึงท้ายคลิป หรือกดข้าม (ใช้เวลาจริง ไม่สนการหยุดเวลาในเกม)
        double length = player.length;
        while (!skipRequested)
        {
            if (player.isPrepared && length > 0 && player.time >= length - 0.05) break;
            if (player.isPrepared && !player.isPlaying && player.frame > 0 && (ulong)player.frame >= player.frameCount - 1) break;
            yield return null;
        }

        started = false;
        player.Stop();
        running = null;
        if (panel != null) panel.Hide(); else SetSelfAlpha(false);
    }

    private void SetSelfAlpha(bool visible)
    {
        CanvasGroup g = GetComponent<CanvasGroup>();
        if (g == null) return;
        g.alpha = visible ? 1f : 0f;
        g.interactable = visible;
        g.blocksRaycasts = visible;
    }
}
