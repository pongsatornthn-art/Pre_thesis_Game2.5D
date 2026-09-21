using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ตัวจัดการสถานะธงเนื้อเรื่องส่วนกลางของเกม (Story Flags Service)
/// ทำหน้าที่เป็น Single Source of Truth ป้องกันไม่ให้ระบบอื่นแอบจำสถานะเอง
/// ลงทะเบียนใน ServiceLocator และรองรับการเซฟ/โหลดผ่าน ISaveable
/// </summary>
public class StoryFlagService : MonoBehaviour, IStoryFlags, ISaveable
{
    // ใช้ HashSet<string> ตอนรันไทม์เพื่อประสิทธิภาพ O(1) ในการตรวจสอบ Has
    private readonly HashSet<string> activeFlags = new HashSet<string>();

    [Header("Debug ดูสถานะธงใน Inspector")]
    [SerializeField] private List<string> debugFlagList = new List<string>();

    public event Action OnChanged;

    [Serializable]
    private struct FlagSaveData
    {
        public List<string> savedFlags;
    }

    private void Awake()
    {
        // ลงทะเบียนบริการกลางเพื่อให้ระบบอื่นเรียกใช้ผ่าน ServiceLocator.Get<IStoryFlags>()
        ServiceLocator.Register<IStoryFlags>(this);
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<IStoryFlags>();
    }

    /// <summary>ตรวจสอบว่าธงนี้ถูกตั้งค่าแล้วหรือไม่</summary>
    public bool Has(StoryFlagId flag)
    {
        if (flag == null || string.IsNullOrEmpty(flag.flagId)) return false;
        return activeFlags.Contains(flag.flagId);
    }

    /// <summary>
    /// ตั้งสถานะธง หากธงถูกตั้งอยู่แล้วจะไม่ยิง OnChanged ซ้ำ
    /// เพื่อป้องกันไม่ให้ระบบเควสหรือทริกเกอร์ทำงานวนรอบโดยไม่จำเป็น
    /// </summary>
    public void Set(StoryFlagId flag)
    {
        if (flag == null || string.IsNullOrEmpty(flag.flagId)) return;

        if (activeFlags.Add(flag.flagId))
        {
            UpdateDebugList();
            Debug.Log($"<color=cyan>🚩 [StoryFlag] ตั้งธง: {flag.flagId}</color>");
            OnChanged?.Invoke();
        }
    }

    /// <summary>ล้างสถานะธง</summary>
    public void Clear(StoryFlagId flag)
    {
        if (flag == null || string.IsNullOrEmpty(flag.flagId)) return;

        if (activeFlags.Remove(flag.flagId))
        {
            UpdateDebugList();
            Debug.Log($"<color=yellow>🏳️ [StoryFlag] ล้างธง: {flag.flagId}</color>");
            OnChanged?.Invoke();
        }
    }

    private void UpdateDebugList()
    {
        // อัปเดตลิสต์ใน Inspector เฉพาะเพื่อการตรวจสอบตอนพัฒนา
        debugFlagList.Clear();
        debugFlagList.AddRange(activeFlags);
    }

    #region ISaveable Implementation

    public string CaptureState()
    {
        // เซฟเฉพาะ flagId ที่เปิดใช้งานอยู่ลง JSON
        FlagSaveData data = new FlagSaveData
        {
            savedFlags = new List<string>(activeFlags)
        };
        return JsonUtility.ToJson(data);
    }

    public void RestoreState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson)) return;

        FlagSaveData data = JsonUtility.FromJson<FlagSaveData>(stateJson);
        activeFlags.Clear();

        if (data.savedFlags != null)
        {
            foreach (string id in data.savedFlags)
            {
                if (!string.IsNullOrEmpty(id))
                {
                    activeFlags.Add(id);
                }
            }
        }

        UpdateDebugList();
        // ยิงแจ้งเตือนเพื่อให้ระบบเควสและทริกเกอร์อัปเดตสถานะใหม่ทันทีหลังโหลดเซฟ
        OnChanged?.Invoke();
    }

    #endregion
}
