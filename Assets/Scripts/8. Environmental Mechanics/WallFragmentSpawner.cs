using TeamOdd.Psychokinesis;
using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.Serialization;
#endif

namespace TeamOdd.Arena
{
    public sealed class WallFragmentSpawner : MonoBehaviour
    {
        [SerializeField] private PsychokinesisObject[] largeFragmentPrefabs;
        [SerializeField] private PsychokinesisObject[] smallFragmentPrefabs;
        [SerializeField] private Transform fragmentParent;
        [SerializeField, Min(1)] private int fragmentCount = 6;
        [SerializeField, Min(0f)] private float spawnRadius = 1.8f;
        [SerializeField] private Vector2 launchSpeed = new Vector2(4f, 8f);
        [SerializeField, Min(0f)] private float spinSpeed = 8f;

#if UNITY_EDITOR
        [Header("Prefab Generation")]
        [FormerlySerializedAs("fragmentPrefab")]
        [SerializeField] private PsychokinesisObject fragmentTemplate;
        [SerializeField] private Vector2 fragmentScale = new Vector2(0.25f, 1.1f);
        [SerializeField] private Vector2 largeFragmentScale = new Vector2(1.6f * 2f / 3f, 2.2f * 2f / 3f);
        [SerializeField, Min(0.01f)] private float fragmentDensity = 5f;

        [ContextMenu("Generate Fragment Prefabs (10 Large, 20 Small)")]
        private void GenerateFragmentPrefabs()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("Prefab 생성은 Play Mode를 종료한 뒤 실행해 주세요.", this);
                return;
            }

            UnityEditor.Undo.RecordObject(this, "Assign generated fragment prefabs");
            largeFragmentPrefabs = WallFragmentPrefabGenerator.Generate(fragmentTemplate, 10, largeFragmentScale, fragmentDensity, true);
            smallFragmentPrefabs = WallFragmentPrefabGenerator.Generate(fragmentTemplate, 20, fragmentScale, fragmentDensity, false);
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            Debug.Log("큰 파편 10개와 작은 파편 20개의 Prefab을 생성하고 목록에 할당했습니다.", this);
        }

#endif

        // point는 월드 좌표의 피격 위치, normal은 벽 바깥쪽을 향하는 표면 법선
        public void Spawn(Vector3 point, Vector3 normal)
        {
            normal.Normalize();
            Quaternion surfaceRotation = Quaternion.LookRotation(normal);

            for (int index = 0; index < fragmentCount; index++)
            {
                PsychokinesisObject[] prefabs = index == 0 ? largeFragmentPrefabs : smallFragmentPrefabs;
                PsychokinesisObject prefab = prefabs[Random.Range(0, prefabs.Length)];
                BoxCollider sourceCollider = prefab.GetComponent<BoxCollider>();
                Vector3 scale = prefab.transform.localScale;
                Vector3 halfExtents = Vector3.Scale(sourceCollider.size * 0.5f, scale);
                Vector3 center = Vector3.Scale(sourceCollider.center, scale);
                Quaternion rotation = Random.rotation;
                float clearance = halfExtents.magnitude + center.magnitude + 0.1f;
                Vector3 position = Vector3.zero;
                bool foundSpace = false;

                for (int attempt = 0; attempt < 8; attempt++)
                {
                    Vector2 offset = Random.insideUnitCircle * spawnRadius;
                    position = point + normal * clearance + surfaceRotation * new Vector3(offset.x, offset.y, 0f);

                    if (Physics.CheckBox(position + rotation * center, halfExtents, rotation,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                        continue;

                    foundSpace = true;
                    break;
                }

                if (foundSpace == false) continue;

                PsychokinesisObject fragment = Instantiate(prefab, position, rotation, fragmentParent);
                fragment.gameObject.SetActive(true);
                Vector3 direction = (normal + Random.insideUnitSphere * 0.6f + Vector3.up * 0.35f).normalized;
                fragment.Body.linearVelocity = direction * Random.Range(launchSpeed.x, launchSpeed.y);
                fragment.Body.angularVelocity = Random.onUnitSphere * spinSpeed;
            }
        }
    }
}
