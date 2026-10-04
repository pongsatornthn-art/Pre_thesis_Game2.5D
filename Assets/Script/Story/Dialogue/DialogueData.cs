using System;
using UnityEngine;

/// <summary>
/// ข้อมูลบทสนทนา 1 บรรทัด (Line)
/// ข้อความและชื่อทุกคนต้องอ้างอิงเป็นคีย์ใน LocalizationData.csv เท่านั้น ห้ามใส่ข้อความตรงๆ
/// </summary>
[Serializable]
public class DialogueLine
{
    [Tooltip("คีย์ชื่อคนพูดใน LocalizationData.csv (เว้นว่าง = เสียงในหัวตัวเอก ไม่โชว์ชื่อ)")]
    public string speakerKey;

    [Tooltip("ตัวละครในซีนที่พูดบรรทัดนี้ — ต้องตรงกับ Speaker Id ของ DialogueSpeaker ที่แปะไว้ (เช่น player / npc_mom)\n" +
             "ข้อความลอยจะไปขึ้นข้างตัวนั้น · เว้นว่าง = ขึ้นตรงที่วางกล่องข้อความไว้")]
    public string speakerId;

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
    [Tooltip("ชื่อรูปแบบการแสดงผล — ต้องตรงกับ Presenter Id ของกล่องข้อความในซีน\n" +
             "world = ลอยข้างตัวละคร · box = กล่องล่างจอ + รูปหน้า · เว้นว่าง = ใช้ค่าเริ่มต้นของ DialogueService")]
    public string presenterId = DialoguePresenterIds.World;

    [Tooltip("รายการบทสนทนาเรียงตามลำดับ")]
    public DialogueLine[] lines;
}
