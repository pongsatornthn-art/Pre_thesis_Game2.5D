using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// จำว่า "ไอเทมที่วางในแมพชิ้นไหนถูกเก็บไปแล้ว" — แก้บั๊ก: โหลดเซฟแล้วกุญแจ/ปืน/กระสุนโผล่ที่เดิม เก็บซ้ำได้ไม่จำกัด
///
/// ไอเทมแต่ละชิ้นต้องมี PickupWatcher แปะคู่ ItemPickup (ของเพื่อน — ไม่แก้ไฟล์เขา)
/// วิธีแปะทีเดียวทั้งซีน: คลิกขวาที่คอมโพเนนต์นี้ → "แปะตัวเฝ้าให้ไอเทมทุกชิ้นในซีน" แล้วเซฟซีน
/// ไอเทมที่ลืมแปะ → ตอนกด Play จะเตือนใน Console (ระบบไม่แปะให้เองตอนเล่น — กติกาเจ้าของ)
///
/// วางที่ [STORY] คู่กับ SaveableEntity
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
        foreach (PickupWatcher w in FindObjectsByType<PickupWatcher>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (string.IsNullOrEmpty(w.PickupId)) continue;
            if (watchers.ContainsKey(w.PickupId))
            {
                Debug.LogWarning($"[WorldPickupTracker] ตัวเฝ้า 2 ชิ้นรหัสซ้ำ ({w.name} กับ {watchers[w.PickupId].name}) — มักเกิดจาก Duplicate · คลิกขวาที่ PickupWatcher → 'สุ่มรหัสใหม่'", w);
                continue;
            }
            w.Bind(this);
            watchers.Add(w.PickupId, w);
        }

        foreach (ItemPickup p in FindObjectsByType<ItemPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (p.GetComponent<PickupWatcher>() == null)
            {
                Debug.LogWarning($"[WorldPickupTracker] ไอเทม '{p.name}' ยังไม่มี PickupWatcher — เก็บแล้วโหลดเซฟจะโผล่ซ้ำ · คลิกขวาที่ WorldPickupTracker → 'แปะตัวเฝ้าให้ไอเทมทุกชิ้นในซีน'", p);
            }
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

#if UNITY_EDITOR
    [ContextMenu("แปะตัวเฝ้าให้ไอเทมทุกชิ้นในซีน")]
    private void AddWatchersInEditor()
    {
        int added = 0;
        foreach (ItemPickup p in FindObjectsByType<ItemPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (p.GetComponent<PickupWatcher>() != null) continue;
            UnityEditor.Undo.AddComponent<PickupWatcher>(p.gameObject);
            added++;
        }
        Debug.Log($"[WorldPickupTracker] แปะตัวเฝ้าเพิ่ม {added} ชิ้น — อย่าลืมเซฟซีน (Ctrl+S)");
    }
#endif
}
