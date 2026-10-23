#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FantasyGarden.Utility
{
    public static class EditorUtils
    {
        private const BindingFlags FLAGS = 
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;

        public static Dictionary<string, T> GetValues<T>(this Type targetClass)
        {
            Dictionary<string, T> result = new();
            
            foreach (FieldInfo field in targetClass.GetFields(FLAGS))
            {
                if (typeof(T).IsAssignableFrom(field.FieldType) == false)
                    continue;
                
                object value = field.IsLiteral ? field.GetRawConstantValue() : field.GetValue(null);
                result[field.Name] = (T)value;
            }

            return result;
        }

        public static List<Type> GetRelatedComponents(string name)
        {
            Type baseType = TypeCache.GetTypesDerivedFrom<MonoBehaviour>()
                .FirstOrDefault(t => t.Name == name || t.FullName == name);

            if (baseType == null)
            {
                Debug.LogError("[EditorUtils] Components not found: " + name);
                return null;
            }

            return TypeCache.GetTypesDerivedFrom(baseType)
                .Where(t => t.IsAbstract == false)
                .ToList();
        }
    }
}

#endif