using UnityEngine;

/// <summary>
/// ⚠️ [เลิกใช้แล้ว — ระบบเก่า] สคริปต์นี้เคยใช้กับ "โหมดเล็ง FPS" (คลิกขวาแล้วซูมกล้องข้ามไหล่)
/// มันเคยสั่ง Cursor.lockState = Locked (ล็อกเมาส์กลางจอ) ตอนกดคลิกขวา
///
/// ตั้งแต่เปลี่ยนเป็นระบบยิงแบบ Alien Shooter (คลิกขวา = "นิ่งขึ้น" ไม่ใช่ซูมกล้อง)
/// โค้ดเดิมทำให้ **เมาส์หายตอนกดคลิกขวา จนเล็งไม่ได้** เลยถอดการทำงานออกทั้งหมด
///
/// 👉 ลบ component นี้ออกจากก้อนใน Scene ได้เลย แล้วค่อยลบไฟล์นี้ทิ้งทีหลัง
/// (เก็บคลาสไว้ก่อนเพื่อไม่ให้ Scene ขึ้น "Missing Script" ตอนนี้)
/// </summary>
public class AimPivotController : MonoBehaviour
{
    [Header("⚠️ สคริปต์นี้เลิกใช้แล้ว ไม่ทำอะไรทั้งนั้น — ลบ component ออกได้เลย")]
    [Tooltip("เก็บไว้เฉยๆ กัน Scene ขึ้น Missing Script")]
    public PlayerCombat playerCombat;
    public float sensitivity = 200f;

    // ไม่มี Update() แล้ว — ไม่ล็อกเมาส์ ไม่หมุนกล้อง ไม่ยุ่งกับอะไรทั้งสิ้น
}
