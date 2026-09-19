using System.Collections;
using UnityEngine;

public class StressTriggerZone : MonoBehaviour
{
    public bool triggerOnlyOnce = true;
    public float cooldownTime = 2.0f;

    private bool hasTriggered = false;
    private bool isCoolingDown = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (PTSDManager.Instance != null && PTSDManager.Instance.currentMode != PTSDMode.None) return;
            if (isCoolingDown) return;
            if (triggerOnlyOnce && hasTriggered) return;

            if (PTSDManager.Instance != null)
            {
                // 🌟 บังคับเข้าโหมด PTSD ทันทีที่เดินชน
                PTSDManager.Instance.TriggerPTSDSurvival();
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
        yield return new WaitUntil(() => PTSDManager.Instance.currentMode == PTSDMode.None);
        yield return new WaitForSeconds(cooldownTime);
        isCoolingDown = false;
    }
}