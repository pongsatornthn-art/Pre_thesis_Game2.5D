using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// เซฟ/โหลดสภาพตัวผู้เล่น: เลือด · ช่อง hotbar ที่ถืออยู่ · กระสุนในแม็กของปืนแต่ละกระบอก
/// วางที่ [STORY] คู่กับ SaveableEntity (ไม่ต้องแปะที่ตัว Player) · โหลดหลังกระเป๋า (ต้องมีปืนในกระเป๋าก่อนถึงจะถือได้)
///
/// ไฟล์ของเพื่อนที่เกี่ยว:
///   PlayerMovement   → ใช้ CurrentHealth (มีอยู่แล้ว) + SetHealthFromSave (เพิ่มให้ 2026-10-04)
///   HotbarController → ใช้ SelectedIndex + SelectSlotFromSave (เพิ่มให้ 2026-10-04)
///   PlayerCombat     → 🔴 ไฟล์ God class ห้ามยัดโค้ดเพิ่ม (CLAUDE.md) จึงอ่าน/เขียนช่อง private
///                       "weaponAmmoMemory" ผ่าน reflection แทน — ถ้าเพื่อนเปลี่ยนชื่อช่องนี้ จะเตือนใน Console
///                       และข้ามแค่เรื่องกระสุน (เลือด/ช่องถือยังทำงาน)
/// </summary>
[RequireComponent(typeof(SaveableEntity))]
public class PlayerStateSaveWrapper : MonoBehaviour, ISaveable, ISaveRestoreOrder
{
    private const string AmmoMemoryField = "weaponAmmoMemory";

    public int RestoreOrder => 10;

    [Tooltip("เว้นว่าง = หา Resources/ItemCatalog เอง")]
    [SerializeField] private ItemCatalog catalog;

    [Serializable]
    private class AmmoSave
    {
        public string itemId;
        public int inMag;
    }

    [Serializable]
    private class PlayerStateData
    {
        public int health = -1;          // -1 = ไม่ได้เซฟ (ไม่แตะ)
        public int hotbarIndex = -1;
        public List<AmmoSave> ammo = new List<AmmoSave>();
    }

    public string CaptureState()
    {
        PlayerStateData data = new PlayerStateData();

        PlayerMovement movement = FindFirstObjectByType<PlayerMovement>();
        if (movement != null) data.health = movement.CurrentHealth;

        HotbarController hotbar = FindFirstObjectByType<HotbarController>();
        if (hotbar != null) data.hotbarIndex = hotbar.SelectedIndex;

        Dictionary<ItemData, int> memory = GetAmmoMemory(FindFirstObjectByType<PlayerCombat>());
        if (memory != null)
        {
            foreach (KeyValuePair<ItemData, int> pair in memory)
            {
                if (pair.Key != null) data.ammo.Add(new AmmoSave { itemId = ItemCatalog.GetId(pair.Key), inMag = pair.Value });
            }
        }

        return JsonUtility.ToJson(data);
    }

    public void RestoreState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson)) return;
        PlayerStateData data = JsonUtility.FromJson<PlayerStateData>(stateJson);
        if (data == null) return;

        // 1. กระสุนต้องใส่ก่อนเลือกช่องถือ — PlayerCombat อ่านค่าจาก memory ตอนถือปืนขึ้นมา
        RestoreAmmo(data.ammo);

        // 2. เลือกช่องถือ → Inventory.EquipItem → PlayerCombat ดึงกระสุนจาก memory ที่ใส่ไว้
        HotbarController hotbar = FindFirstObjectByType<HotbarController>();
        if (hotbar != null && data.hotbarIndex >= 0) hotbar.SelectSlotFromSave(data.hotbarIndex);

        // 3. เลือด
        PlayerMovement movement = FindFirstObjectByType<PlayerMovement>();
        if (movement != null && data.health > 0) movement.SetHealthFromSave(data.health);
    }

    private void RestoreAmmo(List<AmmoSave> saved)
    {
        if (saved == null || saved.Count == 0) return;

        PlayerCombat combat = FindFirstObjectByType<PlayerCombat>();
        Dictionary<ItemData, int> memory = GetAmmoMemory(combat);
        if (memory == null) return;

        if (catalog == null) catalog = ItemCatalog.LoadDefault();
        if (catalog == null)
        {
            Debug.LogWarning("[PlayerStateSaveWrapper] ไม่มี ItemCatalog — กระสุนในแม็กโหลดไม่ได้ (เต็มแม็กแทน)");
            return;
        }

        memory.Clear();
        foreach (AmmoSave a in saved)
        {
            ItemData gun = catalog.Find(a.itemId);
            if (gun != null) memory[gun] = Mathf.Max(0, a.inMag);
        }

        // ปืนที่ถืออยู่ตอนนี้ (ถ้า Equip ไปแล้วก่อนหน้า) ต้องอัปเดตตัวเลขบนจอด้วย
        ItemData equipped = Inventory.Instance != null ? Inventory.Instance.currentEquippedItem : null;
        if (equipped != null && memory.TryGetValue(equipped, out int inMag)) combat.currentAmmoInMag = inMag;
    }

    private static Dictionary<ItemData, int> GetAmmoMemory(PlayerCombat combat)
    {
        if (combat == null) return null;

        FieldInfo field = typeof(PlayerCombat).GetField(AmmoMemoryField, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field == null || !(field.GetValue(combat) is Dictionary<ItemData, int> memory))
        {
            Debug.LogWarning($"[PlayerStateSaveWrapper] ไม่พบช่อง '{AmmoMemoryField}' ใน PlayerCombat (เพื่อนเปลี่ยนชื่อ/ชนิด?) — ข้ามการเซฟกระสุนในแม็ก");
            return null;
        }
        return memory;
    }
}
