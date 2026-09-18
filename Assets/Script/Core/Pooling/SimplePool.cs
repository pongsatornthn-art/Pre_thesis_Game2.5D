using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ระบบ Object Pooling ส่วนกลางของโปรเจกต์
/// 
/// ทำไมต้องมีพูล:
/// กระสุนปืนและเอฟเฟกต์ถูกสร้างและทำลายบ่อยครั้ง หากใช้ Instantiate และ Destroy ทุกนัด
/// จะสร้างขยะในหน่วยความจำ (Garbage Collection) ทำให้เกมกระตุกเป็นช่วงๆ (Frame Drop)
/// การนำวัตถุเดิมกลับมาใช้ซ้ำช่วยให้ประสิทธิภาพลื่นไหลตลอดเวลา
/// </summary>
public static class SimplePool
{
    private static readonly Dictionary<GameObject, Queue<GameObject>> pools = new Dictionary<GameObject, Queue<GameObject>>();
    private static readonly Dictionary<GameObject, GameObject> instanceToPrefab = new Dictionary<GameObject, GameObject>();
    private static Transform poolRoot;

    private static void EnsureRoot()
    {
        if (poolRoot == null)
        {
            GameObject root = new GameObject("[SimplePool_Root]");
            Object.DontDestroyOnLoad(root);
            poolRoot = root.transform;
        }
    }

    /// <summary>
    /// สร้างวัตถุเตรียมไว้ล่วงหน้าเพื่อความลื่นไหล
    /// </summary>
    public static void Prewarm(GameObject prefab, int count, Transform parent = null)
    {
        if (prefab == null || count <= 0) return;
        EnsureRoot();

        if (!pools.TryGetValue(prefab, out Queue<GameObject> queue))
        {
            queue = new Queue<GameObject>();
            pools[prefab] = queue;
        }

        Transform targetParent = parent != null ? parent : poolRoot;

        for (int i = 0; i < count; i++)
        {
            GameObject obj = Object.Instantiate(prefab, targetParent);
            obj.SetActive(false);
            instanceToPrefab[obj] = prefab;
            queue.Enqueue(obj);
        }
    }

    /// <summary>
    /// ยืมวัตถุออกจากพูล
    /// </summary>
    public static GameObject Get(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent = null)
    {
        if (prefab == null) return null;
        EnsureRoot();

        if (!pools.TryGetValue(prefab, out Queue<GameObject> queue))
        {
            queue = new Queue<GameObject>();
            pools[prefab] = queue;
        }

        GameObject obj = null;
        while (queue.Count > 0)
        {
            GameObject candidate = queue.Dequeue();
            if (candidate != null)
            {
                obj = candidate;
                break;
            }
        }

        if (obj == null)
        {
            // ถ้าในพูลหมด ให้ Instantiate ตัวใหม่เพิ่ม (ไม่คืนค่า null เด็ดขาด)
            Transform targetParent = parent != null ? parent : poolRoot;
            obj = Object.Instantiate(prefab, targetParent);
            instanceToPrefab[obj] = prefab;
        }

        if (parent != null) obj.transform.SetParent(parent);
        obj.transform.SetPositionAndRotation(pos, rot);
        obj.SetActive(true);

        IPoolable poolable = obj.GetComponent<IPoolable>();
        poolable?.OnSpawn();

        return obj;
    }

    /// <summary>
    /// ยืม Component ออกจากพูล (Generic สะดวกใช้งาน)
    /// </summary>
    public static T Get<T>(T prefab, Vector3 pos, Quaternion rot, Transform parent = null) where T : Component
    {
        if (prefab == null) return null;
        GameObject go = Get(prefab.gameObject, pos, rot, parent);
        return go != null ? go.GetComponent<T>() : null;
    }

    /// <summary>
    /// ส่งคืนวัตถุกลับเข้าพูล
    /// </summary>
    public static void Return(GameObject instance)
    {
        if (instance == null) return;

        IPoolable poolable = instance.GetComponent<IPoolable>();
        poolable?.OnDespawn();

        instance.SetActive(false);
        EnsureRoot();
        instance.transform.SetParent(poolRoot);

        if (instanceToPrefab.TryGetValue(instance, out GameObject prefab) && prefab != null)
        {
            if (pools.TryGetValue(prefab, out Queue<GameObject> queue))
            {
                queue.Enqueue(instance);
                return;
            }
        }

        // กรณีไม่พบที่มาของ prefab ให้ทำลายทิ้งเพื่อไม่ให้หน่วยความจำรั่วไหล
        Object.Destroy(instance);
    }

    /// <summary>
    /// ส่งคืน Component กลับเข้าพูล
    /// </summary>
    public static void Return<T>(T instance) where T : Component
    {
        if (instance != null) Return(instance.gameObject);
    }
}
