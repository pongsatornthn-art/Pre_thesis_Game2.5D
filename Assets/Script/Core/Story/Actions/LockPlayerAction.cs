using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Action สำหรับล็อกหรือปลดล็อกการเดินของผู้เล่นเฉพาะจุดภายใน Sequence
/// ใช้วิธีเซ็ต isStunned บน PlayerMovement ตามข้อตกลง 12.1 เพื่อหลีกเลี่ยงการแก้ไขโค้ดของเพื่อนร่วมทีม
/// 
/// หมายเหตุ: การตั้ง isStunned จะระงับการเดินและการ Dash แต่ผู้เล่นยังสามารถกดยิงปืนได้
/// </summary>
[Serializable]
public class LockPlayerAction : IStoryAction
{
    [Tooltip("ติ๊กถูกเพื่อล็อกการเดิน / นำติ๊กออกเพื่อปลดล็อก")]
    public bool lockMovement = true;

    public IEnumerator Execute(StoryContext ctx)
    {
        if (ctx?.Player != null)
        {
            PlayerMovement movement = ctx.Player.GetComponent<PlayerMovement>();
            if (movement != null)
            {
                movement.isStunned = lockMovement;
            }
        }
        yield break;
    }
}
