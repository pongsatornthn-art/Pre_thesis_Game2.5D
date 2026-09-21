using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Action สำหรับหน่วงเวลารอจนกว่า Story Flag ที่กำหนดจะถูกตั้งค่าขึ้นมา
/// ใช้สำหรับเหตุการณ์คัตซีนที่ต้องการรอให้ผู้เล่นทำสิ่งใดสิ่งหนึ่งในฉากก่อนดำเนินเหตุการณ์ต่อ
/// </summary>
[Serializable]
public class WaitForFlagAction : IStoryAction
{
    [Tooltip("ธงที่ต้องรอให้ถูกตั้งค่า (Has = true)")]
    public StoryFlagId flag;

    [Tooltip("ระยะเวลาจำกัดสูงสุด (วินาที) — 0 คือรอเรื่อยๆ จนกว่าธงจะขึ้น")]
    public float timeoutSeconds = 0f;

    public IEnumerator Execute(StoryContext ctx)
    {
        if (flag == null || ctx?.Flags == null) yield break;

        float elapsed = 0f;

        while (!ctx.Flags.Has(flag))
        {
            if (timeoutSeconds > 0f && elapsed >= timeoutSeconds)
            {
                Debug.LogWarning($"[WaitForFlagAction] หมดเวลารอธง '{flag.flagId}' ({timeoutSeconds} วินาที)");
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }
}
