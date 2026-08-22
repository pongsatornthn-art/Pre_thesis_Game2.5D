using System;
using System.Collections.Generic;

public static class GameEventBus
{
    private static readonly Dictionary<Type, Delegate> events = new Dictionary<Type, Delegate>();

    public static void Subscribe<T>(Action<T> listener)
    {
        Type eventType = typeof(T);
        if (!events.ContainsKey(eventType))
        {
            events[eventType] = null;
        }
        events[eventType] = (Action<T>)events[eventType] + listener;
    }

    public static void Unsubscribe<T>(Action<T> listener)
    {
        Type eventType = typeof(T);
        if (events.ContainsKey(eventType))
        {
            events[eventType] = (Action<T>)events[eventType] - listener;
        }
    }

    public static void Publish<T>(T eventArgs)
    {
        Type eventType = typeof(T);
        if (events.TryGetValue(eventType, out Delegate d) && d != null)
        {
            ((Action<T>)d).Invoke(eventArgs);
        }
    }
}
