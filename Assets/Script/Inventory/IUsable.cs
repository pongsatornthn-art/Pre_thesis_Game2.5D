using UnityEngine;

/// <summary>
/// ไอเทมที่ "ใช้" ได้ (กดปุ่มใช้แล้วเกิดผล เช่น ยาฟื้นเลือด)
/// ไอเทมชนิดไหนใช้ได้ก็ implement อันนี้ — ระบบที่เรียกใช้ไม่ต้องรู้ว่ามันคือไอเทมอะไร
/// </summary>
public interface IUsable
{
    /// <summary>ใช้ไอเทมนี้</summary>
    /// <param name="user">ผู้ใช้ (ปกติคือก้อน Player)</param>
    /// <returns>true = ใช้สำเร็จ (ผู้เรียกควรหักไอเทมออกจากกระเป๋า)</returns>
    bool Use(GameObject user);
}
