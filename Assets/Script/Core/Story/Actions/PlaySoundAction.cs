using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Action สำหรับเล่นเสียง SFX ผ่าน IAudioService ของโปรเจกต์
/// สามารถเลือกว่าจะรอให้เสียงเล่นจบก่อนไป Action ถัดไป หรือเล่นแล้วผ่านทันที
/// </summary>
[Serializable]
public class PlaySoundAction : IStoryAction
{
    [Tooltip("คลิปเสียงที่ต้องการเล่น")]
    public AudioClip clip;

    [Tooltip("ระดับความดัง (0 ถึง 1)")]
    [Range(0f, 1f)]
    public float volume = 1f;

    [Tooltip("รอให้เสียงเล่นจบก่อนดำเนินเหตุการณ์ถัดไปหรือไม่")]
    public bool waitFinish = false;

    public IEnumerator Execute(StoryContext ctx)
    {
        if (clip != null)
        {
            IAudioService audio = ServiceLocator.Get<IAudioService>();
            Vector3 soundPos = ctx?.Player != null ? ctx.Player.transform.position : Vector3.zero;

            audio?.PlaySFX(clip, soundPos, volume, false);

            if (waitFinish && clip.length > 0f)
            {
                // ใช้ WaitForSecondsRealtime ตามเกณฑ์เพื่อความแม่นยำแม้เกมจะหยุดเวลา
                yield return new WaitForSecondsRealtime(clip.length);
            }
        }
    }
}
