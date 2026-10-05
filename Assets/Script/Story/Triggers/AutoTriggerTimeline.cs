using UnityEngine;
using UnityEngine.Playables;
using System.Collections; // 🌟 ต้องมีเพื่อใช้ Coroutine หน่วงเวลา

public class AutoTriggerTimeline : MonoBehaviour
{
    [Header("คัตซีน Timeline")]
    public PlayableDirector timelineToPlay;

    [Header("จุดเกิดใหม่ (เมื่อโดนผีจับ)")]
    public Transform respawnPoint;

    private Collider triggerCol;
    private bool hasTriggeredOnce = false;

    private void Start()
    {
        triggerCol = GetComponent<Collider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        // ถ้าผู้เล่นเดินชน และกล่องเปิดอยู่ และยังไม่เคยถูกทริกเกอร์ในรอบนี้
        if (other.CompareTag("Player") && triggerCol.enabled && !hasTriggeredOnce)
        {
            hasTriggeredOnce = true; // ล็อกไว้ชั่วคราว
            if (timelineToPlay != null) timelineToPlay.Play();
            triggerCol.enabled = false; // ปิดกล่องกันเดินชนซ้ำซ้อน
        }
    }

    public void ResetTrigger()
    {
        if (timelineToPlay != null)
        {
            timelineToPlay.Stop();
            timelineToPlay.time = 0;
            timelineToPlay.Evaluate();
        }

        // 🌟 สั่งหน่วงเวลา 0.7 วินาที ก่อนจะปลดล็อกและเปิดกล่องใหม่
        StartCoroutine(DelayedResetAndEnable());
    }

    private IEnumerator DelayedResetAndEnable()
    {
        yield return new WaitForSeconds(0.7f); // รอให้ผู้เล่นไปเกิดใหม่ที่จุด Respawn ให้เรียบร้อยก่อน

        hasTriggeredOnce = false; // 🌟 ปลดล็อกให้กลับมาเล่นคัตซีนซ้ำได้เมื่อเดินชนรอบใหม่
        if (triggerCol != null)
        {
            triggerCol.enabled = true; // เปิดกล่องให้ทำงานอีกครั้ง
        }
    }
}