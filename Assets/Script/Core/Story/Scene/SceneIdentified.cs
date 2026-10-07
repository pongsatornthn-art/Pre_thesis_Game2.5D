using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// คลาสแม่ของ "ของในซีนที่ฉากเนื้อเรื่องเรียกด้วยชื่อ" — มุมกล้อง / คัทซีน / มินิเกม
///
/// ทำไมต้องเรียกด้วยชื่อ: ฉาก (StorySequence) เป็นไฟล์ asset ลากของในซีนใส่ไม่ได้ (กติกา Unity · STRUCTURE_CHECKLIST B2)
/// → ของในซีนตั้ง Scene Id ไว้ คำสั่งในฉากใส่ชื่อเดียวกัน แล้วมาหากันที่ทะเบียนนี้
///
/// T = กลุ่มของทะเบียน (มุมกล้องหาเฉพาะมุมกล้อง คัทซีนหาเฉพาะคัทซีน) — คลาสลูกใส่ T เป็นคลาสแม่ของกลุ่มตัวเอง
/// ⚠️ object ที่แปะต้องเปิดอยู่ตอนเริ่มซีน (ลงทะเบียนใน Awake)
/// </summary>
public abstract class SceneIdentified<T> : MonoBehaviour where T : SceneIdentified<T>
{
    [Tooltip("ชื่อเรียก — ต้องตรงกับช่อง Id ในคำสั่งของฉากเนื้อเรื่อง (ห้ามซ้ำกันในกลุ่มเดียวกัน)")]
    [SerializeField] private string sceneId;

    private static readonly Dictionary<string, T> registry = new Dictionary<string, T>();

    public string SceneId => sceneId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => registry.Clear();

    protected virtual void Awake()
    {
        if (string.IsNullOrEmpty(sceneId))
        {
            Debug.LogWarning($"[{GetType().Name}] '{name}' ยังไม่ได้ตั้ง Scene Id — ฉากเนื้อเรื่องจะเรียกตัวนี้ไม่ได้", this);
            return;
        }

        if (registry.TryGetValue(sceneId, out T other) && other != null && other != this)
        {
            Debug.LogWarning($"[{GetType().Name}] Scene Id '{sceneId}' ซ้ำกันระหว่าง '{other.name}' กับ '{name}' — ใช้ตัวล่าสุด", this);
        }
        registry[sceneId] = (T)this;
    }

    protected virtual void OnDestroy()
    {
        if (!string.IsNullOrEmpty(sceneId) && registry.TryGetValue(sceneId, out T current) && current == this)
        {
            registry.Remove(sceneId);
        }
    }

    /// <summary>หาด้วยชื่อ — ไม่เจอคืน null (คำสั่งในฉากเป็นคนเตือน)</summary>
    public static T Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return registry.TryGetValue(id, out T found) && found != null ? found : null;
    }
}
