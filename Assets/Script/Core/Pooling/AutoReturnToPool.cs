using System.Collections;
using UnityEngine;

/// <summary>
/// คอมโพเนนต์คืนวัตถุเข้า SimplePool อัตโนมัติเมื่อครบกำหนดเวลา (เช่น VFX สะเก็ดไฟ/เลือด)
/// เพื่อให้ Particle หรือ Effect ที่ยืมจากพูลไม่ต้องเขียนโค้ดคืนพูลเองซ้ำซ้อน
/// </summary>
public class AutoReturnToPool : MonoBehaviour, IPoolable
{
    [Tooltip("ระยะเวลาคงอยู่ก่อนคืนเข้าพูล (วินาที) หากวัตถุมี ParticleSystem จะดึงค่า duration ให้อัตโนมัติถ้าตั้งค่านี้เป็น 0")]
    [SerializeField] private float lifeTime = 1f;

    private Coroutine returnCoroutine;
    private ParticleSystem cachedParticle;

    private void Awake()
    {
        cachedParticle = GetComponent<ParticleSystem>();
        if (cachedParticle != null && lifeTime <= 0f)
        {
            lifeTime = cachedParticle.main.duration;
        }
    }

    public void OnSpawn()
    {
        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
        }

        float waitTime = lifeTime;
        if (cachedParticle != null)
        {
            cachedParticle.Clear();
            cachedParticle.Play();
            if (lifeTime <= 0f) waitTime = cachedParticle.main.duration;
        }

        returnCoroutine = StartCoroutine(ReturnRoutine(waitTime));
    }

    public void OnDespawn()
    {
        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }
    }

    private IEnumerator ReturnRoutine(float delay)
    {
        // ใช้เวลาจริง ไม่ผูกกับ Time.timeScale
        // ไม่งั้นถ้าผู้เล่นเปิดสมุด/Pause ตอนเอฟเฟกต์กำลังเล่น มันจะค้างไม่คืนเข้าพูลตลอดกาล
        yield return new WaitForSecondsRealtime(delay);
        returnCoroutine = null;
        SimplePool.Return(gameObject);
    }
}
