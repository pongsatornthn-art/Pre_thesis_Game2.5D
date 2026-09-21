using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Action สำหรับสั่งให้ตัวละครเดินหรือวิ่งตามจุด Waypoints ที่กำหนดในฉาก
/// รองรับทั้งการรอให้เดินถึงจุดหมายก่อน (waitFinish = true) หรือให้เดินไปพร้อมกับทำ Action อื่นต่อ
/// </summary>
[Serializable]
public class MoveActorAction : IStoryAction
{
    [Tooltip("วัตถุในฉากที่ต้องการสั่งให้เดิน (เว้นว่างได้หากต้องการใช้ตัวละครที่เพิ่งเสกมา)")]
    public GameObject targetActor;

    [Tooltip("ชื่ออ้างอิงของตัวละครที่ตั้งไว้ใน SpawnActorAction (เว้นว่าง = ใช้ตัวล่าสุดที่เสก)")]
    public string actorTag;

    [Tooltip("เส้นทางการเดินเรียงตามจุด Waypoints")]
    public Transform[] waypoints;

    [Tooltip("ความเร็วในการเคลื่อนที่ (หน่วยต่อวินาที)")]
    public float speed = 3.5f;

    [Tooltip("รอให้เดินถึงจุดหมายปลายทางก่อนดำเนิน Action ถัดไปหรือไม่")]
    public bool waitFinish = true;

    public IEnumerator Execute(StoryContext ctx)
    {
        GameObject actor = ResolveActor(ctx);
        if (actor == null || waypoints == null || waypoints.Length == 0) yield break;

        IEnumerator moveRoutine = MoveRoutine(actor);

        if (waitFinish)
        {
            yield return moveRoutine;
        }
        else if (ctx?.Runner != null)
        {
            ctx.Runner.StartCoroutine(moveRoutine);
        }
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

    private IEnumerator MoveRoutine(GameObject actor)
    {
        for (int i = 0; i < waypoints.Length; i++)
        {
            Transform targetPoint = waypoints[i];
            if (targetPoint == null || actor == null) continue;

            while (actor != null && Vector3.Distance(actor.transform.position, targetPoint.position) > 0.05f)
            {
                actor.transform.position = Vector3.MoveTowards(
                    actor.transform.position,
                    targetPoint.position,
                    speed * Time.unscaledDeltaTime
                );

                // หมุนตัวหันไปในทิศทางการเคลื่อนที่
                Vector3 dir = (targetPoint.position - actor.transform.position).normalized;
                if (dir.sqrMagnitude > 0.001f)
                {
                    dir.y = 0; // ล็อกแกน Y ป้องกันการเอียงหน้าคว่ำ
                    if (dir != Vector3.zero)
                    {
                        actor.transform.rotation = Quaternion.LookRotation(dir);
                    }
                }

                yield return null;
            }
        }
    }
}
