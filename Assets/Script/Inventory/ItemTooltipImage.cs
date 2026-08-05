using UnityEngine;
using UnityEngine.UI;

public class ItemTooltipImage : MonoBehaviour
{
    public static ItemTooltipImage Instance { get; private set; }

    public Image targetImageDisplay;

    void Awake()
    {
        Instance = this;
        ClearDescriptionImage();
    }

    public void ShowDescriptionImage(Sprite newSprite)
    {
        if (newSprite == null)
        {
            ClearDescriptionImage();
            return;
        }
        targetImageDisplay.sprite = newSprite;
        targetImageDisplay.enabled = true;
    }

    public void ClearDescriptionImage()
    {
        targetImageDisplay.sprite = null;
        targetImageDisplay.enabled = false;
    }
}