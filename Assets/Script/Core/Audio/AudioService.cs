using UnityEngine;
using UnityEngine.Audio;
using System.Collections;

public class AudioService : MonoBehaviour, IAudioService
{
    [Header("Mixer")]
    public AudioMixer mainMixer;
    private const string MIXER_MASTER = "MasterVol";
    private const string MIXER_BGM = "BGMVol";
    private const string MIXER_SFX = "SFXVol";

    [Header("Menu Sounds")]
    public MenuSoundThemeSO menuTheme;
    public AudioMixerGroup uiMixerGroup; 
    private AudioSource uiSource; // ลำโพง 2D สำหรับ UI

    [Header("BGM")]
    public AudioMixerGroup bgmMixerGroup;
    private AudioSource bgmSource; 

    [Header("SFX Pooling")]
    public AudioMixerGroup sfxMixerGroup;
    [Tooltip("จำนวนลำโพง SFX ที่เตรียมไว้เวียนกันเล่น (AAA Object Pooling)")]
    public int sfxPoolSize = 10;
    private AudioSource[] sfxPool;
    private int sfxPoolIndex = 0;

    private void Awake()
    {
        // 1. ลงทะเบียนเป็น Service ให้คนทั้งเกมเรียกใช้ได้
        ServiceLocator.Register<IAudioService>(this);

        // 2. สร้างลำโพง UI (เสียงแบนๆ ไม่สนระยะทาง 2D)
        uiSource = gameObject.AddComponent<AudioSource>();
        uiSource.outputAudioMixerGroup = uiMixerGroup;
        uiSource.spatialBlend = 0f; 
        uiSource.playOnAwake = false;

        // 3. สร้างลำโพง BGM
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.outputAudioMixerGroup = bgmMixerGroup;
        bgmSource.spatialBlend = 0f; 
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;

        // 4. สร้างคลังลำโพง SFX ล่วงหน้า (ไม่ต้องไปสร้างใหม่ทีละอันตอนยิงปืน)
        sfxPool = new AudioSource[sfxPoolSize];
        for (int i = 0; i < sfxPoolSize; i++)
        {
            GameObject sfxObj = new GameObject($"SFX_Pool_{i}");
            sfxObj.transform.SetParent(transform);
            AudioSource src = sfxObj.AddComponent<AudioSource>();
            src.outputAudioMixerGroup = sfxMixerGroup;
            src.playOnAwake = false;
            sfxPool[i] = src;
        }
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<IAudioService>();
    }

    public void PlayMenuSound(MenuSoundType type)
    {
        if (menuTheme == null || uiSource == null) return;

        AudioClip clip = menuTheme.GetSound(type);
        if (clip != null)
        {
            uiSource.PlayOneShot(clip); // เสียงทับซ้อนกันได้
        }
    }

    public void PlaySFX(AudioClip clip, Vector3 position, float volume = 1f, bool randomizePitch = true)
    {
        if (clip == null) return;

        // หยิบลำโพงตัวที่ว่างในคิวมาใช้
        AudioSource src = sfxPool[sfxPoolIndex];
        
        src.transform.position = position;
        src.clip = clip;
        src.volume = volume;
        src.spatialBlend = 1f; // ทำให้เป็นเสียง 3D ตามระยะห่างเป๊ะๆ

        // 🎲 ลูกเล่น AAA: สุ่มความเพี้ยนของเสียงนิดนึง หูจะได้ไม่ล้า
        if (randomizePitch)
        {
            src.pitch = Random.Range(0.9f, 1.1f);
        }
        else
        {
            src.pitch = 1f;
        }

        src.Play();

        // เลื่อนคิวไปใช้ลำโพงตัวถัดไปรอบหน้า
        sfxPoolIndex = (sfxPoolIndex + 1) % sfxPoolSize;
    }

    public void PlayBGM(AudioClip clip, float fadeTime = 1f)
    {
        if (bgmSource.clip == clip) return; // ถ้าเป็นเพลงเดิมไม่ต้องเปลี่ยน

        StartCoroutine(FadeBGM(clip, fadeTime));
    }

    private IEnumerator FadeBGM(AudioClip newClip, float fadeTime)
    {
        float startVol = bgmSource.volume;
        
        // ค่อยๆ หรี่เพลงเก่าจนดับ
        while (bgmSource.volume > 0)
        {
            bgmSource.volume -= startVol * Time.deltaTime / fadeTime;
            yield return null;
        }

        bgmSource.clip = newClip;
        bgmSource.Play();

        // ค่อยๆ ดังขึ้นเป็นเพลงใหม่
        while (bgmSource.volume < startVol)
        {
            bgmSource.volume += startVol * Time.deltaTime / fadeTime;
            yield return null;
        }
        bgmSource.volume = startVol;
    }

    // ==========================================
    // ท่อน้ำเสียง (Audio Mixer Volumes)
    // การปรับเสียงต้องแปลงเป็น Logarithmic 
    // ==========================================
    private float VolumeToDb(float volume)
    {
        if (volume <= 0.0001f) return -80f; // ถ้าน้อยมากคือเงียบกริบ
        return Mathf.Log10(volume) * 20f;
    }

    public void SetMasterVolume(float volume)
    {
        if (mainMixer != null) mainMixer.SetFloat(MIXER_MASTER, VolumeToDb(volume));
    }

    public void SetBGMVolume(float volume)
    {
        if (mainMixer != null) mainMixer.SetFloat(MIXER_BGM, VolumeToDb(volume));
    }

    public void SetSFXVolume(float volume)
    {
        if (mainMixer != null) mainMixer.SetFloat(MIXER_SFX, VolumeToDb(volume));
    }
}
