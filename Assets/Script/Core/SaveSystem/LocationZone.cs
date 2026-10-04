using UnityEngine;

/// <summary>
/// พื้นที่ในแมพ — เดินเข้าแล้วจำว่า "ตอนนี้อยู่ห้องไหน" ไว้โชว์ในช่องเซฟ (เช่น "ห้องสมุด")
/// วางเป็นกล่อง Trigger คลุมห้อง · ไม่วางก็ได้ (ช่องเซฟจะโชว์ชื่อจากจุดเซฟแทน)
/// </summary>
[RequireComponent(typeof(Collider))]
public class LocationZone : MonoBehaviour
{
    [Tooltip("key แปลภาษา ชื่อห้อง เช่น LOC_LIBRARY")]
    [SerializeField] private string locationKey;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !string.IsNullOrEmpty(locationKey)) SaveManager.CurrentLocationKey = locationKey;
    }
}
