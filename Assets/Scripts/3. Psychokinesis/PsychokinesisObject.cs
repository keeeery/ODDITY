using System.Collections.Generic;
using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Psychokinesis
{
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PsychokinesisObject : MonoBehaviour
    {
        [SerializeField] private Material availableOutline;
        [SerializeField] private Material unavailableOutline;

        [SerializeField] private Material meshOutline;
        [SerializeField, Min(0f)] private float outlineWidth = 0.05f;

        private static readonly int outlineColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int outlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private MaterialPropertyBlock outlineProperties;
        private Mesh outlineMesh;
        private Rigidbody body;
        private BoxCollider box;
        private MeshRenderer outline;
        private bool originalGravity;
        private bool originalDetectCollisions;
        private float originalAngularDamping;
        private bool canDealThrowDamage;
        private ICombatable throwSender;

        public Rigidbody Body => body;
        public bool IsHeld { get; private set; }
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
            canDealThrowDamage = false;
            throwSender = null;
            Release(Vector3.zero);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (canDealThrowDamage == false) return;
            if (MonsterRegistry.TryGetMonster(collision.collider, out ICombatable target) == false) return;
            if (target is CombatEntity entity && (entity.IsDead || entity.isActiveAndEnabled == false)) return;

            canDealThrowDamage = false;
            DamageInfo info = new DamageInfo(Mathf.RoundToInt(body.mass), collision.GetContact(0).point, collision.collider);
            DamageEvent damageEvent = new DamageEvent(throwSender, target, info);
            CombatManager.Instance.AddCombatEvent(damageEvent);
            throwSender = null;
        }

        private void CreateOutline()
        {
            GameObject outlineObject = new GameObject("Focus Outline");
            outlineObject.transform.SetParent(transform, false);
            Mesh source = GetComponent<MeshFilter>().sharedMesh;
            outlineMesh = Instantiate(source);
            outlineMesh.name = source.name + " Outline";
            Vector3[] vertices = outlineMesh.vertices;
            Vector3[] normals = outlineMesh.normals;
            Dictionary<Vector3, Vector3> smoothNormals = new Dictionary<Vector3, Vector3>();
            // UV와 하드 에지 때문에 분리된 동일 위치 정점도 같은 방향으로 확장한다.
            for (int index = 0; index < vertices.Length; index++)
            {
                smoothNormals.TryGetValue(vertices[index], out Vector3 sum);
                smoothNormals[vertices[index]] = sum + normals[index];
            }
            for (int index = 0; index < vertices.Length; index++)
                normals[index] = smoothNormals[vertices[index]].normalized;
            outlineMesh.normals = normals;
            Bounds bounds = outlineMesh.bounds;
            bounds.Expand(outlineWidth * 2f);
            outlineMesh.bounds = bounds;

            outlineObject.AddComponent<MeshFilter>().sharedMesh = outlineMesh;
            outline = outlineObject.AddComponent<MeshRenderer>();
            Material[] materials = new Material[outlineMesh.subMeshCount];
            for (int index = 0; index < materials.Length; index++) materials[index] = meshOutline;
            outline.sharedMaterials = materials;
            outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outline.receiveShadows = false;
            outlineProperties = new MaterialPropertyBlock();
            outline.enabled = false;
        }

        public void SetFocused(bool isFocused, bool available)
        {
            outline.enabled = isFocused;

            if (isFocused == false) return;

            Material colorMaterial = available ? availableOutline : unavailableOutline;
            outlineProperties.SetColor(outlineColorId, colorMaterial.GetColor(outlineColorId));
            outlineProperties.SetFloat(outlineWidthId, outlineWidth);
            outline.SetPropertyBlock(outlineProperties);
        }

        private void OnDestroy()
        {
            Destroy(outlineMesh);
        }

        public void BeginHold()
        {
            canDealThrowDamage = false;
            throwSender = null;
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

        public void Release(Vector3 velocity, bool isThrowing = false, ICombatable sender = null)
        {
            if (IsHeld == false) return;

            IsHeld = false;
            canDealThrowDamage = isThrowing;
            throwSender = isThrowing ? sender : null;
            body.useGravity = originalGravity;
            body.detectCollisions = originalDetectCollisions;
            body.angularDamping = originalAngularDamping;
            body.linearVelocity = velocity;
            body.angularVelocity = Vector3.zero;
            SetFocused(false, true);
        }
    }
}
