using UnityEngine;
using TMPro;

[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [SerializeField] private string localizationKey;
    [SerializeField] private FontCategory fontCategory = FontCategory.Default;
    
    private TMP_Text textComponent;
    private ILocalizationService localizationService;

    private void Start()
    {
        textComponent = GetComponent<TMP_Text>();
        localizationService = ServiceLocator.Get<ILocalizationService>();

        if (localizationService != null)
        {
            // ดักฟัง Event ถ้าผู้เล่นกดเปลี่ยนภาษา ให้มาอัปเดต UI ด้วย
            localizationService.OnLanguageChanged += UpdateText;
            
            // อัปเดตข้อความครั้งแรกตอนเริ่มเกม
            UpdateText(); 
        }
        else
        {
            Debug.LogWarning("LocalizedText: ไม่พบ LocalizationService กรุณาเช็คว่ามีในฉากหรือไม่");
        }
    }

    private void OnDestroy()
    {
        if (localizationService != null)
        {
            localizationService.OnLanguageChanged -= UpdateText;
        }
    }

    // เผื่อต้องเปลี่ยน Key ผ่านโค้ดทีหลัง
    public void SetKey(string newKey)
    {
        localizationKey = newKey;
        UpdateText();
    }

    private void UpdateText()
    {
        if (localizationService != null)
        {
            // 1. อัปเดตข้อความ
            if (!string.IsNullOrEmpty(localizationKey))
            {
                textComponent.text = localizationService.GetText(localizationKey);
            }

            // 2. อัปเดตฟอนต์ (ถ้ามีฟอนต์ใน Service)
            TMPro.TMP_FontAsset newFont = localizationService.GetFont(fontCategory);
            if (newFont != null)
            {
                textComponent.font = newFont;
            }
        }
    }
}
