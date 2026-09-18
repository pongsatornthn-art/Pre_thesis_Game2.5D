using UnityEngine;
using Unity.Cinemachine;

public class PTSDCameraShake : MonoBehaviour
{
    public CinemachineCamera virtualCamera;

    [Header("Shake Settings")]
    [Tooltip("ความกว้างของการสั่น (ไม่ควรเกิน 3 เพื่อไม่ให้กล้องหลุดผู้เล่น)")]
    public float maxAmplitude = 2.5f;
    [Tooltip("ความถี่/ความรัวของการสั่น (ยิ่งเยอะ ยิ่งสั่นระริก)")]
    public float maxFrequency = 10f;

    private CinemachineBasicMultiChannelPerlin noiseProfile;

    private void Start()
    {
        if (virtualCamera != null)
        {
            noiseProfile = virtualCamera.GetComponent<CinemachineBasicMultiChannelPerlin>();
        }
    }

    private void OnEnable()
    {
        PTSDManager.OnPTSDStateChanged += HandleCameraShake;
    }

    private void OnDisable()
    {
        PTSDManager.OnPTSDStateChanged -= HandleCameraShake;
    }

    private void HandleCameraShake(bool isPTSDActive)
    {
        if (noiseProfile != null)
        {
            if (isPTSDActive)
            {
                noiseProfile.AmplitudeGain = maxAmplitude;
                noiseProfile.FrequencyGain = maxFrequency;
            }
            else
            {
                noiseProfile.AmplitudeGain = 0f;
                noiseProfile.FrequencyGain = 0f;
            }
        }
    }
}