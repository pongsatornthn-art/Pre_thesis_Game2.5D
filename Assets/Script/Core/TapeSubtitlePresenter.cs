using TMPro;
using UnityEngine;

/// <summary>
/// [Claude 2026-11-28] ซับไตเติลตอนฟังเทปที่วิทยุ (งานที่ปอขอ "ใส่ซับให้เทปทั้ง 3 อันตอนฟังเสียง")
/// อ่านเวลาเสียงจาก RadioTapeSystem → หาบรรทัดใน TapeItemData.subtitles → โชว์ข้อความตามภาษาเกม
///
/// วางในซีน: Canvas ของตัวเอง (ไม่อยู่ใต้ UI_Manager — นั่งโต๊ะแล้ว UI_Manager ถูกซ่อน)
///   TapeSubtitles_Canvas (สคริปต์นี้)
///     └ Panel  CanvasGroup (แถบดำโปร่งด้านล่าง)
///         └ Text (TextMeshPro)
/// ลาก RadioTapeSystem ใส่ช่อง Radio (เว้นว่าง = หาเองในซีน)
/// แก้ข้อความ = LocalizationData.csv key TAPE1_* / TAPE2_* / TAPE3_* · แก้เวลา = ไฟล์เทปใน Inspector
/// </summary>
public class TapeSubtitlePresenter : MonoBehaviour
{
    [SerializeField] private RadioTapeSystem radio;
    [SerializeField] private CanvasGroup panel;
    [SerializeField] private TMP_Text text;

    [Tooltip("สีชื่อคนพูด")]
    [SerializeField] private Color speakerColor = new Color(0.85f, 0.7f, 0.42f, 1f);
    [Tooltip("ความเร็วจาง เข้า/ออก")]
    [SerializeField, Min(0.1f)] private float fadeSpeed = 10f;

    private TapeSubtitleLine shown;
    private ILocalizationService loc;

    private void Awake()
    {
        if (radio == null) radio = FindFirstObjectByType<RadioTapeSystem>(FindObjectsInactive.Include);
        if (radio == null) Debug.LogWarning("[TapeSubtitle] ไม่เจอ RadioTapeSystem ในซีน — ซับจะไม่ขึ้น", this);
        if (panel != null) { panel.alpha = 0f; panel.blocksRaycasts = false; panel.interactable = false; }
        if (text != null) text.raycastTarget = false;
    }

    private void OnEnable()
    {
        loc = ServiceLocator.GetOptional<ILocalizationService>();
        if (loc != null) loc.OnLanguageChanged += Redraw;
    }

    private void OnDisable()
    {
        if (loc != null) loc.OnLanguageChanged -= Redraw;
    }

    private void Update()
    {
        TapeSubtitleLine line = CurrentLine();
        if (line != shown)
        {
            shown = line;
            Redraw();
        }

        if (panel != null)
        {
            float target = shown != null ? 1f : 0f;
            panel.alpha = Mathf.MoveTowards(panel.alpha, target, Time.unscaledDeltaTime * fadeSpeed);
        }
    }

    private TapeSubtitleLine CurrentLine()
    {
        if (radio == null || radio.radioAudioSource == null || !radio.IsPlaying) return null;
        int i = radio.CurrentTapeIndex;
        if (radio.tapeItems == null || i < 0 || i >= radio.tapeItems.Length || radio.tapeItems[i] == null) return null;
        return radio.tapeItems[i].LineAt(radio.radioAudioSource.time);
    }

    private void Redraw()
    {
        if (text == null || shown == null) return;   // จางออก = คงข้อความเดิมไว้ระหว่างจาง
        if (loc == null) loc = ServiceLocator.GetOptional<ILocalizationService>();

        string body = Loc(shown.textKey);
        if (!string.IsNullOrEmpty(shown.speakerKey))
            body = $"<color=#{ColorUtility.ToHtmlStringRGB(speakerColor)}>{Loc(shown.speakerKey)}:</color> {body}";
        text.text = body;
        if (loc != null) text.font = loc.GetFont(FontCategory.Dialog);
    }

    private string Loc(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        return loc != null ? loc.GetText(key) : key;
    }
}
