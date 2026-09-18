using UnityEngine;
using UnityEngine.UI;

public class PTSDUIManager : MonoBehaviour
{
    public Image ptsdOverlayImage;
    public GameObject warningText;

    private void OnEnable()
    {
        PTSDManager.OnPTSDStateChanged += HandleStressLevelChange;
    }

    private void OnDisable()
    {
        PTSDManager.OnPTSDStateChanged -= HandleStressLevelChange;
    }

    private void HandleStressLevelChange(bool isPTSDActive)
    {
        if (ptsdOverlayImage != null)
        {
            ptsdOverlayImage.gameObject.SetActive(isPTSDActive);

            Color c = ptsdOverlayImage.color;
            // ถ้าติดสถานะ ให้มืดเต็มที่ (alpha = 1), ไม่ติดสถานะ = โปร่งใส (alpha = 0)
            c.a = isPTSDActive ? 1f : 0f;
            ptsdOverlayImage.color = c;
        }

        if (warningText != null)
        {
            warningText.SetActive(isPTSDActive);
        }
    }
}