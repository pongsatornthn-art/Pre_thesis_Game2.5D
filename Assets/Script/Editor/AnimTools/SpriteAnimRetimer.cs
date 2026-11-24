using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [Claude 2026-11-24] Sprite Anim Retimer — จัดจังหวะอนิเมชันสไปรต์ของตัวละครใหม่ให้เท่ากันทุกทิศ
/// เปิดที่ Tools > Anim Tools > Sprite Anim Retimer
///
///  ปัญหาที่แก้: คีย์เฟรมถูกลากวางด้วยมือ → แต่ละเฟรมค้างนานไม่เท่ากัน และแต่ละทิศยาวไม่เท่ากัน
///  (เช่น Walk_Left 1.45 วิ แต่ Walk_Right 1.25 วิ → เปลี่ยนทิศแล้วจังหวะเท้ากระตุก)
///
///  • โหลดคลิปทั้งโฟลเดอร์ → จัดกลุ่มตามท่า (Walk / Run / Idle ...) → ตั้ง FPS เดียวกันทั้งกลุ่ม → กดใช้
///  • ตั้ง "ค้างกี่จังหวะ" ต่อเฟรมได้ (เช่น 2,1,2,1 = เฟรมเท้าแตะพื้นค้างนานกว่า)
///  • ดูตัวอย่างทุกทิศเล่นพร้อมกันก่อนกดใช้ · แก้ลำดับ/เพิ่ม/ลบเฟรมได้ · ร่างเฟรมกลางอัตโนมัติ (ไว้ให้คนวาดใช้อ้างอิง)
///  • สำรองไฟล์ก่อนแก้ทุกครั้ง (Assets/Animations/_Backup_วันเวลา) + Ctrl+Z ได้
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

    private string folder = "Assets/Animations";
    private readonly List<Group> groups = new List<Group>();
    private ClipInfo editing;
    private Group editingGroup;
    private Vector2 scrollLeft, scrollRight;
    private bool backup = true;
    private bool playPreview = true;
    private float previewSize = 72f;
    private double startTime;

    // FPS ที่แนะนำตามชื่อท่า
    private static float SuggestFps(string name)
    {
        string n = name.ToLower();
        if (n.Contains("shoot") || n.Contains("attack") || n.Contains("slash") || n.Contains("hit")) return 10f;
        if (n.Contains("run")) return 8f;
        if (n.Contains("walk")) return 5f;
        return 4f; // idle / hold
    }

    private void OnEnable()
    {
        startTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
    }

    private void OnDisable() => EditorApplication.update -= Tick;

    private void Tick()
    {
        if (playPreview && groups.Count > 0) Repaint();
    }

    // ═════════════════════════ โหลด ═════════════════════════
    private void Load()
    {
        groups.Clear();
        editing = null;
        var byName = new Dictionary<string, Group>();
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("/_Backup_")) continue;
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            ClipInfo ci = Read(clip);
            if (ci == null) continue;
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
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("โฟลเดอร์", GUILayout.Width(50));
        folder = EditorGUILayout.TextField(folder, EditorStyles.toolbarTextField);
        if (GUILayout.Button("ใช้ที่เลือก", EditorStyles.toolbarButton, GUILayout.Width(70)))
            foreach (Object o in Selection.GetFiltered<Object>(SelectionMode.Assets))
            {
                string p = AssetDatabase.GetAssetPath(o);
                if (AssetDatabase.IsValidFolder(p)) { folder = p; break; }
            }
        if (GUILayout.Button("โหลด", EditorStyles.toolbarButton, GUILayout.Width(50))) Load();
        GUILayout.FlexibleSpace();
        backup = GUILayout.Toggle(backup, "สำรองก่อนแก้", EditorStyles.toolbarButton);
        playPreview = GUILayout.Toggle(playPreview, "▶ เล่นตัวอย่าง", EditorStyles.toolbarButton);
        EditorGUILayout.EndHorizontal();

        if (groups.Count == 0)
        {
            EditorGUILayout.HelpBox("กด \"โหลด\" เพื่ออ่านคลิปทั้งหมดในโฟลเดอร์ (จะจัดกลุ่มตามโฟลเดอร์ย่อย เช่น Walk, Run_Player)", MessageType.Info);
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
}
