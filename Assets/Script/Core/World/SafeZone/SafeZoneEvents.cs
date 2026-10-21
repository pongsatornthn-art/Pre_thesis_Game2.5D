using UnityEngine;

/// <summary>
/// ประกาศเมื่อสถานะผู้เล่นใน Safe Zone เปลี่ยนแปลง (เดินเข้าหรือเดินออกจากห้อง)
/// </summary>
public readonly struct SafeZonePlayerChangedEvent
{
    public readonly SafeZone Zone;
    public readonly bool Inside;

    public SafeZonePlayerChangedEvent(SafeZone zone, bool inside)
    {
        Zone = zone;
        Inside = inside;
    }
}

/// <summary>
/// ประกาศเมื่อมินิเกมล้มเหลว แล้วจุด Safe Zone ถูกย้ายไปยังห้องใหม่
/// </summary>
public readonly struct SafeZoneRelocatedEvent
{
    public readonly SafeZone From;
    public readonly SafeZone To;
    public readonly float CooldownSeconds;

    public SafeZoneRelocatedEvent(SafeZone from, SafeZone to, float cooldownSeconds)
    {
        From = from;
        To = to;
        CooldownSeconds = cooldownSeconds;
    }
}

/// <summary>
/// ประกาศสถานะความเหนื่อยล้า/ตื่นตระหนก (Panic Exhaustion) ตอนเริ่มหรือจบคูลดาวน์
/// </summary>
public readonly struct PanicExhaustionChangedEvent
{
    public readonly bool Active;
    public readonly float SecondsLeft;

    public PanicExhaustionChangedEvent(bool active, float secondsLeft)
    {
        Active = active;
        SecondsLeft = secondsLeft;
    }
}
