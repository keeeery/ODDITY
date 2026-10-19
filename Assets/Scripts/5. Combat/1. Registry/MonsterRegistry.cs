using System.Collections.Generic;
using UnityEngine;

namespace TeamOdd.Combat
{
    public static class MonsterRegistry
    {
        private static readonly Dictionary<Collider, ICombatable> registry = new();

        public static void RegisterMonster(Collider collider, ICombatable combatable)
        {
            if (registry.TryGetValue(collider, out ICombatable existing))
            {
                Debug.LogWarning($"Monster {existing} is already registered.");
                return;
            }
            registry.Add(collider, combatable);
        }

        public static void UnregisterMonster(Collider collider) => registry.Remove(collider);

        public static bool TryGetMonster(Collider collider, out ICombatable result) => registry.TryGetValue(collider, out result);
        
        public static void Clear() => registry.Clear();
    }
}