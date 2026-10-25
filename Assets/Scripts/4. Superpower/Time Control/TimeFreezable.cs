using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace TeamOdd.TimeControl
{
    /// <summary>
    /// 시간 정지 스킬에 멈출 수 있는 오브젝트(보스, 염동력 오브젝트, 파편 등)에 붙여서 사용, 플레이어에는 붙이지 않음
    /// Time.timeScale은 건드리지 않고, 이 컴포넌트가 붙은 오브젝트만 골라서 멈춘다.
    /// - 멈추면 Animator 속도를 0으로, Rigidbody는 속도를 기억해 둔 뒤 kinematic으로, 대상의 DOTween은 일시정지
    /// - 풀리면 기억해 둔 값으로 되돌림
    /// - LocalTime은 멈춰 있는 동안 흐르지 않는 시계, 쿨타임·타이머를 Time.time 대신 이 값으로 재면 정지 시간만큼 자연스럽게 밀림
    /// 정지는 개별 정지(Freeze/Unfreeze)와 전체 정지(FreezeWorld/UnfreezeWorld) 두 갈래이고, 둘 다 횟수로 세므로 겹쳐 걸어도 안전
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TimeFreezable : MonoBehaviour
    {
        [Title("Targets")]
        [Tooltip("비워 두면 자식까지 모두 찾아 쓴다.")]
        [SerializeField] private Animator[] animators;
        [Tooltip("비워 두면 자식까지 모두 찾아 쓴다.")]
        [SerializeField] private Rigidbody[] bodies;

        private struct BodyState
        {
            public bool IsKinematic;
            public Vector3 LinearVelocity;
            public Vector3 AngularVelocity;
        }

        private static readonly List<TimeFreezable> enabledFreezables = new();
        private static int worldFreezeCount;

        private float[] animatorSpeeds;
        private BodyState[] bodyStates;

        private int freezeCount;
        private bool isWorldExempt;
        private bool clearsExemptOnWorldResume; // 전체 정지가 끝나면 예외를 풀지

        private bool isApplied;      // 지금 실제로 멈춰 있는지
        private float pausedDuration; // 지금까지 멈춰 있던 시간의 합
        private float pauseStartTime;

        public static bool IsWorldFrozen => worldFreezeCount > 0;

        // 전체 정지가 시작되거나(true) 끝날 때(false). 화면 효과 등을 붙일 때 사용
        public static event Action<bool> WorldFrozenChanged;

        // 이 오브젝트가 멈추거나(true) 풀릴 때(false). 이펙트·사운드 등을 붙일 때 사용
        public event Action<bool> FrozenChanged;

        [ShowInInspector, ReadOnly] public bool IsFrozen => isApplied;

        // 멈춰 있는 동안 흐르지 않는 시계
        public float LocalTime => (isApplied ? pauseStartTime : Time.time) - pausedDuration;
        public float LocalDeltaTime => isApplied ? 0f : Time.deltaTime;
        public float LocalFixedDeltaTime => isApplied ? 0f : Time.fixedDeltaTime;

        private bool ShouldFreeze => freezeCount > 0 || (worldFreezeCount > 0 && isWorldExempt == false);

        private void Awake()
        {
            if (animators == null || animators.Length == 0) animators = GetComponentsInChildren<Animator>(true);
            if (bodies == null || bodies.Length == 0) bodies = GetComponentsInChildren<Rigidbody>(true);

            animatorSpeeds = new float[animators.Length];
            bodyStates = new BodyState[bodies.Length];
        }

        // 전체 정지 중에 새로 켜지는 오브젝트(플레이어 투척에 부서진 파편 등)는 생성 직후 속도를 받아야 하므로 정지가 끝날 때까지 멈추지 않음
        // 정지 중에 꺼져 있어 예외를 못 풀었던 오브젝트는 여기서 정리
        private void OnEnable()
        {
            enabledFreezables.Add(this);

            if (IsWorldFrozen)
            {
                isWorldExempt = true;
                clearsExemptOnWorldResume = true;
            }
            else if (clearsExemptOnWorldResume)
            {
                isWorldExempt = false;
                clearsExemptOnWorldResume = false;
            }

            Refresh();
        }

        // 풀로 돌아가는 등 꺼질 때 멈춘 채로 남지 않도록 되돌림
        private void OnDisable()
        {
            enabledFreezables.Remove(this);
            if (isApplied) Apply(false);
        }

        #region PUBLIC_METHODS

        // 프리팹에 붙여 두지 않은 오브젝트(보스, 풀에서 꺼낸 파편 등)도 쓸 수 있도록 없으면 붙여서 돌려줌
        public static TimeFreezable GetOrAdd(GameObject target)
        {
            return target.TryGetComponent(out TimeFreezable freezable) ? freezable : target.AddComponent<TimeFreezable>();
        }

        // 이 오브젝트만 멈춤. Unfreeze와 짝을 맞춰 부른다
        public void Freeze()
        {
            freezeCount++;
            Refresh();
        }

        public void Unfreeze()
        {
            freezeCount = Mathf.Max(0, freezeCount - 1);
            Refresh();
        }

        // 켜 두면 전체 정지에 멈추지 않는다. 플레이어가 들고 있는 염동력 오브젝트처럼 정지 중에도 움직여야 할 때 쓴다
        public void SetWorldExempt(bool exempt)
        {
            isWorldExempt = exempt;
            clearsExemptOnWorldResume = false;
            Refresh();
        }

        // 전체 정지 중이면 정지가 끝날 때까지 예외를 유지하고, 아니면 바로 푼다
        // 정지 중에 플레이어가 던진 물체가 손을 떠나자마자 멈추지 않고 날아가도록 할 때 쓴다
        public void ClearWorldExemptOnResume()
        {
            if (IsWorldFrozen) clearsExemptOnWorldResume = true;
            else SetWorldExempt(false);
        }

        // 멈춰서 kinematic이 된 몸체라도 원래 kinematic이었는지를 돌려줌
        public bool IsKinematicIgnoringFreeze(Rigidbody body)
        {
            if (isApplied == false) return body.isKinematic;

            int index = Array.IndexOf(bodies, body);
            return index >= 0 ? bodyStates[index].IsKinematic : body.isKinematic;
        }

        // 풀릴 때 멈추기 전 속도로 되돌리지 않고 멈춘 자리에서 정지한 채 다시 시작하게 함
        // 멈춘 동안 다른 곳으로 옮겨 놓은 경우(염동력으로 들었다 내려찍기 등) 원래 속도가 이어지면 어색하므로 풀기 전에 부른다
        public void DiscardSavedMotion()
        {
            for (int index = 0; index < bodyStates.Length; index++)
            {
                bodyStates[index].LinearVelocity = Vector3.zero;
                bodyStates[index].AngularVelocity = Vector3.zero;
            }
        }

        // 코루틴에서 WaitForSeconds 대신 쓰면 멈춰 있는 동안은 기다리는 시간도 흐르지 않는다
        public IEnumerator Wait(float seconds)
        {
            float endTime = LocalTime + seconds;
            while (LocalTime < endTime) yield return null;
        }

        // 플레이어를 제외한 모든 TimeFreezable을 멈춤. UnfreezeWorld와 짝을 맞춰 부른다
        public static void FreezeWorld()
        {
            worldFreezeCount++;
            RefreshAll();

            if (worldFreezeCount == 1) WorldFrozenChanged?.Invoke(true);
        }

        public static void UnfreezeWorld()
        {
            if (worldFreezeCount == 0) return;

            worldFreezeCount--;
            if (worldFreezeCount > 0) return;

            foreach (TimeFreezable freezable in enabledFreezables)
            {
                if (freezable.clearsExemptOnWorldResume == false) continue;

                freezable.isWorldExempt = false;
                freezable.clearsExemptOnWorldResume = false;
            }

            RefreshAll();
            WorldFrozenChanged?.Invoke(false);
        }

        #endregion

        // 도메인 리로드 없이 플레이 모드에 들어가도 이전 플레이의 정지 상태가 남지 않도록 초기화
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            enabledFreezables.Clear();
            worldFreezeCount = 0;
            WorldFrozenChanged = null;
        }

        private static void RefreshAll()
        {
            // Refresh 중 이벤트로 목록이 바뀔 수 있으므로 거꾸로 순회
            for (int index = enabledFreezables.Count - 1; index >= 0; index--)
                enabledFreezables[index].Refresh();
        }

        private void Refresh()
        {
            if (isActiveAndEnabled == false) return;

            bool shouldFreeze = ShouldFreeze;
            if (shouldFreeze != isApplied) Apply(shouldFreeze);
        }

        private void Apply(bool frozen)
        {
            if (frozen)
            {
                pauseStartTime = Time.time;
                FreezeAnimators();
                FreezeBodies();
            }
            else
            {
                pausedDuration += Time.time - pauseStartTime;
                RestoreAnimators();
                RestoreBodies();
            }

            isApplied = frozen;
            FrozenChanged?.Invoke(frozen);
        }

        private void FreezeAnimators()
        {
            for (int index = 0; index < animators.Length; index++)
            {
                if (animators[index] == null) continue;

                animatorSpeeds[index] = animators[index].speed;
                animators[index].speed = 0f;
            }
        }

        private void RestoreAnimators()
        {
            for (int index = 0; index < animators.Length; index++)
                if (animators[index] != null) animators[index].speed = animatorSpeeds[index];
        }

        // 원래 kinematic이던 몸체도 다른 코드(DOTween 등)가 움직이지 못하도록 트윈은 함께 멈춤
        private void FreezeBodies()
        {
            for (int index = 0; index < bodies.Length; index++)
            {
                Rigidbody body = bodies[index];
                if (body == null) continue;

                bodyStates[index] = new BodyState
                {
                    IsKinematic = body.isKinematic,
                    LinearVelocity = body.isKinematic ? Vector3.zero : body.linearVelocity,
                    AngularVelocity = body.isKinematic ? Vector3.zero : body.angularVelocity,
                };

                body.isKinematic = true;
                DOTween.Pause(body);
                DOTween.Pause(body.transform);
            }
        }

        private void RestoreBodies()
        {
            for (int index = 0; index < bodies.Length; index++)
            {
                Rigidbody body = bodies[index];
                if (body == null) continue;

                BodyState state = bodyStates[index];
                body.isKinematic = state.IsKinematic;

                if (state.IsKinematic == false)
                {
                    body.linearVelocity = state.LinearVelocity;
                    body.angularVelocity = state.AngularVelocity;
                }

                DOTween.Play(body);
                DOTween.Play(body.transform);
            }
        }
    }
}
