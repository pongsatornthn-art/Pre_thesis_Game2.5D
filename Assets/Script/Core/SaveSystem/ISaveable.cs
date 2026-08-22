using UnityEngine;

/// <summary>
/// อินเตอร์เฟสสำหรับทุกสิ่งในเกมที่ต้องการถูก Save/Load 
/// เช่น Spawner, ตัวผู้เล่น, หรือประตู
/// </summary>
public interface ISaveable
{
    // โยนข้อมูลออกมาเป็นก้อน JSON String เพื่อเอาไปเซฟ
    string CaptureState();

    // รับข้อมูล JSON String ตอนโหลดเกม แล้วแปลงกลับไปเป็นตัวแปร
    void RestoreState(string stateJson);
}
