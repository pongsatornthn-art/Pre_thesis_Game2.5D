using System.Collections;
using UnityEngine;

/// <summary>ประกาศก่อนจัดการการตาย — UI จอดำ/ข้อความ "คุณตายแล้ว" มาฟังได้ (ทำตอนมีอาร์ต)</summary>
public readonly struct PlayerDeathHandlingEvent
{
    public readonly bool InPtsd;
    public readonly float DelaySeconds;
    public PlayerDeathHandlingEvent(bool inPtsd, float delaySeconds) { InPtsd = inPtsd; DelaySeconds = delaySeconds; }
}

/// <summary>
/// ตายแล้วเลือกนโยบายตามโลกที่อยู่ แล้วสั่งทำงาน
/// ไม่รู้รายละเอียดว่านโยบายทำอะไร (Strategy) — เปลี่ยนกติกาโดยเลือกชนิดใหม่ในช่องด้านล่าง
/// วางที่ [STORY] คู่กับ PlayerDeathWatcher
/// </summary>
public class DeathHandler : MonoBehaviour
{
    [Header("ตายในโลกปกติ")]
    [SerializeReference, SubclassPicker] private IDeathPolicy realWorldPolicy = new ReloadLastSaveDeath();

    [Header("ตายในโลก PTSD")]
    [SerializeReference, SubclassPicker] private IDeathPolicy ptsdPolicy = new RestartPtsdFromEntryDeath();

    [Tooltip("รอก่อนโหลด (วินาทีจริง) — เวลาให้อนิเมชันตาย / จอดำ")]
    [SerializeField, Min(0f)] private float delaySeconds = 2f;

    private bool handling;

    private void OnEnable() => GameEventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
    private void OnDisable() => GameEventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);

    private void OnPlayerDied(PlayerDiedEvent e)
    {
        if (handling) return;
        StartCoroutine(HandleDeath());
    }

    private IEnumerator HandleDeath()
    {
        handling = true;

        IWorldModeService world = ServiceLocator.GetOptional<IWorldModeService>();
        bool inPtsd = world != null && world.IsInPtsd;

        GameEventBus.Publish(new PlayerDeathHandlingEvent(inPtsd, delaySeconds));
        if (delaySeconds > 0f) yield return new WaitForSecondsRealtime(delaySeconds);

        IDeathPolicy policy = inPtsd ? ptsdPolicy : realWorldPolicy;
        if (policy == null)
        {
            Debug.LogWarning($"[DeathHandler] ยังไม่ได้เลือกนโยบายตาย{(inPtsd ? "ในโลก PTSD" : "ในโลกปกติ")} — ไม่ทำอะไร");
            handling = false;
            yield break;
        }

        policy.Handle(ServiceLocator.GetOptional<SaveManager>());
        // ไม่ต้องรีเซ็ต handling — นโยบายโหลดซีนใหม่ ตัวนี้ถูกทำลายพร้อมซีนเก่า
    }
}
