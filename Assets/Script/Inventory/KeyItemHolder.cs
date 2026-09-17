using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// คลังของสำคัญ (Key Item) — แยกจากกระเป๋าปกติ ไม่กินช่อง ทิ้งไม่ได้
/// ลงทะเบียนตัวเองใน ServiceLocator แบบเดียวกับ LocalizationService / AudioService
/// ใครจะใช้ก็เรียก ServiceLocator.Get&lt;IKeyItemHolder&gt;() ไม่ต้องอ้างอิงคลาสนี้ตรงๆ
/// </summary>
public class KeyItemHolder : MonoBehaviour, IKeyItemHolder
{
    [Header("Debug (ดูเฉยๆ ตอนเล่น)")]
    [SerializeField] private List<KeyItemData> keys = new List<KeyItemData>();

    public event Action OnChanged;
    public IReadOnlyList<KeyItemData> All => keys;

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
}
