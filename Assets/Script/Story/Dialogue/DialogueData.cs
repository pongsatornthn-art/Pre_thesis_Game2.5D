using System;
using UnityEngine;

/// <summary>
/// รูปแบบของตัวแสดงผลบทสนทนา
/// </summary>
public enum DialoguePresenterStyle
{
    WorldSpace, // ข้อความลอยเหนือหรือข้างตัวละครในโลก
    ScreenBox   // กล่องบทพูดที่ด้านล่างของหน้าจอ
}

/// <summary>
/// ข้อมูลบทสนทนา 1 บรรทัด (Line)
/// ข้อความและชื่อทุกคนต้องอ้างอิงเป็นคีย์ใน LocalizationData.csv เท่านั้น ห้ามใส่ข้อความตรงๆ
/// </summary>
[Serializable]
public class DialogueLine
{
    [Tooltip("คีย์ชื่อคนพูดใน LocalizationData.csv (เว้นว่าง = เสียงในหัวตัวเอก ไม่โชว์ชื่อ)")]
    public string speakerKey;

    [Tooltip("คีย์ข้อความใน LocalizationData.csv (ห้ามพิมพ์ข้อความจริงตรงๆ)")]
    public string textKey;

    [Tooltip("รูปภาพใบหน้าผู้พูด (เว้นว่างได้หากไม่มี)")]
    public Sprite portrait;

    [Tooltip("คลิปเสียงพากย์ (เว้นว่างได้หากไม่มี)")]
    public AudioClip voiceClip;

    [Tooltip("เวลาแสดงข้อความค้างไว้หลังพิมพ์จบ (วินาที)")]
    public float holdSeconds = 2.5f;
}

/// <summary>
/// ชุดข้อมูลบทสนทนา 1 บท (ScriptableObject)
/// ดำเนินเรื่องเป็นเส้นตรงตามการตัดสินใจของการออกแบบ
/// </summary>
[CreateAssetMenu(fileName = "Dialogue_", menuName = "Story/Dialogue", order = 5)]
public class DialogueData : ScriptableObject
{
    [Tooltip("รูปแบบกล่องบทพูดที่ต้องการใช้ในบทนี้")]
    public DialoguePresenterStyle style;

    [Tooltip("รายการบทสนทนาเรียงตามลำดับ")]
    public DialogueLine[] lines;
}
