using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [2026-11-22] ชุดภาพเคลื่อนไหวของมอนสเตอร์ 1 ตัว (ทุกท่า × ทุกทิศ)
/// ไม่ต้องสร้างเอง — ใช้เมนู Tools ▸ Monster Sprites ▸ Build Anim Set สร้างจากโฟลเดอร์รูปให้อัตโนมัติ
/// MonsterSpriteAnimator อ่านไฟล์นี้แล้วสลับรูปตามท่า (Idle/Walk/Run/Attack) และทิศที่มอนหันอยู่
/// </summary>
[CreateAssetMenu(menuName = "Monster/Sprite Anim Set", fileName = "MonsterAnimSet")]
public class SpriteAnimSet : ScriptableObject
{
    /// <summary>ทิศเทียบกับกล้อง: Front = หันเข้าหากล้อง · Back = หันหลังให้กล้อง</summary>
    public enum Dir { Front, FrontRight, Right, BackRight, Back, BackLeft, Left, FrontLeft }

    [Serializable]
    public class Clip
    {
        [Tooltip("ชื่อท่า: Idle / Walk / Run / Attack")]
        public string action = "Idle";
        public Dir dir = Dir.Front;
        [Tooltip("เฟรมทั้งหมดเรียงตามลำดับ")]
        public Sprite[] frames = Array.Empty<Sprite>();
        [Tooltip("เฟรมต่อวินาที")]
        public float fps = 10f;
        [Tooltip("วนซ้ำ (Attack ควรปิด = เล่นรอบเดียว)")]
        public bool loop = true;

        public float Duration => frames.Length > 0 && fps > 0f ? frames.Length / fps : 0f;
    }

    public List<Clip> clips = new List<Clip>();

    // ───────── ค้นหา (มีแคช) ─────────
    [NonSerialized] private Dictionary<string, Clip> cache;

    /// <summary>มุมของแต่ละทิศ (องศา ตามเข็มนาฬิกาจาก Back) — ใช้หาทิศที่ใกล้ที่สุดเมื่อไม่มีรูปทิศนั้น</summary>
    public static float AngleOf(Dir d)
    {
        switch (d)
        {
            case Dir.Back: return 0f;
            case Dir.BackRight: return 45f;
            case Dir.Right: return 90f;
            case Dir.FrontRight: return 135f;
            case Dir.Front: return 180f;
            case Dir.FrontLeft: return 225f;
            case Dir.Left: return 270f;
            default: return 315f; // BackLeft
        }
    }

    /// <summary>มีท่านี้ (ทิศใดก็ได้) ไหม</summary>
    public bool HasAction(string action)
    {
        foreach (Clip c in clips) if (c != null && c.frames.Length > 0 && Same(c.action, action)) return true;
        return false;
    }

    /// <summary>หาคลิปของท่า+ทิศ · ไม่มีทิศนั้น = เอาทิศที่ใกล้ที่สุดของท่าเดียวกัน · ไม่มีท่านั้นเลย = null</summary>
    public Clip Find(string action, Dir dir)
    {
        cache ??= new Dictionary<string, Clip>();
        string key = action + "|" + (int)dir;
        if (cache.TryGetValue(key, out Clip hit)) return hit;

        Clip best = null;
        float bestDiff = float.MaxValue;
        float want = AngleOf(dir);
        foreach (Clip c in clips)
        {
            if (c == null || c.frames == null || c.frames.Length == 0 || !Same(c.action, action)) continue;
            float diff = Mathf.Abs(Mathf.DeltaAngle(want, AngleOf(c.dir)));
            if (diff < bestDiff) { bestDiff = diff; best = c; }
        }
        cache[key] = best;
        return best;
    }

    /// <summary>
    /// หาคลิปของท่านี้ที่ทิศใกล้มุมที่หันอยู่ที่สุด (มุมแบบ AngleOf: 0 = Back, 90 = Right, 180 = Front, 270 = Left)
    /// prefer = คลิปที่เล่นอยู่ — ถ้ายังไม่ห่างเกิน hysteresis องศาจากตัวที่ดีที่สุด จะเล่นตัวเดิมต่อ (กันภาพสลับไปมาตอนเดินเฉียงๆ)
    /// </summary>
    public Clip FindByAngle(string action, float angle, Clip prefer = null, float hysteresis = 15f)
    {
        Clip best = null;
        float bestDiff = float.MaxValue;
        foreach (Clip c in clips)
        {
            if (c == null || c.frames == null || c.frames.Length == 0 || !Same(c.action, action)) continue;
            float diff = Mathf.Abs(Mathf.DeltaAngle(angle, AngleOf(c.dir)));
            if (diff < bestDiff) { bestDiff = diff; best = c; }
        }
        if (best != null && prefer != null && prefer != best && Same(prefer.action, action) && prefer.frames.Length > 0)
        {
            float preferDiff = Mathf.Abs(Mathf.DeltaAngle(angle, AngleOf(prefer.dir)));
            if (preferDiff <= bestDiff + hysteresis) return prefer;
        }
        return best;
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private void OnValidate() => cache = null;
}
