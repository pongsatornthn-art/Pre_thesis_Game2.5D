using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class RadioTapeSystem : MonoBehaviour
{
    [Header("Audio")]
    public AudioSource radioAudioSource;
    public AudioClip[] tapes;
    public bool loopTape = false;
    [Range(0f, 1f)] public float volume = 1f;

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
    private bool isPaused;

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
        if (radioAudioSource == null || tapes == null) return;
        if (index < 0 || index >= tapes.Length || tapes[index] == null)
        {
            Debug.LogWarning($"[RadioTape] เทปม้วนที่ {index + 1} ยังไม่ได้ใส่ไฟล์เสียง");
            return;
        }

        // กดม้วนเดิมซ้ำ: ถ้าพักอยู่ให้เล่นต่อ ถ้าเล่นอยู่ก็ไม่ต้องทำอะไร
        if (index == CurrentTapeIndex)
        {
            if (isPaused) TogglePlayStop();
            return;
        }

        CurrentTapeIndex = index;
        isPaused = false;

        radioAudioSource.Stop();
        radioAudioSource.clip = tapes[index];
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
        for (int i = 0; i < tapeButtons.Length; i++)
        {
            if (tapeButtons[i] == null || tapeButtons[i].image == null) continue;
            tapeButtons[i].image.color = (i == CurrentTapeIndex) ? selectedColor : normalColor;
        }
    }
}