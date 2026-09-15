using System.Collections;
using UnityEngine;

public class StressTriggerZone : MonoBehaviour
{
    [Tooltip("ติ๊กถูกถ้าต้องการให้โซนนี้ทำงานแค่ครั้งเดียว")]
    public bool triggerOnlyOnce = false;

    [Tooltip("เวลาที่ต้องรอ (วินาที) ก่อนจะโดนโซนนี้ซ้ำได้อีกครั้ง (แนะนำ 2-3 วิ)")]
    public float cooldownTime = 2.0f;

    private bool hasTriggered = false;

    private bool isCoolingDown = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (PTSDManager.Instance != null && PTSDManager.Instance.isPTSDActive) return;
            if (isCoolingDown) return;

            if (triggerOnlyOnce && hasTriggered) return;

            if (PTSDManager.Instance != null)
            {
                PTSDManager.Instance.SetPTSDState(true);
                hasTriggered = true;

                if (triggerOnlyOnce)
                {
                    GetComponent<Collider>().enabled = false;
                }
                else
                {
                    StartCoroutine(CooldownRoutine());
                }
            }
        }
    }

    private IEnumerator CooldownRoutine()
    {
        isCoolingDown = true;

        yield return new WaitUntil(() => !PTSDManager.Instance.isPTSDActive);

        yield return new WaitForSeconds(cooldownTime);

        isCoolingDown = false;
    }
}