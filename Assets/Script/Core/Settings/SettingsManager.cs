using UnityEngine;

public class SettingsManager : MonoBehaviour
{
    private const string LANG_PREF_KEY = "Game_Language";
    private const string MOUSE_SENS_KEY = "Game_MouseSens";
    private const string VOLUME_KEY = "Game_Volume";
    
    private const string BGM_VOL_KEY = "Game_BGMVol";
    private const string SFX_VOL_KEY = "Game_SFXVol";
    
    // ค่าเริ่มต้น
    private string currentLanguage = "Thai";
    private float mouseSensitivity = 1.0f;
    private float audioVolume = 1.0f; // Master
    private float bgmVolume = 1.0f;
    private float sfxVolume = 1.0f;

    private void Awake()
    {
        LoadSettings();
    }

    private void Start()
    {
        var locService = ServiceLocator.Get<ILocalizationService>();
        if (locService != null) locService.SetLanguage(currentLanguage);
        
        ApplyMouseSensitivity();
        ApplyAudioVolume();
    }

    private void LoadSettings()
    {
        currentLanguage = PlayerPrefs.GetString(LANG_PREF_KEY, "Thai");
        mouseSensitivity = PlayerPrefs.GetFloat(MOUSE_SENS_KEY, 1.0f);
        audioVolume = PlayerPrefs.GetFloat(VOLUME_KEY, 1.0f);
        bgmVolume = PlayerPrefs.GetFloat(BGM_VOL_KEY, 1.0f);
        sfxVolume = PlayerPrefs.GetFloat(SFX_VOL_KEY, 1.0f);
    }

    public void SaveLanguage(string lang)
    {
        currentLanguage = lang;
        PlayerPrefs.SetString(LANG_PREF_KEY, lang);
        PlayerPrefs.Save(); 

        var locService = ServiceLocator.Get<ILocalizationService>();
        if (locService != null) locService.SetLanguage(currentLanguage);
    }

    public void SaveMouseSensitivity(float sens)
    {
        mouseSensitivity = sens;
        PlayerPrefs.SetFloat(MOUSE_SENS_KEY, sens);
        PlayerPrefs.Save();
        ApplyMouseSensitivity();
    }

    public void SaveAudioVolume(float vol)
    {
        audioVolume = vol;
        PlayerPrefs.SetFloat(VOLUME_KEY, vol);
        PlayerPrefs.Save();
        ApplyAudioVolume();
    }

    public void SaveBGMVolume(float vol)
    {
        bgmVolume = vol;
        PlayerPrefs.SetFloat(BGM_VOL_KEY, vol);
        PlayerPrefs.Save();
        ApplyAudioVolume(); // หยอดค่าให้ AudioService
    }

    public void SaveSFXVolume(float vol)
    {
        sfxVolume = vol;
        PlayerPrefs.SetFloat(SFX_VOL_KEY, vol);
        PlayerPrefs.Save();
        ApplyAudioVolume();
    }

    private void ApplyMouseSensitivity()
    {
        // TODO: โยงเข้ากับตัวแปรกล้องของ Player
        Debug.Log($"[Settings] อัปเดตความไวเมาส์เป็น {mouseSensitivity}");
    }

    private void ApplyAudioVolume()
    {
        var audioService = ServiceLocator.Get<IAudioService>();
        if (audioService != null)
        {
            audioService.SetMasterVolume(audioVolume);
            audioService.SetBGMVolume(bgmVolume);
            audioService.SetSFXVolume(sfxVolume);
        }
        Debug.Log($"[Settings] อัปเดต Master: {audioVolume}, BGM: {bgmVolume}, SFX: {sfxVolume}");
    }

    public string GetCurrentLanguage() => currentLanguage;
    public float GetMouseSensitivity() => mouseSensitivity;
    public float GetAudioVolume() => audioVolume;
}
