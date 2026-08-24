using System.Collections.Generic;
using UnityEngine;

public class MeleeHitbox : MonoBehaviour
{
    [Header("Hitbox Properties")]
    [Tooltip("ความกว้างของพัด (Arc Angle)")]
    [Range(0, 360)] public float arcAngle = 70f;
    public float hitRadius = 1.5f;

    public Transform playerTransform; // ลากตัว Player ตัวแม่มาใส่ตรงนี้

    private int currentDamage = 5;
    private float currentKnockback = 0f;

    private List<Collider> hitEnemies = new List<Collider>();

    // รับค่าพลังโจมตีมาจากปืน/ดาบในช่องเก็บของ
    public void SetupPayload(int damage, float knockback)
    {
        currentDamage = damage;
        currentKnockback = knockback;
    }

    public void ResetHits()
    {
        hitEnemies.Clear();
    }

    void OnEnable()
    {
        ResetHits();
    }

    void OnTriggerStay(Collider other)
    {
        IDamageable damageable = other.GetComponent<IDamageable>();

        // เช็กว่าเป็นสิ่งที่โดนตีได้ และไม่ใช่ตัวผู้เล่นตีตัวเอง
        if (damageable != null && other.gameObject != playerTransform.gameObject)
        {
            if (hitEnemies.Contains(other)) return;

            Vector3 directionToTarget = (other.transform.position - playerTransform.position).normalized;
            directionToTarget.y = 0;

            // วัดมุมการฟันดาบ โดยอ้างอิงจากทิศทางที่ก้อนนี้หันหน้าอยู่ (ซึ่งจะหันตามเมาส์ตลอดเวลา)
            Vector3 currentFacingDirection = transform.forward;
            float angleToTarget = Vector3.Angle(currentFacingDirection, directionToTarget);

            // ถ้าศัตรูอยู่ในองศาของพัด
            if (angleToTarget <= arcAngle / 2f)
            {
                damageable.TakeDamage(currentDamage, currentKnockback);
                hitEnemies.Add(other);
                Debug.Log($"<color=orange>[MeleeHitbox] ฟันโดน {other.gameObject.name} ดาเมจ {currentDamage}</color>");
            }
        }
    }

    // วาดภาพกรวยใน Scene เอาไว้ให้เดฟกะระยะมีดได้ง่ายๆ
    private void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();
        // ถ้าปิด Hitbox อยู่ ให้วาดกรวยสีเทา ถ้าเปิดอยู่ให้วาดสีแดงเดือด!
        Gizmos.color = (col != null && col.enabled) ? new Color(1, 0, 0, 0.4f) : new Color(0.5f, 0.5f, 0.5f, 0.2f);
        
        // 🌟 ดึงองศาจากตัวมันเอง เพราะ PlayerCombat จะสั่งหมุนก้อนนี้ให้หันตามเมาส์ 360 องศาแล้ว!
        Vector3 forward = transform.forward;
        Vector3 pos = transform.position;

        // วาดเส้นซ้าย-ขวา
        Vector3 rightBound = Quaternion.Euler(0, arcAngle / 2f, 0) * forward;
        Vector3 leftBound = Quaternion.Euler(0, -arcAngle / 2f, 0) * forward;

        Gizmos.DrawRay(pos, rightBound * hitRadius);
        Gizmos.DrawRay(pos, leftBound * hitRadius);

        // วาดส่วนโค้ง (ประมาณเอาแบบบ้านๆ)
        int segments = 10;
        Vector3 prevPoint = pos + leftBound * hitRadius;
        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments;
            Vector3 currentDir = Vector3.Slerp(leftBound, rightBound, t);
            Vector3 currentPoint = pos + currentDir * hitRadius;
            Gizmos.DrawLine(prevPoint, currentPoint);
            prevPoint = currentPoint;
        }
    }
}