using UnityEngine;
using Sirenix.OdinInspector;
using FantasyGarden.FSM;

namespace TeamOdd.Boss
{
    public class BossController : MonoBehaviourFSM
    {
        [field: Title("Components")]
        [field: SerializeField] public Animator Animator { get; private set; }
        [field: SerializeField] public Rigidbody Rigidbody { get; private set; }
    }
}