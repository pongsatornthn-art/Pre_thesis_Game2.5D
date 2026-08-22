using UnityEngine;

// กำหนดประเภทเสียงปุ่มกดในเมนู
public enum MenuSoundType 
{ 
    Hover, 
    Click, 
    Back, 
    Error 
}

public interface IAudioService
{
    // ระบบเล่นเสียงปุ่มเมนู (2D)
    void PlayMenuSound(MenuSoundType type);
    
    // ระบบเล่นเสียง SFX ทั่วไป (3D) พร้อมเปิดปิดระบบสุ่ม Pitch ได้
    void PlaySFX(AudioClip clip, Vector3 position, float volume = 1f, bool randomizePitch = true);
    
    // ระบบเล่นเพลงบรรยากาศ (2D) พร้อม Fade ตอนสลับเพลง
    void PlayBGM(AudioClip clip, float fadeTime = 1f);

    // ระบบปรับระดับเสียง AudioMixer
    void SetMasterVolume(float volume);
    void SetBGMVolume(float volume);
    void SetSFXVolume(float volume);
}
