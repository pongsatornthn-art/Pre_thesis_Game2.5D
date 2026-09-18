using UnityEngine;

/// <summary>
/// อีเวนต์แจ้งว่าผู้เล่นเพิ่งโดนดาเมจ ยิงผ่าน GameEventBus
///
/// มีไว้เพื่อให้ระบบอื่น (เช่น กรวยกระสุนของปืนที่ต้องเด้งบานตอนเสียหลัก ตาม GDD)
/// รับรู้ได้โดยไม่ต้องผูกกับสคริปต์เลือดของผู้เล่นโดยตรง
/// ระบบอื่น ๆ ในอนาคต (สั่นจอ, เลือดกระเซ็นที่ขอบจอ, เสียงหอบ) มาสมัครฟังเพิ่มได้เลย
/// </summary>
public readonly struct PlayerDamagedEvent
{
    /// <summary>จำนวนดาเมจที่โดน</summary>
    public readonly int Damage;

    /// <summary>ตำแหน่งของผู้เล่นตอนโดน</summary>
    public readonly Vector3 Position;

    public PlayerDamagedEvent(int damage, Vector3 position)
    {
        Damage = damage;
        Position = position;
    }
}
