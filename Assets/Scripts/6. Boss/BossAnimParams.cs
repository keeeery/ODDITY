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

        // HUMANOID FLOAT
        public static readonly int VARIANT = Animator.StringToHash("variant"); // 같은 동작의 A/B 클립 중 무엇을 쓸지 (0: A, 1: B)
        public static readonly int MOVE_X = Animator.StringToHash("moveX");
        public static readonly int MOVE_Y = Animator.StringToHash("moveY");
        public static readonly int START_ANGLE = Animator.StringToHash("startAngle"); // 돌면서 출발할 각도, 0이면 정면 출발

        // HUMANOID BOOL
        public static readonly int IS_STRAFING = Animator.StringToHash("isStrafing");
        public static readonly int IS_GUARDING = Animator.StringToHash("isGuarding");
        public static readonly int USE_REVENGE_STAND_UP = Animator.StringToHash("useRevengeStandUp"); // 그로기 기상을 반격 모션으로

        // HUMANOID TRIGGER
        public static readonly int SLASH = Animator.StringToHash("slash");
        public static readonly int DIAGONAL_SLASH_A = Animator.StringToHash("diagonalSlashA");
        public static readonly int DIAGONAL_SLASH_I = Animator.StringToHash("diagonalSlashI");
        public static readonly int COMBO_3 = Animator.StringToHash("combo3");
        public static readonly int COMBO_4A = Animator.StringToHash("combo4A");
        public static readonly int COMBO_4B = Animator.StringToHash("combo4B");
        public static readonly int TURN_SLASH_LEFT = Animator.StringToHash("turnSlashLeft");
        public static readonly int TURN_SLASH_RIGHT = Animator.StringToHash("turnSlashRight");
        public static readonly int DASH_SLASH_A = Animator.StringToHash("dashSlashA");
        public static readonly int DASH_SLASH_B = Animator.StringToHash("dashSlashB");
        public static readonly int TACKLE_UPPER = Animator.StringToHash("tackleUpper");
        public static readonly int THRUST = Animator.StringToHash("thrust");
        public static readonly int JUMP_SMASH_J = Animator.StringToHash("jumpSmashJ");
        public static readonly int SPIN_SWEEP = Animator.StringToHash("spinSweep");
        public static readonly int RISE_SMASH = Animator.StringToHash("riseSmash");
        public static readonly int GUARD = Animator.StringToHash("guard");
        public static readonly int GUARD_ACCEPT = Animator.StringToHash("guardAccept");
        public static readonly int DEFLECT_SLASH = Animator.StringToHash("deflectSlash");
        public static readonly int DODGE_LEFT = Animator.StringToHash("dodgeLeft");
        public static readonly int DODGE_RIGHT = Animator.StringToHash("dodgeRight");
        public static readonly int TACKLE_COMBO = Animator.StringToHash("tackleCombo");
        public static readonly int COMBO_7 = Animator.StringToHash("combo7");
        public static readonly int HEAVY_SLASH = Animator.StringToHash("heavySlash");
        public static readonly int RISE_SPIN_SMASH = Animator.StringToHash("riseSpinSmash");
        public static readonly int PHASE_SHIFT = Animator.StringToHash("phaseShift");
        public static readonly int DIMENSION_SLASH = Animator.StringToHash("dimensionSlash");
        public static readonly int CLONE_SUMMON = Animator.StringToHash("cloneSummon");
    }
}