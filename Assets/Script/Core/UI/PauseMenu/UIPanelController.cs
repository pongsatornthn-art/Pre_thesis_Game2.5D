using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class UIPanelController : MonoBehaviour
{
    private CanvasGroup canvasGroup;
    private Coroutine currentAnim;

    [Header("Animation Settings")]
    public float fadeSpeed = 8f; // ความไวในการ Fade (ยิ่งเยอะยิ่งไว)
    public bool useScalePopup = true; // เปิด Effect เด้งดึ๋งเวลาโผล่ไหม?

    public bool IsVisible { get; private set; }

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    public void Show()
    {
        IsVisible = true;
        gameObject.SetActive(true);
        if (currentAnim != null) StopCoroutine(currentAnim);
        currentAnim = StartCoroutine(AnimateShow());
    }

    public void Hide()
    {
        // แก้บั๊ก: ถ้าก้อนนี้ถูกปิดการมองเห็นอยู่แล้ว ห้ามเรียก Coroutine เด็ดขาด
        if (!gameObject.activeInHierarchy)
        {
            HideImmediate();
            return;
        }

        IsVisible = false;
        if (currentAnim != null) StopCoroutine(currentAnim);
        currentAnim = StartCoroutine(AnimateHide());
    }

    public void HideImmediate()
    {
        IsVisible = false;
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        gameObject.SetActive(false);
    }

    private IEnumerator AnimateShow()
    {
        // เปิดให้คลิกได้
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        if (useScalePopup) transform.localScale = Vector3.one * 0.8f;
        
        // ใช้ unscaledDeltaTime เสมอ เพราะตอนเรียกฟังก์ชันนี้ Time.timeScale = 0 (เกมถูก Pause)
        while (canvasGroup.alpha < 1f || (useScalePopup && transform.localScale.x < 0.99f))
        {
            canvasGroup.alpha += Time.unscaledDeltaTime * fadeSpeed;
            if (useScalePopup)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one, Time.unscaledDeltaTime * fadeSpeed * 1.5f);
            }
            yield return null; // รอเฟรมถัดไป
        }

        canvasGroup.alpha = 1f;
        if (useScalePopup) transform.localScale = Vector3.one;
    }

    private IEnumerator AnimateHide()
    {
        // ห้ามคลิกซ้ำตอนกำลังซ่อน
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        while (canvasGroup.alpha > 0f)
        {
            canvasGroup.alpha -= Time.unscaledDeltaTime * fadeSpeed * 1.5f; // ตอนซ่อนจะไวกว่าตอนโชว์นิดนึง
            if (useScalePopup)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * 0.9f, Time.unscaledDeltaTime * fadeSpeed);
            }
            yield return null;
        }

        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }
}
