using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [2026-11-22] ซ่อน/โชว์ตัวผู้เล่นในกล้อง (ปิดแค่ Renderer — ไม่ปิด GameObject)
/// ไม่ใช้ SetActive(false) เพราะ Animator ของ GFX จะรีเซ็ตค่า (บั๊กท่าถือปืนหายเดิม)
/// ใช้แบบนับคนเรียก: ระบบไหนสั่งซ่อน ต้องสั่งโชว์คืนเอง — ซ่อนซ้อนกัน 2 ระบบก็ไม่โผล่ก่อนเวลา
/// </summary>
public static class PlayerVisibility
{
    private static readonly HashSet<object> hiders = new HashSet<object>();
    private static readonly Dictionary<Renderer, bool> saved = new Dictionary<Renderer, bool>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { hiders.Clear(); saved.Clear(); }

    public static bool IsHidden => hiders.Count > 0;

    public static void Hide(object owner)
    {
        if (owner == null || !hiders.Add(owner) || hiders.Count > 1) return; // ซ่อนอยู่แล้ว
        PlayerMovement pm = Object.FindFirstObjectByType<PlayerMovement>();
        if (pm == null) return;
        saved.Clear();
        foreach (Renderer r in pm.GetComponentsInChildren<Renderer>(true))
        {
            // UI ลอยบนหัว (หลอดเลือด/Stamina) ไม่ใช่ Renderer — ไม่โดนตรงนี้ ให้ FirstPersonModeController ซ่อนเหมือนเดิม
            saved[r] = r.enabled;
            r.enabled = false;
        }
    }

    public static void Show(object owner)
    {
        if (owner == null || !hiders.Remove(owner) || hiders.Count > 0) return; // ยังมีระบบอื่นซ่อนอยู่
        foreach (KeyValuePair<Renderer, bool> kv in saved)
            if (kv.Key != null) kv.Key.enabled = kv.Value;
        saved.Clear();
    }
}
