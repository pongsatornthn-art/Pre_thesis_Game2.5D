using System;
using UnityEngine;

/// <summary>
/// ข้อมูลหัวช่องเซฟ — สำหรับหน้าจอเลือกช่อง (แบบ Resident Evil) โชว์โดยไม่ต้องโหลดเกม
/// </summary>
public readonly struct SaveSlotInfo
{
    public readonly int Slot;               // SaveManager.AutoSlot = ช่องเซฟอัตโนมัติ
    public readonly bool Exists;
    public readonly DateTime SavedAtLocal;
    public readonly float PlayTimeSeconds;
    public readonly string LocationKey;     // key แปลภาษา ชื่อห้อง/พื้นที่ (เว้นว่างได้)
    public readonly string SceneName;

    public bool IsAuto => Slot == SaveManager.AutoSlot;

    public SaveSlotInfo(int slot, bool exists, DateTime savedAtLocal, float playTimeSeconds, string locationKey, string sceneName)
    {
        Slot = slot;
        Exists = exists;
        SavedAtLocal = savedAtLocal;
        PlayTimeSeconds = playTimeSeconds;
        LocationKey = locationKey;
        SceneName = sceneName;
    }

    /// <summary>เวลาเล่นรูปแบบ 01:23:45</summary>
    public string PlayTimeText
    {
        get
        {
            TimeSpan t = TimeSpan.FromSeconds(PlayTimeSeconds);
            return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
        }
    }
}

/// <summary>คำขอเซฟจากจุดเซฟ — ส่งให้หน้าจอเลือกช่องเอาไปใช้ตอนผู้เล่นกดเลือก</summary>
public readonly struct SaveRequest
{
    public readonly Transform SpawnPoint;
    public readonly string LocationKey;
    public readonly Action<bool> OnFinished;   // true = เซฟแล้ว · false = ยกเลิก/ไม่สำเร็จ

    public SaveRequest(Transform spawnPoint, string locationKey, Action<bool> onFinished)
    {
        SpawnPoint = spawnPoint;
        LocationKey = locationKey;
        OnFinished = onFinished;
    }
}

/// <summary>
/// หน้าจอ "เลือกช่องเซฟ" — ⏳ ทำตอนอาร์ต UI มา
/// คลาสที่ implement ลงทะเบียนตัวเองด้วย ServiceLocator.Register&lt;ISaveSlotPicker&gt;(this)
/// แล้วใช้ SaveManager.GetSlotInfos() โชว์รายการ → ผู้เล่นเลือก → SaveManager.TrySaveToSlot(...) → เรียก request.OnFinished
/// ยังไม่มีหน้าจอนี้ → จุดเซฟเซฟลงช่องเริ่มต้นให้เลย
/// </summary>
public interface ISaveSlotPicker
{
    void OpenSavePicker(SaveRequest request);
}
