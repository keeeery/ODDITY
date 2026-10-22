using UnityEngine;

namespace TeamOdd.Boss
{
    public static class BossAnimParams
    {
        // FLOAT
        public static readonly int DISTANCE_TO_TARGET = Animator.StringToHash("distanceToTarget");
        
        // BOOL
        public static readonly int IS_CHASING = Animator.StringToHash("isChasing");
        
        // TRIGGER
        public static readonly int LEFT_CLAW_ATTACK = Animator.StringToHash("leftClawAttack");
    }
}