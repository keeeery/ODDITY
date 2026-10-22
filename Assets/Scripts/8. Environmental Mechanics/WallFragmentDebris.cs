using System.Collections.Generic;
using UnityEngine;

namespace TeamOdd.Arena
{
    public sealed class WallFragmentDebris : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float stableDuration = 1f;
        [SerializeField, Min(0f)] private float despawnDelay = 3f;
        [SerializeField, Min(0f)] private float linearSpeedThreshold = 0.15f;
        [SerializeField, Min(0f)] private float angularSpeedThreshold = 0.3f;

        private Rigidbody body;
        private readonly HashSet<Collider> supports = new HashSet<Collider>();
        private float stableTime;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            if (supports.Count == 0 || body.linearVelocity.sqrMagnitude > linearSpeedThreshold * linearSpeedThreshold
                || body.angularVelocity.sqrMagnitude > angularSpeedThreshold * angularSpeedThreshold)
            {
                stableTime = 0f;
                return;
            }

            stableTime += Time.fixedDeltaTime;
            if (stableTime >= stableDuration + despawnDelay) Destroy(gameObject);
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
