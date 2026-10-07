using System;
using System.Collections;
using UnityEngine;

/// <summary>ปลดช่องในคลังความทรงจำจากฉากเนื้อเรื่อง — เช่น ประกอบรูปเสร็จ → ปลด "รูปวาดของภรรยา"</summary>
[Serializable, PickerName("คลังความทรงจำ/ปลดช่อง")]
public class UnlockMemoryAction : IStoryAction
{
    public MemoryEntryData entry;

    public IEnumerator Execute(StoryContext ctx)
    {
        IMemoryArchive archive = ServiceLocator.GetOptional<IMemoryArchive>();
        if (entry == null) Debug.LogWarning("[UnlockMemoryAction] ยังไม่ได้เลือกช่อง");
        else if (archive == null) Debug.LogWarning("[UnlockMemoryAction] ไม่มี MemoryArchive ในซีน (แปะที่ [STORY])");
        else archive.Unlock(entry);
        yield break;
    }
}
