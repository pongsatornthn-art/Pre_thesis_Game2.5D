using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// กรอบห้อง 1 ห้อง สำหรับระบบเงาดำห้อง — "ผู้เล่น/กล้องอยู่ห้องไหน" ดูจากตัวนี้ (ไม่ใช้โซนหมอกของปอแล้ว)
///
/// ห้อง = กล่องลูกหลายกล่องรวมกัน (ห้องรูปตัว L ฯลฯ) · กล่อง = ลูก (child) ของ object นี้
///   ตำแหน่ง/ขนาดกล่อง = Position / Scale ของลูก (ไม่มี Mesh ไม่มีตัวชน — ย้าย/ยืดด้วยเครื่องมือ Move/Scale ได้เลย)
///   ไม่สนการหมุน (กล่องตั้งตรงแกนเสมอ)
/// วางทั้งหมดไว้ใต้ Map_Test/Rooms · กฎ: พื้นที่เดินได้ทุกจุดต้องอยู่ในห้องใดห้องหนึ่ง (เช็คด้วยเมนู Tools › ห้อง › เช็คช่องโหว่)
/// เห็นกรอบสีใน Scene view (เลือกห้อง = เห็นชัดขึ้น)
/// </summary>
public class RoomArea : MonoBehaviour
{
    [Tooltip("ชื่อห้องที่คนอ่าน (โชว์ใน Scene view)")]
    public string displayName;

    [Tooltip("สีกรอบใน Scene view")]
    public Color gizmoColor = new Color(0.4f, 0.8f, 1f, 1f);

    private readonly List<Bounds> boxes = new List<Bounds>();
    private bool cached;

    /// <summary>กล่องทั้งหมดของห้อง (พิกัดโลก)</summary>
    public IReadOnlyList<Bounds> Boxes
    {
        get
        {
            if (!cached || !Application.isPlaying) Rebuild();
            return boxes;
        }
    }

    /// <summary>กรอบครอบทั้งห้อง</summary>
    public Bounds TotalBounds
    {
        get
        {
            var list = Boxes;
            if (list.Count == 0) return new Bounds(transform.position, Vector3.zero);
            Bounds b = list[0];
            for (int i = 1; i < list.Count; i++) b.Encapsulate(list[i]);
            return b;
        }
    }

    /// <summary>จุดนี้ (ดูแค่ X/Z) อยู่ในห้องไหม</summary>
    public bool Contains(Vector3 p)
    {
        foreach (Bounds b in Boxes)
            if (p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z) return true;
        return false;
    }

    /// <summary>ระยะ (X/Z) จากจุดถึงห้อง · อยู่ในห้อง = 0</summary>
    public float DistanceXZ(Vector3 p)
    {
        float best = float.MaxValue;
        foreach (Bounds b in Boxes)
        {
            float dx = Mathf.Max(b.min.x - p.x, 0f, p.x - b.max.x);
            float dz = Mathf.Max(b.min.z - p.z, 0f, p.z - b.max.z);
            best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dz * dz));
        }
        return best;
    }

    public void Rebuild()
    {
        boxes.Clear();
        foreach (Transform c in transform)
            boxes.Add(new Bounds(c.position, new Vector3(Mathf.Abs(c.lossyScale.x), Mathf.Abs(c.lossyScale.y), Mathf.Abs(c.lossyScale.z))));
        cached = true;
    }

    private void OnDrawGizmos() => Draw(0.18f, 0.7f);
    private void OnDrawGizmosSelected() => Draw(0.4f, 1f);

    private void Draw(float fill, float line)
    {
        foreach (Transform c in transform)
        {
            Vector3 size = new Vector3(Mathf.Abs(c.lossyScale.x), 0.05f, Mathf.Abs(c.lossyScale.z));
            Vector3 center = new Vector3(c.position.x, 0.12f, c.position.z);
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, fill);
            Gizmos.DrawCube(center, size);
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, line);
            Gizmos.DrawWireCube(center, size);
        }
#if UNITY_EDITOR
        if (transform.childCount > 0)
            UnityEditor.Handles.Label(new Vector3(TotalBounds.center.x, 0.5f, TotalBounds.center.z), string.IsNullOrEmpty(displayName) ? name : displayName);
#endif
    }
}
