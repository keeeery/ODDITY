using UnityEngine;

namespace TeamOdd.Arena
{
    // 벽 파편의 풀 항목 (큰/중간/작은 파편 공통)
    // - 크기별 공유 풀에서 꺼낸 뒤 ApplyVariant로 원본 Prefab의 형태를 입혀 사용
    // - 큰/중간 파편은 PsychokinesisObject가 파괴될 때, 작은 파편은 WallFragmentDebris가 멈춘 뒤 귀환시킨다.
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class WallFragment : MonoBehaviour, IObjectPoolItem
    {
        [SerializeField] private string poolKey;

        private Rigidbody body;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private BoxCollider hitBox;
        private bool isInPool;

        public string Key { get => poolKey; set => poolKey = value; }
        public GameObject GameObject => gameObject;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            hitBox = GetComponent<BoxCollider>();
        }

        public void ApplyVariant(Component prefab)
        {
            meshFilter.sharedMesh = prefab.GetComponent<MeshFilter>().sharedMesh;
            meshRenderer.sharedMaterial = prefab.GetComponent<MeshRenderer>().sharedMaterial;
            BoxCollider sourceCollider = prefab.GetComponent<BoxCollider>();
            hitBox.center = sourceCollider.center;
            hitBox.size = sourceCollider.size;
            body.mass = prefab.GetComponent<Rigidbody>().mass;
            transform.localScale = prefab.transform.localScale;
            body.ResetCenterOfMass();
            body.ResetInertiaTensor();
        }

        private void OnEnable()
        {
            isInPool = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
        }

        public void ReturnToPool()
        {
            if (isInPool) return;

            // 풀을 거치지 않고 배치된 오브젝트는 기존처럼 제거
            if (ObjectPoolManager.Instance == null || string.IsNullOrEmpty(poolKey))
            {
                Destroy(gameObject);
                return;
            }

            isInPool = true;
            ObjectPoolManager.Instance.ReturnToPool(this);
        }
    }
}
