using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// จำว่า "ไอเทมที่วางในแมพชิ้นไหนถูกเก็บไปแล้ว" — แก้บั๊ก: โหลดเซฟแล้วกุญแจ/ปืน/กระสุนโผล่ที่เดิม เก็บซ้ำได้ไม่จำกัด
///
/// ไม่ต้องตั้งค่าทีละชิ้น: ตอนเริ่มซีนจะแปะ PickupWatcher ให้ ItemPickup (ของเพื่อน) ทุกชิ้นเอง
/// ItemPickup ถูกทำลายตอนเก็บ → PickupWatcher แจ้งมาที่นี่ → ถูกเซฟ
/// โหลดเซฟ → ชิ้นที่เคยเก็บแล้วถูกลบออกจากแมพทันที
/// **ไม่แก้ไฟล์เพื่อน** · วางที่ [STORY] คู่กับ SaveableEntity
///
/// รหัสชิ้น = ชื่อซีน + ตำแหน่งใน Hierarchy + ไอเทม + พิกัด (ปัด 0.1)
/// ⚠️ ย้าย/เปลี่ยนชื่อไอเทมในแมพหลังมีเซฟแล้ว → เซฟเก่าจำชิ้นนั้นไม่ได้ (โผล่ใหม่ 1 ครั้ง) — ไม่พังเกม
/// ไม่จำ: ของที่ผู้เล่นกด G ทิ้งลงพื้น (สร้างใหม่ระหว่างเล่น) — โหลดแล้วหายไป
/// </summary>
[RequireComponent(typeof(SaveableEntity))]
public class WorldPickupTracker : MonoBehaviour, ISaveable, ISaveRestoreOrder
{
    public int RestoreOrder => 5;

    private readonly HashSet<string> collected = new HashSet<string>();
    private readonly Dictionary<string, PickupWatcher> watchers = new Dictionary<string, PickupWatcher>();

    [Serializable]
    private class PickupSaveData
    {
        public List<string> collectedIds = new List<string>();
    }

    private void Start()
    {
        foreach (ItemPickup pickup in FindObjectsByType<ItemPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string id = BuildId(pickup);
            if (watchers.ContainsKey(id))
            {
                Debug.LogWarning($"[WorldPickupTracker] ไอเทม 2 ชิ้นได้รหัสเดียวกัน (ชื่อ+ตำแหน่งซ้ำกัน): {pickup.name} — เปลี่ยนชื่อชิ้นใดชิ้นหนึ่ง", pickup);
                continue;
            }

            PickupWatcher watcher = pickup.gameObject.AddComponent<PickupWatcher>();
            watcher.Init(this, id);
            watchers.Add(id, watcher);
        }
    }

    internal void MarkCollected(string id)
    {
        // ถูกเรียกตอนซีนปิดก็ไม่เป็นไร — ตัวนี้ถูกทำลายไปพร้อมซีน ไม่มีผลกับเซฟ
        if (collected.Add(id)) watchers.Remove(id);
    }

    public string CaptureState()
    {
        return JsonUtility.ToJson(new PickupSaveData { collectedIds = new List<string>(collected) });
    }

    public void RestoreState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson)) return;
        PickupSaveData data = JsonUtility.FromJson<PickupSaveData>(stateJson);
        if (data?.collectedIds == null) return;

        foreach (string id in data.collectedIds)
        {
            collected.Add(id);
            if (watchers.TryGetValue(id, out PickupWatcher w) && w != null)
            {
                watchers.Remove(id);
                w.RemoveSilently();
            }
        }
    }

    private static string BuildId(ItemPickup pickup)
    {
        StringBuilder path = new StringBuilder();
        for (Transform t = pickup.transform; t != null; t = t.parent) path.Insert(0, "/" + t.name);

        Vector3 p = pickup.transform.position;
        string itemId = pickup.item != null ? pickup.item.ItemId : "none";
        return $"{pickup.gameObject.scene.name}{path}|{itemId}|{Mathf.RoundToInt(p.x * 10)},{Mathf.RoundToInt(p.y * 10)},{Mathf.RoundToInt(p.z * 10)}";
    }
}
