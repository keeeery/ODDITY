using System.Collections.Generic;
using UnityEngine;

namespace TeamOdd.Arena
{
    // 연출용 작은 파편이 바닥에 멈춘 뒤 일정 시간이 지나면 풀로 귀환시킴
    [RequireComponent(typeof(WallFragment))]
    public sealed class WallFragmentDebris : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float stableDuration = 1f;
        [SerializeField, Min(0f)] private float despawnDelay = 3f;
        [SerializeField, Min(0f)] private float despawnDelayJitter = 1f;
        [SerializeField, Min(0f)] private float linearSpeedThreshold = 0.15f;
        [SerializeField, Min(0f)] private float angularSpeedThreshold = 0.3f;
        [SerializeField, Min(0.01f)] private float shrinkDuration = 0.4f;

        private Rigidbody body;
        private WallFragment fragment;
        private readonly HashSet<Collider> supports = new HashSet<Collider>();
        private float stableTime;
        private float currentDespawnDelay;
        private bool isDespawning;
        private float despawnTime;
        private Vector3 startScale;
        private Vector3 startPosition;
        private Vector3 shrinkPivot;
        private Renderer meshRenderer;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            fragment = GetComponent<WallFragment>();
            meshRenderer = GetComponent<Renderer>();

            // 바닥을 제외한 다른 오브젝트와는 충돌 무시
            body.excludeLayers = ~LayerMask.GetMask("Ground");
        }

        private void OnEnable()
        {
            ResetState();
            currentDespawnDelay = Mathf.Max(0f, despawnDelay + Random.Range(-despawnDelayJitter, despawnDelayJitter));
        }

        private void OnDisable()
        {
            ResetState();
        }

        private void ResetState()
        {
            supports.Clear();
            stableTime = 0f;
            isDespawning = false;
            body.isKinematic = false;
        }

        private void FixedUpdate()
        {
            if (isDespawning)
            {
                UpdateShrink();
                return;
            }

            if (supports.Count == 0 ||
                body.linearVelocity.sqrMagnitude > linearSpeedThreshold * linearSpeedThreshold ||
                body.angularVelocity.sqrMagnitude > angularSpeedThreshold * angularSpeedThreshold)
            {
                stableTime = 0f;
                return;
            }

            stableTime += Time.fixedDeltaTime;

            if (stableTime >= stableDuration + currentDespawnDelay) BeginDespawn();
        }

        // 소멸이 시작되면 물리를 멈추고 작아지며 사라지는 효과
        private void BeginDespawn()
        {
            isDespawning = true;
            despawnTime = 0f;
            startScale = transform.localScale;
            startPosition = transform.position;
            // 중앙을 기준으로 스케일을 줄이면 어색하여 바닥에 닿은 아래쪽 중심을 기준으로 줄어들게 함
            Bounds bounds = meshRenderer.bounds;
            shrinkPivot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            body.isKinematic = true;
        }

        private void UpdateShrink()
        {
            despawnTime += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(despawnTime / shrinkDuration);
            float scale = 1f - t * t;
            transform.localScale = startScale * scale;
            transform.position = shrinkPivot + (startPosition - shrinkPivot) * scale;

            if (t >= 1f) fragment.ReturnToPool();
        }

        private void OnCollisionEnter(Collision collision)
        {
            UpdateSupport(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            UpdateSupport(collision);
        }

        private void UpdateSupport(Collision collision)
        {
            for (int index = 0; index < collision.contactCount; index++)
            {
                if (collision.GetContact(index).normal.y < 0.5f) continue;
                supports.Add(collision.collider);
                return;
            }
            supports.Remove(collision.collider);
        }

        private void OnCollisionExit(Collision collision)
        {
            supports.Remove(collision.collider);
        }
    }
}
