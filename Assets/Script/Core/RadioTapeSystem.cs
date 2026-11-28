using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class RadioTapeSystem : MonoBehaviour
{
    [Header("Audio")]
    public AudioSource radioAudioSource;
    public AudioClip[] tapes;
    [Tooltip("เทปเฉลยต้องปิดไว้ ไม่งั้นเทปไม่จบและมินิเกมไม่ออก")]
    public bool loopTape = false;
    [Range(0f, 1f)] public float volume = 1f;

    // [Claude 2026-11-26] ต้องเก็บเทปในฉากก่อน ถึงจะเล่นได้
    [Header("เทปที่ต้องเก็บก่อน (เรียงตามปุ่ม)")]
    [Tooltip("ไฟล์เทป (TapeItemData) ช่องที่ 1 = ปุ่มที่ 1 · ใส่แล้วปุ่มจะล็อกจนกว่าจะเก็บเทปม้วนนั้นในฉาก\nเว้นว่าง = ปุ่มนั้นเล่นได้เลย (แบบเดิม)")]
    public TapeItemData[] tapeItems;
    public Color lockedColor = new Color(0.35f, 0.35f, 0.35f, 1f);
    [Tooltip("เปลี่ยนข้อความบนปุ่มเป็นชื่อเทป / ข้อความล็อก\nปิดไว้ถ้าปุ่มเป็นรูปตัวเลขอยู่แล้ว (กันตัวหนังสือล้นปุ่ม)")]
    public bool changeButtonText = false;
    [Tooltip("ข้อความบนปุ่มตอนยังไม่มีเทป (ใช้เมื่อเปิด Change Button Text)")]
    public string lockedLabel = "- - -";
    [Tooltip("ข้อความแจ้งตอนกดปุ่มที่ยังไม่มีเทป (เว้นว่างได้)")]
    public TMP_Text lockedHintText;
    public string lockedHintMessage = "ยังไม่มีเทปม้วนนี้";

    [Header("UI (ผูกปุ่มให้อัตโนมัติ ไม่ต้องตั้ง OnClick เอง)")]
    [Tooltip("ปุ่มเทปเรียงตามลำดับ ปุ่มที่ 1 = tapes[0]")]
    public Button[] tapeButtons;
    public Button playPauseButton;
    public Color normalColor = Color.white;
    public Color selectedColor = new Color(1f, 0.85f, 0.3f);

    [Header("Events")]
    public UnityEvent<int> onTapeSelected;
    [Tooltip("เทปม้วนที่เป็นเฉลยของปริศนา (-1 = ไม่ใช้)")]
    public int puzzleTapeIndex = -1;
    [Tooltip("เรียกตอนเลือกเทปเฉลย เช่น ผูกกับ RadioMiniGameManager.SolvePuzzleAndExit")]
    public UnityEvent onPuzzleTapeSelected;

    public int CurrentTapeIndex { get; private set; } = -1;
    public bool IsPlaying => radioAudioSource != null && radioAudioSource.isPlaying;

    private bool isPaused;
    private string[] originalLabels;
    private float hintHideAt;

    /// <summary>ปุ่มนี้เปิดได้หรือยัง (ไม่มีไฟล์เทปผูก = เปิดได้เลย)</summary>
    public bool IsUnlocked(int index)
    {
        if (tapeItems == null || index < 0 || index >= tapeItems.Length || tapeItems[index] == null) return true;
        return tapeItems[index].IsCollected();
    }

    private AudioClip ClipAt(int index)
    {
        if (tapeItems != null && index >= 0 && index < tapeItems.Length && tapeItems[index] != null && tapeItems[index].clip != null)
            return tapeItems[index].clip;
        return tapes != null && index >= 0 && index < tapes.Length ? tapes[index] : null;
    }

    private void OnEnable()
    {
        // เปิดหน้าวิทยุทุกครั้ง → เช็กใหม่ว่าเก็บเทปม้วนไหนมาแล้วบ้าง
        IStoryFlags flags = ServiceLocator.GetOptional<IStoryFlags>();
        if (flags != null) flags.OnChanged += RefreshButtons;
        RefreshButtons();
        if (lockedHintText != null) lockedHintText.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        IStoryFlags flags = ServiceLocator.GetOptional<IStoryFlags>();
        if (flags != null) flags.OnChanged -= RefreshButtons;
    }

    private void Update()
    {
        if (lockedHintText != null && lockedHintText.gameObject.activeSelf && Time.unscaledTime >= hintHideAt)
            lockedHintText.gameObject.SetActive(false);
    }

    private void Awake()
    {
        if (radioAudioSource == null) radioAudioSource = GetComponent<AudioSource>();

        if (radioAudioSource != null)
        {
            radioAudioSource.playOnAwake = false;
            radioAudioSource.spatialBlend = 0f; // 2D กันเสียงเบาตามระยะกล้อง
            radioAudioSource.volume = volume;
        }
        else
        {
            Debug.LogError("[RadioTape] ไม่พบ AudioSource");
        }

        for (int i = 0; i < tapeButtons.Length; i++)
        {
            if (tapeButtons[i] == null) continue;
            int captured = i;
            tapeButtons[i].onClick.AddListener(() => SelectTape(captured));
        }

        if (playPauseButton != null) playPauseButton.onClick.AddListener(TogglePlayStop);

        RefreshButtons();
    }

    public void SelectTape(int index)
    {
        if (radioAudioSource == null) return;
        if (!IsUnlocked(index))
        {
            if (lockedHintText != null)
            {
                lockedHintText.text = lockedHintMessage;
                lockedHintText.gameObject.SetActive(true);
                hintHideAt = Time.unscaledTime + 2f;
            }
            return;
        }
        AudioClip clip = ClipAt(index);
        if (clip == null)
        {
            Debug.LogWarning($"[RadioTape] เทปม้วนที่ {index + 1} ยังไม่ได้ใส่ไฟล์เสียง");
            return;
        }

        // กดม้วนเดิมซ้ำ: พักอยู่ให้เล่นต่อ / เล่นจบแล้วให้เล่นใหม่ / กำลังเล่นอยู่ไม่ต้องทำอะไร
        if (index == CurrentTapeIndex)
        {
            if (isPaused) TogglePlayStop();
            else if (!radioAudioSource.isPlaying) radioAudioSource.Play();
            return;
        }

        CurrentTapeIndex = index;
        isPaused = false;

        radioAudioSource.Stop();
        radioAudioSource.clip = clip;
        radioAudioSource.loop = loopTape;
        radioAudioSource.Play();

        RefreshButtons();
        onTapeSelected?.Invoke(index);

        if (index == puzzleTapeIndex) onPuzzleTapeSelected?.Invoke();
    }

    public void TogglePlayStop()
    {
        if (radioAudioSource == null || radioAudioSource.clip == null) return;

        if (radioAudioSource.isPlaying)
        {
            radioAudioSource.Pause();
            isPaused = true;
        }
        else
        {
            radioAudioSource.UnPause();
            if (!radioAudioSource.isPlaying) radioAudioSource.Play();
            isPaused = false;
        }
    }

    public void StopTape()
    {
        if (radioAudioSource != null) radioAudioSource.Stop();
        CurrentTapeIndex = -1;
        isPaused = false;
        RefreshButtons();
    }

    private void RefreshButtons()
    {
        if (tapeButtons == null) return;
        if (originalLabels == null || originalLabels.Length != tapeButtons.Length)
        {
            originalLabels = new string[tapeButtons.Length];
            for (int i = 0; i < tapeButtons.Length; i++)
            {
                TMP_Text t = tapeButtons[i] != null ? tapeButtons[i].GetComponentInChildren<TMP_Text>(true) : null;
                originalLabels[i] = t != null ? t.text : null;
            }
        }
        for (int i = 0; i < tapeButtons.Length; i++)
        {
            if (tapeButtons[i] == null) continue;
            bool unlocked = IsUnlocked(i);
            if (tapeButtons[i].image != null)
                tapeButtons[i].image.color = !unlocked ? lockedColor : (i == CurrentTapeIndex) ? selectedColor : normalColor;
            TMP_Text label = changeButtonText ? tapeButtons[i].GetComponentInChildren<TMP_Text>(true) : null;
            if (label != null && originalLabels[i] != null)
            {
                // ชื่อเทป: ใช้ชื่อในไฟล์เทป (ถ้ามี) ไม่งั้นใช้ข้อความเดิมบนปุ่ม
                string name = tapeItems != null && i < tapeItems.Length && tapeItems[i] != null ? tapeItems[i].DisplayName : originalLabels[i];
                label.text = unlocked ? name : lockedLabel;
            }
        }
    }
}