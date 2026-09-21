using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Action สำหรับส่งตัวละครหรือวัตถุที่เสกขึ้นมา คืนกลับเข้าสู่ SimplePool
/// เพื่อให้หน่วยความจำถูกนำกลับมาใช้ซ้ำได้อย่างสมบูรณ์
/// </summary>
[Serializable]
public class DespawnActorAction : IStoryAction
{
    [Tooltip("วัตถุที่ต้องการส่งคืนพูล (เว้นว่างได้หากต้องการใช้ตัวที่เพิ่งเสกมา)")]
    public GameObject targetActor;

    [Tooltip("ชื่ออ้างอิงของตัวละครที่ตั้งไว้ใน SpawnActorAction")]
    public string actorTag;

    public IEnumerator Execute(StoryContext ctx)
    {
        GameObject actor = ResolveActor(ctx);

        if (actor != null)
        {
            SimplePool.Return(actor);

            if (ctx != null)
            {
                if (ctx.LastSpawnedActor == actor) ctx.LastSpawnedActor = null;
                if (!string.IsNullOrEmpty(actorTag)) ctx.Actors.Remove(actorTag);
            }
        }

        yield break;
    }

    private GameObject ResolveActor(StoryContext ctx)
    {
        if (targetActor != null) return targetActor;

        if (ctx != null)
        {
            if (!string.IsNullOrEmpty(actorTag) && ctx.Actors.TryGetValue(actorTag, out GameObject namedActor))
            {
                return namedActor;
            }
            return ctx.LastSpawnedActor;
        }
        return null;
    }
}
