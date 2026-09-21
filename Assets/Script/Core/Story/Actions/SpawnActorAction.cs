using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Action สำหรับเสกตัวละครหรือวัตถุในฉากคัตซีน
/// กฎเหล็ก: ต้องยืมวัตถุผ่าน SimplePool เท่านั้น ห้าม Instantiate ตรงๆ
/// เพื่อป้องกันปัญหา Garbage Collection และอาการกระตุก (Frame Drop)
/// </summary>
[Serializable]
public class SpawnActorAction : IStoryAction
{
    [Tooltip("Prefab ของตัวละครหรือวัตถุที่ต้องการเสก (ต้องมีหรือเข้ากันได้กับ SimplePool)")]
    public GameObject actorPrefab;

    [Tooltip("จุดที่ต้องการให้เสก (Transform ในฉาก)")]
    public Transform spawnPoint;

    [Tooltip("ชื่ออ้างอิงของตัวละครนี้ใน Sequence (สำหรับให้ MoveActor / DespawnActor เรียกใช้)")]
    public string actorTag;

    public IEnumerator Execute(StoryContext ctx)
    {
        if (actorPrefab == null) yield break;

        Vector3 pos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        // ยืมวัตถุจาก SimplePool ตามกฎข้อบังคับของโปรเจกต์
        GameObject spawned = SimplePool.Get(actorPrefab, pos, rot);

        if (spawned != null && ctx != null)
        {
            ctx.LastSpawnedActor = spawned;

            if (!string.IsNullOrEmpty(actorTag))
            {
                ctx.Actors[actorTag] = spawned;
            }
        }

        yield break;
    }
}
