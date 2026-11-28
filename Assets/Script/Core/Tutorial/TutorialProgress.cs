using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [Claude 2026-11-28] ความจำ "การ์ดคำสอนไหนเคยขึ้นแล้ว" — เก็บในเครื่อง (PlayerPrefs) แยกจากไฟล์เซฟ
///
/// ทำไมไม่เก็บในเซฟ: เดิมเก็บเป็นธงเรื่องราว → ตาย/โหลดเซฟ/เล่นต่อ = ธงย้อนกลับ = การ์ดเด้งซ้ำ (บั๊กนาเจอ 2026-11-28)
/// แบบเกม AAA: สอนครั้งเดียวต่อผู้เล่น ตายกี่รอบก็ไม่สอนซ้ำ
///
/// ล้างเมื่อไหร่: "เริ่มเกมใหม่" = เปิดซีนเกมโดยไม่ได้โหลดเซฟ (กดเริ่มเกมใหม่ที่เมนู / กด Play ใน Editor) → TutorialService เรียก ClearAll
/// ล้างเองใน Editor: เมนู Tools ▸ Tutorial ▸ Reset Seen Tutorials
/// </summary>
public static class TutorialProgress
{
    private const string PrefKey = "tutorial_seen";
    private static HashSet<string> cache;

    private static HashSet<string> Seen
    {
        get
        {
            if (cache != null) return cache;
            cache = new HashSet<string>();
            string raw = PlayerPrefs.GetString(PrefKey, "");
            foreach (string id in raw.Split('|'))
                if (!string.IsNullOrEmpty(id)) cache.Add(id);
            return cache;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => cache = null;

    public static bool Has(string tutorialId) => !string.IsNullOrEmpty(tutorialId) && Seen.Contains(tutorialId);

    public static void Mark(string tutorialId)
    {
        if (string.IsNullOrEmpty(tutorialId) || !Seen.Add(tutorialId)) return;
        Save();
    }

    public static void ClearAll()
    {
        Seen.Clear();
        PlayerPrefs.DeleteKey(PrefKey);
        PlayerPrefs.Save();
    }

    private static void Save()
    {
        PlayerPrefs.SetString(PrefKey, string.Join("|", Seen));
        PlayerPrefs.Save();
    }
}
