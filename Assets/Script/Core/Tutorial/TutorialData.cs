using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// [Claude 2026-11-28] คำสอน 1 เรื่อง = ไฟล์ 1 ไฟล์ (การ์ดกลางจอ หยุดเกม · ขึ้นครั้งเดียว · ดูซ้ำในสมุดแท็บ Tutorials)
/// สเปก: docs/TUTORIAL_SPEC.md
///
/// วิธีสร้าง: Project คลิกขวา ▸ Create ▸ Tutorial ▸ Tutorial
///   แล้วลากไฟล์ใส่ช่อง Catalog ของ TutorialService (ที่ [CORE_SERVICES] / ก้อน UI การ์ด)
/// ข้อความทุกช่องเป็น "key" ใน Assets/Resources/LocalizationData.csv (คั่นด้วย ;)
/// </summary>
[CreateAssetMenu(fileName = "Tut_New", menuName = "Tutorial/Tutorial")]
public class TutorialData : ScriptableObject
{
    [Tooltip("รหัสคำสอน ห้ามซ้ำ เช่น gun / ptsd · ใช้จำว่าเคยขึ้นแล้ว (เปลี่ยนทีหลัง = เซฟเก่าจะขึ้นซ้ำ)")]
    public string tutorialId;

    [Tooltip("key หัวข้อการ์ด เช่น TUT_GUN_TITLE")]
    public string titleKey;

    [Tooltip("ไอคอนในรายการแท็บ Tutorials (ไม่บังคับ)")]
    public Sprite listIcon;

    [Tooltip("โชว์ในแท็บ Tutorials ของสมุดให้อ่านซ้ำได้ไหม")]
    public bool showInJournal = true;

    [Tooltip("หน้าในการ์ด (1 หน้าขึ้นไป) — หลายหน้า = มีปุ่ม ◀ ▶")]
    public List<TutorialPageData> pages = new List<TutorialPageData>();

    /// <summary>ชื่อธงในความจำกลาง (StoryFlags) — ติดไปกับเซฟอัตโนมัติ</summary>
    public string SeenKey => "tut:" + tutorialId;

    public int PageCount => pages != null ? pages.Count : 0;

    public string Title => Loc(titleKey, name);

    internal static string Loc(string key, string fallback = "")
    {
        if (string.IsNullOrEmpty(key)) return fallback;
        ILocalizationService loc = ServiceLocator.GetOptional<ILocalizationService>();
        return loc != null ? loc.GetText(key) : key;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(tutorialId))
            Debug.LogWarning($"[TutorialData] '{name}' ยังไม่ได้ตั้ง Tutorial Id — คำสอนนี้จะขึ้นไม่ได้", this);
        if (pages == null || pages.Count == 0)
            Debug.LogWarning($"[TutorialData] '{name}' ยังไม่มีหน้าในการ์ด (Pages)", this);
    }
#endif
}

/// <summary>หน้า 1 หน้าในการ์ด: ภาพขยับ (คลิป) + หัวข้อย่อย + ข้อความเป็นข้อ ๆ + ไอคอนปุ่มที่ขยับซ้อนบนคลิป</summary>
[Serializable]
public class TutorialPageData
{
    [Tooltip("คลิปวนสั้น 4–6 วิ ไม่มีเสียง (.mp4) — เว้นว่าง = ใช้ภาพนิ่งช่อง Image แทน")]
    public VideoClip clip;

    [Tooltip("ภาพนิ่งสำรอง (โชว์ตอนไม่มีคลิป / ระหว่างคลิปกำลังโหลด)")]
    public Sprite image;

    [Tooltip("key หัวข้อย่อยของหน้านี้ (เว้นว่าง = ใช้หัวข้อการ์ด)")]
    public string headingKey;

    [Tooltip("key ข้อความทีละข้อ (1–4 ข้อ) — ขึ้นเป็น • ข้อ ๆ")]
    public List<string> bodyKeys = new List<string>();

    [Tooltip("ชื่อชุดไอคอนปุ่มที่จะโชว์ซ้อนบนคลิป (ตรงกับ Overlay Id ของ TutorialOverlay ในการ์ด) เช่น mouse_right / key_e")]
    public List<string> overlayIds = new List<string>();
}

/// <summary>ประกาศตอนการ์ดคำสอนเด้งขึ้น (ครั้งแรกเท่านั้น ไม่นับเปิดดูซ้ำจากสมุด)</summary>
public readonly struct TutorialShownEvent
{
    public readonly TutorialData Tutorial;
    public TutorialShownEvent(TutorialData tutorial) { Tutorial = tutorial; }
}
