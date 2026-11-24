using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [2026-11-24 นา] เปิดกำแพง/ของที่ระบบกล้องหลักซ่อนไว้ ชั่วคราวตอนมองผ่านกล้อง First-Person (มุม FPS / มุม close-up / ปริศนา)
///
/// ปัญหา: กล้องหลัก (มุม 2.5D) ซ่อนกำแพงที่บังตัวละคร —
///   RoomWallFade / RoomWallSmoothHide / HallwayWallSmoothHide = ปิด renderer หรือทำโปร่งตอนผู้เล่นอยู่ในห้อง
///   CameraObstacleFader (บน Main Camera) = ทำของที่บังกล้องโปร่ง — ปิดกล้องหลักตอนเข้า FPS แล้วของค้างโปร่ง
/// → เข้ามุม FPS แล้วตู้/กำแพงหาย เหมือนลอยอยู่กลางอากาศ (เจอที่จุดเก็บแผนที่ห้องครัว)
///
/// ใช้: เข้ามุม FPS → Reveal(this) · ทุกเฟรมระหว่างอยู่ → KeepVisible() · ออก → Restore(this)
/// นับคนเรียก: ซ้อนกันหลายระบบได้ (เช่น ปริศนาเรียก FirstPersonModeController อีกที) — คืนค่าตอนคนสุดท้ายออก
/// (ย้ายมาจาก BookshelfPuzzle ของปอ ที่ทำไว้ใช้เฉพาะปริศนา → ให้ทุกมุม FPS ใช้ร่วมกัน)
/// </summary>
public static class HiddenGeometryReveal
{
    private static readonly HashSet<object> owners = new HashSet<object>();
    private static readonly Dictionary<Renderer, bool> wallStates = new Dictionary<Renderer, bool>();
    private static readonly Dictionary<Material, Material> matSnapshots = new Dictionary<Material, Material>();
    private static readonly List<Renderer> tmp = new List<Renderer>();

    public static bool IsRevealing => owners.Count > 0;

    /// <summary>เริ่มเปิดของที่ถูกซ่อน (extra = กำแพงเพิ่มเติมที่ระบบไม่รู้จัก — เว้นว่างได้)</summary>
    public static void Reveal(object owner, IEnumerable<Renderer> extra = null)
    {
        if (owner == null) return;
        owners.Add(owner);

        tmp.Clear();
        foreach (RoomWallFade w in Object.FindObjectsByType<RoomWallFade>(FindObjectsSortMode.None))
            if (w.wallsToHide != null) foreach (MeshRenderer r in w.wallsToHide) tmp.Add(r);
        foreach (RoomWallSmoothHide w in Object.FindObjectsByType<RoomWallSmoothHide>(FindObjectsSortMode.None))
            if (w.wallsToFade != null) foreach (MeshRenderer r in w.wallsToFade) tmp.Add(r);
        foreach (HallwayWallSmoothHide w in Object.FindObjectsByType<HallwayWallSmoothHide>(FindObjectsSortMode.None))
            if (w.wallsToFade != null) foreach (MeshRenderer r in w.wallsToFade) tmp.Add(r);
        foreach (CameraObstacleFader f in Object.FindObjectsByType<CameraObstacleFader>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            CollectFaderWalls(f, tmp);
        if (extra != null) tmp.AddRange(extra);

        foreach (Renderer r in tmp)
        {
            if (r == null || wallStates.ContainsKey(r)) continue;
            wallStates[r] = r.enabled;
            r.enabled = true;
            foreach (Material m in r.sharedMaterials) MakeOpaque(m);
        }
        tmp.Clear();
    }

    /// <summary>เรียกทุกเฟรมระหว่างอยู่มุม FPS — มีสคริปต์ซ่อนซ้ำ (เช่น trigger ห้องทำงานอีกรอบ) = จำไว้ว่าตอนออกต้องซ่อน แล้วเปิดกลับ</summary>
    public static void KeepVisible()
    {
        if (owners.Count == 0) return;
        tmp.Clear();
        tmp.AddRange(wallStates.Keys);
        foreach (Renderer r in tmp)
        {
            if (r == null) continue;
            if (!r.enabled) { wallStates[r] = false; r.enabled = true; }
            foreach (Material m in r.sharedMaterials) MakeOpaque(m);
        }
        tmp.Clear();
    }

    /// <summary>ออกจากมุม FPS — คนสุดท้ายออก = คืนทุกอย่างเป็นแบบเดิม (ซ่อน/โปร่งเหมือนก่อนเข้า)</summary>
    public static void Restore(object owner)
    {
        if (owner == null || !owners.Remove(owner) || owners.Count > 0) return;

        foreach (var kv in matSnapshots)
        {
            if (kv.Key == null || kv.Value == null) continue;
            kv.Key.CopyPropertiesFromMaterial(kv.Value);
            kv.Key.shaderKeywords = kv.Value.shaderKeywords;
            kv.Key.renderQueue = kv.Value.renderQueue;
            Object.Destroy(kv.Value);
        }
        foreach (var kv in wallStates) if (kv.Key != null) kv.Key.enabled = kv.Value;
        matSnapshots.Clear();
        wallStates.Clear();
    }

    private static void MakeOpaque(Material m)
    {
        if (m == null) return;
        bool faded = (m.HasProperty("_BaseColor") && m.GetColor("_BaseColor").a < 0.99f)
                  || (m.HasProperty("_Color") && m.color.a < 0.99f)
                  || (m.HasProperty("_Surface") && m.GetFloat("_Surface") > 0.5f && m.renderQueue >= 3000);
        if (!faded) return;
        if (!matSnapshots.ContainsKey(m)) matSnapshots[m] = new Material(m);   // เก็บสภาพเดิม (รวมโหมดโปร่ง) ไว้คืนตอนออก

        if (m.HasProperty("_BaseColor")) { Color c = m.GetColor("_BaseColor"); c.a = 1f; m.SetColor("_BaseColor", c); }
        if (m.HasProperty("_Color")) { Color c = m.color; c.a = 1f; m.color = c; }
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 0f);
        if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 1);
        m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.DisableKeyword("_ALPHABLEND_ON");
        m.renderQueue = 2000;
    }

    // CameraObstacleFader เก็บของที่กำลังโปร่งไว้ในตัวแปร private — อ่านผ่าน reflection (ไม่แก้สคริปต์เดิม)
    private static void CollectFaderWalls(CameraObstacleFader fader, List<Renderer> list)
    {
        var field = typeof(CameraObstacleFader).GetField("activeWalls",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (!(field?.GetValue(fader) is System.Collections.IDictionary dict)) return;
        foreach (object fw in dict.Values)
        {
            var rf = fw?.GetType().GetField("renderer");
            if (rf?.GetValue(fw) is Renderer r) list.Add(r);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { owners.Clear(); wallStates.Clear(); matSnapshots.Clear(); }
}
