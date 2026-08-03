using System.Collections.Generic;
using UnityEngine;

public class MeleeHitbox : MonoBehaviour
{
    [Header("Hitbox Properties")]
    public int attackDamage = 10;

    [Tooltip("ความกว้างของพัด (Arc Angle)")]
    [Range(0, 360)] public float arcAngle = 70f;

    public Transform playerTransform; // ลากตัว Player ตัวแม่มาใส่ตรงนี้

    private List<Collider> hitEnemies = new List<Collider>();

    void OnEnable()
    {
        hitEnemies.Clear();
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

            // วัดมุมการฟันดาบ
            Vector3 currentFacingDirection = transform.parent.forward;
            float angleToTarget = Vector3.Angle(currentFacingDirection, directionToTarget);

            // ถ้าศัตรูอยู่ในองศาของพัด
            if (angleToTarget <= arcAngle / 2f)
            {
                damageable.TakeDamage(attackDamage);
                hitEnemies.Add(other);
            }
        }
    }
}