#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [2026-11-22] เครื่องมือสร้างชุดภาพมอนสเตอร์ (SpriteAnimSet) จากโฟลเดอร์รูป — ไม่ต้องลากทีละเฟรม
/// เมนู: Tools ▸ Monster Sprites ▸ Build Anim Set
///
/// อ่านชื่อโฟลเดอร์/ไฟล์เป็น "ท่า_ทิศ" เช่น Idle_FrontLeft · Run_Back · Attack_Right · Idel_Left_Enemy (สะกด Idel ก็ได้)
///   ท่า: Idle/Idel · Walk · Run · Attack     ทิศ: Front · FrontLeft · FrontRight · Left · Right · Back · BackLeft · BackRight
///   เรียงเฟรมตามเลขท้ายชื่อไฟล์ (F1, F2 … F10 เรียงถูก)
/// ทำให้: ตั้งค่ารูปเป็น Sprite แบบพิกเซลคม (Point / ไม่บีบอัด) + ปรับขนาดและจุดหมุนให้เท่ารูปเดิมของมอน (ถ้าใส่ Reference Sprite)
///        แล้วสร้างไฟล์ <ชื่อโฟลเดอร์>_AnimSet.asset ไว้ในโฟลเดอร์นั้น
/// </summary>
public class MonsterSpriteSetBuilder : EditorWindow
{
    [SerializeField] private DefaultAsset rootFolder;
    [SerializeField] private Sprite referenceSprite;
    [SerializeField] private float manualPixelsPerUnit = 100f;
    [SerializeField] private SpriteAlignment manualPivot = SpriteAlignment.BottomCenter;
    [SerializeField] private float idleFps = 8f;      // Stalker Idle 8 เฟรม = วนรอบละ 1 วิ
    [SerializeField] private float walkFps = 5f;      // มินเนียนเดิน 2 เฟรม — 5 = ก้าวละ 0.2 วิ
    [SerializeField] private float runFps = 8f;       // มินเนียนวิ่ง 2 เฟรม
    [SerializeField] private float attackFps = 1.25f; // มินเนียนตี 2 เฟรม: เฟรม 1 (ง้าง) ค้าง 0.8 วิ = ตรงช่วงง้างของ StateAttack · เฟรม 2 (ฟัน) ตอนพุ่ง
    [SerializeField] private bool pixelArt = true;

    private static readonly string[] DirNames = { "backleft", "backright", "frontleft", "frontright", "back", "front", "left", "right" };

    [MenuItem("Tools/Monster Sprites/Build Anim Set")]
    private static void Open() => GetWindow<MonsterSpriteSetBuilder>("Monster Anim Set");

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "1) ลากโฟลเดอร์รูปของมอน 1 ตัว (เช่น MINIONS SPRITE หรือ Enemy_Sprite) ใส่ช่อง Root Folder\n" +
            "2) ลากรูปเดิมของมอนตัวนั้นใส่ Reference Sprite → รูปใหม่จะใหญ่เท่าเดิม ยืนที่เดิม\n" +
            "3) กด Build — ได้ไฟล์ ..._AnimSet ในโฟลเดอร์นั้น เอาไปใส่ MonsterSpriteAnimator", MessageType.Info);

        rootFolder = (DefaultAsset)EditorGUILayout.ObjectField("Root Folder", rootFolder, typeof(DefaultAsset), false);
        referenceSprite = (Sprite)EditorGUILayout.ObjectField("Reference Sprite", referenceSprite, typeof(Sprite), false);
        using (new EditorGUI.DisabledScope(referenceSprite != null))
        {
            manualPixelsPerUnit = EditorGUILayout.FloatField("Pixels Per Unit (ไม่มี Ref)", manualPixelsPerUnit);
            manualPivot = (SpriteAlignment)EditorGUILayout.EnumPopup("Pivot (ไม่มี Ref)", manualPivot);
        }
        EditorGUILayout.Space();
        idleFps = EditorGUILayout.FloatField("Idle FPS", idleFps);
        walkFps = EditorGUILayout.FloatField("Walk FPS", walkFps);
        runFps = EditorGUILayout.FloatField("Run FPS", runFps);
        attackFps = EditorGUILayout.FloatField("Attack FPS", attackFps);
        pixelArt = EditorGUILayout.Toggle(new GUIContent("Pixel Art (Point, ไม่บีบอัด)"), pixelArt);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(rootFolder == null))
        {
            if (GUILayout.Button("Build", GUILayout.Height(32))) Build();
        }
    }

    private class Group
    {
        public string action;
        public SpriteAnimSet.Dir dir;
        public List<string> files = new List<string>();
    }

    private void Build()
    {
        string root = AssetDatabase.GetAssetPath(rootFolder);
        if (!AssetDatabase.IsValidFolder(root)) { EditorUtility.DisplayDialog("Monster Anim Set", "Root Folder ต้องเป็นโฟลเดอร์", "OK"); return; }

        // 1) จัดกลุ่มรูปตาม ท่า+ทิศ
        var groups = new Dictionary<string, Group>();
        var skipped = new List<string>();
        foreach (string full in Directory.GetFiles(root, "*.png", SearchOption.AllDirectories))
        {
            string path = full.Replace('\\', '/');
            string folder = Path.GetFileName(Path.GetDirectoryName(path));
            string file = Path.GetFileNameWithoutExtension(path);
            string action = ParseAction(folder) ?? ParseAction(file);
            SpriteAnimSet.Dir? dir = ParseDir(folder) ?? ParseDir(file);
            if (action == null || dir == null) { skipped.Add(path); continue; }

            string key = action + "|" + dir.Value;
            if (!groups.TryGetValue(key, out Group g)) groups[key] = g = new Group { action = action, dir = dir.Value };
            g.files.Add(path);
        }
        if (groups.Count == 0) { EditorUtility.DisplayDialog("Monster Anim Set", "ไม่เจอรูปที่ชื่อบอกท่า+ทิศเลย (เช่น Idle_Left)", "OK"); return; }
        foreach (Group g in groups.Values) g.files = g.files.OrderBy(FrameNumber).ThenBy(f => f).ToList();

        // 2) ขนาด + จุดหมุน
        float ppu = manualPixelsPerUnit;
        Vector2 pivot = AlignmentToPivot(manualPivot);
        if (referenceSprite != null && !MatchReference(groups, out ppu, out pivot))
        {
            ppu = manualPixelsPerUnit;
            pivot = AlignmentToPivot(manualPivot);
        }

        // 3) ตั้งค่า import ทุกรูป
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (Group g in groups.Values)
                foreach (string f in g.files) ApplyImport(f, ppu, pivot);
        }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.Refresh();

        // 4) สร้าง/อัปเดตไฟล์ชุดภาพ (เก็บ FPS ที่เคยแก้มือไว้)
        string assetPath = $"{root}/{Path.GetFileName(root).Replace(' ', '_')}_AnimSet.asset";
        SpriteAnimSet set = AssetDatabase.LoadAssetAtPath<SpriteAnimSet>(assetPath);
        var oldFps = new Dictionary<string, float>();
        if (set == null) { set = CreateInstance<SpriteAnimSet>(); AssetDatabase.CreateAsset(set, assetPath); }
        else foreach (var c in set.clips) if (c != null) oldFps[c.action + "|" + c.dir] = c.fps;

        set.clips.Clear();
        foreach (Group g in groups.Values.OrderBy(x => x.action).ThenBy(x => x.dir))
        {
            var clip = new SpriteAnimSet.Clip
            {
                action = g.action,
                dir = g.dir,
                frames = g.files.Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s => s != null).ToArray(),
                fps = oldFps.TryGetValue(g.action + "|" + g.dir, out float f) ? f : DefaultFps(g.action),
                loop = g.action != "Attack"
            };
            set.clips.Add(clip);
        }
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(set);
        Selection.activeObject = set;

        string summary = string.Join("\n", groups.Values.GroupBy(x => x.action)
            .Select(a => $"{a.Key}: {a.Count()} ทิศ ({string.Join(", ", a.Select(x => $"{x.dir}={x.files.Count}"))})"));
        Debug.Log($"[MonsterSpriteSetBuilder] สร้าง {assetPath}\nPPU {ppu:0.##} · Pivot {pivot}\n{summary}" +
                  (skipped.Count > 0 ? $"\nข้าม {skipped.Count} ไฟล์ (ชื่อไม่บอกท่า/ทิศ):\n{string.Join("\n", skipped.Take(20))}" : ""), set);
        EditorUtility.DisplayDialog("Monster Anim Set", $"สร้างเสร็จ: {Path.GetFileName(assetPath)}\n\n{summary}" +
                                    (skipped.Count > 0 ? $"\n\nข้าม {skipped.Count} ไฟล์ — ดูรายชื่อใน Console" : ""), "OK");
    }

    private float DefaultFps(string action)
    {
        switch (action)
        {
            case "Walk": return walkFps;
            case "Run": return runFps;
            case "Attack": return attackFps;
            default: return idleFps;
        }
    }

    // ───────── อ่านชื่อ ─────────
    private static string ParseAction(string name)
    {
        string n = name.ToLowerInvariant();
        if (n.Contains("attack") || Regex.IsMatch(n, @"(^|[_\s-])atk([_\s-]|$)")) return "Attack";
        if (n.Contains("idle") || n.Contains("idel")) return "Idle";
        if (n.Contains("walk")) return "Walk";
        if (Regex.IsMatch(n, @"(^|[_\s-])run")) return "Run";
        return null;
    }

    private static SpriteAnimSet.Dir? ParseDir(string name)
    {
        string n = Regex.Replace(name.ToLowerInvariant(), @"[_\s-]", "");
        foreach (string d in DirNames)
        {
            if (!n.Contains(d)) continue;
            switch (d)
            {
                case "backleft": return SpriteAnimSet.Dir.BackLeft;
                case "backright": return SpriteAnimSet.Dir.BackRight;
                case "frontleft": return SpriteAnimSet.Dir.FrontLeft;
                case "frontright": return SpriteAnimSet.Dir.FrontRight;
                case "back": return SpriteAnimSet.Dir.Back;
                case "front": return SpriteAnimSet.Dir.Front;
                case "left": return SpriteAnimSet.Dir.Left;
                case "right": return SpriteAnimSet.Dir.Right;
            }
        }
        return null;
    }

    private static int FrameNumber(string path)
    {
        Match m = Regex.Match(Path.GetFileNameWithoutExtension(path), @"(\d+)(?!.*\d)");
        return m.Success ? int.Parse(m.Groups[1].Value) : 0;
    }

    // ───────── ขนาด/จุดหมุนให้ตรงรูปเดิม ─────────
    private bool MatchReference(Dictionary<string, Group> groups, out float ppu, out Vector2 pivot)
    {
        ppu = manualPixelsPerUnit; pivot = AlignmentToPivot(manualPivot);

        string refPath = AssetDatabase.GetAssetPath(referenceSprite.texture);
        Texture2D refTex = LoadRaw(refPath);
        if (refTex == null) return false;
        Rect rr = referenceSprite.rect;
        // รูปจริงอาจถูกย่อตอน import (Max Size) → เทียบสัดส่วนกับไฟล์ต้นฉบับ
        float rs = refTex.width / (float)referenceSprite.texture.width;
        RectInt refRect = new RectInt(Mathf.RoundToInt(rr.x * rs), Mathf.RoundToInt(rr.y * rs), Mathf.RoundToInt(rr.width * rs), Mathf.RoundToInt(rr.height * rs));
        if (!OpaqueBounds(refTex, refRect, out RectInt refBox)) { DestroyImmediate(refTex); return false; }
        float refPpu = referenceSprite.pixelsPerUnit * rs;
        Vector2 refPivotPx = new Vector2(refRect.x + referenceSprite.pivot.x * rs, refRect.y + referenceSprite.pivot.y * rs);
        float relX = (refPivotPx.x - refBox.xMin) / Mathf.Max(1, refBox.width);
        float relY = (refPivotPx.y - refBox.yMin) / Mathf.Max(1, refBox.height);
        float refWorldH = refBox.height / refPpu;
        DestroyImmediate(refTex);

        // กรอบรวมของท่า Idle (ไม่มี Idle = ทุกท่า) — ใช้กรอบเดียวกันทุกเฟรม ภาพจะไม่สั่น
        IEnumerable<string> sizeFiles = groups.Values.Where(g => g.action == "Idle").SelectMany(g => g.files);
        if (!sizeFiles.Any()) sizeFiles = groups.Values.SelectMany(g => g.files);

        RectInt union = default; bool any = false; Vector2Int texSize = default;
        foreach (string f in sizeFiles)
        {
            Texture2D t = LoadRaw(f);
            if (t == null) continue;
            if (texSize == default) texSize = new Vector2Int(t.width, t.height);
            else if (texSize.x != t.width || texSize.y != t.height)
                Debug.LogWarning($"[MonsterSpriteSetBuilder] รูปขนาดไม่เท่ากัน ({f}) — ภาพอาจขยับตอนเปลี่ยนเฟรม");
            if (OpaqueBounds(t, new RectInt(0, 0, t.width, t.height), out RectInt b))
            {
                if (!any) { union = b; any = true; }
                else
                {
                    int x0 = Mathf.Min(union.xMin, b.xMin), y0 = Mathf.Min(union.yMin, b.yMin);
                    int x1 = Mathf.Max(union.xMax, b.xMax), y1 = Mathf.Max(union.yMax, b.yMax);
                    union = new RectInt(x0, y0, x1 - x0, y1 - y0);
                }
            }
            DestroyImmediate(t);
        }
        if (!any || refWorldH <= 0f) return false;

        ppu = union.height / refWorldH;
        Vector2 px = new Vector2(union.xMin + relX * union.width, union.yMin + relY * union.height);
        pivot = new Vector2(px.x / texSize.x, px.y / texSize.y);
        return true;
    }

    private static Texture2D LoadRaw(string assetPath)
    {
        string full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
        if (!File.Exists(full)) return null;
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        return t.LoadImage(File.ReadAllBytes(full)) ? t : null;
    }

    private static bool OpaqueBounds(Texture2D t, RectInt area, out RectInt box)
    {
        Color32[] px = t.GetPixels32();
        int w = t.width;
        int xMin = int.MaxValue, yMin = int.MaxValue, xMax = -1, yMax = -1;
        for (int y = Mathf.Max(0, area.yMin); y < Mathf.Min(t.height, area.yMax); y++)
            for (int x = Mathf.Max(0, area.xMin); x < Mathf.Min(w, area.xMax); x++)
            {
                if (px[y * w + x].a < 16) continue;
                if (x < xMin) xMin = x;
                if (x > xMax) xMax = x;
                if (y < yMin) yMin = y;
                if (y > yMax) yMax = y;
            }
        if (xMax < 0) { box = default; return false; }
        box = new RectInt(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1);
        return true;
    }

    private static Vector2 AlignmentToPivot(SpriteAlignment a)
    {
        switch (a)
        {
            case SpriteAlignment.TopLeft: return new Vector2(0f, 1f);
            case SpriteAlignment.TopCenter: return new Vector2(0.5f, 1f);
            case SpriteAlignment.TopRight: return new Vector2(1f, 1f);
            case SpriteAlignment.LeftCenter: return new Vector2(0f, 0.5f);
            case SpriteAlignment.RightCenter: return new Vector2(1f, 0.5f);
            case SpriteAlignment.BottomLeft: return new Vector2(0f, 0f);
            case SpriteAlignment.BottomCenter: return new Vector2(0.5f, 0f);
            case SpriteAlignment.BottomRight: return new Vector2(1f, 0f);
            default: return new Vector2(0.5f, 0.5f);
        }
    }

    private void ApplyImport(string path, float ppu, Vector2 pivot)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) return;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = ppu;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.maxTextureSize = 4096;
        if (pixelArt)
        {
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
        }
        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteAlignment = (int)SpriteAlignment.Custom;
        s.spritePivot = pivot;
        s.spriteMeshType = SpriteMeshType.FullRect; // ทุกเฟรมกรอบเท่ากัน — ไม่ตัดขอบตามรูป (กันภาพกระตุก)
        ti.SetTextureSettings(s);
        ti.SaveAndReimport();
    }
}
#endif
