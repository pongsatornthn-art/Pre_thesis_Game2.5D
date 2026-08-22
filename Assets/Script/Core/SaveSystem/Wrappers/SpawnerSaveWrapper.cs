using UnityEngine;

/// <summary>
/// สคริปต์นี้ทำหน้าที่ดึง/ยัด ค่าของ MonsterSpawner
/// ต้องแปะไว้บนกล่องเดียวกับที่มี MonsterSpawner และ SaveableEntity เท่านั้น
/// </summary>
[RequireComponent(typeof(SaveableEntity))]
[RequireComponent(typeof(MonsterSpawner))]
public class SpawnerSaveWrapper : MonoBehaviour, ISaveable
{
    private MonsterSpawner spawner;

    private void Awake()
    {
        spawner = GetComponent<MonsterSpawner>();
    }

    // ก้อนข้อมูลที่จะเซฟ (เราต้องการเซฟแค่ยอดคงเหลือ)
    [System.Serializable]
    private struct SaveData
    {
        public int savedRemainingCount;
    }

    public string CaptureState()
    {
        // 1. ดึงค่ายอดที่เหลือจาก Spawner
        SaveData data = new SaveData { savedRemainingCount = spawner.remainingCount };
        
        // 2. แปลงเป็น JSON String คืนให้ส่วนกลาง
        return JsonUtility.ToJson(data);
    }

    public void RestoreState(string stateJson)
    {
        // 1. แปลง JSON String กลับเป็น Data
        SaveData data = JsonUtility.FromJson<SaveData>(stateJson);
        
        // 2. ยัดยอดคงเหลือกลับใส่ Spawner (โหลดเกมเสร็จเรียบร้อย!)
        spawner.remainingCount = data.savedRemainingCount;
    }
}
