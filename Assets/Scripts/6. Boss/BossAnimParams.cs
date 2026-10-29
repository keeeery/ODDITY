using UnityEngine;

namespace TeamOdd.Boss
{
    public static class BossAnimParams
    {
        // FLOAT
        public static readonly int DISTANCE_TO_TARGET = Animator.StringToHash("distanceToTarget");
        public static readonly int TURN_DIRECTION = Animator.StringToHash("turnDirection");
        public static readonly int CHARGE_RUN_SPEED = Animator.StringToHash("chargeRunSpeed");
        public static readonly int GROGGY_TIME = Animator.StringToHash("groggyTime");

        // BOOL
        public static readonly int IS_CHASING = Animator.StringToHash("isChasing");
        public static readonly int IS_TURNING = Animator.StringToHash("isTurning");
        public static readonly int IS_CHARGING = Animator.StringToHash("isCharging");
        public static readonly int IS_STUNNED = Animator.StringToHash("isStunned");
        public static readonly int IS_GROGGY = Animator.StringToHash("isGroggy");

        // STATE
        public static readonly int GROGGY_STATE = Animator.StringToHash("Groggy");
        
        // TRIGGER
        public static readonly int LEFT_CLAW = Animator.StringToHash("leftClaw");
        public static readonly int LEFT_CLAW_FORWARD = Animator.StringToHash("leftClawForward");
        public static readonly int RIGHT_CLAW = Animator.StringToHash("rightClaw");
        public static readonly int RIGHT_CLAW_FORWARD = Animator.StringToHash("rightClawForward");
        public static readonly int SMASH = Animator.StringToHash("smash");
        public static readonly int SMASH_FORWARD = Animator.StringToHash("smashForward");
        public static readonly int TWO_COMBO = Animator.StringToHash("2Combo");
        public static readonly int TWO_COMBO_FORWARD = Animator.StringToHash("2ComboForward");
        public static readonly int THREE_COMBO = Animator.StringToHash("3Combo");
        public static readonly int THREE_COMBO_FORWARD = Animator.StringToHash("3ComboForward");
        public static readonly int LEFT_TURN_SMASH = Animator.StringToHash("leftTurnSmash");
        public static readonly int RIGHT_TURN_SMASH = Animator.StringToHash("rightTurnSmash");
        
        public static readonly int HOP_BACK = Animator.StringToHash("hopBack");

        public static readonly int ROAR = Animator.StringToHash("roar");
        public static readonly int THROW = Animator.StringToHash("throw");
        public static readonly int SHOCKWAVE = Animator.StringToHash("shockwave");
        public static readonly int JUMP_SMASH = Animator.StringToHash("jumpSmash");
        public static readonly int CHARGE_STUN = Animator.StringToHash("chargeStun");
    }
}