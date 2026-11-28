using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [Claude 2026-11-26] สร้างชุดภาพผู้เล่น (SpriteAnimSet) สำหรับ PlayerSpriteAnimator — ไม่ต้องต่อเส้นใน Animator
/// เมนู: Tools ▸ Player Sprites ▸ Build Player Anim Set
///
/// แหล่งรูป 2 แบบ:
///   • จากคลิปเดิม (.anim) — ใช้ของที่มีอยู่แล้วทั้งหมด (Walk, Run_Player, Walk_Gun ...) กดปุ่มเดียวจบ
///   • จากโฟลเดอร์รูป — สำหรับรูปชุดใหม่ในอนาคต: ตั้งชื่อโฟลเดอร์เป็น "ท่า_ทิศ" เช่น WalkGun_FrontLeft แล้วใส่รูป F1, F2, ...
/// ชื่อท่าที่รู้จัก: Idle · Walk · Run · Idle_Gun(IdleGun/HoldGun) · Walk_Gun · Run_Gun · Shoot · Idle_Pipe(HoldPipe) · Walk_Pipe · Run_Pipe(RunMelee) · Attack_Pipe
/// ท่าอื่นที่ไม่รู้จักก็เก็บไว้ได้ (ใช้ชื่อตามโฟลเดอร์)
/// </summary>
public class PlayerSpriteSetBuilder : EditorWindow
{
    private enum Source { ExistingClips, SpriteFolders }

    [SerializeField] private Source source = Source.ExistingClips;
    [SerializeField] private string clipFolder = "Assets/Animations";
    [SerializeField] private DefaultAsset spriteFolder;
    [SerializeField] private string outputPath = "Assets/Animations/Player_AnimSet.asset";
    [SerializeField] private bool overrideFps = true;
    [SerializeField] private float idleFps = 4f, walkFps = 6f, runFps = 9f, shootFps = 10f, attackFps = 10f;
    private string report = "";
    private Vector2 scroll;

    private static readonly (string key, SpriteAnimSet.Dir dir)[] DirTokens =
    {
        ("backleft", SpriteAnimSet.Dir.BackLeft), ("backright", SpriteAnimSet.Dir.BackRight),
        ("frontleft", SpriteAnimSet.Dir.FrontLeft), ("frontright", SpriteAnimSet.Dir.FrontRight),
        ("back", SpriteAnimSet.Dir.Back), ("front", SpriteAnimSet.Dir.Front), ("down", SpriteAnimSet.Dir.Front),
        ("up", SpriteAnimSet.Dir.Back), ("left", SpriteAnimSet.Dir.Left), ("right", SpriteAnimSet.Dir.Right),
    };

    private static readonly Dictionary<string, string> ActionMap = new Dictionary<string, string>
    {
        { "idle", "Idle" }, { "idel", "Idle" }, { "idleplayer", "Idle" }, { "idelplayer", "Idle" },
        { "walk", "Walk" }, { "walkplayer", "Walk" },
        { "run", "Run" }, { "runplayer", "Run" },
        { "idlegun", "Idle_Gun" }, { "holdgun", "Idle_Gun" }, { "idlegunplayer", "Idle_Gun" }, { "holdgunplayer", "Idle_Gun" },
        { "walkgun", "Walk_Gun" }, { "walkgunplayer", "Walk_Gun" },
        { "rungun", "Run_Gun" }, { "rungunplayer", "Run_Gun" },
        { "shoot", "Shoot" }, { "shootgun", "Shoot" },
        { "holdpipe", "Idle_Pipe" }, { "idlepipe", "Idle_Pipe" }, { "holdpipeplayer", "Idle_Pipe" },
        { "walkpipe", "Walk_Pipe" }, { "walkpipeplayer", "Walk_Pipe" },
        { "runpipe", "Run_Pipe" }, { "runmelee", "Run_Pipe" }, { "runpipeplayer", "Run_Pipe" },
        { "attackpipe", "Attack_Pipe" }, { "attack", "Attack_Pipe" }, { "attackplayer", "Attack_Pipe" },
    };

    [MenuItem("Tools/Player Sprites/Build Player Anim Set")]
    public static void Open() => GetWindow<PlayerSpriteSetBuilder>("Player Anim Set").minSize = new Vector2(420, 420);

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "1) กด Build → ได้ไฟล์ชุดภาพผู้เล่น\n" +
            "2) กด \"ใส่ให้ Player ในซีน\" → แปะ PlayerSpriteAnimator ที่ GFX ให้เอง\n" +
            "ไม่ต้องต่อเส้นใน Animator · เพิ่มท่า/เฟรมใหม่ = ใส่รูปแล้วกด Build ใหม่", MessageType.Info);

        source = (Source)GUILayout.Toolbar((int)source, new[] { "จากคลิปเดิม (.anim)", "จากโฟลเดอร์รูป" });
        if (source == Source.ExistingClips) clipFolder = EditorGUILayout.TextField("โฟลเดอร์คลิป", clipFolder);
        else spriteFolder = (DefaultAsset)EditorGUILayout.ObjectField("โฟลเดอร์รูป", spriteFolder, typeof(DefaultAsset), false);
        outputPath = EditorGUILayout.TextField("บันทึกเป็น", outputPath);

        EditorGUILayout.Space();
        overrideFps = EditorGUILayout.ToggleLeft("ตั้ง FPS ใหม่ทุกท่า (ปิด = ใช้ความเร็วเดิมของคลิป)", overrideFps);
        using (new EditorGUI.DisabledScope(!overrideFps))
        {
            idleFps = EditorGUILayout.Slider("Idle (ยืน)", idleFps, 1f, 20f);
            walkFps = EditorGUILayout.Slider("Walk (เดิน)", walkFps, 1f, 20f);
            runFps = EditorGUILayout.Slider("Run (วิ่ง)", runFps, 1f, 24f);
            shootFps = EditorGUILayout.Slider("Shoot (ยิง)", shootFps, 1f, 24f);
            attackFps = EditorGUILayout.Slider("Attack (ตี)", attackFps, 1f, 24f);
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Build", GUILayout.Height(32))) Build();
        using (new EditorGUI.DisabledScope(AssetDatabase.LoadAssetAtPath<SpriteAnimSet>(outputPath) == null))
            if (GUILayout.Button("ใส่ให้ Player ในซีน", GUILayout.Height(26))) AttachToPlayer();

        if (!string.IsNullOrEmpty(report))
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox(report, MessageType.None);
            EditorGUILayout.EndScrollView();
        }
    }

    // ═════════════════════════ อ่านชื่อ ═════════════════════════
    private static string Norm(string s) => Regex.Replace(s.ToLower(), @"[^a-z]", "");

    /// <summary>แยก "ชื่อท่า + ทิศ" จากชื่อ เช่น Walk_Gun_FrontLeft → (walkgun, FrontLeft)</summary>
    private static bool SplitName(string raw, out string actionKey, out SpriteAnimSet.Dir dir)
    {
        string n = Norm(Regex.Replace(raw, @"\d+$", ""));
        foreach (var (key, d) in DirTokens)
            if (n.EndsWith(key) && n.Length > key.Length)
            {
                actionKey = n.Substring(0, n.Length - key.Length);
                dir = d;
                return true;
            }
        actionKey = n; dir = SpriteAnimSet.Dir.Front;
        return false;
    }

    private static string MapAction(string key, string fallback)
    {
        if (ActionMap.TryGetValue(key, out string a)) return a;
        if (key.EndsWith("player") && ActionMap.TryGetValue(key.Substring(0, key.Length - 6), out a)) return a;
        return fallback;
    }

    private float FpsFor(string action, float original)
    {
        if (!overrideFps) return original;
        if (action.StartsWith("Run")) return runFps;
        if (action.StartsWith("Walk")) return walkFps;
        if (action == "Shoot") return shootFps;
        if (action.StartsWith("Attack")) return attackFps;
        return idleFps;
    }

    // ═════════════════════════ สร้าง ═════════════════════════
    private void Build()
    {
        var clips = source == Source.ExistingClips ? FromClips() : FromFolders();
        if (clips.Count == 0) { report = "ไม่เจอรูป/คลิปที่อ่านได้เลย — เช็กโฟลเดอร์"; return; }

        SpriteAnimSet set = AssetDatabase.LoadAssetAtPath<SpriteAnimSet>(outputPath);
        bool isNew = set == null;
        if (isNew) set = CreateInstance<SpriteAnimSet>();
        else Undo.RecordObject(set, "Build Player Anim Set");
        set.clips = clips;
        if (isNew)
        {
            string dir = Path.GetDirectoryName(outputPath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir)) { Debug.LogError("ไม่มีโฟลเดอร์ " + dir); return; }
            AssetDatabase.CreateAsset(set, outputPath);
        }
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();

        // รายงาน: ท่าไหนมีกี่ทิศ
        var byAction = new SortedDictionary<string, List<string>>();
        foreach (var c in clips)
        {
            if (!byAction.TryGetValue(c.action, out var l)) byAction[c.action] = l = new List<string>();
            l.Add($"{c.dir}({c.frames.Length})");
        }
        var sb = new System.Text.StringBuilder($"สร้างแล้ว: {outputPath}\n{clips.Count} คลิป\n\n");
        foreach (var kv in byAction) sb.AppendLine($"• {kv.Key} — {kv.Value.Count} ทิศ: {string.Join(", ", kv.Value)}");
        foreach (string need in new[] { "Idle", "Walk", "Run", "Idle_Gun", "Walk_Gun", "Run_Gun", "Shoot", "Idle_Pipe", "Walk_Pipe", "Run_Pipe", "Attack_Pipe" })
            if (!byAction.ContainsKey(need)) sb.AppendLine($"  (ไม่มี {need} → ใช้ท่าใกล้เคียงแทน)");
        report = sb.ToString();
        EditorGUIUtility.PingObject(set);
    }

    private List<SpriteAnimSet.Clip> FromClips()
    {
        var list = new List<SpriteAnimSet.Clip>();
        var seen = new HashSet<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { clipFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("/_Backup_") || path.Contains("/DoorAnimator/")) continue;
            string folder = Path.GetFileName(Path.GetDirectoryName(path));
            string fkey = Norm(folder);
            if (fkey.Contains("hitattack") || fkey.Contains("slash")) continue;   // เอฟเฟกต์ ไม่ใช่ท่าตัวละคร

            AnimationClip ac = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (ac == null || !SplitName(ac.name, out string clipKey, out SpriteAnimSet.Dir dir)) continue;
            string action = MapAction(fkey, null) ?? MapAction(clipKey, null);
            if (action == null) continue;

            Sprite[] frames = ReadFrames(ac, out float origFps);
            if (frames.Length == 0) continue;
            string key = action + "|" + dir;
            if (!seen.Add(key)) continue;   // ทิศซ้ำ (เช่นคลิปเก่า) เอาตัวแรก

            list.Add(new SpriteAnimSet.Clip
            {
                action = action,
                dir = dir,
                frames = frames,
                fps = FpsFor(action, origFps),
                loop = action != "Attack_Pipe"   // ยิงค้าง = วนท่ายิงไปเรื่อยๆ · ตีท่อ = รอบเดียว
            });
        }
        return list;
    }

    /// <summary>ดึงรูปตามลำดับคีย์ (ตัดคีย์ปิดท้ายลูปที่เป็นรูปแรกซ้ำ) + ประมาณ FPS เดิม</summary>
    private static Sprite[] ReadFrames(AnimationClip ac, out float fps)
    {
        fps = 6f;
        foreach (EditorCurveBinding b in AnimationUtility.GetObjectReferenceCurveBindings(ac))
        {
            if (b.propertyName != "m_Sprite") continue;
            ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(ac, b);
            if (keys == null || keys.Length == 0) continue;
            var frames = new List<Sprite>();
            float sample = 1f / Mathf.Max(1f, ac.frameRate);
            for (int i = 0; i < keys.Length; i++)
            {
                float end = i + 1 < keys.Length ? keys[i + 1].time : ac.length;
                bool tinyLast = i == keys.Length - 1 && keys.Length > 1 && end - keys[i].time <= sample * 1.5f;
                if (tinyLast && keys[i].value == keys[0].value) continue;
                if (keys[i].value is Sprite s) frames.Add(s);
            }
            if (frames.Count > 0 && ac.length > 0.01f) fps = Mathf.Clamp(Mathf.Round(frames.Count / ac.length * 2f) / 2f, 1f, 24f);
            return frames.ToArray();
        }
        return new Sprite[0];
    }

    private List<SpriteAnimSet.Clip> FromFolders()
    {
        var list = new List<SpriteAnimSet.Clip>();
        if (spriteFolder == null) return list;
        string root = AssetDatabase.GetAssetPath(spriteFolder);
        var groups = new Dictionary<string, List<(int num, string path)>>();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string folder = Path.GetFileName(Path.GetDirectoryName(path));
            if (!groups.TryGetValue(folder, out var g)) groups[folder] = g = new List<(int, string)>();
            Match m = Regex.Match(Path.GetFileNameWithoutExtension(path), @"(\d+)\D*$");
            g.Add((m.Success ? int.Parse(m.Groups[1].Value) : 0, path));
        }
        foreach (var kv in groups)
        {
            if (!SplitName(kv.Key, out string key, out SpriteAnimSet.Dir dir)) continue;
            string action = MapAction(key, kv.Key.Substring(0, Mathf.Max(1, kv.Key.Length - dir.ToString().Length)).TrimEnd('_', ' ', '-'));
            kv.Value.Sort((a, b) => a.num.CompareTo(b.num));
            var frames = new List<Sprite>();
            foreach (var (_, p) in kv.Value)
            {
                Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                if (s != null) frames.Add(s);
            }
            if (frames.Count == 0) continue;
            list.Add(new SpriteAnimSet.Clip { action = action, dir = dir, frames = frames.ToArray(), fps = FpsFor(action, 6f), loop = action != "Attack_Pipe" });
        }
        return list;
    }

    // ═════════════════════════ ใส่ให้ Player ═════════════════════════
    private void AttachToPlayer()
    {
        SpriteAnimSet set = AssetDatabase.LoadAssetAtPath<SpriteAnimSet>(outputPath);
        PlayerMovement pm = FindFirstObjectByType<PlayerMovement>(FindObjectsInactive.Include);
        if (pm == null) { EditorUtility.DisplayDialog("Player Anim Set", "ไม่เจอ Player (PlayerMovement) ในซีนที่เปิดอยู่", "ตกลง"); return; }
        SpriteRenderer sr = pm.animator != null ? pm.animator.GetComponent<SpriteRenderer>() : null;
        if (sr == null) sr = pm.GetComponentInChildren<SpriteRenderer>(true);
        if (sr == null) { EditorUtility.DisplayDialog("Player Anim Set", "ไม่เจอ SpriteRenderer ใต้ Player", "ตกลง"); return; }

        PlayerSpriteAnimator psa = sr.GetComponent<PlayerSpriteAnimator>();
        if (psa == null) psa = Undo.AddComponent<PlayerSpriteAnimator>(sr.gameObject);
        Undo.RecordObject(psa, "Assign Player Anim Set");
        psa.animSet = set;
        EditorUtility.SetDirty(psa);
        Selection.activeGameObject = sr.gameObject;
        EditorGUIUtility.PingObject(sr.gameObject);
        report = $"ใส่ PlayerSpriteAnimator ที่ \"{sr.name}\" แล้ว — กด Ctrl+S เซฟซีน แล้วกด Play ได้เลย\n\n" + report;
    }
}
