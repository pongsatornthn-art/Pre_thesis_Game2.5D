using UnityEngine;

/// <summary>
/// ตัวกลางปุ่ม ESC (งานข้อ 3) — หน้าต่าง/มินิเกมไหน "ใช้ ESC ปิดตัวเองไปแล้ว" ในเฟรมนี้ ให้เรียก MarkHandled()
/// PauseManager เช็คใน LateUpdate (ทำหลังทุก Update) → ถ้ามีคนใช้ไปแล้ว ไม่เปิดเมนูหยุดเกมซ้อน
/// เดิม: กด ESC ปิดกระเป๋า/วิทยุ/ตู้เซฟ/กล่องเก็บของ → เมนูหยุดเกมเด้งขึ้นมาพร้อมกัน
/// [Claude 2026-11-11]
/// </summary>
public static class EscapeGuard
{
    private static int handledFrame = -1;

    /// <summary>เรียกตอนใช้ ESC ปิดหน้าต่างของตัวเองไปแล้ว</summary>
    public static void MarkHandled() => handledFrame = Time.frameCount;

    /// <summary>มีหน้าต่างไหนใช้ ESC ไปแล้วในเฟรมนี้</summary>
    public static bool HandledThisFrame => handledFrame == Time.frameCount;
}
