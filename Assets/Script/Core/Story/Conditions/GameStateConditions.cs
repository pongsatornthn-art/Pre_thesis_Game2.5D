using System;

// [Claude 2026-11-28] เงื่อนไขสถานะเกมทั่วไป — ทำมาให้ TutorialOnEvent แต่ใช้กับระบบเนื้อเรื่องที่ไหนก็ได้

/// <summary>จริงเสมอ — "ทำทันที" (เช่น เด้งคำสอนพื้นฐานตอนเริ่มเกม)</summary>
[Serializable, PickerName("ทั่วไป/ทันที (จริงเสมอ)")]
public class AlwaysCondition : IStoryCondition
{
    public bool IsMet(StoryContext ctx) => true;
}

/// <summary>อยู่ในมุมมองบุคคลที่หนึ่ง (StoryCloseUpView)</summary>
[Serializable, PickerName("กล้อง/อยู่มุม Close-up")]
public class CloseUpActiveCondition : IStoryCondition
{
    public bool IsMet(StoryContext ctx) => StoryCloseUpView.Active != null;
}

/// <summary>นั่งโต๊ะห้องปลอดภัยอยู่ (มุมโต๊ะ วิทยุ/เซฟ)</summary>
[Serializable, PickerName("ห้องปลอดภัย/นั่งโต๊ะอยู่")]
public class SafeDeskActiveCondition : IStoryCondition
{
    public bool IsMet(StoryContext ctx) => RadioMiniGameManager.IsActive;
}
