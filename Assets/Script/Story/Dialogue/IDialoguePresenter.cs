using System.Collections;

/// <summary>
/// อินเตอร์เฟสสำหรับตัวแสดงผลบทสนทนา (Dialogue Presenter)
/// แยกรูปแบบการแสดงผล (เช่น ลอยในโลก WorldSpace หรือ กล่องล่างจอ ScreenBox) ออกจากตัวควบคุม
/// ทำให้สามารถเพิ่มสไตล์การแสดงบทพูดแบบใหม่ได้โดยไม่ต้องแก้ DialogueService ตามหลัก OCP
/// </summary>
public interface IDialoguePresenter
{
    /// <summary>แสดงบทพูด 1 บรรทัดและรอจนกระทั่งจบประโยคหรือพ้นระยะเวลาค้าง</summary>
    IEnumerator Show(DialogueLine line);

    /// <summary>ซ่อนการแสดงผลทันทีและหยุดการทำงานทั้งหมด</summary>
    void HideImmediate();
}
