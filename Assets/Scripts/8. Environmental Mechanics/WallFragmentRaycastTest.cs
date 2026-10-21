using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TeamOdd.Arena
{
    [DefaultExecutionOrder(150)]
    public sealed class WallFragmentRaycastTest : MonoBehaviour
    {
        [SerializeField] private Camera aimCamera;
        [SerializeField] private WallFragmentSpawner fragmentSpawner;
        [SerializeField, Min(1f)] private float rayDistance = 100f;

        private readonly RaycastHit[] hits = new RaycastHit[128];
        private int wallLayer;

        private void Awake()
        {
            wallLayer = LayerMask.NameToLayer("Wall");
        }

        private void LateUpdate()
        {
            if (Cursor.lockState != CursorLockMode.Locked || SimpleInput.GetKeyDown(Key.Q) == false) return;

            Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            int count = Physics.RaycastNonAlloc(ray, hits, rayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float distance = float.PositiveInfinity;
            int nearest = -1;

            for (int index = 0; index < count; index++)
            {
                if (hits[index].collider.transform.IsChildOf(transform) || hits[index].distance >= distance) continue;
                distance = hits[index].distance;
                nearest = index;
            }

            if (nearest < 0) return;

            RaycastHit hit = hits[nearest];
            Debug.DrawLine(ray.origin, hit.point, Color.yellow, 1f);

            if (hit.collider.gameObject.layer == wallLayer) fragmentSpawner.Spawn(hit.point, hit.normal);
        }
    }
}
