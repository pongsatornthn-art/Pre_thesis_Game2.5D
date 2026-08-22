using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// สคริปต์นี้เอาไปแปะที่ปุ่ม (Button) ไหนก็ได้ในเกม
/// จะทำให้ปุ่มนั้นขยายตัวเวลามีเมาส์ชี้ และส่งเสียงแบบ AAA ทันที
/// </summary>
public class UIButtonFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    [Header("Hover Animation")]
    public float scaleMultiplier = 1.1f; // เอาเมาส์ชี้แล้วปุ่มขยาย 10%
    public float transitionSpeed = 15f; // ความไวในการขยาย



    private Vector3 originalScale;
    private Vector3 targetScale;

    private void Start()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;
    }

    private void Update()
    {
        // อัปเดตสเกลตลอดเวลาแบบนุ่มนวล โดยใช้ unscaledDeltaTime (เผื่อเกมหยุดอยู่ก็ยังขยับได้)
        if (transform.localScale != targetScale)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * transitionSpeed);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        targetScale = originalScale * scaleMultiplier; // สั่งให้เป้าหมายขยายขึ้น
        
        ServiceLocator.Get<IAudioService>()?.PlayMenuSound(MenuSoundType.Hover);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = originalScale; // กลับมาขนาดเดิม
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        targetScale = originalScale * (scaleMultiplier * 0.95f); // ตอนกดคลิกให้ปุ่มยุบลงไปนิดนึงให้มีแรงต้าน (Feedback)
        
        ServiceLocator.Get<IAudioService>()?.PlayMenuSound(MenuSoundType.Click);
    }
}
