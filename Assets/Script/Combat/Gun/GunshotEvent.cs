using UnityEngine;

namespace Combat.Gun
{
    /// <summary>
    /// อีเวนต์แจ้งเตือนการยิงปืนผ่าน GameEventBus
    /// เพื่อให้ระบบได้ยินเสียง (เช่น StalkerHearing) รับรู้ตำแหน่งและความดังของเสียงปืน
    /// โดยไม่ต้องผูกโค้ดของปืนเข้ากับ AI มอนสเตอร์โดยตรง (Decoupling)
    /// </summary>
    public readonly struct GunshotEvent
    {
        public readonly Vector3 Position;
        public readonly float Loudness;

        public GunshotEvent(Vector3 pos, float loudness)
        {
            Position = pos;
            Loudness = loudness;
        }
    }
}
