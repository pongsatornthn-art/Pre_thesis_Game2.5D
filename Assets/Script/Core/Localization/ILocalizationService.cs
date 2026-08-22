using System;

public enum FontCategory
{
    Default,
    Header,
    Button,
    Dialog
}

public interface ILocalizationService
{
    string CurrentLanguage { get; }
    void SetLanguage(string language);
    string GetText(string key);
    TMPro.TMP_FontAsset GetFont(FontCategory category);
    event Action OnLanguageChanged;
}
