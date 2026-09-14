using UnityEngine;

public class StressTriggerZone : MonoBehaviour
{
    [Tooltip("ติ๊กถูกถ้าต้องการให้โซนนี้ทำงานแค่ครั้งเดียว (เดินชนซ้ำไม่เกิดอะไรขึ้น)")]
    public bool triggerOnlyOnce = true;
    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (triggerOnlyOnce && hasTriggered) return;

            if (PTSDManager.Instance != null)
            {
                PTSDManager.Instance.SetPTSDState(true);
                hasTriggered = true;
            }
        }
    }
}