#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [Claude 2026-11-28] สร้าง "หุ่นขยับ" ในกรอบภาพของการ์ดคำสอน — ใช้สไปรท์จริงของเกม (ตัวละคร/มอน/เป้าเล็ง) + ไอคอนพิกเซล
/// เมนู: Tools ▸ Tutorial ▸ Build Demo Animations
///
/// ทำอะไร: เปิด prefab [TUTORIAL] → ลบ/สร้างลูกใต้ Card/VideoFrame/Overlays ชื่อ Demo_&lt;id&gt; ใหม่
///   แต่ละชุด = TutorialOverlay (id) + Animator (เวลาจริง เล่นได้ตอนเกมหยุด) + คลิปวนใน Assets/Art/Tutorial/Demos/
/// ไฟล์ TutorialData หน้าไหนอยากโชว์ชุดไหน → ใส่ id (เช่น demo_gun_aim) ในช่อง Overlay Ids
/// แก้ท่าทีหลัง: แก้ในหน้าต่าง Animation ได้เลย (แต่กดสร้างใหม่ = ทับ) หรือแก้ตัวเลขในไฟล์นี้แล้วกดเมนูอีกรอบ
/// </summary>
public static class TutorialDemoBuilder
{
    private const string PrefabPath = "Assets/Prefab/UI/[TUTORIAL].prefab";
    private const string DemoDir = "Assets/Art/Tutorial/Demos";
    private const string IconDir = "Assets/Art/Tutorial/Icons/";
    private const string Player = "Assets/Art/Sprites_2D/Player/";

    private static readonly Color Ink = new Color(0.17f, 0.12f, 0.09f, 1f);
    private static readonly Color Floor = new Color(0.13f, 0.1f, 0.08f, 1f);
    private static readonly Color Red = new Color(0.85f, 0.15f, 0.12f, 1f);

    // ───────── เมนู ─────────

    [MenuItem("Tools/Tutorial/Build Demo Animations")]
    public static void BuildAll()
    {
        PrepareIcons();
        if (!AssetDatabase.IsValidFolder(DemoDir)) AssetDatabase.CreateFolder("Assets/Art/Tutorial", "Demos");

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform overlays = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "Overlays");
            TMP_FontAsset font = root.GetComponentsInChildren<TMP_Text>(true).First().font;

            BuildGunAim(overlays, font);
            BuildGunCrit(overlays, font);
            BuildMelee(overlays, font);
            BuildBasics(overlays, font);
            BuildJournal(overlays, font);
            BuildMap(overlays, font);
            BuildMouseLook(overlays);
            BuildMouseClick(overlays);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[TutorialDemoBuilder] สร้างหุ่นขยับเสร็จ");
    }

    // ───────── ชุด: ปืน ─────────

    /// <summary>หน้า 1 ปืน: ยืนถือปืน → ค้างคลิกขวาเล็ง (เมาส์ขวาแดง + เป้าโผล่) → คลิกซ้ายยิง → มอนสะดุ้ง</summary>
    private static void BuildGunAim(Transform overlays, TMP_FontAsset font)
    {
        var d = new Demo("demo_gun_aim", overlays, 3.6f);
        Sprite[] hold = ClipSprites("Assets/Animations/Idle_Gun/Idle_Gun_Right.anim");
        Sprite[] shoot = ClipSprites("Assets/Animations/Shoot_Gun/Shoot_Gun_Right.anim");
        Sprite[] enemy = FolderSprites("Assets/Art/Enemy_Sprite/Idel_Left_Enemy");

        d.Img("Floor", Icon("White"), new Vector2(0, -205), new Vector2(968, 140), Floor);
        RectTransform player = d.Img("Player", hold[0], new Vector2(-250, -40), new Vector2(330, 330), Color.white, preserve: true);
        RectTransform foe = d.Img("Enemy", enemy[0], new Vector2(260, -30), new Vector2(330, 330), Color.white, preserve: true);
        RectTransform cross = d.Group("Crosshair", new Vector2(250, 10));
        RectTransform[] corners = Corners(d, cross);
        RectTransform flash = d.Img("Muzzle", Icon("White"), new Vector2(-88, -12), new Vector2(26, 18), new Color(1f, 0.85f, 0.45f, 1f));
        RectTransform mouse = d.Img("Mouse", Icon("Mouse"), new Vector2(-400, 150), new Vector2(80, 120), Color.white, preserve: true);

        // ตัวละคร: ถือปืน 0–0.9 → ท่าเล็ง (เฟรมแรกของท่ายิง) → ยิง 2.2 → กลับท่าเล็ง
        var playerKeys = Loop(hold, 0f, 0.9f, 0.19f);
        playerKeys.Add((0.9f, shoot[0]));
        playerKeys.AddRange(Sequence(shoot, 2.2f, 0.08f));
        playerKeys.Add((2.2f + shoot.Length * 0.08f, shoot[0]));
        d.Sprites(player, playerKeys);
        d.Sprites(foe, Loop(enemy, 0f, d.Length, 0.16f));

        // เมาส์: ปกติ → คลิกขวาค้าง 0.9–3.0 · คลิกซ้ายแว้บ 2.2
        d.Sprites(mouse, new List<(float, Sprite)> { (0f, Icon("Mouse")), (0.9f, Icon("Mouse_Right")), (2.15f, Icon("Mouse_Left")), (2.35f, Icon("Mouse_Right")), (3.0f, Icon("Mouse")) });
        d.Pulse(mouse, 0.9f, 1.08f);

        // เป้าเล็ง: โผล่ 0.9 กว้าง → หดถึง 2.0
        d.Active(cross, (0f, false), (0.9f, true), (3.0f, false));
        Converge(d, corners, 0.9f, 2.0f, 80f, 26f, Color.white, Color.white);

        // ยิง: แสงปากกระบอก + มอนแดงแว้บ + ถอยนิดนึง
        d.Active(flash, (0f, false), (2.2f, true), (2.3f, false));
        d.Tint(foe, (0f, Color.white), (2.22f, Color.white), (2.25f, new Color(1f, 0.35f, 0.3f, 1f)), (2.45f, Color.white));
        d.PosX(foe, (0f, 260f), (2.22f, 260f), (2.3f, 285f), (2.6f, 260f));
        d.Save();
    }

    /// <summary>หน้า 2 ปืน: เล็งนิ่ง ๆ เป้าหดจนสุด (เปลี่ยนแดง) → ยิง → CRITICAL! เด้ง</summary>
    private static void BuildGunCrit(Transform overlays, TMP_FontAsset font)
    {
        var d = new Demo("demo_gun_crit", overlays, 4.0f);
        Sprite[] shoot = ClipSprites("Assets/Animations/Shoot_Gun/Shoot_Gun_Right.anim");
        Sprite[] enemy = FolderSprites("Assets/Art/Enemy_Sprite/Idel_Left_Enemy");

        d.Img("Floor", Icon("White"), new Vector2(0, -205), new Vector2(968, 140), Floor);
        RectTransform player = d.Img("Player", shoot[0], new Vector2(-250, -40), new Vector2(330, 330), Color.white, preserve: true);
        RectTransform foe = d.Img("Enemy", enemy[0], new Vector2(260, -30), new Vector2(330, 330), Color.white, preserve: true);
        RectTransform cross = d.Group("Crosshair", new Vector2(250, 10));
        RectTransform[] corners = Corners(d, cross);
        RectTransform dot = d.Img("Dot", Load<Sprite>("Assets/Art/04_Crosshair/center_dot.png"), Vector2.zero, new Vector2(12, 12), Color.white, preserve: true, parent: cross);
        RectTransform flash = d.Img("Muzzle", Icon("White"), new Vector2(-88, -12), new Vector2(30, 20), new Color(1f, 0.85f, 0.45f, 1f));
        RectTransform crit = d.Text("Crit", font, "CRITICAL!", new Vector2(260, 175), 64, Red);

        var keys = new List<(float, Sprite)> { (0f, shoot[0]) };
        keys.AddRange(Sequence(shoot, 2.4f, 0.08f));
        keys.Add((2.4f + shoot.Length * 0.08f, shoot[0]));
        d.Sprites(player, keys);
        d.Sprites(foe, Loop(enemy, 0f, d.Length, 0.16f));

        // เป้าหดช้า ๆ 0–2.2 · แตะสุดแล้วเปลี่ยนแดง
        Converge(d, corners, 0f, 2.2f, 95f, 14f, Color.white, Red);
        d.Tint(dot, (0f, Color.white), (2.2f, Color.white), (2.25f, Red));
        d.Active(cross, (0f, true), (3.2f, false));

        d.Active(flash, (0f, false), (2.4f, true), (2.5f, false));
        d.Tint(foe, (0f, Color.white), (2.42f, Color.white), (2.45f, new Color(1f, 0.25f, 0.2f, 1f)), (2.8f, Color.white));
        d.PosX(foe, (0f, 260f), (2.42f, 260f), (2.5f, 300f), (2.9f, 260f));

        // CRITICAL! เด้งใหญ่แล้วหดเข้าที่
        d.Active(crit, (0f, false), (2.45f, true), (3.6f, false));
        d.Scale(crit, (0f, 1f), (2.45f, 1.7f), (2.6f, 1f), (3.6f, 1f));
        d.Save();
    }

    // ───────── ชุด: ฟัน ─────────

    /// <summary>ฟันเบา (คลิกซ้าย สตามินาลดนิดเดียว) → ฟันหนัก (คลิกขวา สตามินาลดเยอะ มอนกระเด็น) → สตามินาค่อย ๆ เต็ม</summary>
    private static void BuildMelee(Transform overlays, TMP_FontAsset font)
    {
        var d = new Demo("demo_melee", overlays, 4.6f);
        Sprite[] hold = ClipSprites("Assets/Animations/HoldPipe/HoldPipe_Right.anim");
        Sprite[] swing = ClipSprites("Assets/Animations/AttackPipe/AttackPipe_Right.anim");
        Sprite[] slash = ClipSprites("Assets/Animations/slash attack/Slash_Anim.anim");
        Sprite[] heavy = ClipSprites("Assets/Animations/slash attack/Slash_Heavey_Anim.anim");
        Sprite[] enemy = FolderSprites("Assets/Art/Enemy_Sprite/Idel_Left_Enemy");

        d.Img("Floor", Icon("White"), new Vector2(0, -205), new Vector2(968, 140), Floor);
        RectTransform player = d.Img("Player", hold[0], new Vector2(-120, -40), new Vector2(330, 330), Color.white, preserve: true);
        RectTransform foe = d.Img("Enemy", enemy[0], new Vector2(150, -30), new Vector2(330, 330), Color.white, preserve: true);
        RectTransform fx = d.Img("Slash", slash[0], new Vector2(10, -20), new Vector2(260, 260), Color.white, preserve: true);
        RectTransform mouse = d.Img("Mouse", Icon("Mouse"), new Vector2(-400, 150), new Vector2(80, 120), Color.white, preserve: true);

        // หลอดสตามินา (ซ้ายบน ข้างเมาส์)
        d.Img("StaminaBack", Icon("White"), new Vector2(-150, 190), new Vector2(308, 26), Ink);
        RectTransform bar = d.Img("Stamina", Icon("White"), new Vector2(-300, 190), new Vector2(300, 18), new Color(0.95f, 0.78f, 0.25f, 1f));
        bar.pivot = new Vector2(0f, 0.5f);
        bar.anchoredPosition = new Vector2(-300, 190);

        // ตัวละคร: ถือท่อ → ฟันเบา 0.7 (เร็ว) → ถือ → ฟันหนัก 2.2 (ช้ากว่า) → ถือ
        var p = Loop(hold, 0f, 0.7f, 0.19f);
        p.AddRange(Sequence(swing, 0.7f, 0.07f));
        p.AddRange(Loop(hold, 0.7f + swing.Length * 0.07f, 2.2f, 0.19f));
        p.AddRange(Sequence(swing, 2.2f, 0.12f));
        p.AddRange(Loop(hold, 2.2f + swing.Length * 0.12f, d.Length, 0.19f));
        d.Sprites(player, p);
        d.Sprites(foe, Loop(enemy, 0f, d.Length, 0.16f));

        // เอฟเฟกต์ฟัน: เบา = รอยเล็ก · หนัก = รอยใหญ่
        var f = new List<(float, Sprite)>();
        f.AddRange(Sequence(slash, 0.75f, 0.06f));
        f.AddRange(Sequence(heavy, 2.3f, 0.08f));
        d.Sprites(fx, f);
        d.Active(fx, (0f, false), (0.75f, true), (0.75f + slash.Length * 0.06f, false), (2.3f, true), (2.3f + heavy.Length * 0.08f, false));
        d.Scale(fx, (0f, 1f), (2.2f, 1f), (2.3f, 1.5f), (d.Length, 1.5f));

        d.Sprites(mouse, new List<(float, Sprite)> { (0f, Icon("Mouse")), (0.65f, Icon("Mouse_Left")), (0.95f, Icon("Mouse")), (2.15f, Icon("Mouse_Right")), (2.55f, Icon("Mouse")) });

        // มอนโดนตี: เบา = สะดุ้ง · หนัก = แดงนาน + กระเด็น
        d.Tint(foe, (0f, Color.white), (0.8f, Color.white), (0.83f, new Color(1f, 0.4f, 0.35f, 1f)), (1.0f, Color.white),
                    (2.4f, Color.white), (2.43f, new Color(1f, 0.2f, 0.15f, 1f)), (2.9f, Color.white));
        d.PosX(foe, (0f, 150f), (0.8f, 150f), (0.88f, 165f), (1.1f, 150f), (2.4f, 150f), (2.5f, 230f), (3.2f, 230f), (3.8f, 150f));

        // สตามินา: ฟันเบา −10% · ฟันหนัก −35% · ค่อย ๆ ฟื้น
        d.Width(bar, 18f, (0f, 300f), (0.75f, 300f), (0.85f, 270f), (2.25f, 285f), (2.35f, 180f), (3.0f, 180f), (d.Length, 300f));
        d.Save();
    }

    // ───────── ชุด: พื้นฐาน ─────────

    /// <summary>กด D เดิน → ค้าง Shift วิ่ง → ถึงของ ป้าย [E] โผล่ → กด E เก็บ</summary>
    private static void BuildBasics(Transform overlays, TMP_FontAsset font)
    {
        var d = new Demo("demo_basics", overlays, 5.0f);
        Sprite[] idle = ClipSprites("Assets/Animations/Idle_player/Idle_Right.anim");
        Sprite[] walk = ClipSprites("Assets/Animations/Walk/Walk_Right.anim");
        Sprite[] run = ClipSprites("Assets/Animations/Run_Player/Run_Right.anim");

        d.Img("Floor", Icon("White"), new Vector2(0, -205), new Vector2(968, 140), Floor);
        RectTransform item = d.Img("Item", Load<Sprite>("Assets/Art/UI-20260919T083630Z-1-001/UI/ItemInfo_Paper.png"), new Vector2(330, -150), new Vector2(46, 60), Color.white, preserve: true);
        RectTransform glow = d.Img("Glow", Icon("White"), new Vector2(330, -150), new Vector2(70, 70), new Color(1f, 0.9f, 0.5f, 0.25f));
        glow.SetSiblingIndex(item.GetSiblingIndex());
        RectTransform player = d.Img("Player", idle[0], new Vector2(-360, -40), new Vector2(330, 330), Color.white, preserve: true);
        RectTransform ePrompt = Key(d, font, "E", new Vector2(330, -55), false);   // ป้าย [E] ลอยเหนือของ (เหมือนในเกม)
        ePrompt.name = "EPrompt";

        RectTransform keyW = Key(d, font, "W", new Vector2(-370, 200), false);
        RectTransform keyA = Key(d, font, "A", new Vector2(-420, 150), false);
        RectTransform keyS = Key(d, font, "S", new Vector2(-370, 150), false);
        RectTransform keyD = Key(d, font, "D", new Vector2(-320, 150), false);
        RectTransform keyShift = Key(d, font, "Shift", new Vector2(-170, 150), true);
        RectTransform keyE = Key(d, font, "E", new Vector2(-60, 150), false);

        // เดิน 0.3–1.8 · วิ่ง 1.8–2.9 · หยุดที่ของ 2.9 · กด E 3.5
        var p = new List<(float, Sprite)> { (0f, idle[0]) };
        p.AddRange(Loop(walk, 0.3f, 1.8f, 0.16f));
        p.AddRange(Loop(run, 1.8f, 2.9f, 0.1f));
        p.AddRange(Loop(idle, 2.9f, d.Length, 0.19f));
        d.Sprites(player, p);
        d.PosX(player, (0f, -360f), (0.3f, -360f), (1.8f, -170f), (2.9f, 230f), (d.Length, 230f));

        Press(d, keyD, (0.3f, 2.9f));
        Press(d, keyShift, (1.8f, 2.9f));
        Press(d, keyE, (3.5f, 3.75f));
        foreach (var k in new[] { keyW, keyA, keyS }) d.Tint(k, (0f, Color.white), (d.Length, Color.white));

        // ป้าย E โผล่ตอนถึง · เก็บแล้วของหาย
        d.Active(ePrompt, (0f, false), (2.9f, true), (3.75f, false));
        d.Scale(ePrompt, (0f, 1f), (2.9f, 0.6f), (3.05f, 1.1f), (3.2f, 1f), (3.5f, 1f), (3.6f, 0.85f), (3.75f, 1f));
        d.Active(item, (0f, true), (3.75f, false));
        d.Active(glow, (0f, true), (3.75f, false));
        d.Scale(glow, (0f, 1f), (0.6f, 1.25f), (1.2f, 1f), (1.8f, 1.25f), (2.4f, 1f), (3.0f, 1.25f), (3.75f, 1f));
        d.Save();
    }

    // ───────── ชุด: สมุด / แผนที่ ─────────

    /// <summary>กด Tab → สมุดเด้ง → แท็บสลับไล่ทีละอัน</summary>
    private static void BuildJournal(Transform overlays, TMP_FontAsset font)
    {
        var d = new Demo("demo_journal", overlays, 4.4f);
        string ui = "Assets/Art/UI-20260919T083630Z-1-001/UI/";
        RectTransform keyTab = Key(d, font, "Tab", new Vector2(-370, 170), true);
        RectTransform book = d.Img("Book", Load<Sprite>(ui + "Inventory_Book_Center.png"), new Vector2(40, -30), new Vector2(700, 375), Color.white, preserve: true);
        string[] tabs = { "Tab_Inventory/Tab_Inv_Selected", "Tab_KeyItem/Tab_Kitem_Selected", "Tab_Document/Tab_Doc_Selected" };
        var tabRects = new RectTransform[tabs.Length];
        for (int i = 0; i < tabs.Length; i++)
            tabRects[i] = d.Img("Tab" + i, Load<Sprite>(ui + "Tab_Button/" + tabs[i] + ".png"), new Vector2(-330, 70 - i * 60), new Vector2(150, 52), Color.white, preserve: true);

        Press(d, keyTab, (0.5f, 0.75f));
        d.Active(book, (0f, false), (0.75f, true));
        d.Scale(book, (0f, 0.6f), (0.75f, 0.6f), (0.95f, 1.04f), (1.1f, 1f), (d.Length, 1f));
        float[] at = { 1.4f, 2.3f, 3.2f };
        for (int i = 0; i < tabs.Length; i++)
        {
            d.Active(tabRects[i], (0f, false), (0.75f, true));
            d.PosX(tabRects[i], (0f, -330f), (at[i] - 0.01f, -330f), (at[i], -350f), (at[i] + 0.8f, -350f), (at[i] + 0.81f, -330f));
            d.Scale(tabRects[i], (0f, 1f), (at[i] - 0.01f, 1f), (at[i], 1.12f), (at[i] + 0.8f, 1.12f), (at[i] + 0.81f, 1f));
        }
        d.Save();
    }

    /// <summary>กด M → แผนผังคฤหาสน์เด้ง</summary>
    private static void BuildMap(Transform overlays, TMP_FontAsset font)
    {
        var d = new Demo("demo_map", overlays, 3.4f);
        RectTransform keyM = Key(d, font, "M", new Vector2(-400, 170), false);
        RectTransform map = d.Img("Map", Load<Sprite>("Assets/Art/UI/Map/MansionMap_v2.png"), new Vector2(40, -20), new Vector2(720, 460), Color.white, preserve: true);
        Press(d, keyM, (0.5f, 0.75f));
        d.Active(map, (0f, false), (0.75f, true), (3.1f, false));
        d.Scale(map, (0f, 0.6f), (0.75f, 0.6f), (0.95f, 1.04f), (1.1f, 1f), (d.Length, 1f));
        d.Save();
    }

    // ───────── ชุด: เมาส์ซ้อนบนคลิปจริง ─────────

    /// <summary>ซ้อนบนคลิปมุมมองบุคคลที่หนึ่ง (คลิปหันซ้าย-ขวา 5 วิ) — เมาส์เลื่อนตามกล้อง + ลูกศร 2 ข้าง</summary>
    private static void BuildMouseLook(Transform overlays)
    {
        var d = new Demo("mouse_look", overlays, 5.0f);
        RectTransform group = d.Group("MouseGroup", new Vector2(0, -175));
        RectTransform mouse = d.Img("Mouse", Icon("Mouse"), Vector2.zero, new Vector2(70, 105), Color.white, preserve: true, parent: group);
        RectTransform left = d.Img("ArrowL", Icon("Arrow"), new Vector2(-75, 0), new Vector2(48, 44), Color.white, preserve: true, parent: group);
        left.localScale = new Vector3(-1f, 1f, 1f);
        d.Img("ArrowR", Icon("Arrow"), new Vector2(75, 0), new Vector2(48, 44), Color.white, preserve: true, parent: group);

        // คลิปหัน = sin(เวลา) → เมาส์เลื่อนตามจังหวะเดียวกัน
        var k = new List<(float, float)>();
        for (int i = 0; i <= 20; i++) { float t = d.Length * i / 20f; k.Add((t, Mathf.Sin(t / d.Length * Mathf.PI * 2f) * 60f)); }
        d.PosX(group, k.ToArray());
        d.Sprites(mouse, new List<(float, Sprite)> { (0f, Icon("Mouse")) });
        d.Save();
    }

    /// <summary>ซ้อนบนคลิปเล็งของ (วิทยุ/สมุดเซฟ) — เมาส์คลิกซ้ายเป็นจังหวะ</summary>
    private static void BuildMouseClick(Transform overlays)
    {
        var d = new Demo("mouse_click", overlays, 3.0f);
        RectTransform mouse = d.Img("Mouse", Icon("Mouse"), new Vector2(380, -170), new Vector2(70, 105), Color.white, preserve: true);
        d.Sprites(mouse, new List<(float, Sprite)> { (0f, Icon("Mouse")), (2.0f, Icon("Mouse_Left")), (2.25f, Icon("Mouse")) });
        d.Scale(mouse, (0f, 1f), (1.99f, 1f), (2.0f, 0.9f), (2.25f, 0.9f), (2.26f, 1f), (d.Length, 1f));
        d.Save();
    }

    // ───────── ชิ้นส่วนใช้ซ้ำ ─────────

    /// <summary>ปุ่มคีย์บอร์ดพิกเซล + ตัวอักษร</summary>
    private static RectTransform Key(Demo d, TMP_FontAsset font, string label, Vector2 pos, bool wide)
    {
        RectTransform k = d.Img("Key_" + label, Icon(wide ? "Key_Wide" : "Key"), pos, wide ? new Vector2(130, 54) : new Vector2(54, 54), Color.white);
        RectTransform t = d.Text("Label", font, label, new Vector2(0, 3), wide ? 28 : 30, Ink, k);
        t.sizeDelta = k.sizeDelta;
        return k;
    }

    /// <summary>ปุ่มถูกกด (มืดลง + จมนิดนึง) ช่วงเวลาที่ให้มา</summary>
    private static void Press(Demo d, RectTransform key, (float from, float to) hold)
    {
        Color down = new Color(0.95f, 0.55f, 0.5f, 1f);
        d.Tint(key, (0f, Color.white), (hold.from - 0.01f, Color.white), (hold.from, down), (hold.to, down), (hold.to + 0.01f, Color.white));
        d.Scale(key, (0f, 1f), (hold.from - 0.01f, 1f), (hold.from, 0.9f), (hold.to, 0.9f), (hold.to + 0.01f, 1f));
    }

    private static RectTransform[] Corners(Demo d, RectTransform parent)
    {
        string dir = "Assets/Art/04_Crosshair/";
        return new[]
        {
            d.Img("TL", Load<Sprite>(dir + "corner_TL.png"), Vector2.zero, new Vector2(30, 30), Color.white, preserve: true, parent: parent),
            d.Img("TR", Load<Sprite>(dir + "corner_TR.png"), Vector2.zero, new Vector2(30, 30), Color.white, preserve: true, parent: parent),
            d.Img("BL", Load<Sprite>(dir + "corner_BL.png"), Vector2.zero, new Vector2(30, 30), Color.white, preserve: true, parent: parent),
            d.Img("BR", Load<Sprite>(dir + "corner_BR.png"), Vector2.zero, new Vector2(30, 30), Color.white, preserve: true, parent: parent),
        };
    }

    /// <summary>มุมเป้า 4 มุมหุบจากระยะ from → to ช่วง t0–t1 (เปลี่ยนสีตอนถึง)</summary>
    private static void Converge(Demo d, RectTransform[] c, float t0, float t1, float from, float to, Color startCol, Color endCol)
    {
        Vector2[] dir = { new Vector2(-1, 1), new Vector2(1, 1), new Vector2(-1, -1), new Vector2(1, -1) };
        for (int i = 0; i < 4; i++)
        {
            d.PosX(c[i], (0f, dir[i].x * from), (t0, dir[i].x * from), (t1, dir[i].x * to));
            d.PosY(c[i], (0f, dir[i].y * from), (t0, dir[i].y * from), (t1, dir[i].y * to));
            d.Tint(c[i], (0f, startCol), (t1, startCol), (t1 + 0.05f, endCol));
        }
    }

    private static List<(float, Sprite)> Loop(Sprite[] frames, float start, float end, float step)
    {
        var list = new List<(float, Sprite)>();
        int i = 0;
        for (float t = start; t < end - 0.001f; t += step) list.Add((t, frames[i++ % frames.Length]));
        return list;
    }

    private static IEnumerable<(float, Sprite)> Sequence(Sprite[] frames, float start, float step)
    {
        for (int i = 0; i < frames.Length; i++) yield return (start + i * step, frames[i]);
    }

    private static Sprite[] ClipSprites(string clipPath)
    {
        var clip = Load<AnimationClip>(clipPath);
        EditorCurveBinding b = AnimationUtility.GetObjectReferenceCurveBindings(clip).First();
        return AnimationUtility.GetObjectReferenceCurve(clip, b).Select(k => k.value as Sprite).Where(s => s != null).ToArray();
    }

    private static Sprite[] FolderSprites(string folder)
    {
        return AssetDatabase.FindAssets("t:Sprite", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Select(p => Load<Sprite>(p)).Where(s => s != null).ToArray();
    }

    private static Sprite Icon(string name) => Load<Sprite>(IconDir + name + ".png");

    private static T Load<T>(string path) where T : UnityEngine.Object
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(path);
        if (a == null) throw new Exception("[TutorialDemoBuilder] หาไฟล์ไม่เจอ: " + path);
        return a;
    }

    /// <summary>ไอคอนพิกเซล: ตั้งเป็น Sprite + ไม่เบลอ (Point) + ไม่บีบอัด</summary>
    private static void PrepareIcons()
    {
        foreach (string path in Directory.GetFiles(IconDir, "*.png"))
        {
            string p = path.Replace('\\', '/');
            var ti = (TextureImporter)AssetImporter.GetAtPath(p);
            if (ti == null) continue;
            if (ti.textureType == TextureImporterType.Sprite && ti.filterMode == FilterMode.Point) continue;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
    }

    // ───────── ตัวช่วยสร้าง 1 ชุด ─────────

    private class Demo
    {
        public readonly float Length;
        private readonly string id;
        private readonly RectTransform root;
        private readonly AnimationClip clip;

        public Demo(string id, Transform overlays, float length)
        {
            this.id = id;
            Length = length;

            Transform old = overlays.Find("Demo_" + id);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

            var go = new GameObject("Demo_" + id, typeof(RectTransform));
            go.layer = 5;
            root = (RectTransform)go.transform;
            root.SetParent(overlays, false);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = new Vector2(6, 6); root.offsetMax = new Vector2(-6, -6);
            go.AddComponent<RectMask2D>();   // ของขยับเกินกรอบ = ตัดทิ้ง

            var overlay = go.AddComponent<TutorialOverlay>();
            var so = new SerializedObject(overlay);
            so.FindProperty("overlayId").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();

            clip = new AnimationClip { name = id, frameRate = 30 };
        }

        public RectTransform Group(string name, Vector2 pos, RectTransform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
            var r = (RectTransform)go.transform;
            r.SetParent(parent != null ? parent : root, false);
            r.anchoredPosition = pos; r.sizeDelta = Vector2.zero;
            return r;
        }

        public RectTransform Img(string name, Sprite sprite, Vector2 pos, Vector2 size, Color color, bool preserve = false, RectTransform parent = null)
        {
            RectTransform r = Group(name, pos, parent);
            r.sizeDelta = size;
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = sprite; img.color = color; img.preserveAspect = preserve; img.raycastTarget = false;
            return r;
        }

        public RectTransform Text(string name, TMP_FontAsset font, string text, Vector2 pos, float size, Color color, RectTransform parent = null)
        {
            RectTransform r = Group(name, pos, parent);
            r.sizeDelta = new Vector2(600, 100);
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font; t.text = text; t.fontSize = size; t.color = color; t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;   // ไม่ตั้ง outline — จะไปแก้วัสดุฟอนต์ที่ทั้งเกมใช้ร่วมกัน
            return r;
        }

        private string PathOf(Transform t) => AnimationUtility.CalculateTransformPath(t, root);

        public void Sprites(RectTransform target, List<(float t, Sprite s)> keys)
        {
            var binding = EditorCurveBinding.PPtrCurve(PathOf(target), typeof(Image), "m_Sprite");
            var frames = keys.Select(k => new ObjectReferenceKeyframe { time = k.t, value = k.s }).ToList();
            frames.Add(new ObjectReferenceKeyframe { time = Length, value = keys[keys.Count - 1].s });
            AnimationUtility.SetObjectReferenceCurve(clip, binding, frames.ToArray());
        }

        public void PosX(RectTransform r, params (float t, float v)[] k) => Linear(r, typeof(RectTransform), "m_AnchoredPosition.x", k);

        /// <summary>ความกว้าง (หลอดสตามินา) — ความสูงคงที่ตามที่สร้าง</summary>
        public void Width(RectTransform r, float height, params (float t, float v)[] k)
        {
            r.sizeDelta = new Vector2(k[0].v, height);
            Linear(r, typeof(RectTransform), "m_SizeDelta.x", k);
        }
        public void PosY(RectTransform r, params (float t, float v)[] k) => Linear(r, typeof(RectTransform), "m_AnchoredPosition.y", k);

        public void Scale(RectTransform r, params (float t, float v)[] k)
        {
            Linear(r, typeof(Transform), "m_LocalScale.x", k);
            Linear(r, typeof(Transform), "m_LocalScale.y", k);
        }

        /// <summary>ชีพจรเบา ๆ ตลอดคลิป (ไอคอนที่ต้องสังเกต)</summary>
        public void Pulse(RectTransform r, float period, float max)
        {
            var k = new List<(float, float)>();
            for (float t = 0f; t <= Length + 0.001f; t += period * 0.5f) k.Add((t, k.Count % 2 == 0 ? 1f : max));
            Scale(r, k.ToArray());
        }

        public void Tint(RectTransform r, params (float t, Color c)[] k)
        {
            Type type = r.GetComponent<Graphic>().GetType();
            Linear(r, type, "m_Color.r", k.Select(x => (x.t, x.c.r)).ToArray());
            Linear(r, type, "m_Color.g", k.Select(x => (x.t, x.c.g)).ToArray());
            Linear(r, type, "m_Color.b", k.Select(x => (x.t, x.c.b)).ToArray());
            Linear(r, type, "m_Color.a", k.Select(x => (x.t, x.c.a)).ToArray());
        }

        public void Active(RectTransform r, params (float t, bool on)[] k)
        {
            var curve = new AnimationCurve(k.Select(x => new Keyframe(x.t, x.on ? 1f : 0f)).ToArray());
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(PathOf(r), typeof(GameObject), "m_IsActive"), curve);
            r.gameObject.SetActive(k[0].on);
        }

        private void Linear(RectTransform r, Type type, string prop, (float t, float v)[] k)
        {
            var curve = new AnimationCurve(k.Select(x => new Keyframe(x.t, x.v)).ToArray());
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(PathOf(r), type, prop), curve);
        }

        /// <summary>บันทึกคลิปวน + Animator (เวลาจริง) ลงชุดนี้</summary>
        public void Save()
        {
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            settings.stopTime = Length;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            string clipPath = $"{DemoDir}/{id}.anim";
            string ctrlPath = $"{DemoDir}/{id}.controller";
            AssetDatabase.DeleteAsset(clipPath);
            AssetDatabase.DeleteAsset(ctrlPath);
            AssetDatabase.CreateAsset(clip, clipPath);
            AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPathWithClip(ctrlPath, clip);

            var anim = root.gameObject.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;   // การ์ดหยุดเกม (timeScale 0) หุ่นยังขยับ
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            root.gameObject.SetActive(false);   // การ์ดเปิดให้เองตามหน้า
        }
    }
}
#endif
