using UnityEngine;

namespace TeamOdd.Psychokinesis
{
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class PsychokinesisObject : MonoBehaviour
    {
        [SerializeField] private Material availableOutline;
        [SerializeField] private Material unavailableOutline;

        private Rigidbody body;
        private BoxCollider box;
        private LineRenderer outline;
        private bool originalGravity;
        private bool originalDetectCollisions;
        private float originalAngularDamping;

        public Rigidbody Body => body;
        public bool IsHeld { get; private set; }
        public Vector3 FocusPosition => transform.TransformPoint(box.center);
        public Vector3 HalfExtents => Vector3.Scale(box.size * 0.5f, new Vector3(
            Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y), Mathf.Abs(transform.lossyScale.z)));
        public float CollisionRadius => HalfExtents.magnitude;
        public bool IsAvailable(float maxMass) => isActiveAndEnabled && Body.isKinematic == false && Body.mass <= maxMass;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            box = GetComponent<BoxCollider>();
            CreateOutline();
        }

        private void OnDisable()
        {
            if (IsHeld) Release(Vector3.zero);
        }

        public void Configure(Material available, Material unavailable)
        {
            availableOutline = available;
            unavailableOutline = unavailable;
        }

        private void CreateOutline()
        {
            GameObject outlineObject = new GameObject("Focus Outline");
            outlineObject.transform.SetParent(transform, false);
            outline = outlineObject.AddComponent<LineRenderer>();
            outline.useWorldSpace = false;
            outline.widthMultiplier = 0.025f;
            outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outline.receiveShadows = false;
            outline.positionCount = 16;
            Vector3 min = box.center - box.size * 0.5f - Vector3.one * 0.015f;
            Vector3 max = box.center + box.size * 0.5f + Vector3.one * 0.015f;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, max.y, min.z), new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z),
                new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z),
                new Vector3(min.x, max.y, max.z), new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, max.z), new Vector3(min.x, max.y, min.z)
            };
            outline.SetPositions(corners);
            outline.enabled = false;
        }

        public void SetFocused(bool isFocused, bool available)
        {
            outline.enabled = isFocused;

            if (isFocused) outline.sharedMaterial = available ? availableOutline : unavailableOutline;
        }

        public void BeginHold()
        {
            originalGravity = body.useGravity;
            originalDetectCollisions = body.detectCollisions;
            originalAngularDamping = body.angularDamping;
            body.useGravity = false;
            body.detectCollisions = false;
            body.angularDamping = 6f;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            IsHeld = true;
        }

        public void Release(Vector3 velocity)
        {
            if (IsHeld == false) return;

            IsHeld = false;
            body.useGravity = originalGravity;
            body.detectCollisions = originalDetectCollisions;
            body.angularDamping = originalAngularDamping;
            body.linearVelocity = velocity;
            body.angularVelocity = Vector3.zero;
            SetFocused(false, true);
        }
    }
}
