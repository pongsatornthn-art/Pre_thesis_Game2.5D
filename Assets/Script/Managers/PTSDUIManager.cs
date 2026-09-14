using UnityEngine;
using UnityEngine.UI;

public class PTSDUIManager : MonoBehaviour
{
    public Image ptsdOverlayImage;
    public GameObject warningText;

    private void OnEnable()
    {
        PTSDManager.OnPTSDStateChanged += HandlePTSDStateChange;
    }

    private void OnDisable()
    {
        PTSDManager.OnPTSDStateChanged -= HandlePTSDStateChange;
    }

    private void HandlePTSDStateChange(bool isActive)
    {
        if (ptsdOverlayImage != null)
        {
            ptsdOverlayImage.gameObject.SetActive(isActive);
            Color c = ptsdOverlayImage.color;
            c.a = isActive ? 1f : 0f;
            ptsdOverlayImage.color = c;
        }

        if (warningText != null)
        {
            warningText.SetActive(isActive);
        }
    }
}