using System;

/// <summary>
/// ตัวนับในความจำกลาง — แยกจาก IStoryFlags ตามหลัก Interface Segregation
/// ระบบที่ใช้แค่ธงไม่ต้องรู้จักตัวนับ
/// </summary>
public interface IStoryCounters
{
    int Get(StoryCounterId counter);
    void Add(StoryCounterId counter, int amount = 1);   // ติดลบได้ แต่ค่าจะไม่ต่ำกว่า 0
    void Set(StoryCounterId counter, int value);
    event Action OnChanged;                              // แจ้งเมื่อค่าใดๆ ในความจำกลางเปลี่ยน
}
