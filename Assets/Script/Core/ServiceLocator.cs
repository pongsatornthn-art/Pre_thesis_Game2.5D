using System;
using System.Collections.Generic;
using UnityEngine;

public static class ServiceLocator
{
    private static readonly Dictionary<Type, object> services = new Dictionary<Type, object>();

    public static void Register<T>(T service)
    {
        var type = typeof(T);
        if (services.ContainsKey(type))
        {
            Debug.LogWarning($"ServiceLocator: Service {type} is already registered. Overwriting.");
            services[type] = service;
        }
        else
        {
            services.Add(type, service);
        }
    }

    public static T Get<T>()
    {
        var type = typeof(T);
        if (services.TryGetValue(type, out var service))
        {
            return (T)service;
        }
        Debug.LogError($"ServiceLocator: Service {type} not found!");
        return default;
    }

    /// <summary>
    /// เหมือน Get แต่ไม่มีก็คืน null เงียบๆ — ใช้กับบริการที่ "ไม่มีก็ได้"
    /// (ซีนทดสอบที่ไม่มี [JOURNAL] / ซีนแมพที่ไม่มี [CORE_SERVICES]) ไม่ให้ Console แดงทั้งที่เกมไม่ได้พัง
    /// ผู้เรียกต้องเช็ค null เอง และเตือนเองถ้าบริการนั้นสำคัญ
    /// </summary>
    public static T GetOptional<T>() where T : class
    {
        return services.TryGetValue(typeof(T), out var found) ? (T)found : null;
    }

    /// <summary>ดึงบริการแบบ "ไม่มีก็ได้" — ไม่พิมพ์ error ลง Console (ใช้กับบริการที่ไม่บังคับ เช่น UI ที่รออาร์ต)</summary>
    public static bool TryGet<T>(out T service)
    {
        if (services.TryGetValue(typeof(T), out var found))
        {
            service = (T)found;
            return true;
        }
        service = default;
        return false;
    }

    public static void Unregister<T>()
    {
        var type = typeof(T);
        if (services.ContainsKey(type))
        {
            services.Remove(type);
        }
    }
}
