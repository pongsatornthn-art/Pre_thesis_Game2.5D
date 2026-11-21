using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [ข้อ 11] จุดกลางให้ระบบอื่น "ย้อมสี/ปรับความแรง" ไฟทีละดวง ทับบรรยากาศของ WorldLighting
///
/// ทำไมต้องมี: WorldLighting สั่งสี+ความแรงไฟทุกดวงทุกเฟรม → ถ้าสคริปต์อื่นไปเปลี่ยน Light ตรงๆ จะโดนทับทันที
/// ทางนี้: ZoneLighting (ไฟโซน) ฝากค่าไว้ที่นี่ → WorldLighting อ่านไปผสมตอนวาดไฟ ไม่ตีกัน
/// ไฟที่ WorldLighting ไม่ได้คุม (อยู่นอก Lamps Root) → ZoneLighting เปลี่ยนตรงๆ เอง (เช็คด้วย IsManaged)
/// </summary>
public static class LampOverrides
{
    public struct Entry
    {
        /// <summary>สีที่ต้องการ</summary>
        public Color color;
        /// <summary>ผสมแค่ไหน 0–1 (0 = ไม่ย้อม)</summary>
        public float amount;
        /// <summary>ความแรงเป้าหมาย = ความแรงตั้งต้นของดวงนั้น × ค่านี้</summary>
        public float intensityMul;
    }

    private static readonly Dictionary<Light, Entry> entries = new Dictionary<Light, Entry>();
    private static readonly HashSet<Light> managed = new HashSet<Light>();

    // WorldLighting ลงทะเบียนไฟที่ตัวเองคุม
    public static void RegisterManaged(Light l) { if (l != null) managed.Add(l); }
    public static void UnregisterManaged(Light l) { if (l != null) managed.Remove(l); }
    public static bool IsManaged(Light l) => l != null && managed.Contains(l);

    public static void Set(Light l, Color color, float amount, float intensityMul)
    {
        if (l == null) return;
        if (amount <= 0f) { entries.Remove(l); return; }
        entries[l] = new Entry { color = color, amount = Mathf.Clamp01(amount), intensityMul = Mathf.Max(0f, intensityMul) };
    }

    public static void Clear(Light l)
    {
        if (l != null) entries.Remove(l);
    }

    public static bool TryGet(Light l, out Entry e)
    {
        if (l != null && entries.TryGetValue(l, out e) && e.amount > 0f) return true;
        e = default;
        return false;
    }
}
