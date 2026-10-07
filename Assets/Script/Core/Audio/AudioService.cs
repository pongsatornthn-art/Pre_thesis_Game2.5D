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
    [Tooltip("ลำโพง 2D สำหรับเสียงเมนู — กดคลิกขวาที่คอมโพเนนต์ → 'สร้างลำโพงให้ครบ' ถ้ายังไม่มี")]
    [SerializeField] private AudioSource uiSource;

    [Header("BGM")]
    public AudioMixerGroup bgmMixerGroup;
    [SerializeField] private AudioSource bgmSource;

    [Header("SFX Pooling")]
    public AudioMixerGroup sfxMixerGroup;
    [Tooltip("จำนวนลำโพง SFX ที่สร้างตอนกด 'สร้างลำโพงให้ครบ'")]
    public int sfxPoolSize = 10;
    [Tooltip("ลำโพง SFX ที่เวียนกันเล่น (AAA Object Pooling) — เป็น object ลูกในก้อนนี้ เห็น/ปรับได้ใน Inspector")]
    [SerializeField] private AudioSource[] sfxPool = new AudioSource[0];
    private int sfxPoolIndex = 0;

    private void Awake()
    {
        // 1. ลงทะเบียนเป็น Service ให้คนทั้งเกมเรียกใช้ได้
        ServiceLocator.Register<IAudioService>(this);

        // [2026-10-07] เลิกสร้างลำโพงด้วยโค้ดตอนเริ่มเกม — ทุกตัวต้องอยู่ใน prefab แก้ได้ใน Inspector (กติกาเจ้าของ)
        if (uiSource == null || bgmSource == null || sfxPool == null || sfxPool.Length == 0)
        {
            Debug.LogError("[AudioService] ลำโพงยังไม่ครบ — คลิกขวาที่คอมโพเนนต์ AudioService → 'สร้างลำโพงให้ครบ' (ทำใน Editor ครั้งเดียว)", this);
        }
    }

#if UNITY_EDITOR
    /// <summary>สร้างลำโพงทั้งหมดเป็น component/object จริงใน Editor — กดครั้งเดียว แล้วเซฟ prefab</summary>
    [ContextMenu("สร้างลำโพงให้ครบ")]
    private void CreateSpeakersInEditor()
    {
        if (uiSource == null)
        {
            uiSource = UnityEditor.Undo.AddComponent<AudioSource>(gameObject);
            uiSource.outputAudioMixerGroup = uiMixerGroup;
            uiSource.spatialBlend = 0f;
            uiSource.playOnAwake = false;
        }
        if (bgmSource == null)
        {
            bgmSource = UnityEditor.Undo.AddComponent<AudioSource>(gameObject);
            bgmSource.outputAudioMixerGroup = bgmMixerGroup;
            bgmSource.spatialBlend = 0f;
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
        }
        if (sfxPool == null || sfxPool.Length == 0)
        {
            sfxPool = new AudioSource[sfxPoolSize];
            for (int i = 0; i < sfxPoolSize; i++)
            {
                GameObject sfxObj = new GameObject($"SFX_Pool_{i}");
                UnityEditor.Undo.RegisterCreatedObjectUndo(sfxObj, "Create SFX speaker");
                sfxObj.transform.SetParent(transform, false);
                AudioSource src = sfxObj.AddComponent<AudioSource>();
                src.outputAudioMixerGroup = sfxMixerGroup;
                src.playOnAwake = false;
                src.spatialBlend = 1f;
                sfxPool[i] = src;
            }
        }
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

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
        if (clip == null || sfxPool == null || sfxPool.Length == 0) return;

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
        sfxPoolIndex = (sfxPoolIndex + 1) % sfxPool.Length;
    }

    public void PlayBGM(AudioClip clip, float fadeTime = 1f)
    {
        if (bgmSource == null || bgmSource.clip == clip) return; // ยังไม่มีลำโพง / เป็นเพลงเดิมไม่ต้องเปลี่ยน

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
