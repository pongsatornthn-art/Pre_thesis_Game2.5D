using UnityEngine;
using Unity.AI.Navigation;

/// <summary>
/// สถานะของห้อง Safe Zone
/// </summary>
public enum SafeZoneState
{
    Dormant,    // ห้องธรรมดา ยังไม่ถูกสุ่มเป็น Safe Zone
    Safe,       // ห้องปลอดภัย เล่นมินิเกมได้ และบอสเข้าไม่ได้
    Unsafe      // ห้องไม่ปลอดภัย บอสเข้าได้ และต้องรอนับถอยหลังคูลดาวน์หมดจึงจะเล่นมินิเกมได้
}

/// <summary>
/// จุด Safe Zone ประจำห้อง (1 ตัวต่อ 1 ห้อง)
/// ทำหน้าที่เก็บสถานะของห้อง นับเวลาคูลดาวน์ และเปิด-ปิดทางเข้าของบอส (NavMeshLink)
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class SafeZone : MonoBehaviour
{
    [Header("การแสดงผล")]
    [Tooltip("คีย์แปลภาษาชื่อห้อง (เว้นว่างได้)")]
    [SerializeField] private string displayNameKey;

    [Tooltip("จุดที่ลูกศร HUD ชี้ (ถ้าเว้นว่างจะใช้ตำแหน่งของห้องนี้)")]
    [SerializeField] private Transform hudAnchor;

    [Header("ทางเข้าของบอส")]
    [Tooltip("ทางเข้าห้องสำหรับบอส (NavMeshLink) — Safe/Dormant จะปิด, Unsafe จะเปิดให้บอสเข้า")]
    [SerializeField] private NavMeshLink[] stalkerEntrances;

    private BoxCollider boxCollider;

    /// <summary>สถานะปัจจุบันของห้อง</summary>
    public SafeZoneState State { get; private set; } = SafeZoneState.Dormant;

    /// <summary>เวลาคูลดาวน์ที่เหลือก่อนเล่นมินิเกมได้อีกครั้ง (วินาที)</summary>
    public float CooldownRemaining { get; private set; } = 0f;

    /// <summary>ผู้เล่นอยู่ในห้องนี้หรือไม่</summary>
    public bool IsPlayerInside { get; private set; } = false;

    /// <summary>สามารถเริ่มเล่นมินิเกมที่ห้องนี้ได้หรือไม่</summary>
    public bool CanPlayMinigame => State != SafeZoneState.Dormant && CooldownRemaining <= 0f;

    /// <summary>ตำแหน่งเป้าหมายสำหรับให้ UI / ลูกศรชี้</summary>
    public Vector3 AnchorPosition => hudAnchor != null ? hudAnchor.position : transform.position;

    /// <summary>คีย์แปลภาษาชื่อห้อง</summary>
    public string DisplayNameKey => displayNameKey;

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider>();
        if (!boxCollider.isTrigger)
        {
            boxCollider.isTrigger = true;
            Debug.LogWarning($"[{gameObject.name}] BoxCollider ไม่ได้ติ๊ก Is Trigger — ระบบได้ปรับเป็น Trigger ให้อัตโนมัติ", this);
        }

        if (stalkerEntrances == null || stalkerEntrances.Length == 0)
        {
            Debug.LogWarning($"[{gameObject.name}] ยังไม่มี Stalker Entrances (NavMeshLink) — บอสจะไม่สามารถเดินเข้าห้องนี้ได้ตอนเกิดเหตุ", this);
        }

        ApplyStalkerEntrances(false);
    }

    private void Update()
    {
        if (CooldownRemaining > 0f)
        {
            CooldownRemaining -= Time.deltaTime;
            if (CooldownRemaining <= 0f)
            {
                CooldownRemaining = 0f;
            }
        }
    }

    /// <summary>
    /// กำหนดสถานะของห้อง พร้อมเปิด/ปิดทางเดินของบอส
    /// </summary>
    public void SetState(SafeZoneState newState)
    {
        State = newState;
        bool bossCanEnter = (newState == SafeZoneState.Unsafe);
        ApplyStalkerEntrances(bossCanEnter);
    }

    /// <summary>
    /// เริ่มนับถอยหลังคูลดาวน์ห้องนี้
    /// </summary>
    public void StartCooldown(float seconds)
    {
        CooldownRemaining = Mathf.Max(0f, seconds);
    }

    /// <summary>
    /// รีเซ็ตสถานะห้องกลับเป็นค่าตั้งต้น (Dormant, คูลดาวน์เป็น 0, ทางเข้าบอสปิด)
    /// </summary>
    public void ResetZone()
    {
        State = SafeZoneState.Dormant;
        CooldownRemaining = 0f;
        ApplyStalkerEntrances(false);
    }

    private void ApplyStalkerEntrances(bool enableLinks)
    {
        if (stalkerEntrances == null) return;
        for (int i = 0; i < stalkerEntrances.Length; i++)
        {
            if (stalkerEntrances[i] != null)
            {
                stalkerEntrances[i].enabled = enableLinks;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            IsPlayerInside = true;
            GameEventBus.Publish(new SafeZonePlayerChangedEvent(this, true));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            IsPlayerInside = false;
            GameEventBus.Publish(new SafeZonePlayerChangedEvent(this, false));
        }
    }

    private void OnDrawGizmos()
    {
        Color color = State switch
        {
            SafeZoneState.Safe => new Color(0f, 1f, 0f, 0.25f),
            SafeZoneState.Unsafe => new Color(1f, 0f, 0f, 0.25f),
            _ => new Color(0.5f, 0.5f, 0.5f, 0.2f)
        };

        Gizmos.color = color;
        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null)
        {
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(col.center, col.size);
            Gizmos.color = new Color(color.r, color.g, color.b, 0.8f);
            Gizmos.DrawWireCube(col.center, col.size);
            Gizmos.matrix = oldMatrix;
        }

        if (hudAnchor != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(hudAnchor.position, 0.2f);
        }
    }
}
