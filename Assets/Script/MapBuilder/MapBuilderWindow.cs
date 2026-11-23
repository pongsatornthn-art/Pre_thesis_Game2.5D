using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [Map Builder] หน้าต่างช่วยจัดแมพ — เปิดที่ Tools > Map Builder > เปิดหน้าต่าง (Ctrl+Shift+M)
///
///   1) วางของ   — เลือก prefab จากรูป แล้วคลิกในฉากเพื่อวาง (ติดพื้น/ติดผนังเอง, ชิดกริด, หมุน R, สุ่มหมุน/ขนาด, ลากวางรัวๆ)
///   2) จัดของ   — วางลงพื้น / ชิดกริด / หมุน / จัดแนว / เว้นระยะ / ก๊อปเป็นแถว / แทนที่ prefab / รวมกลุ่ม / ตั้งชื่อเรียง
///   3) ห้อง     — สร้าง Trigger ซ่อนกำแพง + กล่องเบลอ ของห้องใหม่ในคลิกเดียว
///   4) ตรวจซีน  — หาสคริปต์หาย / วัสดุหาย / ของซ้อนกัน / ของตกใต้แมพ
///
/// เป็นเครื่องมือใน Editor อย่างเดียว ไม่ติดไปในเกมที่ build และทุกอย่าง Ctrl+Z ได้
/// </summary>
public class MapBuilderWindow : EditorWindow
{
    [MenuItem("Tools/Map Builder/เปิดหน้าต่าง %#m", false, 0)]
    public static void Open()
    {
        MapBuilderWindow w = GetWindow<MapBuilderWindow>();
        w.titleContent = new GUIContent("Map Builder", EditorGUIUtility.IconContent("d_Prefab Icon").image);
        w.minSize = new Vector2(320, 400);
        w.Show();
    }

    private static readonly string[] Tabs = { "วางของ", "จัดของ", "ห้อง", "ตรวจซีน" };
    private int tab;
    private Vector2 scroll;

    // ═════════════════════════ วางของ ═════════════════════════
    private const string DefaultFolders = "Assets/Prefab/Furniture|Assets/Prefab/Atmos|Assets/Prefab/Door|Assets/Prefab";
    private List<string> folders = new List<string>();
    private int folderIndex;
    private string search = "";
    private List<GameObject> palette = new List<GameObject>();
    private GameObject brush;
    private float thumbSize = 72f;

    private bool painting;
    private bool eraseMode;
    private bool snapGrid = true;
    private bool alignToWall = true;
    private bool randomYaw;
    private bool randomYaw90 = true;
    private Vector2 randomScale = new Vector2(1f, 1f);
    private float dragSpacing = 1f;
    private Transform parentOverride;
    private bool autoParent = true;

    private float yaw;                    // หมุนที่ผู้ใช้ตั้ง (R)
    private float rollYaw, rollScale = 1; // ค่าสุ่มของชิ้นถัดไป
    private Vector3 lastPlaced = new Vector3(9999, 9999, 9999);

    // ข้อมูลพรีวิวของแปรง (คำนวณครั้งเดียวต่อ prefab)
    private struct GhostPart { public Mesh mesh; public Matrix4x4 local; public Material[] mats; }
    private readonly List<GhostPart> ghost = new List<GhostPart>();
    private Bounds brushBounds;           // กรอบในพิกัดของ prefab เอง

    // ผลลัพธ์ตำแหน่งวางของเฟรมนี้
    private bool hasPlacement;
    private Vector3 placePos;
    private Quaternion placeRot;
    private bool placeOnWall;
    private GameObject eraseTarget;

    // ═════════════════════════ จัดของ ═════════════════════════
    private int arrayCount = 3;
    private Vector3 arrayOffset = new Vector3(1f, 0f, 0f);
    private GameObject replacePrefab;
    private bool replaceKeepScale = true;
    private string groupName = "Group";
    private string renameBase = "Item";
    private int renameStart = 1;

    // ═════════════════════════ ห้อง ═════════════════════════
    private readonly MapBuilderRoom.Settings room = new MapBuilderRoom.Settings();
    private RoomWallFade addWallsTarget;

    // ═════════════════════════ ตรวจซีน ═════════════════════════
    private List<MapBuilderCheck.Issue> issues;
    private float belowY = -5f;
    private bool[] kindFilter = { true, true, true, true, true, true };

    // ─────────────────────────────────────────────────────────
    private void OnEnable()
    {
        folders = new List<string>(EditorPrefs.GetString("MapBuilder.Folders", DefaultFolders).Split('|'));
        folders.RemoveAll(string.IsNullOrWhiteSpace);
        if (folders.Count == 0) folders.AddRange(DefaultFolders.Split('|'));
        folderIndex = Mathf.Clamp(EditorPrefs.GetInt("MapBuilder.FolderIndex", 0), 0, folders.Count - 1);
        snapGrid = EditorPrefs.GetBool("MapBuilder.SnapGrid", true);
        alignToWall = EditorPrefs.GetBool("MapBuilder.AlignWall", true);
        autoParent = EditorPrefs.GetBool("MapBuilder.AutoParent", true);
        room.blurMaterial = AssetDatabase.LoadAssetAtPath<Material>(MapBuilderRoom.DefaultBlurMat);
        RefreshPalette();
        SceneView.duringSceneGui += OnSceneGUI;
        Selection.selectionChanged += Repaint;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        Selection.selectionChanged -= Repaint;
        painting = false;
        SaveFolders();
    }

    private void SaveFolders()
    {
        EditorPrefs.SetString("MapBuilder.Folders", string.Join("|", folders));
        EditorPrefs.SetInt("MapBuilder.FolderIndex", folderIndex);
    }

    private void OnGUI()
    {
        tab = GUILayout.Toolbar(tab, Tabs, GUILayout.Height(24));
        if (tab != 0 && painting) StopPainting();
        EditorGUILayout.Space(4);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        switch (tab)
        {
            case 0: DrawPaintTab(); break;
            case 1: DrawArrangeTab(); break;
            case 2: DrawRoomTab(); break;
            case 3: DrawCheckTab(); break;
        }
        EditorGUILayout.EndScrollView();

        if (AssetPreview.IsLoadingAssetPreviews()) Repaint();
    }

    // ═════════════════════════════════════════════════════════
    // 1) วางของ
    // ═════════════════════════════════════════════════════════
    private void DrawPaintTab()
    {
        // ── โฟลเดอร์ ──
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        string[] names = folders.ConvertAll(f => f.Replace("Assets/", "").Replace('/', '∕')).ToArray();
        folderIndex = EditorGUILayout.Popup(folderIndex, names);
        if (EditorGUI.EndChangeCheck()) { SaveFolders(); RefreshPalette(); }
        if (GUILayout.Button(new GUIContent("+", "เพิ่มโฟลเดอร์ที่เลือกในหน้าต่าง Project"), GUILayout.Width(24))) AddSelectedFolder();
        if (GUILayout.Button(new GUIContent("−", "เอาโฟลเดอร์นี้ออกจากรายการ"), GUILayout.Width(24)) && folders.Count > 1)
        {
            folders.RemoveAt(folderIndex); folderIndex = 0; SaveFolders(); RefreshPalette();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.BeginChangeCheck();
        search = EditorGUILayout.TextField("ค้นหา", search);
        if (EditorGUI.EndChangeCheck()) RefreshPalette();

        // ── ปุ่มเริ่ม/หยุด ──
        EditorGUILayout.Space(4);
        Color old = GUI.backgroundColor;
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = painting && !eraseMode ? new Color(0.4f, 1f, 0.5f) : old;
        if (GUILayout.Button(painting && !eraseMode ? "■ หยุดวาง (Esc)" : "▶ เริ่มวาง", GUILayout.Height(30)))
        {
            if (painting && !eraseMode) StopPainting(); else StartPainting(false);
        }
        GUI.backgroundColor = painting && eraseMode ? new Color(1f, 0.5f, 0.45f) : old;
        if (GUILayout.Button(painting && eraseMode ? "■ หยุดลบ" : "ยางลบ", GUILayout.Height(30), GUILayout.Width(90)))
        {
            if (painting && eraseMode) StopPainting(); else StartPainting(true);
        }
        GUI.backgroundColor = old;
        EditorGUILayout.EndHorizontal();

        if (painting)
            EditorGUILayout.HelpBox(eraseMode
                ? "คลิกของในฉากเพื่อลบ (ลบได้เฉพาะของจากโฟลเดอร์นี้)  ·  Esc = หยุด"
                : "คลิก = วาง  ·  Shift+ลาก = วางรัวๆ\nR หรือ [ ] = หมุน 90°  ·  Shift+ลูกกลิ้ง = หมุน 15°\nCtrl+คลิก = ดูดแบบจากของในฉาก  ·  Esc = หยุด", MessageType.Info);

        if (painting && !eraseMode)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"หมุน {Mathf.RoundToInt(yaw)}°", GUILayout.Width(70));
            if (GUILayout.Button("↺ −90", EditorStyles.miniButtonLeft)) yaw = Mathf.Repeat(yaw - 90f, 360f);
            if (GUILayout.Button("↻ +90", EditorStyles.miniButtonMid)) yaw = Mathf.Repeat(yaw + 90f, 360f);
            if (GUILayout.Button("0°", EditorStyles.miniButtonRight)) yaw = 0f;
            EditorGUILayout.EndHorizontal();
        }

        // ── ตัวเลือก ──
        EditorGUILayout.Space(4);
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.BeginHorizontal();
        snapGrid = EditorGUILayout.ToggleLeft("ชิดกริด", snapGrid, GUILayout.Width(70));
        using (new EditorGUI.DisabledScope(!snapGrid))
            MapBuilderOps.GridSize = EditorGUILayout.FloatField(MapBuilderOps.GridSize, GUILayout.Width(50));
        GUILayout.Label("ม.", GUILayout.Width(20));
        alignToWall = EditorGUILayout.ToggleLeft(new GUIContent("ติดผนังได้", "ชี้ที่ผนังแล้วหันหน้าออกจากผนังเอง (รูปภาพ/คราบ/นาฬิกา)"), alignToWall);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        randomYaw = EditorGUILayout.ToggleLeft("สุ่มหมุน", randomYaw, GUILayout.Width(70));
        using (new EditorGUI.DisabledScope(!randomYaw))
            randomYaw90 = EditorGUILayout.ToggleLeft("ทีละ 90°", randomYaw90, GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("สุ่มขนาด", GUILayout.Width(70));
        randomScale.x = EditorGUILayout.FloatField(randomScale.x, GUILayout.Width(45));
        GUILayout.Label("ถึง", GUILayout.Width(22));
        randomScale.y = EditorGUILayout.FloatField(randomScale.y, GUILayout.Width(45));
        GUILayout.FlexibleSpace();
        GUILayout.Label("ระยะลากวาง", GUILayout.Width(70));
        dragSpacing = Mathf.Max(0.05f, EditorGUILayout.FloatField(dragSpacing, GUILayout.Width(40)));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        autoParent = EditorGUILayout.ToggleLeft(new GUIContent("เก็บเข้ากลุ่มเอง", "ของที่วางไปอยู่ใต้ [MAP]/ชื่อโฟลเดอร์ — Hierarchy ไม่รก"), autoParent, GUILayout.Width(110));
        using (new EditorGUI.DisabledScope(autoParent))
            parentOverride = (Transform)EditorGUILayout.ObjectField(parentOverride, typeof(Transform), true);
        EditorGUILayout.EndHorizontal();
        MapBuilderOps.GroundY = EditorGUILayout.FloatField(new GUIContent("ความสูงพื้นสำรอง", "ใช้ตอนชี้ที่ว่างที่ไม่มีพื้น"), MapBuilderOps.GroundY);
        if (EditorGUI.EndChangeCheck())
        {
            EditorPrefs.SetBool("MapBuilder.SnapGrid", snapGrid);
            EditorPrefs.SetBool("MapBuilder.AlignWall", alignToWall);
            EditorPrefs.SetBool("MapBuilder.AutoParent", autoParent);
            Reroll();
        }

        // ── ตาราง prefab ──
        EditorGUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label($"{palette.Count} ชิ้น" + (brush != null ? $"  ·  แปรง: {brush.name}" : ""), EditorStyles.miniBoldLabel);
        GUILayout.FlexibleSpace();
        thumbSize = GUILayout.HorizontalSlider(thumbSize, 48f, 128f, GUILayout.Width(80));
        if (GUILayout.Button("รีเฟรช", EditorStyles.miniButton, GUILayout.Width(50))) RefreshPalette();
        EditorGUILayout.EndHorizontal();

        float width = position.width - 24f;
        int cols = Mathf.Max(1, Mathf.FloorToInt(width / (thumbSize + 4f)));
        for (int i = 0; i < palette.Count; i += cols)
        {
            EditorGUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(i + cols, palette.Count); j++) DrawThumb(palette[j]);
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawThumb(GameObject p)
    {
        if (p == null) return;
        Texture tex = AssetPreview.GetAssetPreview(p);
        if (tex == null) tex = AssetPreview.GetMiniThumbnail(p);
        bool sel = p == brush;
        GUIStyle st = new GUIStyle(GUI.skin.button) { imagePosition = ImagePosition.ImageAbove, fontSize = 9, wordWrap = true, padding = new RectOffset(2, 2, 2, 2) };
        Color old = GUI.backgroundColor;
        if (sel) GUI.backgroundColor = new Color(0.35f, 0.75f, 1f);
        string label = p.name.Replace("Furn_", "").Replace("Atmos_", "");
        if (GUILayout.Button(new GUIContent(label, tex, p.name), st, GUILayout.Width(thumbSize), GUILayout.Height(thumbSize + 14f)))
        {
            if (Event.current.button == 1) { EditorGUIUtility.PingObject(p); }
            else
            {
                SetBrush(p);
                if (!painting || eraseMode) StartPainting(false);
            }
        }
        GUI.backgroundColor = old;
    }

    private void AddSelectedFolder()
    {
        foreach (Object o in Selection.GetFiltered<Object>(SelectionMode.Assets))
        {
            string path = AssetDatabase.GetAssetPath(o);
            if (AssetDatabase.IsValidFolder(path) && !folders.Contains(path))
            {
                folders.Add(path); folderIndex = folders.Count - 1; SaveFolders(); RefreshPalette();
                return;
            }
        }
        EditorUtility.DisplayDialog("Map Builder", "คลิกเลือกโฟลเดอร์ในหน้าต่าง Project ก่อน แล้วค่อยกด +", "ตกลง");
    }

    private void RefreshPalette()
    {
        palette.Clear();
        if (folders.Count == 0) return;
        string folder = folders[Mathf.Clamp(folderIndex, 0, folders.Count - 1)];
        if (!AssetDatabase.IsValidFolder(folder)) return;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;
            if (!string.IsNullOrEmpty(search) && go.name.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            palette.Add(go);
        }
        palette.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
        Repaint();
    }

    private void SetBrush(GameObject p)
    {
        brush = p;
        ghost.Clear();
        brushBounds = new Bounds(Vector3.zero, Vector3.one * 0.2f);
        if (p == null) return;
        Matrix4x4 rootInv = p.transform.worldToLocalMatrix;
        bool any = false;
        foreach (MeshFilter mf in p.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            if (mr == null) continue;
            Matrix4x4 local = rootInv * mf.transform.localToWorldMatrix;
            ghost.Add(new GhostPart { mesh = mf.sharedMesh, local = local, mats = mr.sharedMaterials });
            Encapsulate(ref brushBounds, ref any, local, mf.sharedMesh.bounds);
        }
        foreach (SpriteRenderer sr in p.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.sprite == null) continue;
            Encapsulate(ref brushBounds, ref any, rootInv * sr.transform.localToWorldMatrix, sr.sprite.bounds);
        }
        Reroll();
    }

    private static void Encapsulate(ref Bounds b, ref bool any, Matrix4x4 m, Bounds local)
    {
        Vector3 c = local.center, e = local.extents;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
            Vector3 w = m.MultiplyPoint3x4(corner);
            if (!any) { b = new Bounds(w, Vector3.zero); any = true; } else b.Encapsulate(w);
        }
    }

    private void Reroll()
    {
        rollYaw = !randomYaw ? 0f : randomYaw90 ? 90f * Random.Range(0, 4) : Random.Range(0f, 360f);
        float lo = Mathf.Max(0.01f, Mathf.Min(randomScale.x, randomScale.y));
        float hi = Mathf.Max(0.01f, Mathf.Max(randomScale.x, randomScale.y));
        rollScale = Random.Range(lo, hi);
    }

    private void StartPainting(bool erase)
    {
        if (!erase && brush == null) { ShowNotification(new GUIContent("เลือก prefab ก่อน")); return; }
        painting = true;
        eraseMode = erase;
        Tools.hidden = true;  // ซ่อนลูกศรย้าย/หมุน ระหว่างวาง
        SceneView.lastActiveSceneView?.Focus();
        SceneView.RepaintAll();
        Repaint();
    }

    private void StopPainting()
    {
        painting = false;
        eraseMode = false;
        Tools.hidden = false;
        SceneView.RepaintAll();
        Repaint();
    }

    // ─────────── ฉาก ───────────
    private void OnSceneGUI(SceneView sv)
    {
        if (!painting) return;
        Event e = Event.current;
        int id = GUIUtility.GetControlID(FocusType.Passive);
        if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(id);

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { StopPainting(); e.Use(); return; }

        if (eraseMode) { HandleErase(e); return; }

        // หมุน: R / ] = +90 · [ = −90 · Shift = ทีละ 15° · Shift+ลูกกลิ้งเมาส์ = หมุนทีละ 15°
        if (e.type == EventType.KeyDown && !e.control && !e.alt &&
            (e.keyCode == KeyCode.R || e.keyCode == KeyCode.RightBracket || e.keyCode == KeyCode.LeftBracket))
        {
            float step = e.shift ? 15f : 90f;
            yaw = Mathf.Repeat(yaw + (e.keyCode == KeyCode.LeftBracket ? -step : step), 360f);
            e.Use();
        }
        if (e.type == EventType.ScrollWheel && e.shift && !e.control && !e.alt)
        {
            float d = Mathf.Abs(e.delta.y) > Mathf.Abs(e.delta.x) ? e.delta.y : e.delta.x;
            yaw = Mathf.Repeat(yaw + Mathf.Sign(d) * 15f, 360f);
            e.Use();
            ComputePlacement(e.mousePosition);
            sv.Repaint();
            Repaint();
        }

        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag || e.type == EventType.Layout || e.type == EventType.Repaint)
            ComputePlacement(e.mousePosition);

        bool leftDown = e.type == EventType.MouseDown && e.button == 0 && !e.alt;
        bool leftDrag = e.type == EventType.MouseDrag && e.button == 0 && !e.alt && e.shift;

        if (leftDown && e.control)
        {
            PickBrushUnderMouse(e.mousePosition);
            e.Use();
        }
        else if (leftDown && hasPlacement)
        {
            Place();
            e.Use();
        }
        else if (leftDrag && hasPlacement)
        {
            if (Vector3.Distance(placePos, lastPlaced) >= dragSpacing) Place();
            e.Use();
        }

        if (e.type == EventType.Repaint && hasPlacement) DrawGhost(sv.camera);
        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag) sv.Repaint();
    }

    private bool RaycastScene(Vector2 mouse, out Vector3 point, out Vector3 normal)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mouse);
        point = normal = Vector3.zero;
        float best = float.MaxValue;
        bool found = false;

        // 1) Collider (พื้น/ผนัง/เฟอร์นิเจอร์) — ข้ามกล่องเบลอ/Trigger เพราะไม่มี Collider ทึบ
        Physics.SyncTransforms();
        if (Physics.Raycast(ray, out RaycastHit ph, 1000f, ~0, QueryTriggerInteraction.Ignore))
        { best = ph.distance; point = ph.point; normal = ph.normal; found = true; }

        // 2) ไม่โดน Collider → ลองโมเดลในฉาก (ของที่ไม่มี Collider)
        if (!found)
        {
            object snap = HandleUtility.RaySnap(ray);
            if (snap is RaycastHit sh) { point = sh.point; normal = sh.normal; found = true; }
        }

        // 3) พื้นสำรอง
        if (!found)
        {
            Plane plane = new Plane(Vector3.up, new Vector3(0, MapBuilderOps.GroundY, 0));
            if (plane.Raycast(ray, out float d) && d < 1000f) { point = ray.GetPoint(d); normal = Vector3.up; found = true; }
        }
        return found;
    }

    private void ComputePlacement(Vector2 mouse)
    {
        hasPlacement = false;
        if (brush == null || !RaycastScene(mouse, out Vector3 p, out Vector3 n)) return;

        float scale = rollScale;
        placeOnWall = alignToWall && Mathf.Abs(n.y) < 0.5f;

        if (placeOnWall)
        {
            Vector3 flatN = new Vector3(n.x, 0, n.z).normalized;
            placeRot = Quaternion.LookRotation(flatN, Vector3.up) * Quaternion.Euler(0, yaw, 0);
            if (snapGrid)
            {
                float g = MapBuilderOps.GridSize;
                Vector3 along = Vector3.Cross(Vector3.up, flatN);
                float a = Vector3.Dot(p, along);
                p += along * (MapBuilderOps.Snap(a, g) - a);
                p.y = MapBuilderOps.Snap(p.y, g);
            }
            // ดันออกจากผนังให้หลังของชิ้นแตะผนังพอดี (+ กัน z-fight นิดหน่อย)
            Vector3 back = placeRot * new Vector3(0, 0, brushBounds.min.z * scale);
            p += flatN * (-Vector3.Dot(back, flatN) + 0.005f);
        }
        else
        {
            placeRot = Quaternion.Euler(0, yaw + rollYaw, 0);
            if (snapGrid)
            {
                float g = MapBuilderOps.GridSize;
                p.x = MapBuilderOps.Snap(p.x, g);
                p.z = MapBuilderOps.Snap(p.z, g);
            }
            p.y += -brushBounds.min.y * scale; // ฐานติดพื้น
        }

        placePos = p;
        hasPlacement = true;
    }

    private void DrawGhost(Camera cam)
    {
        Matrix4x4 m = Matrix4x4.TRS(placePos, placeRot, Vector3.one * rollScale);
        foreach (GhostPart g in ghost)
        {
            Matrix4x4 mm = m * g.local;
            int subs = g.mesh.subMeshCount;
            for (int s = 0; s < subs; s++)
            {
                Material mat = g.mats != null && g.mats.Length > 0 ? g.mats[Mathf.Min(s, g.mats.Length - 1)] : null;
                if (mat != null) Graphics.DrawMesh(g.mesh, mm, mat, 0, cam, s);
            }
        }
        Handles.matrix = m;
        Handles.color = placeOnWall ? new Color(1f, 0.8f, 0.3f) : new Color(0.3f, 1f, 0.5f);
        Handles.DrawWireCube(brushBounds.center, brushBounds.size);
        Handles.matrix = Matrix4x4.identity;
        Handles.color = Color.white;
        Handles.Label(placePos + Vector3.up * (brushBounds.size.y * rollScale + 0.2f),
            $"{brush.name}  {Mathf.RoundToInt(placeRot.eulerAngles.y)}°" + (placeOnWall ? "  [ผนัง]" : ""), EditorStyles.whiteMiniLabel);
    }

    private Transform PlacementParent()
    {
        if (!autoParent) return parentOverride;
        string folder = folders[Mathf.Clamp(folderIndex, 0, folders.Count - 1)];
        string leaf = folder.Substring(folder.LastIndexOf('/') + 1);
        return MapBuilderOps.GetOrCreatePath("[MAP]/" + leaf);
    }

    private void Place()
    {
        Undo.IncrementCurrentGroup();
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(brush, UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Undo.RegisterCreatedObjectUndo(inst, "Map Builder: Place " + brush.name);
        Transform parent = PlacementParent();
        if (parent != null) inst.transform.SetParent(parent, true);
        inst.transform.SetPositionAndRotation(placePos, placeRot);
        inst.transform.localScale = Vector3.Scale(brush.transform.localScale, Vector3.one * rollScale);
        lastPlaced = placePos;
        Reroll();
    }

    private void PickBrushUnderMouse(Vector2 mouse)
    {
        GameObject hit = HandleUtility.PickGameObject(mouse, false);
        if (hit == null) return;
        GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(hit);
        GameObject asset = root != null ? PrefabUtility.GetCorrespondingObjectFromSource(root) : null;
        if (asset == null) { ShowNotification(new GUIContent("ของชิ้นนี้ไม่ใช่ prefab")); return; }
        SetBrush(asset);
        yaw = Mathf.Repeat(root.transform.eulerAngles.y, 360f);
        Repaint();
    }

    // ─────────── ยางลบ ───────────
    private bool CanErase(GameObject root)
    {
        if (root == null) return false;
        GameObject asset = PrefabUtility.GetCorrespondingObjectFromSource(root);
        if (asset == null) return false;
        string folder = folders[Mathf.Clamp(folderIndex, 0, folders.Count - 1)];
        return AssetDatabase.GetAssetPath(asset).StartsWith(folder + "/");
    }

    private void HandleErase(Event e)
    {
        if (e.type == EventType.MouseMove)
        {
            GameObject hit = HandleUtility.PickGameObject(e.mousePosition, false);
            GameObject root = hit != null ? PrefabUtility.GetOutermostPrefabInstanceRoot(hit) : null;
            eraseTarget = CanErase(root) ? root : null;
            SceneView.RepaintAll();
        }
        if (e.type == EventType.Repaint && eraseTarget != null)
        {
            Bounds b = MapBuilderOps.WorldBounds(eraseTarget);
            Handles.color = new Color(1f, 0.3f, 0.25f);
            Handles.DrawWireCube(b.center, b.size);
            Handles.Label(b.max + Vector3.up * 0.1f, "ลบ: " + eraseTarget.name, EditorStyles.whiteMiniLabel);
            Handles.color = Color.white;
        }
        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            if (eraseTarget != null) { Undo.DestroyObjectImmediate(eraseTarget); eraseTarget = null; }
            e.Use();
        }
    }

    // ═════════════════════════════════════════════════════════
    // 2) จัดของ
    // ═════════════════════════════════════════════════════════
    private void DrawArrangeTab()
    {
        Transform[] sel = Selection.transforms;
        EditorGUILayout.HelpBox(sel.Length == 0 ? "เลือกของในฉาก (Hierarchy/Scene) ก่อน" : $"เลือกอยู่ {sel.Length} ชิ้น  ·  ทุกปุ่ม Ctrl+Z ได้", sel.Length == 0 ? MessageType.Warning : MessageType.None);
        using (new EditorGUI.DisabledScope(sel.Length == 0))
        {
            Header("ตำแหน่ง");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("วางลงพื้น", "ปุ่มลัด: End"))) MapBuilderOps.DropToFloor(sel);
            if (GUILayout.Button(new GUIContent("ชิดกริด XZ", "ปุ่มลัด: Alt+G"))) MapBuilderOps.SnapToGrid(sel, true, false, true);
            if (GUILayout.Button("ชิดกริด XYZ")) MapBuilderOps.SnapToGrid(sel, true, true, true);
            EditorGUILayout.EndHorizontal();
            MapBuilderOps.GridSize = EditorGUILayout.FloatField("ขนาดกริด (ม.)", MapBuilderOps.GridSize);

            Header("หมุน");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("−90°")) MapBuilderOps.RotateY(sel, -90f);
            if (GUILayout.Button(new GUIContent("+90°", "ปุ่มลัด: Alt+R"))) MapBuilderOps.RotateY(sel, 90f);
            if (GUILayout.Button("180°")) MapBuilderOps.RotateY(sel, 180f);
            if (GUILayout.Button("สุ่ม 90°")) MapBuilderOps.RandomRotateY(sel, true);
            if (GUILayout.Button("สุ่มอิสระ")) MapBuilderOps.RandomRotateY(sel, false);
            if (GUILayout.Button("รีเซ็ต")) MapBuilderOps.ResetRotation(sel);
            EditorGUILayout.EndHorizontal();

            Header("จัดแนว (ตามกรอบรวมของที่เลือก)");
            AlignRow("X  ซ้าย-ขวา", 0, "ซ้าย", "กลาง", "ขวา");
            AlignRow("Z  หน้า-หลัง", 2, "หน้า", "กลาง", "หลัง");
            AlignRow("Y  สูง-ต่ำ", 1, "ล่าง", "กลาง", "บน");
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("เว้นระยะเท่ากัน", GUILayout.Width(100));
            if (GUILayout.Button("ตามแกน X")) MapBuilderOps.Distribute(sel, 0);
            if (GUILayout.Button("ตามแกน Z")) MapBuilderOps.Distribute(sel, 2);
            EditorGUILayout.EndHorizontal();

            Header("ก๊อปเป็นแถว (เช่น เก้าอี้รอบโต๊ะ, ชั้นหนังสือเรียงผนัง)");
            EditorGUILayout.BeginHorizontal();
            arrayCount = Mathf.Clamp(EditorGUILayout.IntField("จำนวนเพิ่ม", arrayCount), 1, 100);
            EditorGUILayout.EndHorizontal();
            arrayOffset = EditorGUILayout.Vector3Field("ระยะห่างแต่ละชิ้น", arrayOffset);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("ใช้ขนาดของชิ้น →X") && sel.Length > 0) arrayOffset = new Vector3(MapBuilderOps.WorldBounds(sel[0].gameObject).size.x, 0, 0);
            if (GUILayout.Button("ใช้ขนาดของชิ้น →Z") && sel.Length > 0) arrayOffset = new Vector3(0, 0, MapBuilderOps.WorldBounds(sel[0].gameObject).size.z);
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button($"ก๊อป {arrayCount} ชิ้น")) MapBuilderOps.DuplicateArray(sel, arrayCount, arrayOffset);

            Header("แทนที่ด้วย prefab อื่น (ตำแหน่ง/การหมุนเดิม)");
            EditorGUILayout.BeginHorizontal();
            replacePrefab = (GameObject)EditorGUILayout.ObjectField(replacePrefab, typeof(GameObject), false);
            replaceKeepScale = EditorGUILayout.ToggleLeft("ขนาดเดิม", replaceKeepScale, GUILayout.Width(75));
            EditorGUILayout.EndHorizontal();
            using (new EditorGUI.DisabledScope(replacePrefab == null))
                if (GUILayout.Button("แทนที่") && EditorUtility.DisplayDialog("Map Builder", $"แทนที่ {sel.Length} ชิ้นด้วย {replacePrefab.name}?\n(Ctrl+Z ย้อนได้)", "แทนที่", "ยกเลิก"))
                    MapBuilderOps.ReplaceWithPrefab(sel, replacePrefab, replaceKeepScale);

            Header("จัดระเบียบ Hierarchy");
            EditorGUILayout.BeginHorizontal();
            groupName = EditorGUILayout.TextField(groupName);
            if (GUILayout.Button("รวมเป็นกลุ่ม", GUILayout.Width(100))) MapBuilderOps.Group(sel, groupName);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            renameBase = EditorGUILayout.TextField(renameBase);
            renameStart = EditorGUILayout.IntField(renameStart, GUILayout.Width(36));
            if (GUILayout.Button("ตั้งชื่อเรียง", GUILayout.Width(100))) MapBuilderOps.RenameSequential(sel, renameBase, renameStart);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField($"ตัวอย่าง: {renameBase}_{renameStart:00}, {renameBase}_{renameStart + 1:00}, ...", EditorStyles.miniLabel);
        }
    }

    private void AlignRow(string label, int axis, string a, string b, string c)
    {
        Transform[] sel = Selection.transforms;
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(100));
        if (GUILayout.Button(a)) MapBuilderOps.Align(sel, axis, MapBuilderOps.AlignMode.Min);
        if (GUILayout.Button(b)) MapBuilderOps.Align(sel, axis, MapBuilderOps.AlignMode.Center);
        if (GUILayout.Button(c)) MapBuilderOps.Align(sel, axis, MapBuilderOps.AlignMode.Max);
        EditorGUILayout.EndHorizontal();
    }

    private static void Header(string t)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField(t, EditorStyles.boldLabel);
    }

    // ═════════════════════════════════════════════════════════
    // 3) ห้อง
    // ═════════════════════════════════════════════════════════
    private void DrawRoomTab()
    {
        Transform[] sel = Selection.transforms;
        EditorGUILayout.HelpBox(
            "สร้างห้องใหม่ให้ครบชุดในคลิกเดียว:\n" +
            "• Trigger + RoomWallFade (ซ่อนกำแพงฝั่งกล้องตอนเดินเข้า)\n" +
            "• กล่องเบลอ BOXBlur + RoomFader (ห้องที่ไม่ได้อยู่จะเบลอ)\n\n" +
            "วิธีใช้: เลือกพื้น + กำแพงทั้งหมดของห้องนั้น → ตั้งชื่อ → กดสร้าง", MessageType.Info);

        Header("ตั้งค่า");
        room.roomName = EditorGUILayout.TextField("ชื่อห้อง", room.roomName);
        room.autoWalls = EditorGUILayout.Toggle(new GUIContent("หากำแพงฝั่งกล้องเอง", "เลือกกำแพงที่อยู่ฝั่งเดียวกับ Main Camera ใส่ RoomWallFade ให้"), room.autoWalls);
        room.makeBlur = EditorGUILayout.Toggle("สร้างกล่องเบลอ", room.makeBlur);
        using (new EditorGUI.DisabledScope(!room.makeBlur))
        {
            room.blurMaterial = (Material)EditorGUILayout.ObjectField("วัสดุเบลอ", room.blurMaterial, typeof(Material), false);
            room.blurHeight = EditorGUILayout.FloatField("ความสูงกล่องเบลอ", room.blurHeight);
        }
        room.triggerHeight = EditorGUILayout.FloatField("ความสูง Trigger", room.triggerHeight);
        room.inset = EditorGUILayout.FloatField(new GUIContent("หด Trigger จากขอบ", "กันเดินชิดกำแพงแล้วชน trigger ของห้องข้างๆ"), room.inset);
        room.parent = (Transform)EditorGUILayout.ObjectField("เก็บไว้ใต้", room.parent, typeof(Transform), true);

        if (sel.Length > 0)
        {
            Bounds b = MapBuilderOps.WorldBounds(MapBuilderOps.TopLevel(sel));
            EditorGUILayout.LabelField($"ขนาดห้องจากของที่เลือก: {b.size.x:0.0} × {b.size.z:0.0} ม.", EditorStyles.miniLabel);
            if (room.autoWalls)
            {
                var walls = MapBuilderRoom.FindCameraSideWalls(MapBuilderOps.TopLevel(sel), b, MapBuilderRoom.CameraSide(b.center));
                EditorGUILayout.LabelField(walls.Count == 0 ? "กำแพงฝั่งกล้อง: ไม่เจอ (ใส่เองทีหลังได้ด้านล่าง)"
                    : "กำแพงฝั่งกล้อง: " + string.Join(", ", walls.ConvertAll(w => w.name)), EditorStyles.wordWrappedMiniLabel);
            }
        }

        EditorGUILayout.Space(4);
        using (new EditorGUI.DisabledScope(sel.Length == 0))
            if (GUILayout.Button("สร้างห้องจากของที่เลือก", GUILayout.Height(30)))
                MapBuilderRoom.Build(sel, room);

        Header("เพิ่มกำแพงเข้าห้องที่มีอยู่");
        addWallsTarget = (RoomWallFade)EditorGUILayout.ObjectField("Trigger ของห้อง", addWallsTarget, typeof(RoomWallFade), true);
        EditorGUILayout.LabelField("เลือกกำแพงในฉาก แล้วกดปุ่ม → จะถูกซ่อนตอนผู้เล่นอยู่ในห้องนี้", EditorStyles.wordWrappedMiniLabel);
        using (new EditorGUI.DisabledScope(addWallsTarget == null || sel.Length == 0))
            if (GUILayout.Button("เพิ่มที่เลือกเป็นกำแพงที่ซ่อน"))
            {
                int n = MapBuilderRoom.AddWalls(addWallsTarget, sel);
                ShowNotification(new GUIContent($"เพิ่ม {n} ชิ้น"));
            }
    }

    // ═════════════════════════════════════════════════════════
    // 4) ตรวจซีน
    // ═════════════════════════════════════════════════════════
    private void DrawCheckTab()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("ตรวจซีนนี้", GUILayout.Height(28))) issues = MapBuilderCheck.Scan(belowY);
        GUILayout.Label("ต่ำกว่า y =", GUILayout.Width(70));
        belowY = EditorGUILayout.FloatField(belowY, GUILayout.Width(40));
        GUILayout.Label("= ตกแมพ", GUILayout.Width(55));
        EditorGUILayout.EndHorizontal();

        if (issues == null) { EditorGUILayout.HelpBox("กดตรวจเพื่อหา: สคริปต์หาย, prefab หาย, วัสดุหาย, ของซ้อนกันเป๊ะ, ของตกใต้แมพ, Collider ขนาด 0", MessageType.Info); return; }
        issues.RemoveAll(i => i.go == null);
        if (issues.Count == 0) { EditorGUILayout.HelpBox("ไม่เจอปัญหา 👍", MessageType.Info); return; }

        // ตัวกรอง + จำนวน
        var count = new int[6];
        foreach (var i in issues) count[(int)i.kind]++;
        EditorGUILayout.BeginHorizontal();
        for (int k = 0; k < 6; k++)
        {
            if (count[k] == 0) continue;
            kindFilter[k] = GUILayout.Toggle(kindFilter[k], $"{MapBuilderCheck.Label((MapBuilderCheck.Kind)k)} ({count[k]})", EditorStyles.miniButton);
        }
        EditorGUILayout.EndHorizontal();

        int fixable = issues.FindAll(i => i.fixable && kindFilter[(int)i.kind]).Count;
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("เลือกทั้งหมดที่แสดง"))
            Selection.objects = issues.FindAll(i => kindFilter[(int)i.kind]).ConvertAll(i => (Object)i.go).ToArray();
        using (new EditorGUI.DisabledScope(fixable == 0))
            if (GUILayout.Button($"แก้อัตโนมัติ ({fixable})") &&
                EditorUtility.DisplayDialog("Map Builder", $"แก้ {fixable} รายการ?\n• สคริปต์หาย → ลบช่องที่หาย\n• ของซ้อน → ลบชิ้นที่ซ้ำ\n(Ctrl+Z ย้อนได้)", "แก้เลย", "ยกเลิก"))
            {
                foreach (var i in issues.FindAll(x => x.fixable && kindFilter[(int)x.kind])) MapBuilderCheck.Fix(i);
                issues = MapBuilderCheck.Scan(belowY);
                GUIUtility.ExitGUI();
            }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        foreach (var i in issues)
        {
            if (i.go == null || !kindFilter[(int)i.kind]) continue;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label(MapBuilderCheck.Label(i.kind), EditorStyles.miniBoldLabel, GUILayout.Width(70));
            GUILayout.Label($"{i.go.name} — {i.message}", EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("ดู", EditorStyles.miniButton, GUILayout.Width(32)))
            {
                Selection.activeGameObject = i.go;
                EditorGUIUtility.PingObject(i.go);
                SceneView.lastActiveSceneView?.FrameSelected();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
