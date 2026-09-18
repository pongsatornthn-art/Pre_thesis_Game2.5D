/// <summary>
/// อินเตอร์เฟซสำหรับวัตถุที่ใช้งานผ่าน Object Pool
/// 
/// ทำไมมีแค่นี้:
/// ยึดหลัก Interface Segregation Principle (ISP) — มีแค่ 2 เมธอด OnSpawn และ OnDespawn
/// เพื่อให้วัตถุรีเซ็ตสถานะภายในตัวเอง (เช่น เลือด, ตัวนับเวลา, ทิศทาง) ตอนยืมออกจากพูลและตอนคืนเข้าพูล
/// </summary>
public interface IPoolable
{
    /// <summary>ถูกเรียกเมื่อถูกดึงออกจากพูลมาใช้งาน</summary>
    void OnSpawn();

    /// <summary>ถูกเรียกเมื่อถูกส่งคืนกลับเข้าพูล</summary>
    void OnDespawn();
}
