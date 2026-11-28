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

    /// <summary>ล้างความจำการ์ดที่เคยเห็น (เก็บในเครื่อง ไม่ใช่ในเซฟ) — กด Play ใหม่จากซีนเกมก็ล้างให้เองอยู่แล้ว</summary>
    [MenuItem("Tools/Tutorial/Reset Seen Tutorials")]
    public static void ResetSeen()
    {
        TutorialProgress.ClearAll();
        Debug.Log("[Tutorial] ล้างความจำการ์ดคำสอนแล้ว — ทุกใบจะเด้งใหม่");
    }

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
            // [2026-11-28 รอบ 2]
            BuildDoorHold(overlays);
            BuildMinionGrab(overlays);
            BuildBossGrab(overlays);
            BuildBreathCharge(overlays);
            BuildBreathTiming(overlays);
            BuildMeleeBreak(overlays);
            BuildGunPadlock(overlays);
            BuildSneak(overlays, font);

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
        Sprite[] enemy = MinionFrames();

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
        Sprite[] enemy = MinionFrames();

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
        Sprite[] enemy = MinionFrames();

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
        // [2026-11-28] เดิม = รูปกระดาษลอย ๆ + กล่องเรืองแสงสี่เหลี่ยม (นาว่าดูกาก) → ซองจดหมายพิกเซลวางบนพื้น + ประกายวิบวับ 3 จุด
        RectTransform item = d.Img("Item", Icon("Letter"), new Vector2(330, -168), new Vector2(66, 51), Color.white, preserve: true);
        var sparkles = new RectTransform[3];
        Vector2[] sparkPos = { new Vector2(300, -132), new Vector2(362, -142), new Vector2(338, -112) };
        for (int i = 0; i < 3; i++) sparkles[i] = d.Img("Sparkle" + i, Icon("Sparkle"), sparkPos[i], new Vector2(22, 22), Color.white, preserve: true);
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
        // ประกาย: กะพริบสลับจังหวะกัน (ย่อ-ขยาย + จาง) · เก็บแล้วหาย
        for (int i = 0; i < 3; i++)
        {
            float o = i * 0.33f;
            var k = new List<(float, float)> { (0f, 0.2f) };
            for (float t = o; t < 3.75f; t += 1f) { k.Add((t, 0.2f)); k.Add((t + 0.25f, 1.1f)); k.Add((t + 0.5f, 0.2f)); }
            k.Add((3.75f, 0.2f)); k.Add((d.Length, 0.2f));
            d.Scale(sparkles[i], k.ToArray());
            d.Active(sparkles[i], (0f, true), (3.75f, false));
        }
        d.Save();
    }

    // ───────── ชุด: สมุด / แผนที่ ─────────

    /// <summary>กด I → สมุดเด้ง → แท็บสลับไล่ทีละอัน</summary>
    private static void BuildJournal(Transform overlays, TMP_FontAsset font)
    {
        var d = new Demo("demo_journal", overlays, 4.4f);
        string ui = "Assets/Art/UI-20260919T083630Z-1-001/UI/";
        RectTransform keyTab = Key(d, font, "I", new Vector2(-400, 170), false);   // [2026-11-28] เปิดสมุด = I (เดิมเขียนผิดเป็น Tab)
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

    // ───────── รอบ 2: มินิเกม ─────────

    private static readonly Color Green = new Color(0.35f, 0.75f, 0.35f, 1f);
    private static readonly Color Track = new Color(0.22f, 0.18f, 0.15f, 1f);

    /// <summary>ยันประตู: กดค้างปุ่มวงกลม → ลากขึ้น → หลอดขึ้นถึงโซนเขียวบน (ผีกระแทกให้ลดลงบ้าง) → ค้างไว้จนล็อก</summary>
    private static void BuildDoorHold(Transform overlays)
    {
        var d = new Demo("demo_door", overlays, 4.6f);
        RectTransform door = d.Img("Door", Icon("Door"), new Vector2(-60, -10), new Vector2(200, 320), Color.white, preserve: true);
        // หลอดแนวตั้ง (ซ้าย) — ฐานล่าง โตขึ้นบน
        d.Img("GaugeBack", Icon("White"), new Vector2(-330, -10), new Vector2(54, 330), Ink);
        RectTransform zone = d.Img("Zone", Icon("White"), new Vector2(-330, 122), new Vector2(46, 62), new Color(Green.r, Green.g, Green.b, 0.55f));
        RectTransform fill = d.Img("Fill", Icon("White"), new Vector2(-330, -171), new Vector2(46, 160), new Color(0.85f, 0.2f, 0.15f, 1f));
        fill.pivot = new Vector2(0.5f, 0f); fill.anchoredPosition = new Vector2(-330, -171);
        // ปุ่มวงกลม + เมาส์ (ขวา)
        d.Img("Circle", Icon("Circle"), new Vector2(250, -60), new Vector2(90, 90), Color.white, preserve: true);
        RectTransform mouse = d.Img("Mouse", Icon("Mouse"), new Vector2(310, -110), new Vector2(70, 105), Color.white, preserve: true);
        RectTransform arrow = d.Img("ArrowUp", Icon("Arrow"), new Vector2(250, 60), new Vector2(48, 44), Color.white, preserve: true);
        arrow.localEulerAngles = new Vector3(0, 0, 90);

        // เมาส์: วิ่งมาที่วงกลม 0.4 → กดค้าง 0.6 → ลากขึ้น 3 จังหวะ → ค้างบน
        d.PosX(mouse, (0f, 310f), (0.4f, 262f), (d.Length, 262f));
        d.PosY(mouse, (0f, -110f), (0.4f, -70f), (0.6f, -70f), (1.1f, 10f), (1.15f, -70f), (1.6f, 10f), (1.65f, -70f), (2.2f, 30f), (d.Length, 30f));
        d.Sprites(mouse, new List<(float, Sprite)> { (0f, Icon("Mouse")), (0.6f, Icon("Mouse_Left")), (4.2f, Icon("Mouse")) });
        d.Active(arrow, (0f, false), (0.6f, true), (2.3f, false));
        d.PosY(arrow, (0f, 40f), (0.6f, 40f), (1.1f, 80f), (1.15f, 40f), (1.6f, 80f), (1.65f, 40f), (2.2f, 80f), (d.Length, 80f));

        // หลอด: 50% → ขึ้นตามจังหวะลาก · ผีกระแทก 1.7 ลดลง · เข้าโซนบน 2.4 ค้างจนล็อก
        d.Height(fill, 46f, (0f, 165f), (0.6f, 165f), (1.1f, 215f), (1.6f, 250f), (1.75f, 205f), (2.4f, 300f), (d.Length, 300f));
        d.Tint(zone, (0f, new Color(Green.r, Green.g, Green.b, 0.4f)), (2.4f, new Color(Green.r, Green.g, Green.b, 0.4f)),
                     (2.5f, new Color(Green.r, Green.g, Green.b, 1f)), (3.7f, new Color(Green.r, Green.g, Green.b, 1f)), (3.9f, new Color(1f, 1f, 0.6f, 1f)), (d.Length, new Color(Green.r, Green.g, Green.b, 1f)));
        // ประตูโดนผีกระแทก (ขยับ) แล้วปิดสนิท
        d.PosX(door, (0f, -60f), (1.7f, -60f), (1.75f, -40f), (1.9f, -60f), (d.Length, -60f));
        d.Save();
    }

    /// <summary>โดนมินเนียนจับ: เข็มวิ่งซ้าย→ขวา · คลิกซ้ายตอนเข็มอยู่ในช่อง → สะบัดหลุด มินเนียนกระเด็น</summary>
    private static void BuildMinionGrab(Transform overlays)
    {
        var d = new Demo("demo_grab", overlays, 3.4f);
        Sprite[] enemy = MinionFrames();
        Sprite[] idle = ClipSprites("Assets/Animations/Idle_player/Idle_Right.anim");
        d.Img("Floor", Icon("White"), new Vector2(0, -205), new Vector2(968, 140), Floor);
        RectTransform player = d.Img("Player", idle[0], new Vector2(-70, -40), new Vector2(330, 330), Color.white, preserve: true);
        RectTransform foe = d.Img("Enemy", enemy[0], new Vector2(40, -30), new Vector2(330, 330), Color.white, preserve: true);
        d.Img("Bar", Icon("White"), new Vector2(0, 190), new Vector2(560, 30), Track);
        RectTransform zone = d.Img("Zone", Icon("White"), new Vector2(110, 190), new Vector2(90, 30), Green);
        RectTransform needle = d.Img("Needle", Load<Sprite>("Assets/Art/02_Progress_Bar/needle_marker.png"), new Vector2(-270, 190), new Vector2(16, 56), Color.white, preserve: true);
        RectTransform mouse = d.Img("Mouse", Icon("Mouse"), new Vector2(390, 150), new Vector2(70, 105), Color.white, preserve: true);

        d.Sprites(foe, Loop(enemy, 0f, d.Length, 0.16f));
        d.Sprites(player, new List<(float, Sprite)> { (0f, idle[0]) });
        d.PosX(needle, (0f, -270f), (1.6f, 120f), (1.65f, 120f), (d.Length, 120f));   // หยุดตอนคลิกโดน
        d.Sprites(mouse, new List<(float, Sprite)> { (0f, Icon("Mouse")), (1.6f, Icon("Mouse_Left")), (1.85f, Icon("Mouse")) });
        d.Tint(zone, (0f, Green), (1.6f, Green), (1.65f, new Color(1f, 1f, 0.6f, 1f)), (2.0f, Green), (d.Length, Green));
        // ตัวละครสั่น (โดนจับ) → สะบัด → มินเนียนกระเด็น
        d.PosX(player, (0f, -70f), (0.2f, -66f), (0.4f, -74f), (0.6f, -66f), (0.8f, -74f), (1.0f, -66f), (1.2f, -74f), (1.4f, -66f), (1.6f, -70f), (d.Length, -70f));
        d.PosX(foe, (0f, 40f), (1.65f, 40f), (1.85f, 260f), (d.Length, 260f));
        d.Tint(foe, (0f, Color.white), (1.65f, Color.white), (1.7f, new Color(1f, 0.4f, 0.35f, 1f)), (2.2f, Color.white), (d.Length, Color.white));
        d.Save();
    }

    /// <summary>โดนบอสจับ (หน้ามือผี): หลอดค่อย ๆ ลด · คลิกซ้ายรัว ๆ ดันหลอดจนเต็ม = หลุด</summary>
    private static void BuildBossGrab(Transform overlays)
    {
        var d = new Demo("demo_boss_grab", overlays, 3.6f);
        RectTransform hands = d.Img("Hands", Load<Sprite>("Assets/Art/05_Hands_and_Health/POV_Strangle_v2.png"), new Vector2(0, 0), new Vector2(980, 548), Color.white);
        d.Img("BarBack", Icon("White"), new Vector2(0, -215), new Vector2(520, 30), Ink);
        RectTransform fill = d.Img("Fill", Icon("White"), new Vector2(-256, -215), new Vector2(256, 22), new Color(0.85f, 0.2f, 0.15f, 1f));
        fill.pivot = new Vector2(0f, 0.5f); fill.anchoredPosition = new Vector2(-256, -215);
        RectTransform mouse = d.Img("Mouse", Icon("Mouse"), new Vector2(380, -150), new Vector2(80, 120), Color.white, preserve: true);

        // คลิกรัวทุก 0.14 วิ ช่วง 0.6–2.8
        var m = new List<(float, Sprite)> { (0f, Icon("Mouse")) };
        for (float t = 0.6f; t < 2.8f; t += 0.14f) { m.Add((t, Icon("Mouse_Left"))); m.Add((t + 0.07f, Icon("Mouse"))); }
        d.Sprites(mouse, m);
        d.Width(fill, 22f, (0f, 256f), (0.6f, 200f), (1.2f, 300f), (1.6f, 280f), (2.2f, 420f), (2.8f, 512f), (d.Length, 512f));
        // มือสั่น (จอสั่น) · หลุดแล้วหยุด
        var shake = new List<(float, float)>();
        for (float t = 0f; t < 2.85f; t += 0.07f) shake.Add((t, (shake.Count % 2 == 0 ? 1f : -1f) * 7f));
        shake.Add((2.9f, 0f)); shake.Add((d.Length, 0f));
        d.PosX(hands, shake.ToArray());
        d.Tint(hands, (0f, Color.white), (2.8f, Color.white), (3.1f, new Color(1f, 1f, 1f, 0.25f)), (d.Length, new Color(1f, 1f, 1f, 0.25f)));
        d.Save();
    }

    /// <summary>หายใจขั้น 1: ในห้องเขียว ค้าง Space → จอหัวใจเติมเต็ม</summary>
    private static void BuildBreathCharge(Transform overlays)
    {
        var d = new Demo("demo_breath_charge", overlays, 3.4f);
        string ecg = "Assets/Art/UI/PTSDMINIGAME/";
        d.Img("Frame", Load<Sprite>(ecg + "ECG_Frame.png"), new Vector2(0, 40), new Vector2(880, 187), Color.white, preserve: true);
        RectTransform screen = d.Img("Screen", Load<Sprite>(ecg + "ECG_Screen_Green.png"), new Vector2(0, 40), new Vector2(880, 187), Color.white, preserve: true);
        var img = screen.GetComponent<Image>(); img.type = Image.Type.Filled; img.fillMethod = Image.FillMethod.Horizontal; img.fillOrigin = 0; img.fillAmount = 0f;
        RectTransform space = d.Img("Space", Load<Sprite>("Assets/Art/01_QTE_Hold_Prompt/key_space.png"), new Vector2(0, -170), new Vector2(240, 96), Color.white, preserve: true);
        d.Fill(screen, (0f, 0f), (0.5f, 0f), (2.6f, 1f), (d.Length, 1f));
        Press(d, space, (0.5f, 2.6f));
        d.Save();
    }

    /// <summary>หายใจขั้น 2: ค้าง Space เข็มวิ่ง · ปล่อยตอนอยู่ในช่องเขียว × 3 ครั้ง</summary>
    private static void BuildBreathTiming(Transform overlays)
    {
        var d = new Demo("demo_breath_timing", overlays, 4.8f);
        d.Img("Bar", Icon("White"), new Vector2(0, 60), new Vector2(640, 40), Track);
        RectTransform zone = d.Img("Zone", Icon("White"), new Vector2(120, 60), new Vector2(110, 40), Green);
        RectTransform needle = d.Img("Needle", Load<Sprite>("Assets/Art/02_Progress_Bar/needle_marker.png"), new Vector2(-310, 60), new Vector2(18, 72), Color.white, preserve: true);
        RectTransform space = d.Img("Space", Load<Sprite>("Assets/Art/01_QTE_Hold_Prompt/key_space.png"), new Vector2(0, -150), new Vector2(240, 96), Color.white, preserve: true);
        var pips = new RectTransform[3];
        for (int i = 0; i < 3; i++) pips[i] = d.Img("Pip" + i, Icon("White"), new Vector2(-50 + i * 50, 150), new Vector2(30, 30), Track);

        // รอบ 1: ค้าง 0.3–1.4 (เข็ม −310 → 120) ปล่อยโดน · รอบ 2: ช่องย้าย −100 ค้าง 1.8–2.5 · รอบ 3: ช่อง 220 ค้าง 3.0–4.2
        d.PosX(needle, (0f, -310f), (0.3f, -310f), (1.4f, 120f), (1.6f, 120f), (1.61f, -310f), (1.8f, -310f), (2.5f, -100f), (2.7f, -100f), (2.71f, -310f), (3.0f, -310f), (4.2f, 220f), (d.Length, 220f));
        d.PosX(zone, (0f, 120f), (1.6f, 120f), (1.61f, -100f), (2.7f, -100f), (2.71f, 220f), (d.Length, 220f));
        Press(d, space, (0.3f, 1.4f));
        d.Tint(pips[0], (0f, Track), (1.4f, Track), (1.45f, Green), (d.Length, Green));
        d.Tint(pips[1], (0f, Track), (2.5f, Track), (2.55f, Green), (d.Length, Green));
        d.Tint(pips[2], (0f, Track), (4.2f, Track), (4.25f, Green), (d.Length, Green));
        // ปุ่มกดรอบ 2–3 (Press ใช้ได้ทีละช่วง → ทำเองด้วยสี)
        Color down = new Color(0.95f, 0.55f, 0.5f, 1f);
        d.Tint(space, (0f, Color.white), (0.29f, Color.white), (0.3f, down), (1.4f, down), (1.41f, Color.white), (1.79f, Color.white), (1.8f, down), (2.5f, down), (2.51f, Color.white),
                      (2.99f, Color.white), (3.0f, down), (4.2f, down), (4.21f, Color.white), (d.Length, Color.white));
        d.Save();
    }

    // ───────── รอบ 2: หน้าเพิ่มของการ์ดเดิม ─────────

    /// <summary>ท่อทุบสิ่งกีดขวาง: เดินถือท่อมา → ฟัน 2 ที กองไม้สั่น → พัง ไม้กระเด็น → เดินผ่าน</summary>
    private static void BuildMeleeBreak(Transform overlays)
    {
        var d = new Demo("demo_melee_break", overlays, 4.4f);
        Sprite[] hold = ClipSprites("Assets/Animations/HoldPipe/HoldPipe_Right.anim");
        Sprite[] walk = ClipSprites("Assets/Animations/WalkPipe/WalkPipe_Right.anim");
        Sprite[] swing = ClipSprites("Assets/Animations/AttackPipe/AttackPipe_Right.anim");
        d.Img("Floor", Icon("White"), new Vector2(0, -205), new Vector2(968, 140), Floor);
        RectTransform debris = d.Img("Debris", Icon("Debris"), new Vector2(150, -95), new Vector2(288, 192), Color.white, preserve: true);
        var planks = new RectTransform[3];
        for (int i = 0; i < 3; i++) planks[i] = d.Img("Plank" + i, Icon("Plank"), new Vector2(150, -90), new Vector2(96, 36), Color.white, preserve: true);
        RectTransform player = d.Img("Player", hold[0], new Vector2(-380, -40), new Vector2(330, 330), Color.white, preserve: true);
        RectTransform mouse = d.Img("Mouse", Icon("Mouse"), new Vector2(-400, 150), new Vector2(70, 105), Color.white, preserve: true);

        var p = Loop(walk, 0f, 0.9f, 0.16f);
        p.AddRange(Sequence(swing, 1.0f, 0.08f));
        p.AddRange(Sequence(swing, 1.6f, 0.08f));
        p.AddRange(Loop(walk, 2.6f, d.Length, 0.16f));
        d.Sprites(player, p);
        d.PosX(player, (0f, -380f), (0.9f, -120f), (2.6f, -120f), (d.Length, 330f));
        d.Sprites(mouse, new List<(float, Sprite)> { (0f, Icon("Mouse")), (1.0f, Icon("Mouse_Left")), (1.2f, Icon("Mouse")), (1.6f, Icon("Mouse_Left")), (1.8f, Icon("Mouse")) });

        d.PosX(debris, (0f, 150f), (1.15f, 150f), (1.2f, 165f), (1.3f, 140f), (1.4f, 150f), (d.Length, 150f));
        d.Active(debris, (0f, true), (1.8f, false));
        float[] dx = { -180f, 60f, 260f }, dy = { 60f, 140f, 40f }, rot = { 220f, -300f, 160f };
        for (int i = 0; i < 3; i++)
        {
            d.Active(planks[i], (0f, false), (1.8f, true), (2.6f, false));
            d.PosX(planks[i], (0f, 150f), (1.8f, 150f), (2.6f, 150f + dx[i]));
            d.PosY(planks[i], (0f, -90f), (1.8f, -90f), (2.1f, -90f + dy[i]), (2.6f, -190f));
            d.Rotate(planks[i], (0f, 0f), (1.8f, 0f), (2.6f, rot[i]));
        }
        d.Save();
    }

    /// <summary>ปืนยิงแม่กุญแจ: เล็งที่แม่กุญแจ → ยิง → กุญแจหลุดร่วง → ประตูเปิด</summary>
    private static void BuildGunPadlock(Transform overlays)
    {
        var d = new Demo("demo_gun_padlock", overlays, 3.8f);
        Sprite[] shoot = ClipSprites("Assets/Animations/Shoot_Gun/Shoot_Gun_Right.anim");
        Sprite[] hold = ClipSprites("Assets/Animations/Idle_Gun/Idle_Gun_Right.anim");
        d.Img("Floor", Icon("White"), new Vector2(0, -205), new Vector2(968, 140), Floor);
        RectTransform door = d.Img("Door", Icon("Door"), new Vector2(250, -10), new Vector2(200, 320), Color.white, preserve: true);
        RectTransform lockR = d.Img("Padlock", Icon("Padlock"), new Vector2(205, -10), new Vector2(54, 66), Color.white, preserve: true);
        RectTransform player = d.Img("Player", hold[0], new Vector2(-250, -40), new Vector2(330, 330), Color.white, preserve: true);
        RectTransform cross = d.Group("Crosshair", new Vector2(205, -10));
        RectTransform[] corners = Corners(d, cross);
        RectTransform flash = d.Img("Muzzle", Icon("White"), new Vector2(-88, -12), new Vector2(26, 18), new Color(1f, 0.85f, 0.45f, 1f));
        RectTransform mouse = d.Img("Mouse", Icon("Mouse"), new Vector2(-400, 150), new Vector2(70, 105), Color.white, preserve: true);

        var p = Loop(hold, 0f, 0.6f, 0.19f);
        p.Add((0.6f, shoot[0]));
        p.AddRange(Sequence(shoot, 1.8f, 0.08f));
        p.Add((1.8f + shoot.Length * 0.08f, shoot[0]));
        d.Sprites(player, p);
        d.Sprites(mouse, new List<(float, Sprite)> { (0f, Icon("Mouse")), (0.6f, Icon("Mouse_Right")), (1.75f, Icon("Mouse_Left")), (1.95f, Icon("Mouse_Right")), (2.6f, Icon("Mouse")) });
        d.Active(cross, (0f, false), (0.6f, true), (2.2f, false));
        Converge(d, corners, 0.6f, 1.6f, 70f, 22f, Color.white, Red);
        d.Active(flash, (0f, false), (1.8f, true), (1.9f, false));

        // กุญแจ: โดนยิง → เปลี่ยนเป็นแบบเปิด → ร่วงลงพื้น → จาง
        d.Sprites(lockR, new List<(float, Sprite)> { (0f, Icon("Padlock")), (1.85f, Icon("Padlock_Open")) });
        d.PosY(lockR, (0f, -10f), (1.85f, -10f), (2.4f, -160f), (d.Length, -160f));
        d.Rotate(lockR, (0f, 0f), (1.85f, 0f), (2.4f, -70f), (d.Length, -70f));
        d.Tint(lockR, (0f, Color.white), (2.6f, Color.white), (3.0f, new Color(1f, 1f, 1f, 0f)), (d.Length, new Color(1f, 1f, 1f, 0f)));
        // ประตูเปิด (บานหุบเข้าหาบานพับ)
        d.Scale(door, (0f, 1f), (2.6f, 1f), (3.2f, 1f), (d.Length, 1f));
        d.WidthOnly(door, (0f, 200f), (2.6f, 200f), (3.2f, 40f), (d.Length, 40f));
        d.Save();
    }

    /// <summary>ย่อตัว (C): เดินช้า ๆ ผ่านหลังมอน · เกือบโดนสังเกต "?" ขึ้นแล้วหาย</summary>
    private static void BuildSneak(Transform overlays, TMP_FontAsset font)
    {
        var d = new Demo("demo_sneak", overlays, 5.0f);
        Sprite[] walk = ClipSprites("Assets/Animations/Walk/Walk_Right.anim");
        Sprite[] enemy = MinionFrames();
        d.Img("Floor", Icon("White"), new Vector2(0, -205), new Vector2(968, 140), Floor);
        RectTransform foe = d.Img("Enemy", enemy[0], new Vector2(250, -30), new Vector2(330, 330), Color.white, preserve: true);
        RectTransform q = d.Text("Question", font, "?", new Vector2(250, 150), 90, new Color(1f, 0.85f, 0.3f, 1f));
        RectTransform player = d.Img("Player", walk[0], new Vector2(-400, -50), new Vector2(300, 300), Color.white, preserve: true);
        RectTransform keyC = Key(d, font, "C", new Vector2(-380, 190), false);

        d.Sprites(player, Loop(walk, 0f, d.Length, 0.28f));          // ก้าวช้ากว่าเดินปกติ
        d.PosX(player, (0f, -400f), (d.Length, 40f));
        d.Scale(player, (0f, 0.9f), (d.Length, 0.9f));                // ตัวเตี้ยลงนิด = ย่อ
        d.Sprites(foe, Loop(enemy, 0f, d.Length, 0.16f));
        Press(d, keyC, (0.1f, 0.35f));
        d.Active(q, (0f, false), (2.6f, true), (3.8f, false));
        d.Tint(q, (0f, new Color(1f, 0.85f, 0.3f, 0f)), (2.6f, new Color(1f, 0.85f, 0.3f, 0f)), (2.9f, new Color(1f, 0.85f, 0.3f, 1f)), (3.4f, new Color(1f, 0.85f, 0.3f, 1f)), (3.8f, new Color(1f, 0.85f, 0.3f, 0f)), (d.Length, new Color(1f, 0.85f, 0.3f, 0f)));
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

    /// <summary>[2026-11-28] รูปมินเนียนจริงในเกม (เดิมใช้ Enemy_Sprite ที่หน้าตาเหมือนบอส → ผู้เล่นเข้าใจผิดว่าบอสตีได้)</summary>
    private static Sprite[] MinionFrames() => new[] { Load<Sprite>("Assets/Art/มินเนียน/Minion.png") };

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

        /// <summary>ความสูง (หลอดแนวตั้ง) — ความกว้างคงที่</summary>
        public void Height(RectTransform r, float width, params (float t, float v)[] k)
        {
            r.sizeDelta = new Vector2(width, k[0].v);
            Linear(r, typeof(RectTransform), "m_SizeDelta.y", k);
        }

        /// <summary>ความกว้างอย่างเดียว ใช้ขนาดเดิมของรูป (ประตูหุบ)</summary>
        public void WidthOnly(RectTransform r, params (float t, float v)[] k) => Linear(r, typeof(RectTransform), "m_SizeDelta.x", k);

        /// <summary>หมุนแกน Z (องศา)</summary>
        public void Rotate(RectTransform r, params (float t, float v)[] k) => Linear(r, typeof(Transform), "localEulerAnglesRaw.z", k);

        /// <summary>Image แบบ Filled — ค่าเติม 0–1</summary>
        public void Fill(RectTransform r, params (float t, float v)[] k) => Linear(r, typeof(Image), "m_FillAmount", k);

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
