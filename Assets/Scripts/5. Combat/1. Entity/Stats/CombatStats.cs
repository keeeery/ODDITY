using UnityEngine;
using Sirenix.OdinInspector;

namespace TeamOdd.Combat
{
    [CreateAssetMenu(fileName = "Stats", menuName = "Scriptable/Combat/Stats", order = 0)]
    public class CombatStats : ScriptableObject
    {
        [field: Title("Settings")]
        [field: SerializeField] public int MaxHp { get; private set; }
        [field: SerializeField] public int Atk { get; private set; }
        [field: SerializeField] public int Def { get; private set; }
    }
}