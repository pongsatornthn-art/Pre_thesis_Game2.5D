using UnityEngine;
using System.Collections;

public class MuzzleFlashEffect : MonoBehaviour
{
    public Light flashLight;
    public float flashDuration = 0.05f;

    void Awake()
    {
        if (flashLight == null) flashLight = GetComponent<Light>();
        if (flashLight != null) flashLight.enabled = false;
    }

    public void PlayFlash()
    {
        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(FlashRoutine());
        }
    }

    private IEnumerator FlashRoutine()
    {
        if (flashLight != null)
        {
            flashLight.enabled = true;
            yield return new WaitForSeconds(flashDuration);
            flashLight.enabled = false;
        }
    }
}