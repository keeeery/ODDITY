using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    /// 전체 정지에 배율(timeScale)을 주면 멈추지 않고 그 배율로 느리게 흐름(감속). 정지는 배율 0인 감속과 같음
    /// - 감속 중에는 Animator 속도와 DOTween에 배율을 곱하고, Rigidbody는 속도에 배율을, 중력에 배율의 제곱을 곱해 같은 궤적을 느리게 그림
    /// - LocalTime도 배율만큼 느리게 흐름
    /// 멈춰 있는 동안 메시 위에 파란 홀로그램 막이 덮이도록 렌더러 목록과 막의 진하기를 들고 있고, 그리는 일은 TimeFreezeOverlayFeature가 맡음
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TimeFreezable : MonoBehaviour
    {
        [Title("Targets")]
        [Tooltip("비워 두면 자식까지 모두 찾아 쓴다.")]
        [SerializeField] private Animator[] animators;
        [Tooltip("비워 두면 자식까지 모두 찾아 쓴다.")]
        [SerializeField] private Rigidbody[] bodies;
        [Tooltip("시간 정지 막을 씌울 렌더러, 비워 두면 자식의 MeshRenderer와 SkinnedMeshRenderer를 모두 찾아 쓴다.")]
        [SerializeField] private Renderer[] overlayRenderers;

        [Title("Overlay")]
        [Tooltip("멈출 때 막이 발밑에서 머리끝까지 덮이는 데 걸리는 시간(초)")]
        [SerializeField, Min(0.01f)] private float overlayCoverDuration = 0.3f;
        [Tooltip("풀릴 때 막이 머리끝에서 발밑까지 걷히는 데 걸리는 시간(초)")]
        [SerializeField, Min(0.01f)] private float overlayUncoverDuration = 0.4f;
        [Tooltip("멈추는 순간 막에 글리치가 일었다가 잦아드는 시간(초), 정지가 이어지는 동안은 잔잔하게 둔다.")]
        [SerializeField, Min(0.01f)] private float overlayGlitchDuration = 0.35f;

        private struct BodyState
        {
            public bool IsKinematic;
            public Vector3 LinearVelocity;
            public Vector3 AngularVelocity;
        }

        private static readonly List<TimeFreezable> enabledFreezables = new();
        private static readonly List<TimeFreezable> overlayTargets = new();
        private static readonly List<float> worldTimeScales = new(); // 걸려 있는 전체 정지들의 배율, 0이면 정지

        private float[] animatorSpeeds;
        private BodyState[] bodyStates;
        private MeshFilter[] overlayMeshFilters; // MeshRenderer의 메시, 풀에서 꺼낼 때 메시가 바뀔 수 있어 그릴 때마다 읽음
        private float overlayProgress;   // 시간에 따라 일정하게 진행하는 값, 보이는 높이는 여기에 이징을 걸어서 씀
        private bool isOverlayCovering;  // 덮이는 중(true)인지 걷히는 중(false)인지

        private int freezeCount;
        private bool isWorldExempt;
        private bool clearsExemptOnWorldResume; // 전체 정지가 끝나면 예외를 풀지

        private float appliedTimeScale = 1f; // 지금 실제로 적용된 배율, 0이면 멈춰 있음
        private float pausedDuration; // 지금까지 멈추거나 느려져서 밀린 시간의 합
        private float pauseStartTime; // 지금 배율이 적용되기 시작한 시각

        // 감속도 전체 정지의 한 종류라 감속 중에도 true
        public static bool IsWorldFrozen => worldTimeScales.Count > 0;

        // 여러 전체 정지가 겹치면 가장 느린 배율을 따름
        private static float WorldTimeScale => worldTimeScales.Min();

        // 시간 정지 막이 조금이라도 덮여 있는 오브젝트, TimeFreezeOverlayFeature가 매 프레임 읽어 그림
        public static IReadOnlyList<TimeFreezable> OverlayTargets => overlayTargets;

        // 전체 정지가 시작되거나(true) 끝날 때(false). 화면 효과 등을 붙일 때 사용
        public static event Action<bool> WorldFrozenChanged;

        // 이 오브젝트가 멈추거나(true) 풀릴 때(false). 이펙트·사운드 등을 붙일 때 사용
        public event Action<bool> FrozenChanged;

        [ShowInInspector, ReadOnly] public bool IsFrozen => appliedTimeScale == 0f;
        [ShowInInspector, ReadOnly] public float TimeScale => appliedTimeScale;

        // 멈춰 있는 동안 흐르지 않고, 느려져 있는 동안 배율만큼 느리게 흐르는 시계
        public float LocalTime => Time.time - pausedDuration - (Time.time - pauseStartTime) * (1f - appliedTimeScale);
        public float LocalDeltaTime => Time.deltaTime * appliedTimeScale;
        public float LocalFixedDeltaTime => Time.fixedDeltaTime * appliedTimeScale;

        // 시간 정지 막이 덮인 정도(0~1), 0이면 없음, 1이면 오브젝트 전체를 덮음
        // 멈추면 아래에서 위로 덮이며 늘고, 풀리면 위에서 아래로 걷히며 줄어듦
        // 덮일 때는 빠르게 올라오다 머리끝에서, 걷힐 때는 빠르게 내려가다 발밑에서 감속(cubic ease-out)
        public float OverlayProgress => isOverlayCovering ? 1f - Cube(1f - overlayProgress) : Cube(overlayProgress);
        public IReadOnlyList<Renderer> OverlayRenderers => overlayRenderers;

        // 막의 글리치 세기(0~1), 멈춘 순간에만 1에서 줄어듦
        public float OverlayGlitch
        {
            get
            {
                if (IsFrozen == false) return 0f;

                float remain = 1f - Mathf.Clamp01((Time.time - pauseStartTime) / overlayGlitchDuration);
                return remain * remain;
            }
        }

        // 개별 정지가 우선이고, 전체 정지는 예외가 아닐 때만 받음
        private float TargetTimeScale => freezeCount > 0 ? 0f
            : worldTimeScales.Count > 0 && isWorldExempt == false ? WorldTimeScale : 1f;

        private void Awake()
        {
            if (animators == null || animators.Length == 0) animators = GetComponentsInChildren<Animator>(true);
            if (bodies == null || bodies.Length == 0) bodies = GetComponentsInChildren<Rigidbody>(true);

            if (overlayRenderers == null || overlayRenderers.Length == 0)
                overlayRenderers = Array.FindAll(GetComponentsInChildren<Renderer>(true), item => item is MeshRenderer or SkinnedMeshRenderer);

            animatorSpeeds = new float[animators.Length];
            bodyStates = new BodyState[bodies.Length];
            overlayMeshFilters = Array.ConvertAll(overlayRenderers, item => item != null ? item.GetComponent<MeshFilter>() : null);
        }

        // 정지 중에도 막이 덮이고 걷혀야 하므로 LocalDeltaTime이 아닌 Time.deltaTime으로 진행
        private void Update()
        {
            // 덮이다 말고 걷히는 등 방향이 바뀌면, 보이는 높이가 튀지 않도록 새 곡선에서 같은 높이가 나오는 지점부터 이어감
            if (isOverlayCovering != IsFrozen)
            {
                float shown = OverlayProgress;
                isOverlayCovering = IsFrozen;
                overlayProgress = IsFrozen ? 1f - CubeRoot(1f - shown) : CubeRoot(shown);
            }

            float target = IsFrozen ? 1f : 0f;
            if (Mathf.Approximately(overlayProgress, target)) return;

            float duration = IsFrozen ? overlayCoverDuration : overlayUncoverDuration;
            SetOverlayProgress(Mathf.MoveTowards(overlayProgress, target, Time.deltaTime / duration));
        }

        // 감속 중에는 속도가 배율만큼 줄어 있으므로, 같은 궤적을 느리게 그리도록 중력도 배율의 제곱만큼만 받게 함
        private void FixedUpdate()
        {
            if (IsFrozen || appliedTimeScale >= 1f) return;

            Vector3 extraGravity = Physics.gravity * (appliedTimeScale * appliedTimeScale - 1f);
            foreach (Rigidbody body in bodies)
            {
                if (body == null || body.isKinematic || body.useGravity == false) continue;
                body.AddForce(extraGravity, ForceMode.Acceleration);
            }
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
            if (appliedTimeScale != 1f) Apply(1f);
            SetOverlayProgress(0f);
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

        // index번째 막 렌더러의 서브메시 수, 메시가 없으면 0
        public int GetOverlaySubMeshCount(int index)
        {
            Renderer target = overlayRenderers[index];
            Mesh mesh = target is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                : overlayMeshFilters[index] != null ? overlayMeshFilters[index].sharedMesh : null;
            return mesh != null ? mesh.subMeshCount : 0;
        }

        // 멈춰서 kinematic이 된 몸체라도 원래 kinematic이었는지를 돌려줌
        public bool IsKinematicIgnoringFreeze(Rigidbody body)
        {
            if (IsFrozen == false) return body.isKinematic;

            int index = Array.IndexOf(bodies, body);
            return index >= 0 ? bodyStates[index].IsKinematic : body.isKinematic;
        }

        // 멈춘 동안 kinematic으로 고정한 몸체가 풀린 뒤에도 kinematic으로 남게 함(쓰러져 충돌을 없앤 보스 등)
        public void KeepKinematicAfterUnfreeze(Rigidbody body)
        {
            if (IsFrozen == false) return;

            int index = Array.IndexOf(bodies, body);
            if (index < 0) return;

            bodyStates[index].IsKinematic = true;
            bodyStates[index].LinearVelocity = Vector3.zero;
            bodyStates[index].AngularVelocity = Vector3.zero;
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

        // 플레이어를 제외한 모든 TimeFreezable을 멈춤. 같은 timeScale로 UnfreezeWorld와 짝을 맞춰 부른다
        // timeScale을 0보다 크게 주면 멈추지 않고 그 배율로 느리게 흐름(0.5면 절반 빠르기)
        public static void FreezeWorld(float timeScale = 0f)
        {
            worldTimeScales.Add(timeScale);
            RefreshAll();

            if (worldTimeScales.Count == 1) WorldFrozenChanged?.Invoke(true);
        }

        public static void UnfreezeWorld(float timeScale = 0f)
        {
            if (worldTimeScales.Remove(timeScale) == false) return;

            // 다른 전체 정지가 남아 있으면 남은 것 중 가장 느린 배율로 맞춤
            if (worldTimeScales.Count > 0)
            {
                RefreshAll();
                return;
            }

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
            overlayTargets.Clear();
            worldTimeScales.Clear();
            WorldFrozenChanged = null;
        }

        private static float Cube(float value) => value * value * value;
        private static float CubeRoot(float value) => Mathf.Pow(Mathf.Max(0f, value), 1f / 3f);

        // 막이 덮이기 시작하면 그릴 목록에 넣고, 다 걷히면 뺌
        private void SetOverlayProgress(float progress)
        {
            bool wasVisible = overlayProgress > 0f;
            overlayProgress = progress;

            if (wasVisible == false && progress > 0f) overlayTargets.Add(this);
            else if (wasVisible && progress <= 0f) overlayTargets.Remove(this);
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

            float timeScale = TargetTimeScale;
            if (timeScale != appliedTimeScale) Apply(timeScale);
        }

        // timeScale이 1보다 작으면 그 배율로 느리게(0이면 멈춤), 1이면 원래대로 되돌림
        private void Apply(float timeScale)
        {
            // 지금 배율로 흐르는 동안 밀린 시간을 쌓아 두고 새 배율로 다시 잼
            pausedDuration += (Time.time - pauseStartTime) * (1f - appliedTimeScale);
            pauseStartTime = Time.time;

            bool wasFrozen = IsFrozen;
            if (timeScale < 1f)
            {
                FreezeAnimators(timeScale);
                FreezeBodies(timeScale);
            }
            else
            {
                RestoreAnimators();
                RestoreBodies();
            }

            appliedTimeScale = timeScale;
            if (wasFrozen != IsFrozen) FrozenChanged?.Invoke(IsFrozen);
        }

        // 평소 빠르기일 때의 속도를 기억해 두고 거기에 배율을 곱함
        private void FreezeAnimators(float timeScale)
        {
            for (int index = 0; index < animators.Length; index++)
            {
                if (animators[index] == null) continue;

                if (appliedTimeScale == 1f) animatorSpeeds[index] = animators[index].speed;
                animators[index].speed = animatorSpeeds[index] * timeScale;
            }
        }

        private void RestoreAnimators()
        {
            for (int index = 0; index < animators.Length; index++)
                if (animators[index] != null) animators[index].speed = animatorSpeeds[index];
        }

        // 원래 kinematic이던 몸체도 다른 코드(DOTween 등)가 움직이지 못하도록 트윈은 함께 멈춤
        // 감속이면 kinematic으로 바꾸지 않고 평소 빠르기의 속도와 트윈에 배율을 곱함
        private void FreezeBodies(float timeScale)
        {
            for (int index = 0; index < bodies.Length; index++)
            {
                Rigidbody body = bodies[index];
                if (body == null) continue;

                // 지금 속도를 평소 빠르기로 되돌려 기억, 이미 멈춰 있으면 멈출 때 기억한 값을 그대로 씀
                if (IsFrozen == false)
                {
                    bodyStates[index] = new BodyState
                    {
                        IsKinematic = body.isKinematic,
                        LinearVelocity = body.isKinematic ? Vector3.zero : body.linearVelocity / appliedTimeScale,
                        AngularVelocity = body.isKinematic ? Vector3.zero : body.angularVelocity / appliedTimeScale,
                    };
                }

                if (timeScale == 0f)
                {
                    body.isKinematic = true;
                    DOTween.Pause(body);
                    DOTween.Pause(body.transform);
                    continue;
                }

                BodyState state = bodyStates[index];
                if (IsFrozen)
                {
                    body.isKinematic = state.IsKinematic;
                    DOTween.Play(body);
                    DOTween.Play(body.transform);
                }

                if (state.IsKinematic == false)
                {
                    body.linearVelocity = state.LinearVelocity * timeScale;
                    body.angularVelocity = state.AngularVelocity * timeScale;
                }

                ScaleTweens(body, timeScale);
                ScaleTweens(body.transform, timeScale);
            }
        }

        private void RestoreBodies()
        {
            for (int index = 0; index < bodies.Length; index++)
            {
                Rigidbody body = bodies[index];
                if (body == null) continue;

                if (IsFrozen)
                {
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
                else if (body.isKinematic == false)
                {
                    // 감속에서 풀리면 지금 속도를 배율로 나눠 평소 빠르기로 되돌림
                    body.linearVelocity /= appliedTimeScale;
                    body.angularVelocity /= appliedTimeScale;
                }

                ScaleTweens(body, 1f);
                ScaleTweens(body.transform, 1f);
            }
        }

        private static void ScaleTweens(object target, float timeScale)
        {
            List<Tween> tweens = DOTween.TweensByTarget(target);
            if (tweens == null) return;

            foreach (Tween tween in tweens)
                tween.timeScale = timeScale;
        }
    }
}
