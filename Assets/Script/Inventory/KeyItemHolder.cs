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

    [Header("Catalog สำรองสำหรับโหลดเซฟ (เว้นว่างได้ — ปกติหาจาก ItemCatalog ใน Resources)")]
    [SerializeField] private List<KeyItemData> keyCatalog = new List<KeyItemData>();

    public event Action OnChanged;
    public IReadOnlyList<KeyItemData> All => keys;

    [System.Serializable]
    private struct SaveData
    {
        public List<string> savedItemIds;   // 2026-10-07: จำด้วยรหัสไอเทม — ของสำคัญที่ไม่ใช่กุญแจ (เช่น รูปวาด) ไม่มีรหัสประตู
        public List<string> savedDoorIds;   // เซฟรุ่นเก่า (ก่อน 2026-10-07) — อ่านอย่างเดียว
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
        SaveData data = new SaveData { savedItemIds = new List<string>() };
        foreach (KeyItemData k in keys)
        {
            if (k != null) data.savedItemIds.Add(k.ItemId);
        }
        return JsonUtility.ToJson(data);
    }

    public void RestoreState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson)) return;

        SaveData data = JsonUtility.FromJson<SaveData>(stateJson);
        keys.Clear();

        if (data.savedItemIds != null && data.savedItemIds.Count > 0)
        {
            ItemCatalog catalog = ItemCatalog.LoadDefault();
            foreach (string id in data.savedItemIds)
            {
                KeyItemData key = FindById(id, catalog);
                if (key != null) keys.Add(key);
                else Debug.LogWarning($"[KeyItemHolder] โหลดเซฟแล้วหาของสำคัญรหัส \"{id}\" ไม่เจอ — คลิกขวา ItemCatalog (Resources) → เติมไอเทมทั้งโปรเจกต์อัตโนมัติ");
            }
        }
        else if (data.savedDoorIds != null)
        {
            foreach (string doorId in data.savedDoorIds) RestoreLegacyDoorKey(doorId);   // เซฟรุ่นเก่า
        }

        OnChanged?.Invoke();
    }

    private KeyItemData FindById(string id, ItemCatalog catalog)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (keyCatalog != null)
        {
            KeyItemData k = keyCatalog.Find(x => x != null && x.ItemId == id);
            if (k != null) return k;
        }
        return catalog != null ? catalog.Find(id) as KeyItemData : null;
    }

    private KeyItemData[] resourceKeys;   // เซฟรุ่นเก่าเท่านั้น

    /// <summary>เซฟรุ่นเก่าจำด้วยรหัสประตู — หาจาก Key Catalog / Resources · ไม่เจอ = สร้างตัวชั่วคราว (เปิดประตูได้ แต่ไม่มีรูป)</summary>
    private void RestoreLegacyDoorKey(string doorId)
    {
        if (string.IsNullOrEmpty(doorId)) return;

        KeyItemData matchedKey = keyCatalog != null ? keyCatalog.Find(k => k != null && k.targetDoorID == doorId) : null;

        if (matchedKey == null)
        {
            if (resourceKeys == null) resourceKeys = Resources.LoadAll<KeyItemData>("");
            foreach (KeyItemData k in resourceKeys)
            {
                if (k != null && k.targetDoorID == doorId) { matchedKey = k; break; }
            }
        }

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

    #endregion
}
