using System.Collections.Generic;
using System.IO;
using UnityEngine;

// --- Data Models (โครงสร้างไฟล์เซฟ) ---
[System.Serializable]
public class GameSaveData
{
    // เก็บรายการวัตถุทั้งหมดในฉาก
    public List<EntitySaveData> entities = new List<EntitySaveData>();
}

[System.Serializable]
public class EntitySaveData
{
    public string uuid; // รหัสประจำตัววัตถุ (เช่น ของ Spawner)
    public List<ComponentSaveData> components = new List<ComponentSaveData>(); // สคริปต์ต่างๆ ในวัตถุนี้
}

[System.Serializable]
public class ComponentSaveData
{
    public string componentName; // ชื่อสคริปต์ (เช่น SpawnerSaveWrapper)
    public string stateJson; // ข้อมูลก้อน JSON ที่ซ่อนอยู่ข้างใน
}

/// <summary>
/// ผู้จัดการศูนย์กลางการ Save / Load ไฟล์ (JSON)
/// เอาไปแปะไว้ที่ [CORE_SERVICES]
/// </summary>
public class SaveManager : MonoBehaviour
{
    private string SaveFilePath => Path.Combine(Application.persistentDataPath, "gamesave.json");

    private void Awake()
    {
        ServiceLocator.Register<SaveManager>(this);
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<SaveManager>();
    }

    [ContextMenu("TEST SAVE (จำลองการกดเซฟ)")]
    public void SaveGame()
    {
        GameSaveData gameData = new GameSaveData();

        // 1. กวาดสายตาหาทุกวัตถุในฉากที่มีรหัส UUID
        foreach (var entity in Object.FindObjectsByType<SaveableEntity>(FindObjectsSortMode.None))
        {
            if (string.IsNullOrEmpty(entity.UUID)) continue;

            EntitySaveData entityData = new EntitySaveData { uuid = entity.UUID };

            // 2. ดึงสคริปต์ทุกอันในวัตถุนั้น ที่มีป้าย ISaveable
            var saveables = entity.GetComponents<ISaveable>();
            foreach (var saveable in saveables)
            {
                ComponentSaveData compData = new ComponentSaveData
                {
                    componentName = saveable.GetType().Name,
                    stateJson = saveable.CaptureState() // ขอข้อมูลจากสคริปต์นั้นๆ
                };
                entityData.components.Add(compData);
            }

            // ถ้าวัตถุนั้นมีข้อมูลเซฟ ก็ยัดใส่กระเป๋าใหญ่
            if (entityData.components.Count > 0)
            {
                gameData.entities.Add(entityData);
            }
        }

        // 3. แปลงเป็นอักษร JSON แบบสวยงาม (Pretty Print)
        string finalJson = JsonUtility.ToJson(gameData, true);

        // 4. เขียนทับลงไฟล์ในเครื่อง
        File.WriteAllText(SaveFilePath, finalJson);
        Debug.Log($"[SaveManager] 💾 เซฟเกมสำเร็จ! บันทึกลงที่: {SaveFilePath}");
    }

    [ContextMenu("TEST LOAD (จำลองการโหลดเกม)")]
    public void LoadGame()
    {
        if (!File.Exists(SaveFilePath))
        {
            Debug.LogWarning("[SaveManager] ❌ ไม่พบไฟล์เซฟ! เริ่มเกมใหม่");
            return;
        }

        // 1. อ่านไฟล์ขึ้นมา
        string json = File.ReadAllText(SaveFilePath);
        GameSaveData gameData = JsonUtility.FromJson<GameSaveData>(json);

        // 2. หากล่องข้อมูลแต่ละใบ
        foreach (var entityData in gameData.entities)
        {
            // 3. ตามหาวัตถุจริงในฉาก ที่มี UUID ตรงกับกล่องข้อมูล
            var entityObj = FindEntityByUUID(entityData.uuid);
            if (entityObj != null)
            {
                // 4. กระจายข้อมูลให้แต่ละสคริปต์
                var saveables = entityObj.GetComponents<ISaveable>();
                foreach (var saveable in saveables)
                {
                    string typeName = saveable.GetType().Name;
                    var compData = entityData.components.Find(c => c.componentName == typeName);
                    
                    if (compData != null)
                    {
                        saveable.RestoreState(compData.stateJson); // คืนความทรงจำ
                    }
                }
            }
        }

        Debug.Log("[SaveManager] 📂 โหลดเกมสำเร็จ!");
    }

    private SaveableEntity FindEntityByUUID(string uuid)
    {
        // อาจจะช้าหน่อยถ้าฉากใหญ่ แต่สำหรับเกมอินดี้ไม่มีปัญหาครับ
        var allEntities = Object.FindObjectsByType<SaveableEntity>(FindObjectsSortMode.None);
        foreach (var entity in allEntities)
        {
            if (entity.UUID == uuid) return entity;
        }
        return null;
    }
}
