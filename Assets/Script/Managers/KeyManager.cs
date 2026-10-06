using System;
using System.Collections.Generic;
using UnityEngine;

public class KeyManager : MonoBehaviour, IKeyItemHolder
{
    private List<KeyItemData> collectedKeys = new List<KeyItemData>();

    public event Action OnChanged;
    public IReadOnlyList<KeyItemData> All => collectedKeys.AsReadOnly();

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
        if (!collectedKeys.Contains(key))
        {
            collectedKeys.Add(key);
            Debug.Log($"[KeyManager] เก็บกุญแจ: {key.itemName} ลงคลังเรียบร้อย!");
            OnChanged?.Invoke(); // อัปเดตบอกระบบอื่นว่าของในคลังเปลี่ยน
        }
    }

    public bool Has(string doorID)
    {
        return collectedKeys.Exists(k => k.targetDoorID == doorID);
    }

    public bool Consume(string doorID)
    {
        KeyItemData keyToUse = collectedKeys.Find(k => k.targetDoorID == doorID);

        if (keyToUse != null)
        {
            if (keyToUse.consumeOnUse)
            {
                collectedKeys.Remove(keyToUse);
                Debug.Log($"[KeyManager] ใช้กุญแจรหัส {doorID} ไปแล้ว");
                OnChanged?.Invoke();
            }
            return true;
        }

        return false;
    }
}