using System.Collections;
using UnityEngine;

/// <summary>
/// บริบทข้อมูลส่วนกลางที่ส่งต่อให้แต่ละ Action ใช้ร่วมกันระหว่างดำเนินฉาก
/// รวมศูนย์การอ้างอิง Service หลักและตัวละครผู้เล่นไว้ที่เดียว เพื่อลดภาระการค้นหาซ้ำซ้อน
/// </summary>
public class StoryContext
{
    public GameObject Player;
    public IStoryFlags Flags;
    public IQuestService Quests;
    public MonoBehaviour Runner;
    public GameObject LastSpawnedActor;
    public System.Collections.Generic.Dictionary<string, GameObject> Actors = new System.Collections.Generic.Dictionary<string, GameObject>();
}

/// <summary>
/// สัญญาสำหรับคำสั่งเหตุการณ์ในเนื้อเรื่อง (Action)
/// แต่ละคำสั่งจะทำงานแบบ Coroutine จนกว่าจะเสร็จสิ้น จึงจะส่งต่อไปยัง Action ถัดไป
/// ออกแบบตามหลัก Open-Closed Principle (OCP) เพื่อให้สามารถเพิ่มคำสั่งประเภทใหม่ได้โดยไม่ต้องแก้ไข StoryDirector
/// </summary>
public interface IStoryAction
{
    IEnumerator Execute(StoryContext ctx);
}
