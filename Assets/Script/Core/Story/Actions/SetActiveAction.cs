using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Action สำหรับเปิดหรือปิดการทำงานของ GameObject ในฉาก (เช่น เปิดประตูทางลับ / ซ่อนไอเทม)
/// </summary>
[Serializable]
public class SetActiveAction : IStoryAction
{
    [Tooltip("วัตถุในฉากที่ต้องการเปิดหรือปิด")]
    public GameObject targetObject;

    [Tooltip("สถานะที่ต้องการตั้ง (true = เปิด / false = ปิด)")]
    public bool setActive = true;

    public IEnumerator Execute(StoryContext ctx)
    {
        if (targetObject != null)
        {
            targetObject.SetActive(setActive);
        }
        yield break;
    }
}
