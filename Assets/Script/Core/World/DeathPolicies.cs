using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// นโยบาย "ตายแล้วเกิดอะไร" — Strategy Pattern
/// เปลี่ยนกติกาทีหลัง = สร้างคลาสลูกใหม่ แล้วเลือกในช่องของ DeathHandler ใน Inspector · ไม่ต้องแก้ของเดิม
/// (เจ้าของยังไม่คอนเฟิมกติกาตายในโลก PTSD — QUEST_SAVE_SPEC.md หัวข้อ 5)
/// </summary>
public interface IDeathPolicy
{
    void Handle(SaveManager save);
}

/// <summary>โลกปกติ: กลับไปเซฟล่าสุด ไม่ว่าช่องปกติหรือช่องอัตโนมัติ (ไม่มีเซฟ = เริ่มซีนใหม่)</summary>
[Serializable, PickerName("กลับไปเซฟล่าสุด")]
public class ReloadLastSaveDeath : IDeathPolicy
{
    public void Handle(SaveManager save)
    {
        if (save != null && save.LoadMostRecent()) return;

        Debug.Log("[Death] ไม่มีไฟล์เซฟ — เริ่มซีนใหม่");
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}

/// <summary>
/// โลก PTSD (ค่าเริ่มต้นที่ตกลงไว้): เริ่มโลก PTSD ใหม่ตั้งแต่ทางเข้า — ของที่เก็บรอบนั้นหาย ต้องเก็บใหม่
/// ไม่มีจุดเริ่มใหม่ (เช่น ซีนเริ่มมาในโลก PTSD เลย) → ถอยไปเซฟล่าสุด
/// </summary>
[Serializable, PickerName("เริ่มโลก PTSD ใหม่ตั้งแต่ทางเข้า")]
public class RestartPtsdFromEntryDeath : IDeathPolicy
{
    public void Handle(SaveManager save)
    {
        // สำเนาอยู่ในหน่วยความจำ (ไม่ใช่ไฟล์) — ถือ reference ฉากเนื้อเรื่องไว้ได้ จึงเล่นต่อจากจุดเข้าโลกได้
        GameSaveData snapshot = PtsdCheckpoint.Instance != null ? PtsdCheckpoint.Instance.EntrySnapshot : null;
        if (save != null && snapshot != null)
        {
            save.LoadSnapshot(snapshot, fromCheckpoint: true);
            return;
        }

        Debug.LogWarning("[Death] ไม่มีจุดเริ่มใหม่ของโลก PTSD (ลืมแปะ PtsdCheckpoint?) — กลับไปเซฟล่าสุดแทน");
        new ReloadLastSaveDeath().Handle(save);
    }
}
