/// <summary>
/// ตัวกั้นปุ่มลัดเกม — จุดเดียวที่บอกว่า "ตอนนี้ห้ามกดปุ่มเล่นเกม" (สมุด I/J/K/M/N · ฮอตบาร์ 1-4 · F เก็บของ · ปุ่มลัด PTSD)
/// ใช้ตอน: นั่งโต๊ะมุมมองบุคคลที่หนึ่ง (RadioMiniGameManager) / หน้าเลือกช่องเซฟ-โหลดเปิดอยู่
/// บัคเดิม: นั่งโต๊ะแล้วกด I → สมุดเปิดเบื้องหลัง (มองไม่เห็นเพราะโต๊ะซ่อน UI) → เกมหยุด กล้องหมุนไม่ได้
/// เพิ่มเงื่อนไขใหม่ที่นี่ที่เดียว ระบบที่เช็คอยู่แล้วได้ผลทันที
/// [Claude 2026-11-11]
/// </summary>
public static class GameplayInputGate
{
    public static bool Blocked => RadioMiniGameManager.IsActive || SaveSlotPickerUI.IsOpen
                                  || MinigameBase.AnyRunning || SafeMinigame.AnyActive || PTSDMinigameController.AnyActive;
}
