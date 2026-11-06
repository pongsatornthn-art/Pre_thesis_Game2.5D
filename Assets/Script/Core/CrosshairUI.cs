using UnityEngine;
using UnityEngine.UI;

public class CrosshairUI : MonoBehaviour
{
    [Header("Crosshair")]
    [Tooltip("ถ้าเว้นว่าง จะสร้างจุดกลางจอให้อัตโนมัติ")]
    public Image crosshairImage;
    public Sprite crosshairSprite;       // ใส่รูป crosshair ของคุณเองได้
    public float size = 8f;

    [Header("Colors")]
    public Color normalColor = new Color(1f, 1f, 1f, 0.8f);
    public Color highlightColor = Color.yellow;

    void Awake()
    {
        if (crosshairImage == null)
        {
            GameObject go = new GameObject("Crosshair", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(size, size);

            crosshairImage = go.GetComponent<Image>();
            crosshairImage.raycastTarget = false; // ไม่บังเมาส์
        }

        if (crosshairSprite != null) crosshairImage.sprite = crosshairSprite;
        crosshairImage.color = normalColor;
    }

    public void SetVisible(bool visible)
    {
        if (crosshairImage != null) crosshairImage.gameObject.SetActive(visible);
    }

    // เรียกตอนเล็งโดนของที่โต้ตอบได้ เพื่อเปลี่ยนสี
    public void SetHighlight(bool highlight)
    {
        if (crosshairImage != null)
            crosshairImage.color = highlight ? highlightColor : normalColor;
    }
}