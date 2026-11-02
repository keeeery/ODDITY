using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using TeamOdd.CameraMove;
using TeamOdd.UI;
using UnityEngine;

namespace TeamOdd.Boss
{
    // 씬이 시작되면 보스 등장 연출을 재생하고, 끝난 뒤에 보스 상태머신을 시작한다.
    // - 카메라를 보스 정면의 연출 위치로 보간 없이 즉시 옮김
    // - 대표 패턴 하나의 애니메이션만 재생(판정·예고 없음), 동시에 수식어와 이름을 띄움
    // - 연출 동안 카메라는 천천히 다가서며(달리 인) 보스 주위를 살짝 돌고, 타격 신호마다 가볍게 흔들림
    // - 모션이 끝나면(OnAttackEnded 신호 또는 최대 시간) 이름을 지우고 카메라를 플레이어 추적 시점으로 빠르게 보간
    // - 연출 동안 HUD를 숨기고, 카메라가 돌아오기 시작하면 서서히 다시 나타나게 함
    // - 카메라가 돌아오면 보스 상태머신을 시작하고 플레이어 조작을 풀어 줌
    // 보스 씬마다 빈 오브젝트에 붙이고 보스·대표 패턴·이름을 지정한다. 이름 UI는 BossIntroTitle(UI Canvas 아래)을 쓴다.
    public class BossIntroCinematic : MonoBehaviour
    {
        [Title("References")]
        [Tooltip("비어 있으면 씬에서 보스를 찾는다.")]
        [SerializeField] private BossMonster boss;
        [Tooltip("모션만 빌려 쓸 대표 패턴, 보스의 자식에 있는 패턴 상태를 넣는다. 한 번에 끝나는(OnAttackEnded가 한 번인) 패턴을 고른다.")]
        [SerializeField] private BossMeleePattern showcasePattern;
        [Tooltip("비어 있으면 씬에서 찾는다.")]
        [SerializeField] private BossIntroTitle title;
        [Tooltip("연출 동안 숨길 HUD 캔버스, 비어 있으면 이름 UI(BossIntroTitle)가 속한 최상위 캔버스에 CanvasGroup을 붙여 쓴다. 이름 UI는 부모 그룹을 무시하므로 같이 숨지 않는다.")]
        [SerializeField] private CanvasGroup[] hudGroups;

        [Title("Name")]
        [SerializeField] private string bossName;
        [SerializeField] private string epithet;

        [Title("Shot")]
        [Tooltip("보스 정면으로 떨어진 수평 거리(m)")]
        [SerializeField, Min(0f)] private float shotDistance = 6f;
        [Tooltip("보스 발밑에서 카메라까지 높이(m)")]
        [SerializeField] private float shotHeight = 2.5f;
        [Tooltip("보스 기준 오른쪽(+)/왼쪽(-)으로 비켜 선 거리(m), 0이면 정면")]
        [SerializeField] private float shotSideOffset = 0f;
        [Tooltip("카메라가 바라볼 보스 발밑 기준 높이(m)")]
        [SerializeField] private float lookHeight = 2.5f;
        [Tooltip("0보다 크면 연출이 끝나는 위치에서 보스가 화면 세로의 이 비율을 채우도록 거리·높이를 자동으로 맞춘다. 이때 shotDistance·shotHeight·lookHeight는 무시하고, 연출 시작 때 보스 메시의 지금 자세로 잰다.")]
        [SerializeField, Range(0f, 1f)] private float screenFill;
        [Tooltip("켜면 이름 UI가 화면 가운데에서 오른쪽으로 치우친 만큼 보스를 왼쪽으로 치우치게 둔다. 이름 UI가 없거나 끄면 framingOffset을 씀")]
        [SerializeField] private bool mirrorTitleOffset = true;
        [Tooltip("화면에서 보스가 놓일 가로 위치, 화면 가운데 기준 화면 너비 비율. 음수면 왼쪽")]
        [SerializeField, Range(-0.4f, 0.4f)] private float framingOffset = -0.08f;

        [Title("Shot Movement")]
        [Tooltip("연출 시작 때 shotDistance보다 이만큼(m) 더 떨어진 곳에서 출발해 천천히 다가섬")]
        [SerializeField, Min(0f)] private float dollyDistance = 1f;
        [Tooltip("연출 동안 보스 주위를 도는 전체 각도(도), 왼쪽 끝에서 오른쪽 끝으로. 음수면 반대 방향")]
        [SerializeField] private float orbitAngle = 12f;
        [Tooltip("연출 동안 카메라 높이 변화(m), 음수면 내려가며 보스를 더 올려다봄")]
        [SerializeField] private float heightDrift = -0.4f;
        [Tooltip("위 움직임이 끝까지 가는 시간(초), 감속하며 멈춤")]
        [SerializeField, Min(0.1f)] private float moveDuration = 4f;
        [Tooltip("이 신호들이 오면 카메라를 가볍게 흔듦(타격 순간)")]
        [SerializeField] private string[] impactEvents = { "OnHit", "OnClawHit", "OnShockwave", "OnRoarDrop" };
        [Tooltip("타격 신호마다 더할 흔들림 세기(trauma 0~1)와 사라지는 시간(초)")]
        [SerializeField, Range(0f, 1f)] private float impactTrauma = 0.3f;
        [SerializeField, Min(0.01f)] private float impactFadeTime = 0.35f;

        [Title("Timing (초)")]
        [Tooltip("씬 전환 화면이 다 켜질 때까지 기다리는 시간, 이 동안 카메라는 이미 연출 위치에 있다")]
        [SerializeField, Min(0f)] private float startDelay = 0.3f;
        [Tooltip("모션 시작 뒤 이름이 뜨기까지")]
        [SerializeField, Min(0f)] private float titleDelay = 0.4f;
        [Tooltip("OnAttackEnded 신호가 오지 않을 때 모션을 끝난 것으로 보는 시간")]
        [SerializeField, Min(0.1f)] private float maxMotionDuration = 5f;
        [Tooltip("모션이 끝난 뒤 이름을 띄운 채 머무는 시간")]
        [SerializeField, Min(0f)] private float holdAfterMotion = 0.4f;
        [Tooltip("모션이 일찍 끝나도 이름이 최소 이만큼은 떠 있음")]
        [SerializeField, Min(0f)] private float minTitleDuration = 1.5f;
        [Tooltip("추적 시점으로 돌아오는 시간, 음수면 CameraController 설정을 따른다")]
        [SerializeField] private float returnDuration = 0.6f;
        [Tooltip("카메라가 돌아오기 시작한 뒤 HUD가 나타나기 시작할 때까지")]
        [SerializeField, Min(0f)] private float hudFadeDelay = 0.2f;
        [Tooltip("HUD가 서서히 나타나는 시간")]
        [SerializeField, Min(0.01f)] private float hudFadeDuration = 0.6f;

        private CameraController cameraController;
        private bool isMotionEnded;
        private bool isShotMoving;
        private float shotTime;
        private float titleShownTime;
        private float[] hudAlphas;   // 숨기기 전 각 HUD의 알파, 나타날 때 이 값으로 돌림
        private Sequence hudSequence;

        private float framingYaw;   // framingOffset만큼 보스를 옆으로 두려고 카메라를 더 돌리는 각도(도)

        private const int MAX_FRAME_POINTS = 4000;   // 화면 채움 계산에 쓰는 메시 점 수 상한

        // 보스 Start보다 먼저 상태머신 시작을 막아야 하므로 Awake에서 처리
        private void Awake()
        {
            if (boss == null) boss = FindAnyObjectByType<BossMonster>();
            if (boss != null) boss.DefersStart = true;
        }

        private void Start()
        {
            if (title == null) title = FindAnyObjectByType<BossIntroTitle>(FindObjectsInactive.Include);
            if (hudGroups == null || hudGroups.Length == 0) hudGroups = FindDefaultHudGroups();
            StartCoroutine(Play());
        }

        // 카메라 LateUpdate보다 먼저 이번 프레임 연출 위치를 넘김
        private void Update()
        {
            if (isShotMoving == false || cameraController == null) return;

            shotTime += Time.deltaTime;
            cameraController.MoveShot(GetShotPosition(shotTime, out Quaternion rotation), rotation);
        }

        private void OnDestroy()
        {
            hudSequence?.Kill();
            if (boss != null && boss.AnimEventListener != null)
                boss.AnimEventListener.OnOccursAnimStateEvent -= OnAnimEvent;
        }

        private IEnumerator Play()
        {
            if (boss == null)
            {
                Debug.LogWarning($"{name}: 보스가 없어 등장 연출을 건너뜁니다.", this);
                yield break;
            }

            Player.PlayerController playerController = Player.Player.Instance.PlayerController;
            cameraController = FindAnyObjectByType<CameraController>();

            playerController.SetActionLocked(true);
            HideHud();
            if (cameraController != null)
            {
                Camera viewCamera = cameraController.GetComponentInChildren<Camera>();
                framingYaw = GetFramingYaw(viewCamera);
                if (screenFill > 0f) FitToScreen(viewCamera);

                shotTime = 0f;
                cameraController.CutToShot(GetShotPosition(0f, out Quaternion rotation), rotation);
                isShotMoving = true;
            }

            if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

            yield return PlayMotion();

            if (holdAfterMotion > 0f) yield return new WaitForSeconds(holdAfterMotion);
            while (Time.time - titleShownTime < minTitleDuration) yield return null;

            // 돌아가는 동안은 마지막 연출 위치에서 출발하도록 움직임을 멈춤
            isShotMoving = false;
            if (title != null) title.Hide();
            FadeInHud();
            if (cameraController != null) yield return cameraController.ReturnFromShot(returnDuration).WaitForCompletion();

            playerController.SetActionLocked(false);
            boss.StartStateMachine();
        }

        // 패턴 상태에 들어가지 않고 트리거만 걸어 모션을 재생, 루트 모션은 꺼 두어 보스가 연출 위치에서 벗어나지 않음
        private IEnumerator PlayMotion()
        {
            if (showcasePattern == null)
            {
                ShowTitle();
                yield return new WaitForSeconds(maxMotionDuration);
                yield break;
            }

            Animator animator = boss.Animator;
            animator.applyRootMotion = false;

            isMotionEnded = false;
            boss.AnimEventListener.OnOccursAnimStateEvent += OnAnimEvent;
            animator.SetTrigger(showcasePattern.AnimTrigger);

            float elapsed = 0f;
            bool isTitleShown = false;
            while (isMotionEnded == false && elapsed < maxMotionDuration)
            {
                if (isTitleShown == false && elapsed >= titleDelay)
                {
                    ShowTitle();
                    isTitleShown = true;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // 모션이 이름보다 먼저 끝나도 이름은 띄움
            if (isTitleShown == false) ShowTitle();

            boss.AnimEventListener.OnOccursAnimStateEvent -= OnAnimEvent;
            animator.ResetTrigger(showcasePattern.AnimTrigger);
        }

        private void ShowTitle()
        {
            titleShownTime = Time.time;
            if (title != null) title.Show(bossName, epithet);
        }

        private void OnAnimEvent(string eventName, string param)
        {
            if (eventName == "OnAttackEnded")
            {
                isMotionEnded = true;
                return;
            }

            if (cameraController != null && System.Array.IndexOf(impactEvents, eventName) >= 0)
                cameraController.AddTrauma(impactTrauma, impactFadeTime);
        }

        // time초 지난 연출 위치, 감속 곡선을 따라 다가서며 보스 주위를 돌고 높이를 바꿈
        private Vector3 GetShotPosition(float time, out Quaternion rotation)
        {
            float t = DOVirtual.EasedValue(0f, 1f, Mathf.Clamp01(time / moveDuration), Ease.OutSine);

            Transform bossTransform = boss.transform;
            Vector3 forward = Vector3.ProjectOnPlane(bossTransform.forward, Vector3.up).normalized;
            if (forward == Vector3.zero) forward = Vector3.forward;
            forward = Quaternion.Euler(0f, Mathf.Lerp(-orbitAngle, orbitAngle, t) * 0.5f, 0f) * forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            float distance = shotDistance + dollyDistance * (1f - t);
            float height = shotHeight + heightDrift * t;
            Vector3 position = bossTransform.position + forward * distance + right * shotSideOffset + Vector3.up * height;
            Vector3 look = bossTransform.position + Vector3.up * lookHeight;
            rotation = Quaternion.Euler(0f, framingYaw, 0f) * Quaternion.LookRotation(look - position);
            return position;
        }

        // 화면 가운데에서 이름 UI와 반대쪽(또는 framingOffset, 화면 너비 비율)만큼 옆에 보이도록 돌릴 각도, 카메라를 오른쪽으로 돌리면 보스는 왼쪽에 보임
        private float GetFramingYaw(Camera viewCamera)
        {
            float offset = mirrorTitleOffset && title != null ? -title.HorizontalOffsetRatio : framingOffset;
            if (viewCamera == null || Mathf.Approximately(offset, 0f)) return 0f;

            float tanHalfHorizontal = Mathf.Tan(viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * viewCamera.aspect;
            return -Mathf.Atan(offset * 2f * tanHalfHorizontal) * Mathf.Rad2Deg;
        }

        // 보스 메시를 지금 자세로 구워 화면 세로를 screenFill만큼 채우는 거리를 찾음
        // 높이는 몸의 세로 가운데를 바라보게 두고, 연출이 끝나는 위치(달리·궤도·높이 변화가 다 끝난 곳) 기준으로 맞춤
        private void FitToScreen(Camera viewCamera)
        {
            if (viewCamera == null) return;

            List<Vector3> points = CollectBossPoints();
            if (points.Count == 0) return;

            float bottom = float.MaxValue, top = float.MinValue;
            foreach (Vector3 point in points)
            {
                bottom = Mathf.Min(bottom, point.y);
                top = Mathf.Max(top, point.y);
            }

            float center = (bottom + top) * 0.5f - boss.transform.position.y;
            shotHeight = center - heightDrift;   // 높이 변화가 끝난 뒤 가운데에 오도록
            lookHeight = center;

            float tanHalfFov = Mathf.Tan(viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float near = 0.5f, far = 300f;
            for (int i = 0; i < 30; i++)
            {
                shotDistance = (near + far) * 0.5f;
                if (GetScreenFill(points, tanHalfFov) > screenFill) near = shotDistance;
                else far = shotDistance;
            }
            shotDistance = far;
        }

        // 연출 끝 위치에서 본 점들의 세로 화면 범위(0~1, 1이면 화면 위아래 끝까지)
        private float GetScreenFill(List<Vector3> points, float tanHalfFov)
        {
            Vector3 position = GetShotPosition(moveDuration, out Quaternion rotation);
            Quaternion inverse = Quaternion.Inverse(rotation);
            float min = float.MaxValue, max = float.MinValue;

            foreach (Vector3 point in points)
            {
                Vector3 local = inverse * (point - position);
                if (local.z < 0.05f) return float.MaxValue;   // 카메라가 몸 안이나 뒤에 있음

                float y = local.y / (local.z * tanHalfFov);
                min = Mathf.Min(min, y);
                max = Mathf.Max(max, y);
            }

            return (max - min) * 0.5f;
        }

        // 보스 몸(스킨 메시)을 애니메이션을 한 번 갱신한 지금 자세로 구워 월드 좌표 점으로 모음
        // 이펙트용 일반 메시(음파 고리 등)는 몸 크기를 부풀리므로 넣지 않음
        private List<Vector3> CollectBossPoints()
        {
            boss.Animator.Update(0f);

            List<Vector3> points = new();
            List<Vector3> vertices = new();
            Mesh baked = new();

            foreach (SkinnedMeshRenderer skinned in boss.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (skinned.enabled == false || skinned.sharedMesh == null) continue;

                // 크기까지 구운 정점이라 위치·회전만 곱함
                skinned.BakeMesh(baked, true);
                baked.GetVertices(vertices);
                Matrix4x4 matrix = Matrix4x4.TRS(skinned.transform.position, skinned.transform.rotation, Vector3.one);

                int step = Mathf.Max(1, vertices.Count / MAX_FRAME_POINTS);
                for (int i = 0; i < vertices.Count; i += step)
                    points.Add(matrix.MultiplyPoint3x4(vertices[i]));
            }

            Destroy(baked);
            return points;
        }

        // 이름 UI가 놓인 최상위 캔버스, 그룹이 없으면 붙임
        private CanvasGroup[] FindDefaultHudGroups()
        {
            Canvas canvas = title != null ? title.GetComponentInParent<Canvas>(true) : null;
            if (canvas == null) return new CanvasGroup[0];

            GameObject root = canvas.rootCanvas.gameObject;
            if (root.TryGetComponent(out CanvasGroup group) == false) group = root.AddComponent<CanvasGroup>();
            return new[] { group };
        }

        // 연출 시작과 함께 바로 숨김
        private void HideHud()
        {
            hudAlphas = new float[hudGroups.Length];
            for (int index = 0; index < hudGroups.Length; index++)
            {
                if (hudGroups[index] == null) continue;
                hudAlphas[index] = hudGroups[index].alpha;
                hudGroups[index].alpha = 0f;
            }
        }

        // 숨기기 전 알파로 서서히 되돌림, 시간은 실제 초
        private void FadeInHud()
        {
            hudSequence?.Kill();
            hudSequence = DOTween.Sequence().SetUpdate(true).SetTarget(this).SetDelay(hudFadeDelay)
                .OnKill(() => hudSequence = null);

            for (int index = 0; index < hudGroups.Length; index++)
                if (hudGroups[index] != null)
                    hudSequence.Join(hudGroups[index].DOFade(hudAlphas[index], hudFadeDuration).SetEase(Ease.OutSine));
        }

#if UNITY_EDITOR
        // 연출 카메라의 시작(노랑)·끝(주황) 위치와 바라보는 지점을 씬 뷰에 표시
        private void OnDrawGizmosSelected()
        {
            BossMonster target = boss != null ? boss : FindAnyObjectByType<BossMonster>();
            if (target == null) return;

            BossMonster saved = boss;
            boss = target;
            DrawShotGizmo(GetShotPosition(0f, out Quaternion startRotation), startRotation, Color.yellow);
            DrawShotGizmo(GetShotPosition(moveDuration, out Quaternion endRotation), endRotation, new Color(1f, 0.5f, 0f));
            boss = saved;
        }

        private void DrawShotGizmo(Vector3 position, Quaternion rotation, Color color)
        {
            Gizmos.color = color;
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawWireSphere(position, 0.3f);
            Gizmos.DrawLine(position, boss.transform.position + Vector3.up * lookHeight);
            Gizmos.matrix = Matrix4x4.TRS(position, rotation, Vector3.one);
            Gizmos.DrawFrustum(Vector3.zero, 50f, 1.5f, 0.1f, 16f / 9f);
            Gizmos.matrix = Matrix4x4.identity;
        }
#endif
    }
}
