using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// คลังของสำคัญ (Key Item) — แยกจากกระเป๋าปกติ ไม่กินช่อง ทิ้งไม่ได้
/// ลงทะเบียนตัวเองใน ServiceLocator แบบเดียวกับ LocalizationService / AudioService
/// ใครจะใช้ก็เรียก ServiceLocator.Get<IKeyItemHolder>() ไม่ต้องอ้างอิงคลาสนี้ตรงๆ
/// รองรับการบันทึก/โหลดสถานะเกมผ่าน ISaveable
/// </summary>
public class KeyItemHolder : MonoBehaviour, IKeyItemHolder, ISaveable
{
    [Header("Debug (ดูเฉยๆ ตอนเล่น)")]
    [SerializeField] private List<KeyItemData> keys = new List<KeyItemData>();

    [Header("Catalog สำรองสำหรับโหลดเซฟ (เว้นว่างได้ ระบบจะค้นหาจาก Resources ด้วย)")]
    [SerializeField] private List<KeyItemData> keyCatalog = new List<KeyItemData>();

    public event Action OnChanged;
    public IReadOnlyList<KeyItemData> All => keys;

    [System.Serializable]
    private struct SaveData
    {
        public List<string> savedDoorIds;
    }

    private void Awake()
    {
        ServiceLocator.Register<IKeyItemHolder>(this);
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<IKeyItemHolder>();
    }

    public void Add(KeyItemData key)
    {
        if (key == null) return;
        if (keys.Contains(key)) return;   // กันเก็บซ้ำ

        keys.Add(key);
        Debug.Log($"<color=cyan>🗝️ เก็บของสำคัญ: {key.itemName}</color>");
        OnChanged?.Invoke();
    }

    public bool Has(string doorId)
    {
        if (string.IsNullOrEmpty(doorId)) return false;

        foreach (KeyItemData k in keys)
        {
            if (k != null && k.targetDoorID == doorId) return true;
        }
        return false;
    }

    public bool Consume(string doorId)
    {
        if (string.IsNullOrEmpty(doorId)) return false;

        for (int i = 0; i < keys.Count; i++)
        {
            KeyItemData k = keys[i];
            if (k == null || k.targetDoorID != doorId) continue;

            // เจอกุญแจที่ตรงรหัสแล้ว — ถ้าตั้งไว้ว่าใช้แล้วหาย ก็หักออกจากคลัง
            if (k.consumeOnUse)
            {
                keys.RemoveAt(i);
                Debug.Log($"<color=cyan>🗝️ ใช้ {k.itemName} แล้วหายไป</color>");
                OnChanged?.Invoke();
            }
            return true;
        }
        return false;
    }

    #region ISaveable Implementation

    public string CaptureState()
    {
        SaveData data = new SaveData { savedDoorIds = new List<string>() };
        foreach (KeyItemData k in keys)
        {
            if (k != null && !string.IsNullOrEmpty(k.targetDoorID))
            {
                data.savedDoorIds.Add(k.targetDoorID);
            }
        }
        return JsonUtility.ToJson(data);
    }

    public void RestoreState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson)) return;

        SaveData data = JsonUtility.FromJson<SaveData>(stateJson);
        keys.Clear();

        if (data.savedDoorIds == null || data.savedDoorIds.Count == 0)
        {
            OnChanged?.Invoke();
            return;
        }

        // ค้นหา KeyItemData ที่ตรงกับ doorId จาก Catalog หรือ Resources
        // โหลดจาก Resources แบบขี้เกียจ — ถ้า Catalog ครบก็ไม่ต้องแตะดิสก์เลย
        KeyItemData[] resourceKeys = null;

        foreach (string doorId in data.savedDoorIds)
        {
            if (string.IsNullOrEmpty(doorId)) continue;

            KeyItemData matchedKey = null;

            // 1. ค้นหาใน Catalog ที่ระบุใน Inspector
            if (keyCatalog != null)
            {
                matchedKey = keyCatalog.Find(k => k != null && k.targetDoorID == doorId);
            }

            // 2. ค้นหาใน Resources
            if (matchedKey == null)
            {
                if (resourceKeys == null) resourceKeys = Resources.LoadAll<KeyItemData>("");

                for (int i = 0; i < resourceKeys.Length; i++)
                {
                    if (resourceKeys[i] != null && resourceKeys[i].targetDoorID == doorId)
                    {
                        matchedKey = resourceKeys[i];
                        break;
                    }
                }
            }

            // 3. ถ้าหา asset ไม่เจอ ให้สร้าง instance ชั่วคราวเพื่อให้ logic Has/Consume ยังทำงานได้
            //    แต่กุญแจตัวนี้จะ "ไม่มีรูป ไม่มีคำอธิบาย" ต้องเตือนไว้ ไม่งั้นตามหาสาเหตุไม่เจอ
            if (matchedKey == null)
            {
                Debug.LogWarning(
                    $"[KeyItemHolder] โหลดเซฟแล้วหา asset กุญแจของประตู \"{doorId}\" ไม่เจอ " +
                    $"— สร้างตัวชั่วคราวแทน (เปิดประตูได้ แต่ไม่มีรูปในสมุด)\n" +
                    $"แก้โดยลาก asset กุญแจใส่ช่อง Key Catalog ที่ก้อน [JOURNAL] หรือย้าย asset ไปโฟลเดอร์ Resources");

                matchedKey = ScriptableObject.CreateInstance<KeyItemData>();
                matchedKey.targetDoorID = doorId;
                matchedKey.itemName = doorId;
            }

            keys.Add(matchedKey);
        }

        OnChanged?.Invoke();
    }

    #endregion
}
