using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Rx = System.Text.RegularExpressions.Regex;

/// <summary>
/// [Claude 2026-11-25] Sprite Anim Retimer — จัดจังหวะ + สร้างคลิปอนิเมชันสไปรต์
/// เปิดที่ Tools > Anim Tools > Sprite Anim Retimer
///
///  แท็บ 1 "จัดจังหวะคลิปเดิม"
///   • โหลดคลิปทั้งโฟลเดอร์ → จัดกลุ่มตามท่า → ตั้ง FPS เดียวกันทั้งกลุ่ม → กดใช้
///   • ตั้ง "ค้างกี่จังหวะ" ต่อเฟรมได้ · ดูตัวอย่างทุกทิศ · แก้ลำดับ/เพิ่ม/ลบเฟรม · ร่างเฟรมกลาง
///   • สำรองไฟล์ก่อนแก้ทุกครั้ง + Ctrl+Z ได้
///
///  แท็บ 2 "สร้างคลิปใหม่"  (เพิ่มใหม่)
///   • เลือกสไปรต์/รูป/โฟลเดอร์ใน Project → กด "อ่านสไปรต์" → จัดกลุ่มเป็นคลิปให้อัตโนมัติจากชื่อ
///     เช่น Walk_Left_0, Walk_Left_1, Walk_Left_2 → คลิป "Walk_Left"
///   • ตั้ง FPS / ค้างกี่จังหวะ / ลูป ได้ (หรือให้เดาจากชื่อท่า) · ดูตัวอย่างก่อนสร้าง
///   • สร้างไฟล์ .anim ลงโฟลเดอร์ปลายทาง (แยกโฟลเดอร์ย่อยตามท่าได้) และเพิ่ม State ลง Animator Controller ให้ได้
/// </summary>
public class SpriteAnimRetimer : EditorWindow
{
    [MenuItem("Tools/Anim Tools/Sprite Anim Retimer")]
    public static void Open()
    {
        var w = GetWindow<SpriteAnimRetimer>("Anim Retimer");
        w.minSize = new Vector2(560, 480);
    }

    // ───────── ข้อมูล ─────────
    private class Frame
    {
        public Sprite sprite;
        public int hold = 1;         // ค้างกี่จังหวะ (1 จังหวะ = 1/FPS วินาที)
        public float oldDuration;    // ความยาวเดิม (ไว้โชว์)
        public bool draft;           // เฟรมร่างที่สร้างอัตโนมัติ
    }

    private class ClipInfo
    {
        public AnimationClip clip;
        public EditorCurveBinding binding;
        public List<Frame> frames = new List<Frame>();
        public float oldLength;
        public bool loop;
        public bool dirty;
        public string note = "";
    }

    private class Group
    {
        public string name;
        public List<ClipInfo> clips = new List<ClipInfo>();
        public float fps = 6f;
        public string holdPattern = "";
        public bool foldout = true;
        public bool selected = true;
    }

    /// <summary>คลิปที่กำลังจะสร้าง (ยังไม่ได้เขียนไฟล์)</summary>
    private class Draft
    {
        public string name;
        public string group;   // ชื่อโฟลเดอร์ย่อยปลายทาง (โหมดจัดกลุ่มตามโฟลเดอร์)
        public bool include = true;
        public ClipInfo info = new ClipInfo();
    }

    private string folder = "Assets/Animations";
    private readonly List<Group> groups = new List<Group>();
    private readonly List<string> skippedClips = new List<string>();
    private ClipInfo editing;
    private Group editingGroup;
    private Vector2 scrollLeft, scrollRight;
    private bool backup = true;
    private bool playPreview = true;
    private float previewSize = 72f;
    private double startTime;

    // ── สถานะแท็บ "สร้างคลิปใหม่" ──
    private int tab;
    private readonly List<Draft> drafts = new List<Draft>();
    private string outFolder = "Assets/Animations";
    private bool subByPose = true;
    private string targetPath = "";
    private AnimationClip refClip;
    private bool autoFps = true;
    private float newFps = 6f;
    private string newHold = "";
    private bool autoLoop = true;
    private bool loopManual = true;
    private bool overwrite = false;
    private int groupMode = 0; // 0 = 1 โฟลเดอร์ = 1 คลิป · 1 = ตามชื่อสไปรต์ · 2 = รวมเป็นคลิปเดียว
    private string mergeName = "NewClip";
    private AnimatorController controller;
    private string treeStateName = "Run_Gun_Tree";
    private string refStateName = "Idle_Gun_Tree";
    private readonly List<AnimationClip> lastCreated = new List<AnimationClip>();
    private Vector2 scrollCreate;

    // FPS ที่แนะนำตามชื่อท่า
    private static float SuggestFps(string name)
    {
        string n = name.ToLower();
        if (n.Contains("shoot") || n.Contains("attack") || n.Contains("slash") || n.Contains("hit")) return 10f;
        if (n.Contains("run")) return 8f;
        if (n.Contains("walk")) return 5f;
        return 4f; // idle / hold
    }

    // ท่าที่ควรเล่นครั้งเดียว (ไม่ลูป)
    private static bool SuggestLoop(string name)
    {
        string n = name.ToLower();
        return !(n.Contains("shoot") || n.Contains("attack") || n.Contains("slash") || n.Contains("hit")
              || n.Contains("die") || n.Contains("death") || n.Contains("dead") || n.Contains("hurt") || n.Contains("jump"));
    }

    private void OnEnable()
    {
        startTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
    }

    private void OnDisable() => EditorApplication.update -= Tick;

    private void Tick()
    {
        if (playPreview && (groups.Count > 0 || drafts.Count > 0)) Repaint();
    }

    // ═════════════════════════ โหลด ═════════════════════════
    private void Load()
    {
        groups.Clear();
        skippedClips.Clear();
        editing = null;
        var byName = new Dictionary<string, Group>();
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("/_Backup_")) continue;
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            ClipInfo ci = Read(clip);
            if (ci == null) { skippedClips.Add(Path.GetFileName(path)); continue; }
            string gname = Path.GetFileName(Path.GetDirectoryName(path));
            if (!byName.TryGetValue(gname, out Group g))
            {
                g = new Group { name = gname, fps = SuggestFps(gname) };
                byName[gname] = g;
                groups.Add(g);
            }
            g.clips.Add(ci);
        }
        groups.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
        foreach (Group g in groups) g.clips.Sort((a, b) => string.Compare(a.clip.name, b.clip.name, System.StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>อ่านคีย์สไปรต์จากคลิป — ตัดคีย์ปิดท้ายลูป (เฟรมแรกซ้ำที่ท้าย) และคีย์ที่ค้างแค่ 1 sample ทิ้ง</summary>
    private static ClipInfo Read(AnimationClip clip)
    {
        if (clip == null) return null;
        foreach (EditorCurveBinding b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            if (b.propertyName != "m_Sprite") continue;
            ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, b);
            if (keys == null || keys.Length == 0) continue;
            var ci = new ClipInfo
            {
                clip = clip,
                binding = b,
                oldLength = clip.length,
                loop = AnimationUtility.GetAnimationClipSettings(clip).loopTime
            };
            float sample = 1f / Mathf.Max(1f, clip.frameRate);
            for (int i = 0; i < keys.Length; i++)
            {
                float end = i + 1 < keys.Length ? keys[i + 1].time : clip.length;
                float dur = end - keys[i].time;
                bool tiny = dur <= sample * 1.5f && keys.Length > 1;
                if (tiny && i == keys.Length - 1)
                {
                    // คีย์ท้ายที่ค้าง 1 sample: เป็นคีย์ปิดลูป หรือเฟรมที่แทบไม่โผล่ → ตัดทิ้ง
                    bool closes = keys[i].value == keys[0].value;
                    ci.note += closes ? "ตัดคีย์ปิดท้ายลูป · " : $"ตัดเฟรมท้าย {Name(keys[i].value)} (โผล่แค่ {dur * 1000f:0} ms) · ";
                    continue;
                }
                ci.frames.Add(new Frame { sprite = keys[i].value as Sprite, oldDuration = dur });
            }
            return ci;
        }
        return null;
    }

    private static string Name(Object o) => o != null ? o.name : "(ว่าง)";

    // ═════════════════════════ ใช้ ═════════════════════════
    private static int[] ParsePattern(string s)
    {
        var list = new List<int>();
        if (!string.IsNullOrWhiteSpace(s))
            foreach (string p in s.Split(',', ' ', ';'))
                if (int.TryParse(p.Trim(), out int v) && v > 0) list.Add(Mathf.Min(v, 16));
        return list.ToArray();
    }

    private static void ApplyPattern(Group g)
    {
        int[] pat = ParsePattern(g.holdPattern);
        foreach (ClipInfo c in g.clips)
        {
            for (int i = 0; i < c.frames.Count; i++)
                c.frames[i].hold = pat.Length == 0 ? 1 : pat[Mathf.Min(i, pat.Length - 1)];
            c.dirty = true;
        }
    }

    private static float NewLength(ClipInfo c, float fps)
    {
        int ticks = 0;
        foreach (Frame f in c.frames) ticks += Mathf.Max(1, f.hold);
        return ticks / Mathf.Max(0.1f, fps);
    }

    private void ApplyGroup(Group g)
    {
        if (backup) Backup(g.clips);
        foreach (ClipInfo c in g.clips) Write(c, g.fps);
        AssetDatabase.SaveAssets();
        ShowNotification(new GUIContent($"จัดจังหวะ {g.clips.Count} คลิปของ {g.name} แล้ว"));
    }

    /// <summary>เขียนคีย์ใหม่: เฟรมเรียงห่างเท่ากันตาม FPS × hold + คีย์ปิดท้ายให้เฟรมสุดท้ายค้างครบจังหวะ</summary>
    private static void Write(ClipInfo c, float fps)
    {
        if (c.frames.Count == 0) return;
        float step = 1f / Mathf.Max(0.1f, fps);
        var keys = new List<ObjectReferenceKeyframe>();
        float t = 0f;
        foreach (Frame f in c.frames)
        {
            keys.Add(new ObjectReferenceKeyframe { time = t, value = f.sprite });
            t += step * Mathf.Max(1, f.hold);
        }
        keys.Add(new ObjectReferenceKeyframe { time = t, value = c.frames[c.frames.Count - 1].sprite });

        Undo.RecordObject(c.clip, "Sprite Anim Retimer");
        float oldLen = Mathf.Max(0.0001f, c.clip.length);

        // ขยับ Animation Event ตามสัดส่วนเวลาใหม่ (ถ้ามี)
        AnimationEvent[] ev = AnimationUtility.GetAnimationEvents(c.clip);
        if (ev != null && ev.Length > 0)
        {
            foreach (AnimationEvent e in ev) e.time = e.time / oldLen * t;
            AnimationUtility.SetAnimationEvents(c.clip, ev);
        }

        AnimationUtility.SetObjectReferenceCurve(c.clip, c.binding, keys.ToArray());
        EditorUtility.SetDirty(c.clip);
        c.oldLength = c.clip.length;
        foreach (Frame f in c.frames) f.oldDuration = step * Mathf.Max(1, f.hold);
        c.dirty = false;
        c.note = "";
    }

    private static void Backup(List<ClipInfo> clips)
    {
        string dir = "Assets/Animations/_Backup_" + System.DateTime.Now.ToString("yyyyMMdd_HHmm");
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Animations", Path.GetFileName(dir));
        foreach (ClipInfo c in clips)
        {
            string src = AssetDatabase.GetAssetPath(c.clip);
            string dst = dir + "/" + Path.GetFileName(src);
            if (!File.Exists(dst)) AssetDatabase.CopyAsset(src, dst);
        }
    }

    // ═════════════════════════ วาด UI ═════════════════════════
    private void OnGUI()
    {
        tab = GUILayout.Toolbar(tab, new[] { "จัดจังหวะคลิปเดิม", "สร้างคลิปใหม่" }, GUILayout.Height(22));
        if (tab == 1) { DrawCreator(); return; }

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("โฟลเดอร์", GUILayout.Width(50));
        folder = EditorGUILayout.TextField(folder, EditorStyles.toolbarTextField);
        if (GUILayout.Button("ใช้ที่เลือก", EditorStyles.toolbarButton, GUILayout.Width(70)))
            PickSelectedFolder(ref folder);
        if (GUILayout.Button("โหลด", EditorStyles.toolbarButton, GUILayout.Width(50))) Load();
        GUILayout.FlexibleSpace();
        backup = GUILayout.Toggle(backup, "สำรองก่อนแก้", EditorStyles.toolbarButton);
        playPreview = GUILayout.Toggle(playPreview, "▶ เล่นตัวอย่าง", EditorStyles.toolbarButton);
        EditorGUILayout.EndHorizontal();

        if (skippedClips.Count > 0)
        {
            int show = Mathf.Min(8, skippedClips.Count);
            EditorGUILayout.HelpBox($"ข้าม {skippedClips.Count} คลิปที่ไม่มีคีย์สไปรต์ (คลิปว่าง): " +
                string.Join(", ", skippedClips.GetRange(0, show)) + (skippedClips.Count > show ? " ..." : ""), MessageType.Warning);
        }

        if (groups.Count == 0)
        {
            EditorGUILayout.HelpBox("กด \"โหลด\" เพื่ออ่านคลิปทั้งหมดในโฟลเดอร์ (จะจัดกลุ่มตามโฟลเดอร์ย่อย เช่น Walk, Run_Player)\nถ้ายังไม่มีคลิป ให้ไปแท็บ \"สร้างคลิปใหม่\"", MessageType.Info);
            return;
        }

        EditorGUILayout.BeginHorizontal();
        // ── ซ้าย: กลุ่ม ──
        scrollLeft = EditorGUILayout.BeginScrollView(scrollLeft, GUILayout.Width(position.width * (editing != null ? 0.55f : 1f)));
        foreach (Group g in groups) DrawGroup(g);
        EditorGUILayout.Space(8);
        if (GUILayout.Button("ใช้กับทุกกลุ่มที่ติ๊ก", GUILayout.Height(28)) &&
            EditorUtility.DisplayDialog("Anim Retimer", "จัดจังหวะทุกกลุ่มที่ติ๊กไว้?\n(สำรองไฟล์ก่อน + Ctrl+Z ได้)", "ใช้", "ยกเลิก"))
            foreach (Group g in groups) if (g.selected) ApplyGroup(g);
        EditorGUILayout.EndScrollView();

        // ── ขวา: แก้เฟรมของคลิปที่เลือก ──
        if (editing != null)
        {
            scrollRight = EditorGUILayout.BeginScrollView(scrollRight);
            DrawEditor();
            EditorGUILayout.EndScrollView();
        }
        EditorGUILayout.EndHorizontal();
    }

    private static void PickSelectedFolder(ref string target)
    {
        foreach (Object o in Selection.GetFiltered<Object>(SelectionMode.Assets))
        {
            string p = AssetDatabase.GetAssetPath(o);
            if (AssetDatabase.IsValidFolder(p)) { target = p; break; }
        }
    }

    private void DrawGroup(Group g)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        g.selected = EditorGUILayout.Toggle(g.selected, GUILayout.Width(16));
        g.foldout = EditorGUILayout.Foldout(g.foldout, $"{g.name}  ({g.clips.Count} คลิป)", true, EditorStyles.foldoutHeader);
        EditorGUILayout.EndHorizontal();
        if (!g.foldout) { EditorGUILayout.EndVertical(); return; }

        // ความยาวเดิมของแต่ละทิศ (เห็นเลยว่าไม่เท่ากัน)
        float minL = float.MaxValue, maxL = 0f;
        foreach (ClipInfo c in g.clips) { minL = Mathf.Min(minL, c.oldLength); maxL = Mathf.Max(maxL, c.oldLength); }
        if (maxL - minL > 0.05f)
            EditorGUILayout.HelpBox($"ตอนนี้แต่ละทิศยาวไม่เท่ากัน: {minL:0.00}–{maxL:0.00} วิ → เปลี่ยนทิศแล้วจังหวะกระตุก", MessageType.Warning);

        EditorGUILayout.BeginHorizontal();
        g.fps = EditorGUILayout.Slider(new GUIContent("FPS", "กี่เฟรมต่อวินาที — Idle 3-4 · Walk 5-6 · Run 8-10 · ยิง/ตี 10-12"), g.fps, 1f, 24f);
        if (GUILayout.Button(new GUIContent("แนะนำ", "ใช้ค่าตามชนิดท่า"), EditorStyles.miniButton, GUILayout.Width(50))) g.fps = SuggestFps(g.name);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        g.holdPattern = EditorGUILayout.TextField(new GUIContent("ค้างกี่จังหวะ", "เว้นว่าง = ทุกเฟรมเท่ากัน\nเช่น 2,1,2,1 = เฟรมที่ 1 และ 3 ค้างนานเป็น 2 เท่า (ท่าเท้าแตะพื้น)"), g.holdPattern);
        if (GUILayout.Button("ตั้งทุกคลิป", EditorStyles.miniButton, GUILayout.Width(70))) ApplyPattern(g);
        EditorGUILayout.EndHorizontal();

        // แถวคลิป
        foreach (ClipInfo c in g.clips)
        {
            EditorGUILayout.BeginHorizontal();
            DrawPreview(c, g.fps, previewSize * 0.6f);
            EditorGUILayout.BeginVertical();
            GUILayout.Label(c.clip.name + (c.dirty ? "  •" : ""), EditorStyles.boldLabel);
            GUILayout.Label($"{c.frames.Count} เฟรม · เดิม {c.oldLength:0.00} วิ → ใหม่ {NewLength(c, g.fps):0.00} วิ · จังหวะเดิม: {Uneven(c)}", EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(c.note)) GUILayout.Label(c.note, EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            if (GUILayout.Button("แก้เฟรม", EditorStyles.miniButton, GUILayout.Width(60))) { editing = c; editingGroup = g; }
            EditorGUILayout.EndHorizontal();
        }
        if (GUILayout.Button($"ใช้กับกลุ่ม {g.name}"))
            ApplyGroup(g);
        EditorGUILayout.EndVertical();
    }

    private static string Uneven(ClipInfo c)
    {
        float mn = float.MaxValue, mx = 0f;
        foreach (Frame f in c.frames) { mn = Mathf.Min(mn, f.oldDuration); mx = Mathf.Max(mx, f.oldDuration); }
        if (c.frames.Count < 2 || mn <= 0f) return "-";
        float r = mx / mn;
        return r < 1.15f ? "สม่ำเสมอ" : $"ไม่เท่ากัน ({mn * 1000f:0}–{mx * 1000f:0} ms)";
    }

    /// <summary>เล่นตัวอย่างตามจังหวะใหม่</summary>
    private void DrawPreview(ClipInfo c, float fps, float size)
    {
        Rect r = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
        EditorGUI.DrawRect(r, new Color(0.13f, 0.13f, 0.13f));
        if (c.frames.Count == 0) return;
        int ticks = 0;
        foreach (Frame f in c.frames) ticks += Mathf.Max(1, f.hold);
        int tick = (int)((EditorApplication.timeSinceStartup - startTime) * fps);
        if (!c.loop && playPreview) tick = Mathf.Min(tick % (ticks + Mathf.CeilToInt(fps * 0.6f)), ticks - 1); // ไม่ลูป: ค้างท้ายแป๊บนึงแล้วเริ่มใหม่
        else tick %= ticks;
        if (!playPreview) tick = 0;
        int idx = 0;
        for (int acc = 0; idx < c.frames.Count; idx++)
        {
            acc += Mathf.Max(1, c.frames[idx].hold);
            if (tick < acc) break;
        }
        DrawSprite(r, c.frames[Mathf.Min(idx, c.frames.Count - 1)].sprite);
    }

    private static void DrawSprite(Rect r, Sprite s)
    {
        if (s == null || s.texture == null) return;
        Rect tr = s.textureRect;
        Texture2D tex = s.texture;
        Rect uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
        float aspect = tr.width / tr.height;
        Rect dst = r;
        if (aspect > 1f) { dst.height = r.width / aspect; dst.y += (r.height - dst.height) * 0.5f; }
        else { dst.width = r.height * aspect; dst.x += (r.width - dst.width) * 0.5f; }
        GUI.DrawTextureWithTexCoords(dst, tex, uv, true);
    }

    // ───────── แก้เฟรมรายคลิป ─────────
    private void DrawEditor()
    {
        ClipInfo c = editing;
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(c.clip.name, EditorStyles.largeLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("ปิด", GUILayout.Width(40))) { editing = null; return; }
        EditorGUILayout.EndHorizontal();

        DrawPreview(c, editingGroup.fps, 160f);
        EditorGUILayout.LabelField($"ความยาวใหม่ {NewLength(c, editingGroup.fps):0.00} วิ ที่ {editingGroup.fps:0.#} FPS", EditorStyles.miniLabel);

        int remove = -1, up = -1, tween = -1, dup = -1;
        for (int i = 0; i < c.frames.Count; i++)
        {
            Frame f = c.frames[i];
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            Rect thumb = GUILayoutUtility.GetRect(40, 40, GUILayout.Width(40), GUILayout.Height(40));
            EditorGUI.DrawRect(thumb, new Color(0.13f, 0.13f, 0.13f));
            DrawSprite(thumb, f.sprite);
            EditorGUILayout.BeginVertical();
            EditorGUI.BeginChangeCheck();
            f.sprite = (Sprite)EditorGUILayout.ObjectField(f.sprite, typeof(Sprite), false);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("ค้าง", GUILayout.Width(26));
            f.hold = Mathf.Clamp(EditorGUILayout.IntField(f.hold, GUILayout.Width(30)), 1, 16);
            GUILayout.Label($"จังหวะ (เดิม {f.oldDuration * 1000f:0} ms){(f.draft ? "  · ร่าง" : "")}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck()) c.dirty = true;
            EditorGUILayout.EndVertical();
            if (GUILayout.Button(new GUIContent("↑", "เลื่อนขึ้น"), GUILayout.Width(22)) && i > 0) up = i;
            if (GUILayout.Button(new GUIContent("⧉", "ทำซ้ำเฟรมนี้"), GUILayout.Width(22))) dup = i;
            if (GUILayout.Button(new GUIContent("✕", "ลบเฟรม"), GUILayout.Width(22))) remove = i;
            EditorGUILayout.EndHorizontal();

            if (i < c.frames.Count - 1 || c.loop)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("+ ร่างเฟรมกลาง", "สร้างรูปกึ่งกลางระหว่างเฟรมนี้กับเฟรมถัดไป (ร่างไว้ให้คนวาดใช้อ้างอิง)"), EditorStyles.miniButton, GUILayout.Width(110))) tween = i;
                EditorGUILayout.EndHorizontal();
            }
        }

        if (up > 0) { var t = c.frames[up]; c.frames.RemoveAt(up); c.frames.Insert(up - 1, t); c.dirty = true; }
        if (dup >= 0) { var s = c.frames[dup]; c.frames.Insert(dup + 1, new Frame { sprite = s.sprite, hold = s.hold }); c.dirty = true; }
        if (remove >= 0 && c.frames.Count > 1) { c.frames.RemoveAt(remove); c.dirty = true; }
        if (tween >= 0) MakeTween(c, tween);

        EditorGUILayout.Space(4);
        if (GUILayout.Button("+ เพิ่มเฟรมท้าย")) { c.frames.Add(new Frame { sprite = c.frames.Count > 0 ? c.frames[c.frames.Count - 1].sprite : null }); c.dirty = true; }
        EditorGUILayout.Space(4);
        if (GUILayout.Button("ใช้กับคลิปนี้", GUILayout.Height(26)))
        {
            if (backup) Backup(new List<ClipInfo> { c });
            Write(c, editingGroup.fps);
            AssetDatabase.SaveAssets();
            ShowNotification(new GUIContent("บันทึกแล้ว"));
        }
    }

    // ───────── ร่างเฟรมกลาง ─────────
    /// <summary>
    /// รูปกลางระหว่าง 2 เฟรม: จุดที่ทึบทั้งคู่ = สีเฉลี่ย · จุดที่มีแค่รูปเดียว = ใส่แบบลายตารางหมากรุก (ดูเป็นภาพซ้อนเคลื่อนไหว)
    /// พิกเซลยังคม (ไม่เบลอ) — ใช้เป็นแบบร่างให้คนวาดเกลาต่อ ไม่ใช่งานจบ
    /// </summary>
    private void MakeTween(ClipInfo c, int i)
    {
        Frame a = c.frames[i];
        Frame b = c.frames[(i + 1) % c.frames.Count];
        if (a.sprite == null || b.sprite == null) return;
        string pa = AssetDatabase.GetAssetPath(a.sprite.texture), pb = AssetDatabase.GetAssetPath(b.sprite.texture);
        var ta = new Texture2D(2, 2); var tb = new Texture2D(2, 2);
        if (!ta.LoadImage(File.ReadAllBytes(pa)) || !tb.LoadImage(File.ReadAllBytes(pb))) { ShowNotification(new GUIContent("อ่านรูปไม่ได้")); return; }

        int w = Mathf.Max(ta.width, tb.width), h = Mathf.Max(ta.height, tb.height);
        var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color32[] o = new Color32[w * h];
        Color32[] A = ta.GetPixels32(), B = tb.GetPixels32();
        int ax = (w - ta.width) / 2, ay = (h - ta.height) / 2, bx = (w - tb.width) / 2, by = (h - tb.height) / 2;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color32 ca = Px(A, ta.width, ta.height, x - ax, y - ay);
                Color32 cb = Px(B, tb.width, tb.height, x - bx, y - by);
                bool oa = ca.a > 127, ob = cb.a > 127;
                Color32 r;
                if (oa && ob) r = new Color32((byte)((ca.r + cb.r) / 2), (byte)((ca.g + cb.g) / 2), (byte)((ca.b + cb.b) / 2), 255);
                else if (oa || ob) r = ((x + y) & 1) == 0 ? (oa ? ca : cb) : new Color32(0, 0, 0, 0);
                else r = new Color32(0, 0, 0, 0);
                o[y * w + x] = r;
            }
        outTex.SetPixels32(o);
        outTex.Apply();

        string dir = Path.GetDirectoryName(pa).Replace('\\', '/');
        string path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{Path.GetFileNameWithoutExtension(pa)}_tween_{Path.GetFileNameWithoutExtension(pb)}.png");
        File.WriteAllBytes(path, outTex.EncodeToPNG());
        DestroyImmediate(outTex); DestroyImmediate(ta); DestroyImmediate(tb);
        AssetDatabase.ImportAsset(path);

        // ตั้งค่านำเข้าให้เหมือนเฟรมต้นฉบับ (Sprite, PPU, pivot, Point filter, ไม่บีบอัด)
        var src = (TextureImporter)AssetImporter.GetAtPath(pa);
        var dst = (TextureImporter)AssetImporter.GetAtPath(path);
        if (src != null && dst != null)
        {
            var st = new TextureImporterSettings();
            src.ReadTextureSettings(st);
            dst.SetTextureSettings(st);
            dst.textureType = TextureImporterType.Sprite;
            dst.spriteImportMode = SpriteImportMode.Single;
            dst.textureCompression = src.textureCompression;
            dst.SaveAndReimport();
        }
        Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        c.frames.Insert(i + 1, new Frame { sprite = sp, hold = 1, draft = true });
        c.dirty = true;
        EditorGUIUtility.PingObject(sp);
    }

    private static Color32 Px(Color32[] p, int w, int h, int x, int y) =>
        x < 0 || y < 0 || x >= w || y >= h ? new Color32(0, 0, 0, 0) : p[y * w + x];

    // ═════════════════════════ สร้างคลิปใหม่ ═════════════════════════

    private float DraftFps(Draft d) => autoFps ? SuggestFps(d.name) : newFps;
    private bool DraftLoop(Draft d) => autoLoop ? SuggestLoop(d.name) : loopManual;

    /// <summary>ชื่อท่า = ชื่อคลิปที่ตัดชื่อทิศท้ายออก เช่น Walk_Left → Walk (ต้องมี _ - หรือเว้นวรรคคั่น)</summary>
    private static string PoseOf(string clipName)
    {
        string p = Rx.Replace(clipName,
            @"[\s_\-]+(Back[\s_\-]?Left|Back[\s_\-]?Right|Front[\s_\-]?Left|Front[\s_\-]?Right|Up[\s_\-]?Left|Up[\s_\-]?Right|Down[\s_\-]?Left|Down[\s_\-]?Right|Up|Down|Left|Right|Front|Back|Side|North|South|East|West|NE|NW|SE|SW|N|S|E|W)$",
            "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return string.IsNullOrEmpty(p) ? clipName : p;
    }

    private string ClipDir(Draft d)
    {
        if (!subByPose) return outFolder;
        string sub = string.IsNullOrEmpty(d.group) ? PoseOf(d.name) : d.group;
        return outFolder.TrimEnd('/') + "/" + sub;
    }

    private string ClipPath(Draft d) => ClipDir(d) + "/" + d.name + ".anim";

    private static string KeyOf(Sprite s)
    {
        string k = Rx.Replace(s.name, @"[\s_\-\.]*\d+$", "");
        return k.Length == 0 ? s.texture.name : k;
    }

    private static long NumOf(string n)
    {
        var m = Rx.Match(n, @"(\d+)$");
        return m.Success && long.TryParse(m.Value, out long v) ? v : 0;
    }

    /// <summary>ดึงสไปรต์จากสิ่งที่เลือกใน Project: สไปรต์เดี่ยว / รูป (ทุกชิ้นที่ slice) / โฟลเดอร์</summary>
    private static List<Sprite> CollectSelectedSprites(IEnumerable<Object> source)
    {
        var set = new HashSet<Sprite>();
        var list = new List<Sprite>();

        void AddAllAt(string path)
        {
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Sprite s && set.Add(s)) list.Add(s);
        }

        foreach (Object o in source)
        {
            string p = AssetDatabase.GetAssetPath(o);
            if (string.IsNullOrEmpty(p)) continue;
            if (AssetDatabase.IsValidFolder(p))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { p }))
                    AddAllAt(AssetDatabase.GUIDToAssetPath(guid));
            }
            else if (o is Sprite sp) { if (set.Add(sp)) list.Add(sp); }
            else AddAllAt(p);
        }
        return list;
    }

    private static string FolderOf(Sprite s) =>
        Path.GetDirectoryName(AssetDatabase.GetAssetPath(s)).Replace('\\', '/');

    /// <summary>อ่านสไปรต์จากที่เลือก (หรือที่ลากมาวาง) แล้วจัดเป็นคลิปตามโหมดที่เลือก</summary>
    private void CollectDrafts(IEnumerable<Object> source = null)
    {
        drafts.Clear();
        List<Sprite> sprites = CollectSelectedSprites(source ?? Selection.objects);
        if (sprites.Count == 0)
        {
            ShowNotification(new GUIContent("ไม่พบสไปรต์ (ต้องตั้งรูปเป็น Sprite แล้ว)"));
            return;
        }

        var byKey = new Dictionary<string, List<Sprite>>();
        foreach (Sprite s in sprites)
        {
            string key = groupMode == 0 ? FolderOf(s) : groupMode == 2 ? mergeName : KeyOf(s);
            if (!byKey.TryGetValue(key, out List<Sprite> l)) byKey[key] = l = new List<Sprite>();
            l.Add(s);
        }

        var keys = new List<string>(byKey.Keys);
        keys.Sort(System.StringComparer.OrdinalIgnoreCase);
        foreach (string key in keys)
        {
            List<Sprite> l = byKey[key];
            l.Sort((a, b) =>
            {
                if (groupMode != 0)
                {
                    int k = string.Compare(KeyOf(a), KeyOf(b), System.StringComparison.OrdinalIgnoreCase);
                    if (k != 0) return k;
                }
                int n = NumOf(a.name).CompareTo(NumOf(b.name));
                return n != 0 ? n : string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase);
            });

            var d = new Draft();
            if (groupMode == 0)
            {
                d.name = Path.GetFileName(key);                              // RunGun_Back
                d.group = Path.GetFileName(Path.GetDirectoryName(key));       // RunGun_Player
            }
            else d.name = key;
            foreach (Sprite s in l) d.info.frames.Add(new Frame { sprite = s });
            ApplyHold(d);
            drafts.Add(d);
        }
    }

    /// <summary>พื้นที่ลากวาง: ลากโฟลเดอร์/สไปรต์จาก Project มาวางได้เลย (หลายอันพร้อมกันได้)</summary>
    private void DrawDropZone()
    {
        Rect r = GUILayoutUtility.GetRect(0, 56, GUILayout.ExpandWidth(true));
        var style = new GUIStyle(EditorStyles.helpBox) { alignment = TextAnchor.MiddleCenter, fontSize = 12, wordWrap = true };
        GUI.Box(r, "⬇ ลากโฟลเดอร์ / สไปรต์ จาก Project มาวางตรงนี้\n(เช่น โฟลเดอร์ RunGun_Player ทั้งโฟลเดอร์ — ลากหลายโฟลเดอร์พร้อมกันได้)", style);

        Event e = Event.current;
        if ((e.type == EventType.DragUpdated || e.type == EventType.DragPerform) && r.Contains(e.mousePosition))
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                Object[] refs = DragAndDrop.objectReferences;
                if (refs == null || refs.Length == 0)
                {
                    var list = new List<Object>();
                    foreach (string p in DragAndDrop.paths)
                    {
                        Object o = AssetDatabase.LoadAssetAtPath<Object>(p);
                        if (o != null) list.Add(o);
                    }
                    refs = list.ToArray();
                }
                CollectDrafts(refs);
            }
            e.Use();
        }
    }

    private void ApplyHold(Draft d)
    {
        int[] pat = ParsePattern(newHold);
        for (int i = 0; i < d.info.frames.Count; i++)
            d.info.frames[i].hold = pat.Length == 0 ? 1 : pat[Mathf.Min(i, pat.Length - 1)];
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private void DrawCreator()
    {
        scrollCreate = EditorGUILayout.BeginScrollView(scrollCreate);

        EditorGUILayout.HelpBox(
            "ลากโฟลเดอร์ (เช่น RunGun_Player) มาวางในกรอบด้านล่าง → โหมดเริ่มต้นจะสร้าง 1 คลิปต่อ 1 โฟลเดอร์ย่อย\n" +
            "เช่น RunGun_Back, RunGun_BackLeft, ... → คลิปชื่อเดียวกัน เรียงเฟรมตามเลขท้ายชื่อไฟล์\n" +
            "แล้วตรวจตัวอย่าง ปรับ FPS/จังหวะ และกด \"สร้างคลิป\"", MessageType.Info);

        DrawDropZone();
        if (GUILayout.Button("หรือ: อ่านสไปรต์จากที่เลือกใน Project", GUILayout.Height(24))) CollectDrafts();
        EditorGUILayout.Space(4);

        // ── ตั้งค่า ──
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("ตั้งค่าการสร้าง", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        outFolder = EditorGUILayout.TextField("โฟลเดอร์ปลายทาง", outFolder);
        if (GUILayout.Button("ใช้ที่เลือก", EditorStyles.miniButton, GUILayout.Width(70))) PickSelectedFolder(ref outFolder);
        EditorGUILayout.EndHorizontal();

        subByPose = EditorGUILayout.Toggle(new GUIContent("แยกโฟลเดอร์ย่อยตามท่า", "Walk_Left → <ปลายทาง>/Walk/Walk_Left.anim (ตรงกับที่แท็บจัดจังหวะจัดกลุ่ม)"), subByPose);
        targetPath = EditorGUILayout.TextField(new GUIContent("Path ไป SpriteRenderer", "ตำแหน่งของ SpriteRenderer เทียบกับ GameObject ที่มี Animator\nเว้นว่าง = อยู่ GameObject เดียวกับ Animator · ถ้าอยู่ที่ลูกชื่อ GFX ให้ใส่ GFX"), targetPath);
        EditorGUI.BeginChangeCheck();
        refClip = (AnimationClip)EditorGUILayout.ObjectField(new GUIContent("ดึง Path จากคลิปตัวอย่าง", "ลากคลิปที่เล่นได้ปกติกับ Player อยู่แล้ว (เช่น Run_Back) มาใส่ → คัดลอก Path ให้อัตโนมัติ"), refClip, typeof(AnimationClip), false);
        if (EditorGUI.EndChangeCheck() && refClip != null)
        {
            string rp = PathFromClip(refClip);
            if (rp != null) targetPath = rp;
            else ShowNotification(new GUIContent("คลิปนี้ไม่มีคีย์สไปรต์"));
        }

        autoFps = EditorGUILayout.Toggle(new GUIContent("FPS ตามชื่อท่า", "Idle 4 · Walk 5 · Run 8 · Attack/Shoot/Hit 10"), autoFps);
        if (!autoFps) newFps = EditorGUILayout.Slider("FPS", newFps, 1f, 24f);

        EditorGUI.BeginChangeCheck();
        newHold = EditorGUILayout.TextField(new GUIContent("ค้างกี่จังหวะ", "เว้นว่าง = เท่ากันทุกเฟรม · เช่น 2,1,2,1"), newHold);
        if (EditorGUI.EndChangeCheck()) foreach (Draft d in drafts) ApplyHold(d);

        autoLoop = EditorGUILayout.Toggle(new GUIContent("ลูปตามชื่อท่า", "Attack/Shoot/Hit/Die/Jump = เล่นครั้งเดียว ที่เหลือลูป"), autoLoop);
        if (!autoLoop) loopManual = EditorGUILayout.Toggle("ลูป", loopManual);

        overwrite = EditorGUILayout.Toggle(new GUIContent("เขียนทับคลิปชื่อเดิม", "เปิด = แทนที่คีย์สไปรต์ในไฟล์เดิม (GUID เดิม Controller ไม่หลุด)\nปิด = สร้างไฟล์ใหม่ชื่อต่อท้ายเลข"), overwrite);
        controller = (AnimatorController)EditorGUILayout.ObjectField(new GUIContent("เพิ่ม State ลง Controller", "ไม่บังคับ — ถ้าใส่ จะสร้าง State ชื่อเดียวกับคลิปให้ (ไม่ต่อ Transition)"), controller, typeof(AnimatorController), false);

        groupMode = EditorGUILayout.Popup(new GUIContent("วิธีจัดกลุ่มเป็นคลิป", "เลือกก่อนลาก/อ่านสไปรต์ — เปลี่ยนแล้วต้องลากหรืออ่านใหม่"), groupMode,
            new[] { "1 โฟลเดอร์ = 1 คลิป (ชื่อตามโฟลเดอร์)", "ตามชื่อสไปรต์ (Walk_Left_0, _1, ...)", "รวมทั้งหมดเป็นคลิปเดียว" });
        if (groupMode == 2) mergeName = EditorGUILayout.TextField("ชื่อคลิป", mergeName);
        playPreview = EditorGUILayout.Toggle("▶ เล่นตัวอย่าง", playPreview);
        EditorGUILayout.EndVertical();

        // ── Blend Tree 8 ทิศ ──
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("สร้าง Blend Tree 8 ทิศ ใน Controller (ข้างบน)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("ใช้กับคลิปที่เพิ่งสร้าง หรือเลือกคลิป/โฟลเดอร์คลิป (เช่น RunGun_Player) ใน Project ก่อนกดก็ได้\n" +
            "ตำแหน่งแต่ละทิศคัดลอกจาก Blend Tree อ้างอิง โดยจับคู่จากคำท้ายชื่อคลิป (Back, BackLeft, Front ...)", MessageType.None);
        treeStateName = EditorGUILayout.TextField(new GUIContent("ชื่อ State ที่จะสร้าง", "ต้องตรงกับช่องใน PlayerMovement เช่น runGunState = Run_Gun_Tree"), treeStateName);
        refStateName = EditorGUILayout.TextField(new GUIContent("State อ้างอิง (Blend Tree)", "Tree เดิมที่ใช้ AimX/AimZ แบบเดียวกัน เช่น Idle_Gun_Tree"), refStateName);
        if (GUILayout.Button("สร้าง / อัปเดต Blend Tree", GUILayout.Height(24))) BuildBlendTree();
        EditorGUILayout.EndVertical();

        // ── รายการคลิปที่จะสร้าง ──
        EditorGUILayout.Space(4);
        if (drafts.Count == 0)
        {
            GUILayout.Label("ยังไม่มีรายการ — เลือกสไปรต์ใน Project แล้วกด \"อ่านสไปรต์\"", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndScrollView();
            return;
        }

        GUILayout.Label($"คลิปที่จะสร้าง ({drafts.Count})", EditorStyles.boldLabel);
        foreach (Draft d in drafts)
        {
            d.info.loop = DraftLoop(d);
            float fps = DraftFps(d);
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            d.include = EditorGUILayout.Toggle(d.include, GUILayout.Width(16));
            DrawPreview(d.info, fps, 56f);
            EditorGUILayout.BeginVertical();
            d.name = EditorGUILayout.TextField(d.name);
            bool exists = File.Exists(ClipPath(d));
            GUILayout.Label($"{d.info.frames.Count} เฟรม · {NewLength(d.info, fps):0.00} วิ @ {fps:0.#} FPS · {(d.info.loop ? "ลูป" : "เล่นครั้งเดียว")}" +
                            (exists ? (overwrite ? " · จะเขียนทับ" : " · มีไฟล์ชื่อนี้แล้ว (จะสร้างชื่อใหม่)") : ""), EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            if (GUILayout.Button(new GUIContent("⇄", "กลับลำดับเฟรม"), GUILayout.Width(26)))
            {
                d.info.frames.Reverse();
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(6);
        if (GUILayout.Button("สร้างคลิป", GUILayout.Height(30))) CreateClips();
        EditorGUILayout.EndScrollView();
    }

    private void CreateClips()
    {
        if (!outFolder.StartsWith("Assets"))
        {
            ShowNotification(new GUIContent("โฟลเดอร์ปลายทางต้องอยู่ใต้ Assets"));
            return;
        }
        // ยังไม่ได้ใส่ Path แต่ใส่ Controller → ลองดึงจากคลิปที่มีอยู่ใน Controller
        if (string.IsNullOrWhiteSpace(targetPath) && controller != null)
            foreach (AnimationClip ac in controller.animationClips)
            {
                string pp = PathFromClip(ac);
                if (!string.IsNullOrEmpty(pp)) { targetPath = pp; break; }
            }
        int made = 0, updated = 0;
        AnimationClip last = null;
        var seen = new HashSet<string>();
        var written = new List<string>();
        lastCreated.Clear();

        foreach (Draft d in drafts)
        {
            if (!d.include || d.info.frames.Count == 0 || string.IsNullOrWhiteSpace(d.name)) continue;
            if (d.info.frames.Exists(f => f.sprite == null)) continue;

            string dir = ClipDir(d);
            EnsureFolder(dir);
            string path = dir + "/" + d.name + ".anim";
            if (!seen.Add(path)) continue; // ชื่อซ้ำกันในรายการ

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            float fps = DraftFps(d);

            if (clip != null && !overwrite)
            {
                path = AssetDatabase.GenerateUniqueAssetPath(path);
                clip = null;
            }

            if (clip == null)
            {
                clip = new AnimationClip { frameRate = fps, name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(clip, path);
                made++;
            }
            else
            {
                // เขียนทับ: ล้างคีย์สไปรต์เดิมก่อน (คง Event/GUID ไว้)
                foreach (EditorCurveBinding b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                    if (b.propertyName == "m_Sprite") AnimationUtility.SetObjectReferenceCurve(clip, b, null);
                clip.frameRate = fps;
                updated++;
            }

            d.info.clip = clip;
            d.info.binding = EditorCurveBinding.PPtrCurve(targetPath.Trim(), typeof(SpriteRenderer), "m_Sprite");
            Write(d.info, fps);

            AnimationClipSettings st = AnimationUtility.GetAnimationClipSettings(clip);
            st.loopTime = DraftLoop(d);
            AnimationUtility.SetAnimationClipSettings(clip, st);
            EditorUtility.SetDirty(clip);

            if (controller != null) AddToController(controller, clip);
            written.Add(path);
            lastCreated.Add(clip);
            last = clip;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (last != null) { Selection.activeObject = last; EditorGUIUtility.PingObject(last); }
        // ตรวจซ้ำหลังบันทึกจริง: โหลดไฟล์กลับมาดูว่ามีคีย์สไปรต์ไหม
        int bad = 0;
        foreach (string wp in written)
        {
            if (PathFromClip(AssetDatabase.LoadAssetAtPath<AnimationClip>(wp)) == null)
            {
                bad++;
                Debug.LogWarning($"[Anim Retimer] {wp} ไม่มีคีย์สไปรต์หลังบันทึก");
            }
        }
        ShowNotification(new GUIContent($"สร้าง {made} · เขียนทับ {updated} คลิป" + (bad > 0 ? $" · ⚠ ว่างเปล่า {bad} คลิป (ดู Console)" : " · ตรวจแล้วครบ")));

        // รีเฟรชแท็บจัดจังหวะ ถ้าโฟลเดอร์อยู่ในช่วงที่โหลดไว้
        if (groups.Count > 0 && outFolder.StartsWith(folder)) Load();
    }

    // ═════════════ Blend Tree 8 ทิศ ═════════════
    private static string DirKey(string clipName)
    {
        int i = clipName.LastIndexOf('_');
        return (i >= 0 ? clipName.Substring(i + 1) : clipName).ToLowerInvariant();
    }

    private static AnimatorState FindState(AnimatorStateMachine sm, string name)
    {
        foreach (ChildAnimatorState cs in sm.states)
            if (cs.state.name == name) return cs.state;
        foreach (ChildAnimatorStateMachine sub in sm.stateMachines)
        {
            AnimatorState s = FindState(sub.stateMachine, name);
            if (s != null) return s;
        }
        return null;
    }

    private List<AnimationClip> ClipsForTree()
    {
        var list = new List<AnimationClip>();
        foreach (Object o in Selection.objects)
        {
            string p = AssetDatabase.GetAssetPath(o);
            if (string.IsNullOrEmpty(p)) continue;
            if (AssetDatabase.IsValidFolder(p))
            {
                foreach (string g in AssetDatabase.FindAssets("t:AnimationClip", new[] { p }))
                {
                    string cp = AssetDatabase.GUIDToAssetPath(g);
                    if (cp.Contains("/_Backup_")) continue;
                    var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(cp);
                    if (c != null && !list.Contains(c)) list.Add(c);
                }
            }
            else if (o is AnimationClip ac && !list.Contains(ac)) list.Add(ac);
        }
        if (list.Count == 0) list.AddRange(lastCreated);
        return list;
    }

    private void BuildBlendTree()
    {
        string newName = treeStateName.Trim(), refName = refStateName.Trim();
        if (controller == null) { ShowNotification(new GUIContent("ใส่ Animator Controller ก่อน")); return; }
        if (newName.Length == 0 || newName == refName) { ShowNotification(new GUIContent("ชื่อ State ต้องไม่ว่าง และต้องไม่ซ้ำกับ State อ้างอิง")); return; }
        if (controller.layers.Length == 0) { ShowNotification(new GUIContent("Controller ไม่มี Layer")); return; }

        AnimatorStateMachine root = controller.layers[0].stateMachine;
        AnimatorState refState = FindState(root, refName);
        BlendTree refTree = refState != null ? refState.motion as BlendTree : null;
        if (refTree == null) { ShowNotification(new GUIContent($"ไม่พบ Blend Tree อ้างอิง '{refName}' ใน Controller")); return; }

        List<AnimationClip> clips = ClipsForTree();
        if (clips.Count == 0) { ShowNotification(new GUIContent("ไม่พบคลิป — เลือกคลิป/โฟลเดอร์คลิปใน Project หรือสร้างคลิปก่อน")); return; }

        var prefixes = new HashSet<string>();
        foreach (AnimationClip c in clips)
        {
            int i = c.name.LastIndexOf('_');
            prefixes.Add(i > 0 ? c.name.Substring(0, i) : c.name);
        }
        if (prefixes.Count > 1)
        {
            ShowNotification(new GUIContent("คลิปที่เลือกมีหลายชุด เลือกทีละชุด (ดู Console)"));
            Debug.LogWarning("[Anim Retimer] พบหลายชุด: " + string.Join(", ", prefixes));
            return;
        }

        var byDir = new Dictionary<string, AnimationClip>();
        foreach (AnimationClip c in clips) byDir[DirKey(c.name)] = c;

        var tree = new BlendTree
        {
            name = newName,
            blendType = refTree.blendType,
            blendParameter = refTree.blendParameter,
            blendParameterY = refTree.blendParameterY,
            useAutomaticThresholds = refTree.useAutomaticThresholds
        };

        var kids = new List<ChildMotion>();
        var missing = new List<string>();
        foreach (ChildMotion ch in refTree.children)
        {
            var rc = ch.motion as AnimationClip;
            if (rc == null) continue;
            string key = DirKey(rc.name);
            if (!byDir.TryGetValue(key, out AnimationClip nc)) { missing.Add(key); continue; }
            kids.Add(new ChildMotion
            {
                motion = nc,
                position = ch.position,
                threshold = ch.threshold,
                timeScale = ch.timeScale,
                cycleOffset = ch.cycleOffset,
                mirror = ch.mirror,
                directBlendParameter = ch.directBlendParameter
            });
        }
        if (kids.Count == 0)
        {
            ShowNotification(new GUIContent("จับคู่ทิศไม่ได้เลย — เช็กคำท้ายชื่อคลิปให้ตรงกับของ Tree อ้างอิง"));
            return;
        }
        tree.children = kids.ToArray();

        AssetDatabase.AddObjectToAsset(tree, controller);
        tree.hideFlags = HideFlags.HideInHierarchy;

        AnimatorState st = FindState(root, newName);
        if (st == null) st = root.AddState(newName);
        else if (st.motion is BlendTree old && AssetDatabase.IsSubAsset(old))
        {
            AssetDatabase.RemoveObjectFromAsset(old);
            DestroyImmediate(old, true);
        }
        st.motion = tree;
        st.speed = refState.speed;
        st.speedParameterActive = refState.speedParameterActive;
        st.speedParameter = refState.speedParameter;
        st.writeDefaultValues = refState.writeDefaultValues;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        string msg = $"สร้าง '{newName}' แล้ว: ใส่ {kids.Count} ทิศ";
        if (missing.Count > 0) { msg += $" · ขาด {missing.Count} ทิศ (ดู Console)"; Debug.LogWarning("[Anim Retimer] ไม่มีคลิปสำหรับทิศ: " + string.Join(", ", missing)); }
        ShowNotification(new GUIContent(msg));
    }

    /// <summary>Path ของคีย์สไปรต์ในคลิป (null = ไม่มีคีย์สไปรต์)</summary>
    private static string PathFromClip(AnimationClip c)
    {
        if (c == null) return null;
        foreach (EditorCurveBinding b in AnimationUtility.GetObjectReferenceCurveBindings(c))
            if (b.propertyName == "m_Sprite") return b.path;
        return null;
    }

    private static void AddToController(AnimatorController ctrl, AnimationClip clip)
    {
        if (ctrl.layers.Length == 0) ctrl.AddLayer("Base Layer");
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        foreach (ChildAnimatorState cs in sm.states)
            if (cs.state.motion == clip) return;
        AnimatorState state = sm.AddState(clip.name);
        state.motion = clip;
        EditorUtility.SetDirty(ctrl);
    }
}