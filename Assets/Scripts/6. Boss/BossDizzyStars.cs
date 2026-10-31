using Sirenix.OdinInspector;
using TeamOdd.TimeControl;
using UnityEngine;

namespace TeamOdd.Boss
{
    // 헤롱거리는 동안 머리 위를 맴도는 작은 반짝이, BossDizzy Sway 겹의 가중치를 따라 나타나고 사라진다.
    // 반짝이 하나는 가늘게 늘린 마름모 두 장을 십자로 겹친 것이고, 기울어진 타원 궤도를 돌며 깜빡인다.
    // 가중치가 어느 정도 오른 뒤에야 보이고 먼저 사라져서 피격 직후와 회복 순간에는 화면을 가리지 않는다.
    // 보스 시간(TimeFreezable)을 따라 시간 정지 중에는 궤도도 멈춘다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BossDizzy))]
    [DefaultExecutionOrder(10)] // BossDizzy가 뼈를 흔든 뒤에 머리 위치를 읽음
    public sealed class BossDizzyStars : MonoBehaviour
    {
        // 반짝이 하나의 사각형 수(세로·가로 빛줄기)
        private const int QuadsPerStar = 2;

        [Title("Star")]
        [Tooltip("TeamOdd/Hit Debris 머티리얼(정점 색 마름모), 비워 두면 그리지 않는다.")]
        [SerializeField] private Material starMaterial;
        [Tooltip("궤도 중심, 비워 두면 이름이 \" Head\"로 끝나는 뼈를 찾는다.")]
        [SerializeField] private Transform head;
        [SerializeField, Range(1, 8)] private int count = 4;
        [Tooltip("빛줄기 길이(m)")]
        [SerializeField, Min(0.01f)] private float size = 0.45f;
        [Tooltip("빛줄기 굵기, 길이 대비 비율")]
        [SerializeField, Range(0.05f, 1f)] private float thickness = 0.22f;
        [SerializeField] private Color color = new(1f, 0.9f, 0.55f, 1f);
        [SerializeField, Range(0f, 1f)] private float opacity = 0.75f;

        [Title("Orbit")]
        [Tooltip("머리 위로 띄우는 높이(m)")]
        [SerializeField] private float height = 1.2f;
        [SerializeField, Min(0f)] private float radius = 1.1f;
        [Tooltip("도는 속도(도/초)")]
        [SerializeField] private float speed = 110f;
        [Tooltip("궤도를 앞뒤로 기울이는 각도(도), 한쪽이 위로 올라간 타원으로 보이게 함")]
        [SerializeField, Range(0f, 45f)] private float tilt = 15f;
        [Tooltip("반짝이마다 위아래로 떠오르는 폭(m)")]
        [SerializeField, Min(0f)] private float bob = 0.12f;

        [Title("Twinkle")]
        [Tooltip("깜빡이는 빈도(회/초)")]
        [SerializeField, Min(0f)] private float twinkleFrequency = 1.6f;
        [Tooltip("깜빡일 때 가장 어두워지는 정도(0~1)")]
        [SerializeField, Range(0f, 1f)] private float twinkleDepth = 0.5f;

        [Title("Visibility")]
        [Tooltip("BossDizzy 가중치가 x를 넘으면 나타나기 시작해 y에서 다 보인다.")]
        [SerializeField] private Vector2 weightRange = new(0.35f, 0.9f);

        private BossDizzy dizzy;
        private TimeFreezable clock;
        private Camera viewCamera;

        private Transform starObject;
        private MeshRenderer starRenderer;
        private Mesh mesh;
        private Vector3[] vertices;
        private Color[] colors;

        private void Awake()
        {
            dizzy = GetComponent<BossDizzy>();
            if (head == null) head = FindHead();
        }

        private void Start()
        {
            BossMonster boss = GetComponent<BossMonster>();
            clock = boss != null ? boss.Clock : TimeFreezable.GetOrAdd(gameObject);
        }

        private void OnDisable()
        {
            if (starRenderer != null) starRenderer.enabled = false;
        }

        private void OnDestroy()
        {
            if (starObject != null) Destroy(starObject.gameObject);
            if (mesh != null) Destroy(mesh);
        }

        private void LateUpdate()
        {
            float visibility = Mathf.InverseLerp(weightRange.x, weightRange.y, dizzy.Sway.Weight);
            if (visibility <= 0f || starMaterial == null || head == null)
            {
                if (starRenderer != null) starRenderer.enabled = false;
                return;
            }

            EnsureMesh();
            starRenderer.enabled = true;
            UpdateMesh(visibility * visibility * (3f - 2f * visibility));
        }

        private void UpdateMesh(float visibility)
        {
            if (viewCamera == null) viewCamera = Camera.main;
            Vector3 right = viewCamera != null ? viewCamera.transform.right : Vector3.right;
            Vector3 up = viewCamera != null ? viewCamera.transform.up : Vector3.up;

            float time = clock != null ? clock.LocalTime : Time.time;
            Vector3 center = head.position + Vector3.up * height;
            Quaternion orbitRotation = Quaternion.AngleAxis(transform.eulerAngles.y, Vector3.up) * Quaternion.AngleAxis(tilt, Vector3.right);

            for (int star = 0; star < count; star++)
            {
                float angle = (time * speed + star * 360f / count) * Mathf.Deg2Rad;
                Vector3 local = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(time * 2.3f + star * 1.7f) * bob, Mathf.Sin(angle) * radius);
                Vector3 position = center + orbitRotation * local;

                float twinkle = 1f - twinkleDepth * (0.5f + 0.5f * Mathf.Sin((time * twinkleFrequency + star * 0.37f) * Mathf.PI * 2f));
                Color starColor = color;
                starColor.a = opacity * visibility * twinkle;

                // 다 보이기 전에는 작게 시작해 커지며 나타남
                float length = size * Mathf.Lerp(0.4f, 1f, visibility) * Mathf.Lerp(0.85f, 1f, twinkle);
                float width = length * thickness;

                int quad = star * QuadsPerStar;
                SetQuad(quad, position, right * width, up * length, starColor);
                SetQuad(quad + 1, position, right * length, up * width, starColor);
            }

            // 반짝이 수를 줄인 경우 남는 사각형은 접어 둠
            for (int quad = count * QuadsPerStar; quad < vertices.Length / 4; quad++)
                SetQuad(quad, Vector3.zero, Vector3.zero, Vector3.zero, Color.clear);

            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.RecalculateBounds();
        }

        // horizontal·vertical은 사각형의 가로·세로 전체 길이
        private void SetQuad(int quad, Vector3 center, Vector3 horizontal, Vector3 vertical, Color quadColor)
        {
            int vertex = quad * 4;
            Vector3 halfRight = horizontal * 0.5f;
            Vector3 halfUp = vertical * 0.5f;

            vertices[vertex] = center - halfRight - halfUp;
            vertices[vertex + 1] = center + halfRight - halfUp;
            vertices[vertex + 2] = center - halfRight + halfUp;
            vertices[vertex + 3] = center + halfRight + halfUp;

            for (int corner = 0; corner < 4; corner++) colors[vertex + corner] = quadColor;
        }

        // 보스 자식으로 두면 시간 정지 오버레이 등 보스 렌더러를 모으는 기능에 섞이므로 씬 최상위에 두고 월드 좌표로 그림
        private void EnsureMesh()
        {
            if (starObject != null) return;

            int quadCount = 8 * QuadsPerStar;
            vertices = new Vector3[quadCount * 4];
            colors = new Color[quadCount * 4];
            var uvs = new Vector2[quadCount * 4];
            var triangles = new int[quadCount * 6];

            for (int quad = 0; quad < quadCount; quad++)
            {
                int vertex = quad * 4;
                uvs[vertex] = new Vector2(0f, 0f);
                uvs[vertex + 1] = new Vector2(1f, 0f);
                uvs[vertex + 2] = new Vector2(0f, 1f);
                uvs[vertex + 3] = new Vector2(1f, 1f);

                int triangle = quad * 6;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 2;
                triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex + 2;
                triangles[triangle + 4] = vertex + 3;
                triangles[triangle + 5] = vertex + 1;
            }

            mesh = new Mesh { name = "Dizzy Stars" };
            mesh.MarkDynamic();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;

            var meshObject = new GameObject($"{name} Dizzy Stars");
            meshObject.layer = 2; // Ignore Raycast
            meshObject.AddComponent<MeshFilter>().sharedMesh = mesh;

            starRenderer = meshObject.AddComponent<MeshRenderer>();
            starRenderer.sharedMaterial = starMaterial;
            starRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            starRenderer.receiveShadows = false;

            starObject = meshObject.transform;
        }

        private Transform FindHead()
        {
            // 휴머노이드 보스는 뼈 이름 규칙이 달라 아바타의 머리 뼈를 씀, 이름으로 찾으면 머리 부위 콜라이더("Hitbox Head")가 걸리므로 먼저 봄
            if (TryGetComponent(out Animator animator) && animator.isHuman)
            {
                Transform humanHead = animator.GetBoneTransform(HumanBodyBones.Head);
                if (humanHead != null) return humanHead;
            }

            foreach (Transform bone in GetComponentsInChildren<Transform>(true))
                if (bone.name.EndsWith(" Head")) return bone;

            Debug.LogWarning($"[{nameof(BossDizzyStars)}] {name}에서 머리 뼈(\" Head\")를 찾지 못했습니다. Head를 직접 지정하세요.", this);
            return null;
        }
    }
}
