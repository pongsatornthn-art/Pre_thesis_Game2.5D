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

    public static void Unregister<T>()
    {
        var type = typeof(T);
        if (services.ContainsKey(type))
        {
            services.Remove(type);
        }
    }
}
