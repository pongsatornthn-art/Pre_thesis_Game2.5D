using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// จุดเกิดสำหรับมอนสเตอร์ในโลก PTSD 
/// เป็นผู้จ่าย Data (Injection) ให้กับมอนสเตอร์เมื่อมันเกิดออกมา
/// </summary>
public class MonsterSpawner : MonoBehaviour
{
    public GameObject monsterPrefabToSpawn;
    public PTSDMonsterAI.PatrolBehaviorType spawnBehavior;
    public PatrolZone linkedZone;
    public PatrolRoute linkedRoute;

    [Min(1)] public int spawnCount = 1;
    public float spawnRadius = 2f;

    [HideInInspector]
    public int remainingCount = -1;

    private List<GameObject> spawnedMonsters = new List<GameObject>();

    private void Awake()
    {
        if (remainingCount == -1) remainingCount = spawnCount;
    }

    private void OnEnable()
    {
        // 🌟 เปลี่ยนชื่อ Event เป็นตัวใหม่ (รับค่า bool)
        PTSDManager.OnPTSDStateChanged += HandlePTSDCheck;
    }

    private void OnDisable()
    {
        PTSDManager.OnPTSDStateChanged -= HandlePTSDCheck;
    }

    // 🌟 เปลี่ยนให้รับค่า bool แทน float
    private void HandlePTSDCheck(bool isPTSDActive)
    {
        if (isPTSDActive)
        {
            if (remainingCount <= 0) return;

            if (monsterPrefabToSpawn != null && spawnedMonsters.Count == 0)
            {
                for (int i = 0; i < remainingCount; i++)
                {
                    Vector3 spawnPos = transform.position;
                    // ... (ส่วนสุ่มตำแหน่งเกิด)
                    GameObject monster = Instantiate(monsterPrefabToSpawn, spawnPos, transform.rotation);
                    spawnedMonsters.Add(monster);

                    var ai = monster.GetComponent<PTSDMonsterAI>();
                    if (ai != null)
                    {
                        ai.InjectBehavior(spawnBehavior, linkedZone, linkedRoute);
                        ai.OnDeath += () => HandleMonsterDeath(monster);
                    }
                }
            }
        }
        else
        {
            foreach (var monster in spawnedMonsters)
            {
                if (monster != null) Destroy(monster);
            }
            spawnedMonsters.Clear();
        }
    }

    private void HandleMonsterDeath(GameObject deadMonster)
    {
        if (remainingCount > 0) remainingCount--;
        spawnedMonsters.Remove(deadMonster);
        Destroy(deadMonster, 2f);
    }
}