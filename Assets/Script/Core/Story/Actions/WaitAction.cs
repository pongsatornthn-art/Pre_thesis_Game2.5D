using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Action สำหรับหน่วงเวลาระหว่างเหตุการณ์
/// ใช้ WaitForSecondsRealtime เพื่อป้องกันปัญหากรณีเกมหยุดเวลา (Pause) ทำให้ Coroutine ค้าง
/// </summary>
[Serializable]
public class WaitAction : IStoryAction
{
    [Tooltip("ระยะเวลาที่ต้องรอ (วินาที) อิงตามเวลาจริง Realtime")]
    public float duration = 1f;

    public IEnumerator Execute(StoryContext ctx)
    {
        if (duration > 0f)
        {
            yield return new WaitForSecondsRealtime(duration);
        }
    }
}
