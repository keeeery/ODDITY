using System;
using System.Collections;
using TeamOdd.Arena;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TeamOdd.Boss
{
    // 투척 시작 시점(추후 보스 손 같은 지점)에 큰 염동력 오브젝트를 띄웠다가 플레이어 방향으로 던지는 패턴
    public sealed class PsychokinesisThrowPattern : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WallFragmentSpawner fragmentSpawner;
        [SerializeField] private Transform target;
        [Tooltip("오브젝트를 띄우고 던질 위치, 비어 있으면 이 오브젝트의 Transform을 사용한다.")]
        [SerializeField] private Transform throwOrigin;
        [Tooltip("투척 직후 오브젝트와 충돌을 무시할 보스의 루트, 비어 있으면 무시하지 않는다.")]
        [SerializeField] private Transform owner;

        [Header("Timing")]
        [Tooltip("투척 지점에 오브젝트를 띄워 두는 시간(초)")]
        [SerializeField, Min(0f)] private float holdDuration = 0.8f;
        [Tooltip("투척 후 패턴 종료까지의 대기 시간(초)")]
        [SerializeField, Min(0f)] private float recoveryDuration = 1f;
        [Tooltip("투척 후 보스와의 충돌을 계속 무시하는 시간(초)")]
        [SerializeField, Min(0f)] private float ownerIgnoreDuration = 0.3f;

        [Header("Hold")]
        [Tooltip("띄워 두는 동안의 회전 속도(도/초)")]
        [SerializeField, Min(0f)] private float holdSpinSpeed = 90f;

        [Header("Throw")]
        [SerializeField, Min(0.1f)] private float throwSpeed = 25f;
        [Tooltip("대상 Transform 기준 조준 높이(m)")]
        [SerializeField] private float aimHeight = 1f;
        [Tooltip("켜면 중력으로 떨어지는 만큼 위로 보정해 조준 지점에 도달하도록 던진다.")]
        [SerializeField] private bool compensateGravity = true;
        [SerializeField, Min(0f)] private float throwSpinSpeed = 3f;

        private Coroutine routine;
        private Rigidbody heldBody;
        private Collider[] ownerColliders;
        private Collider[] ignoredColliders;

        public event Action Completed;
        public bool IsRunning => routine != null;

        private Transform Origin => throwOrigin != null ? throwOrigin : transform;

        private void Awake()
        {
            ownerColliders = owner != null ? owner.GetComponentsInChildren<Collider>() : Array.Empty<Collider>();
        }

        private void OnEnable()
        {
            Spawn();
            routine = StartCoroutine(Run());
        }

        private void OnDisable()
        {
            // 띄운 채로 패턴이 끊기면 그 자리에 떨어뜨림
            if (heldBody != null) heldBody.isKinematic = false;
            heldBody = null;
            RestoreOwnerCollisions();
            routine = null;
        }

        private IEnumerator Run()
        {
            Quaternion spin = Random.rotation;

            for (float elapsed = 0f; elapsed < holdDuration; elapsed += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();

                spin = Quaternion.AngleAxis(holdSpinSpeed * Time.fixedDeltaTime, Vector3.up) * spin;
                heldBody.MovePosition(Origin.position);
                heldBody.MoveRotation(spin);
            }

            Throw();
            heldBody = null;

            float ignoreDuration = Mathf.Min(ownerIgnoreDuration, recoveryDuration);
            yield return new WaitForSeconds(ignoreDuration);
            RestoreOwnerCollisions();
            yield return new WaitForSeconds(recoveryDuration - ignoreDuration);

            routine = null;
            Completed?.Invoke();
        }

        private void Spawn()
        {
            WallFragment fragment = fragmentSpawner.SpawnFragment(FragmentSize.Large, Origin.position, Random.rotation);

            heldBody = fragment.GetComponent<Rigidbody>();
            heldBody.isKinematic = true;
            ignoredColliders = fragment.GetComponentsInChildren<Collider>();
            SetOwnerCollisionsIgnored(true);
        }

        private void Throw()
        {
            heldBody.isKinematic = false;
            Vector3 aimPoint = target.position + Vector3.up * aimHeight;
            heldBody.linearVelocity = GetThrowVelocity(heldBody.worldCenterOfMass, aimPoint);
            heldBody.angularVelocity = Random.onUnitSphere * throwSpinSpeed;
        }

        private Vector3 GetThrowVelocity(Vector3 start, Vector3 aimPoint)
        {
            Vector3 offset = aimPoint - start;

            if (compensateGravity == false) return offset.normalized * throwSpeed;

            // 직선 거리를 throwSpeed로 이동하는 시간 동안 중력에 의한 낙하량만큼 초기 속도 보정
            float flightTime = offset.magnitude / throwSpeed;
            return offset / flightTime - 0.5f * flightTime * Physics.gravity;
        }

        private void SetOwnerCollisionsIgnored(bool ignored)
        {
            for (int index = 0; index < ignoredColliders.Length; index++)
                for (int ownerIndex = 0; ownerIndex < ownerColliders.Length; ownerIndex++)
                    Physics.IgnoreCollision(ignoredColliders[index], ownerColliders[ownerIndex], ignored);
        }

        private void RestoreOwnerCollisions()
        {
            if (ignoredColliders == null) return;

            SetOwnerCollisionsIgnored(false);
            ignoredColliders = null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(Origin.position, 0.3f);
            Gizmos.DrawLine(Origin.position, target.position + Vector3.up * aimHeight);
        }
#endif
    }
}
