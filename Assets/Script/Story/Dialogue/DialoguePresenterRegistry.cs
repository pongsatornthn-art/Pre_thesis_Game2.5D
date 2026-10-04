using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ชื่อมาตรฐานของ Presenter ที่มีอยู่แล้ว — ใช้แทนการพิมพ์สตริงเองกันพิมพ์ผิด
/// เพิ่มสไตล์ใหม่ไม่ต้องแก้ไฟล์นี้ แค่ตั้ง presenterId ของ Presenter ตัวใหม่เป็นชื่ออะไรก็ได้
/// </summary>
public static class DialoguePresenterIds
{
    public const string World = "world"; // ข้อความลอยข้างตัวละคร
    public const string Box = "box";     // กล่องข้อความล่างจอ + รูปหน้า
}

/// <summary>
/// สมุดรายชื่อ Presenter — แต่ละ Presenter ลงชื่อตัวเองตอน Awake
/// DialogueService ถามหาด้วยชื่อ จึงไม่ต้องรู้จักคลาส Presenter ตัวไหนเลย (OCP)
/// → เพิ่มสไตล์ที่ 3, 4, 5 = สร้างคลาสใหม่ที่ implement IDialoguePresenter แล้วลงชื่อ ไม่ต้องแก้ Service
/// </summary>
public static class DialoguePresenterRegistry
{
    private static readonly Dictionary<string, IDialoguePresenter> presenters = new Dictionary<string, IDialoguePresenter>();

    // ล้างของค้างจากรอบเล่นก่อน กรณีปิด Domain Reload ไว้ใน Enter Play Mode Options
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => presenters.Clear();

    public static void Register(string id, IDialoguePresenter presenter)
    {
        if (string.IsNullOrEmpty(id) || presenter == null) return;

        if (presenters.TryGetValue(id, out IDialoguePresenter existing) && !IsDead(existing) && existing != presenter)
        {
            Debug.LogWarning($"[DialoguePresenterRegistry] มี Presenter ชื่อ '{id}' ซ้ำ 2 ตัว — ใช้ตัวล่าสุดแทน");
        }
        presenters[id] = presenter;
    }

    public static void Unregister(string id, IDialoguePresenter presenter)
    {
        if (string.IsNullOrEmpty(id)) return;

        // ลบเฉพาะถ้ายังเป็นตัวเดียวกัน กันตัวเก่าที่ถูกทำลายทีหลังไปลบตัวใหม่ทิ้ง
        if (presenters.TryGetValue(id, out IDialoguePresenter current) && current == presenter)
        {
            presenters.Remove(id);
        }
    }

    public static IDialoguePresenter Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (presenters.TryGetValue(id, out IDialoguePresenter presenter) && !IsDead(presenter)) return presenter;
        return null;
    }

    // MonoBehaviour ที่ถูก Destroy แล้วจะ == null ตามกติกา Unity แต่ interface เทียบ null ธรรมดาไม่เห็น
    private static bool IsDead(IDialoguePresenter presenter)
    {
        return presenter == null || (presenter is Object unityObj && unityObj == null);
    }
}
