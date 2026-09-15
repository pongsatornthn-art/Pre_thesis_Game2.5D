using UnityEngine;
using Unity.Cinemachine;

public class PTSDCameraShake : MonoBehaviour
{
    [Header("Cinemachine")]
    public CinemachineCamera vCam;
    private CinemachineBasicMultiChannelPerlin noiseProfile;

    [Header("Shake Settings (ปรับให้น้อยจะได้ไม่น่ารำคาญ)")]
    public float maxAmplitude = 0.5f;
    public float maxFrequency = 1.5f;

    private void Awake()
    {
        if (vCam == null) vCam = GetComponent<CinemachineCamera>();

        if (vCam != null)
        {
            noiseProfile = vCam.GetComponent<CinemachineBasicMultiChannelPerlin>();
        }
    }

    private void Start()
    {
        HandleCameraShake(false);
    }

    private void OnEnable()
    {
        PTSDManager.OnPTSDStateChanged += HandleCameraShake;
    }

    private void OnDisable()
    {
        PTSDManager.OnPTSDStateChanged -= HandleCameraShake;
    }
    private void HandleCameraShake(bool isActive)
    {
        if (noiseProfile == null) return;

        noiseProfile.AmplitudeGain = isActive ? maxAmplitude : 0f;
        noiseProfile.FrequencyGain = isActive ? maxFrequency : 0f;
    }
}