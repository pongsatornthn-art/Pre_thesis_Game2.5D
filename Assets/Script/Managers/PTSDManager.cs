using UnityEngine;
using UnityEngine.UI;

public class PTSDManager : MonoBehaviour
{
    [Header("Visual FX")]
    public Image vignetteImage;
    public float pulseSpeed = 2f;
    public float minAlpha = 0.3f;
    public float maxAlpha = 0.8f;

    [Header("Camera Shake FX")]
    public Camera playerCamera;
    public float shakeAmount = 0.05f;
    private Vector3 originalCamPos;

    [Header("Audio FX")]
    public AudioSource heartbeatSFX;
    public AudioSource tinnitusSFX;

    [Header("Enemy Spawning (ระบบเสกจาก Prefab)")]
    public GameObject stalkerPrefab;
    public Transform spawnPoint;

    private GameObject spawnedStalker;
    private bool isPtsdActive = false;

    void Start()
    {
        if (vignetteImage != null) vignetteImage.gameObject.SetActive(false);
        if (playerCamera != null) originalCamPos = playerCamera.transform.localPosition;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            TogglePTSDMode();
        }

        if (isPtsdActive)
        {
            HandleVisualEffects();
        }
    }

    void TogglePTSDMode()
    {
        isPtsdActive = !isPtsdActive;

        if (isPtsdActive)
        {
            Debug.Log("<color=red>เข้าสู่โหมด PTSD: เสก Stalker ออกมา!</color>");

            if (vignetteImage != null) vignetteImage.gameObject.SetActive(true);
            if (heartbeatSFX != null) heartbeatSFX.Play();
            if (tinnitusSFX != null) tinnitusSFX.Play();
            if (playerCamera != null) originalCamPos = playerCamera.transform.localPosition;

            if (stalkerPrefab != null)
            {
                if (spawnPoint != null)
                {
                    spawnedStalker = Instantiate(stalkerPrefab, spawnPoint.position, spawnPoint.rotation);
                }
                else
                {
                    spawnedStalker = Instantiate(stalkerPrefab, transform.position, Quaternion.identity);
                }
            }
        }
        else
        {
            Debug.Log("<color=green>ออกจากโหมด PTSD: ลบ Stalker ทิ้ง</color>");

            if (vignetteImage != null) vignetteImage.gameObject.SetActive(false);
            if (heartbeatSFX != null) heartbeatSFX.Stop();
            if (tinnitusSFX != null) tinnitusSFX.Stop();
            if (playerCamera != null) playerCamera.transform.localPosition = originalCamPos;

            if (spawnedStalker != null)
            {
                Destroy(spawnedStalker);
            }
        }
    }

    void HandleVisualEffects()
    {
        if (vignetteImage != null)
        {
            Color c = vignetteImage.color;
            c.a = Mathf.Lerp(minAlpha, maxAlpha, Mathf.PingPong(Time.time * pulseSpeed, 1f));
            vignetteImage.color = c;
        }

        if (playerCamera != null)
        {
            Vector3 shakePos = originalCamPos + (Random.insideUnitSphere * shakeAmount);
            shakePos.z = originalCamPos.z;
            playerCamera.transform.localPosition = shakePos;
        }
    }
}