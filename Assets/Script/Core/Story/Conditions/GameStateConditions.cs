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

/// <summary>[2026-11-28] มินิเกมยันประตูเล่นอยู่ (DoorBarricadeStruggle ของปอ)</summary>
[Serializable, PickerName("มินิเกม/ยันประตูอยู่")]
public class DoorStruggleActiveCondition : IStoryCondition
{
    public bool IsMet(StoryContext ctx) => DoorBarricadeStruggle.AnyActive;
}

/// <summary>[2026-11-28] โดนมินเนียนตะครุบอยู่ (มินิเกมสะบัดหลุด)</summary>
[Serializable, PickerName("มินิเกม/โดนมินเนียนจับอยู่")]
public class MinionGrabActiveCondition : IStoryCondition
{
    public bool IsMet(StoryContext ctx) => MinionGrabStruggle.AnyActive;
}

/// <summary>[2026-11-28] โดนบอสจับอยู่ (หน้ามือผี StalkerJumpscareUI ของปอ)</summary>
[Serializable, PickerName("มินิเกม/โดนบอสจับอยู่")]
public class BossGrabActiveCondition : IStoryCondition
{
    public bool IsMet(StoryContext ctx) => StalkerJumpscareUI.AnyActive;
}

/// <summary>[2026-11-28] ยืนในห้องเขียวของโลก PTSD A (จุดที่ค้าง Space หายใจออกจาก PTSD ได้)</summary>
[Serializable, PickerName("โลก PTSD/อยู่ในห้องเขียว (หายใจออกได้)")]
public class BreathingRoomCondition : IStoryCondition
{
    private PTSDMinigameCharger[] chargers;

    public bool IsMet(StoryContext ctx)
    {
        PTSDManager pm = PTSDManager.Instance;
        if (pm == null || pm.currentMode != PTSDMode.Survival_TypeA) return false;   // PTSD B หายใจออกไม่ได้ = ไม่สอน
        if (chargers == null) chargers = UnityEngine.Object.FindObjectsByType<PTSDMinigameCharger>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
        foreach (PTSDMinigameCharger c in chargers)
            if (c != null && c.isActiveAndEnabled && c.isPlayerInSafeZone) return true;
        return false;
    }
}

/// <summary>[2026-11-28] อยู่ในโลก PTSD B (เควสโต๊ะวาดรูป) — ใช้เด้งการ์ดสอนย่อง
/// (ไม่สอนตอน PTSD ครั้งแรก เพราะเป็นฉากบอสไล่ ย่องแล้วตายฟรี)</summary>
[Serializable, PickerName("โลก PTSD/อยู่ใน PTSD B (โต๊ะวาดรูป)")]
public class PtsdTypeBCondition : IStoryCondition
{
    public bool IsMet(StoryContext ctx) =>
        PTSDManager.Instance != null && PTSDManager.Instance.currentMode == PTSDMode.Narrative_TypeB;
}

/// <summary>นั่งโต๊ะห้องปลอดภัยอยู่ (มุมโต๊ะ วิทยุ/เซฟ)</summary>
[Serializable, PickerName("ห้องปลอดภัย/นั่งโต๊ะอยู่")]
public class SafeDeskActiveCondition : IStoryCondition
{
    public bool IsMet(StoryContext ctx) => RadioMiniGameManager.IsActive;
}
