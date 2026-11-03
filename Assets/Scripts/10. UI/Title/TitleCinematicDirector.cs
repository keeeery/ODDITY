using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Study3D.FpsGame;
using TeamOdd.Boss;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TeamOdd
{
    // 메인 화면 시네마틱
    // 1) 휴머노이드 보스가 높이 떠올라 대기 동작으로 떠 있고, 카메라도 따라 오름
    // 2) 1인칭 시점에서 염동력으로 물체를 들어 올려 보스에게 던짐
    // 3) 보스가 날아온 물체를 반격 베기(GuardAttack)로 그대로 받아쳐 되돌려 보냄
    // 4) 되돌아온 물체가 슬로모션으로 다가와 화면을 덮으며 암전
    // 5) 배치해 둔 홀로그램 보스를 메뉴 항목 오른쪽에 두고 맥박마다 글리치로 잠깐씩 나타나게 함 (메뉴는 Arcanist 인트로처럼 순서대로 나타남)
    // 연출 중 아무 키나 누르면 바로 메뉴로 넘어감. 보스는 게임 로직 없이 애니메이터만 돌림
    public class TitleCinematicDirector : MonoBehaviour
    {
        private static readonly int IdleState = Animator.StringToHash("Idle");
        private static readonly int WalkState = Animator.StringToHash("Walk");
        private static readonly int GuardAttackState = Animator.StringToHash("GuardAttack");

        private const string DeflectReleaseEvent = "OnDeflectRelease";

        [Title("Camera")]
        [SerializeField] private Transform cameraRig; // 위치·방향(앞으로 다가감), 카메라는 자식으로 손떨림만 얹음
        [SerializeField] private Camera viewCamera;
        [SerializeField] private float cinematicFov = 62f;
        [SerializeField] private float handheldAngle = 0.6f; // 평소 손떨림 각도(도)
        [SerializeField] private float handheldFrequency = 0.45f;
        [SerializeField, Min(0f)] private float shakeAngle = 2.5f; // 흔들림 1일 때 각도(도)
        [SerializeField, Min(0f)] private float shakeDecay = 5f;
        [SerializeField, Min(0f)] private float recoilReturn = 6f;

        [Title("Boss")]
        [SerializeField] private Animator bossAnimator;
        [SerializeField] private AnimStateEventListener bossEvents;
        [SerializeField] private Vector3 strikeOffset = new(0f, 2.4f, 2.2f); // 칼에 맞아 튕겨 나가는 자리(보스 기준)
        [SerializeField] private float lookHeight = 2.2f; // 시작할 때 카메라가 바라볼 보스 몸 높이
        [SerializeField] private float cameraHeight = 1.7f; // 카메라 처음 높이 = 보스 오브젝트 y + 이 값

        // 도약 전 보스 소개: 발끝부터 머리까지 훑은 뒤 전신이 보이게 물러나 처음 구도로 이어지거나(Reveal),
        // 발·검을 쥔 손·얼굴을 짧게 끊어 보여준 뒤 처음 구도로 넘어감(Cuts). 거리·높이는 보스 키(발~머리)에 비례
        public enum IntroStyle { None, Reveal, Cuts }

        [System.Serializable]
        public struct IntroCut
        {
            public HumanBodyBones target; // 바라볼 뼈
            public Vector3 offset; // 뼈 기준 카메라 자리(보스 기준 방향, 보스 키 배수, z가 +면 보스 앞쪽)
            public Vector3 drift; // 컷 동안 카메라가 천천히 흘러가는 거리(보스 키 배수)
            public float fov;
            public float duration;
        }

        [Title("Intro")]
        [SerializeField] private IntroStyle introStyle = IntroStyle.None; // 걸어오는 경우 소개가 끝나는 자리에서 바로 도약
        [SerializeField, Min(0.1f)] private float revealDistance = 0.55f; // 훑을 때 보스 앞 거리(보스 키 배수)
        [SerializeField] private float revealSide = 0.25f; // 훑을 때 카메라 기준 보스 오른쪽으로 비킨 거리(보스 키 배수, 음수면 왼쪽), 정면보다 입체감
        [SerializeField] private float revealFov = 45f;
        [SerializeField, Min(0.01f)] private float revealDuration = 2.4f; // 발끝 → 머리(초)
        // 머리까지 훑은 자리에서 전신 구도를 거치지 않고 바로 도약을 따라감: 물러나며 살짝 내려앉았다가 보스와 함께 솟는 포물선 경로
        // 물러남·높이·시선 모두 보스가 다 오르는 순간 함께 끝남 (jumpProgress)
        // 시선: 웅크리는 동안 머리에서 발밑 땅으로 천천히 내려와 박차는 순간의 흙먼지를 화면 아래에 담고,
        // 보스가 뜬 직후엔 먼지에 잠깐 머물렀다가 점점 빠르게 보스를 쫓아 올라감 (먼지 → 보스로 이어지는 한 번의 틸트)
        [SerializeField] private float revealLaunchLook = 0.8f; // 땅을 차는 순간 바라볼 높이(m, 바닥 기준)
        [SerializeField] private IntroCut[] introCuts =
        {
            new() { target = HumanBodyBones.RightFoot, offset = new Vector3(0.35f, 0.05f, 0.4f), drift = new Vector3(0f, 0f, -0.05f), fov = 40f, duration = 0.5f },
            new() { target = HumanBodyBones.RightHand, offset = new Vector3(-0.3f, -0.05f, 0.35f), drift = new Vector3(0.06f, 0f, 0f), fov = 38f, duration = 0.45f },
            new() { target = HumanBodyBones.Head, offset = new Vector3(0.12f, -0.08f, 0.3f), drift = new Vector3(0f, 0f, -0.04f), fov = 35f, duration = 0.55f },
        };
        [SerializeField] private float cutShake = 0.15f; // 컷이 바뀔 때 짧은 흔들림, 0이면 없음

        // 보스가 뒤쪽에서 카메라를 향해 걸어오다 제자리(씬에 배치한 위치)에 닿으면 걸음의 힘을 실은 채 웅크려 도약
        // 걸음 이동량은 걷기 애니메이션의 발 이동(deltaPosition)을 그대로 써 발이 미끄러지지 않음
        [Title("Walk In")]
        [SerializeField, Min(0f)] private float walkDistance = 5f; // 걸어오는 거리(m), 0이면 제자리에서 바로 도약
        [SerializeField, Min(0.1f)] private float walkAnimSpeed = 1f;
        [SerializeField, Range(0f, 1f)] private float walkStartPhase = 0.2f; // 걷기 동작을 이 지점부터 재생, 첫 화면부터 걸음 중간
        [SerializeField, Min(0.1f)] private float walkFallbackSpeed = 1.6f; // 애니메이션 이동량을 못 읽을 때 쓰는 걷는 속도(m/s)
        [SerializeField, Min(0.01f)] private float walkGlide = 0.25f; // 웅크리는 동안 걸음 속도가 줄어드는 시간(초), 뚝 멈추지 않게
        [SerializeField, Min(0f)] private float walkToJumpBlend = 0.25f; // 걷기 → 도약 준비 동작 섞는 시간(초)
        // 걸어오는 동안 카메라: 조금 낮고 보스 쪽으로 다가간 자리에서 시작해, 보스가 다가오는 만큼 천천히 뒷걸음질치며 올라와
        // 보스가 도착할 때 처음 구도(전신)에 닿음. 보스가 화면 속에서 점점 커지되 너무 빨리 커지지 않는 추적 숏
        [SerializeField] private float walkDolly = 2f; // 시작할 때 처음 구도보다 보스 쪽으로 다가가 있는 거리(m)
        [SerializeField] private float walkDrop = 0.6f; // 시작할 때 처음 구도보다 낮은 높이(m), 올려다보는 각

        // 도약 카메라: 카메라맨이 고개로 먼저 보스를 쫓고(빠른 틸트), 몸은 한 박자 늦게 일부만 따라 올라가며 물러남(크레인)
        // 끝까지 보스보다 조금 낮게 남아 하늘을 배경으로 살짝 올려다보는 구도가 되고, 공중 출렁임은 고개로만 부드럽게 받음
        [Title("Rise Camera")]
        [SerializeField, Range(0f, 1f)] private float craneRatio = 0.75f; // 보스가 오른 높이 중 카메라가 따라 오르는 비율
        [SerializeField] private float cranePullBack = 1.5f; // 보스가 꼭대기까지 오르면 카메라가 뒤로 물러나는 거리(m)
        // 걸어오는 동안의 뒷걸음질을 처음 구도에서 딱 멈추지 않고 일부 남겨 도약하며 마저 함
        // 수평으로 물러나던 카메라가 멈춤 없이 위로 휘어 올라가는 J자 경로가 됨 (0이면 멈췄다 수직으로 오르는 L자)
        [SerializeField, Range(0f, 1f)] private float jumpDollyShare = 0.4f; // 도약하며 마저 하는 뒷걸음질 몫
        // 도약 카메라 이동(물러남·높이·시선)은 시간이 아니라 도약 진행에 묶여, 보스가 다 오르는 순간 위치와 각도가 함께 멈춤
        [SerializeField, Range(0f, 1f)] private float jumpCrouchShare = 0.3f; // 땅을 차기 전 웅크리는 동안 해 두는 카메라 이동 몫
        [SerializeField, Min(0.01f)] private float craneSmooth = 0.45f; // 위치와 바라보는 점이 같이 따라붙는 시간(초), 클수록 늦게
        [Tooltip("보스가 움직이는 쪽으로 이 시간만큼 앞을 내다보고 비워 둠(리드 룸). 솟구칠 때 위쪽 여백이 생겨 답답하지 않음")]
        [SerializeField, Min(0f)] private float lookLead = 0.12f;
        [SerializeField, Min(0.01f)] private float leadSmooth = 0.15f; // 보스 속도를 부드럽게 읽는 시간(초), 박차는 순간 시선이 튀지 않게
        [SerializeField] private float riseFovKick = 5f; // 가장 빠르게 솟구칠 때 넓어지는 시야(도), 속도감
        [SerializeField] private float riseFovSpeed = 15f; // 이 속도(m/s)에서 riseFovKick을 다 씀
        // 씬 빌더가 GuardAttack 클립 길이와 OnDeflectRelease 시점으로 채움
        [Tooltip("반격 베기 시작부터 베는 순간(OnDeflectRelease)까지 걸리는 시간(초)")]
        [SerializeField, Min(0f)] private float strikeLead = 0.3f;
        [Tooltip("GuardAttack 상태에서 베는 순간의 진행률(0~1)")]
        [SerializeField, Range(0f, 1f)] private float strikeNormalizedTime = 0.15f;
        [SerializeField] private float strikeTimeout = 1f; // 베는 신호가 안 오면 이만큼 뒤에 그냥 튕겨 냄
        // 보스 프리팹 HumanoidGuardCounter의 releaseBurst를 씬 빌더가 그대로 옮겨 둠, 받아칠 때 터짐
        [SerializeField] private HumanoidReflectBurst releaseBurst = new();

        // 보스가 높이 떠올라 하늘(스카이박스)을 등지고 받아 침, 카메라는 올려다보며 따라감
        [Title("Rise")]
        [Tooltip("씬 빌더가 점프 시작 클립(Jump_Start_ZeroHeight)으로 덮어쓴 상태")]
        [SerializeField] private string riseState = "PhaseShift";
        [Tooltip("점프 시작 동작에서 웅크렸다 펴며 땅을 차는 시점(상태 진행률 0~1), 이 순간에 맞춰 떠오름")]
        [SerializeField, Range(0f, 1f)] private float takeoffAt = 0.6f;
        [SerializeField, Min(0.1f)] private float takeoffAnimSpeed = 1f; // 땅을 차기 전 웅크리는 준비 동작 재생 속도 (1보다 작으면 묵직하게 천천히)
        [SerializeField] private float riseHeight = 8.5f;
        [SerializeField, Min(0.01f)] private float riseDuration = 1.5f;
        [Tooltip("상승 진행(가로 0~1 시간, 세로 0~1 높이). 박차는 순간 가장 빠르고 감속하며, 살짝 넘쳤다가 붙잡히듯 내려앉음")]
        [SerializeField] private AnimationCurve riseCurve = CreateRiseCurve();
        [SerializeField] private float levitateHeight = 0.8f; // 이만큼 떠오른 뒤부터 다리를 공중부양 자세로 (땅에서 꺾으면 발이 묻힘)
        [SerializeField, Range(0f, 1f)] private float idleBlendAt = 0.4f; // 상승 진행도가 이만큼일 때 대기(Idle) 자세로 넘어감
        [SerializeField] private float hoverAmplitude = 0.15f;
        [SerializeField] private float hoverFrequency = 0.5f;
        [SerializeField, Min(0.01f)] private float hoverFadeIn = 1f; // 다 오른 뒤 출렁임이 커지는 시간(초), 멈춘 몸이 갑자기 다시 움직이지 않게
        [Tooltip("상승 시간 중 이만큼 지나면 물체 발사 대기를 시작 (1이면 다 오른 뒤)")]
        [SerializeField, Range(0f, 1f)] private float throwAt = 0.6f;
        [SerializeField, Min(0f)] private float afterRiseHold = 0f; // 다 오른 뒤 공중에 떠 있다가 물체를 발사하기까지(초)
        [SerializeField] private BossFootDust takeoffDust; // 땅을 찰 때 발밑 흙먼지 (점프 착지 때와 같은 먼지)
        [SerializeField, Min(0f)] private float takeoffDustAmount = 1f;

        // 서 있는 애니메이션 위에 다리를 보스 좌우축으로 굽혀 공중에 늘어뜨림 (+: 정강이·발끝이 뒤·아래로, -: 허벅지가 앞으로)
        [Title("Levitation Pose")]
        [SerializeField] private Vector2 thighAngles = new(-28f, -8f); // (왼쪽, 오른쪽)
        [SerializeField] private Vector2 kneeAngles = new(55f, 25f);
        [Tooltip("발끝이 보스 정면에서 아래로 숙여지는 각도, 대기 동작에서 발이 바깥으로 벌어져 있어도 정면 기준으로 맞춤")]
        [SerializeField] private float footAngle = 60f;
        [SerializeField] private float legSwayAngle = 4f;
        [SerializeField, Min(0.01f)] private float levitationBlend = 0.5f; // 땅을 찬 뒤 다리가 늘어지는 시간(초)

        [Title("Projectile")]
        [SerializeField] private Transform projectile;
        [SerializeField] private TrailRenderer projectileTrail; // 빠르게 날아가는 궤적이 읽히도록 발사 순간부터 남김
        // 가상 플레이어 - 카메라 - 보스가 거의 한 줄: 물체는 카메라 뒤 가상 플레이어에서 보스로 발사돼 카메라 옆을 스쳐 가고,
        // 받아친 물체는 가상 플레이어 쪽으로 돌아오다 그 앞의 카메라를 덮음
        [Tooltip("카메라 기준 가상 플레이어 위치(m), 카메라가 위를 봐도 수평 방향 기준으로 잼. 옆·아래로 비켜 있어야 물체가 카메라를 뚫지 않고 스쳐 감")]
        [SerializeField] private Vector3 virtualPlayerOffset = new(-0.9f, -0.7f, -3.5f);
        [SerializeField, Min(0f)] private float chargeDuration = 0.1f; // 발사 직전 힘을 모으는 시간(초)
        [SerializeField] private float throwSpeed = 30f;
        [SerializeField] private float throwArc = 0f;
        [SerializeField] private float deflectSpeed = 50f;
        [SerializeField] private float spinSpeed = 600f; // 날아가는 동안 도는 속도(도/초)

        [Title("Timing")]
        [SerializeField, Min(0.01f)] private float openFade = 0.9f;
        [SerializeField, Min(0f)] private float openHold = 0.15f; // 완전히 밝아진 뒤 보스가 움직이기 전까지(초)
        [SerializeField, Min(0f)] private float releaseHitStop = 0.1f; // 받아치는 순간 멈칫
        [SerializeField, Range(0f, 1f)] private float approachDarken = 0.5f; // 맞기 전까지 어두워지는 정도
        [SerializeField, Min(0f)] private float surfaceGap = 0.08f; // 물체 표면이 근평면 앞에서 멈추는 여유(m), 안쪽이 비쳐 보이지 않게
        [SerializeField, Min(0.1f)] private float approachTimeout = 3f; // 받아친 뒤 이 시간(초, 실제 시간) 안에 못 닿아도 암전으로 넘어감
        [SerializeField, Min(0.01f)] private float coverBlackout = 0.4f; // 화면을 덮은 채 완전히 어두워지는 시간(초)
        [SerializeField] private float spinStopDistance = 2f; // 표면이 이 거리(m)보다 가까워지면 회전이 멈춰 감(모서리가 튀어나오지 않게)
        [SerializeField] private float slowMoDistance = 4f; // 물체가 이만큼 다가오면 느려지기 시작
        [SerializeField, Range(0.05f, 1f)] private float slowMoScale = 0.5f;
        [SerializeField, Min(0f)] private float coverFadeLength = 1.4f; // 표면이 이 거리(m)까지 다가오면서부터 어두워짐
        [SerializeField, Min(0f)] private float coverCenterSpeed = 30f; // 맞은 뒤 물체가 화면 한가운데로 모이는 빠르기
        [SerializeField, Min(0f)] private float coverShakeMargin = 20f; // 맞은 뒤 흔들림·시야 변화로 가장자리가 드러나지 않게 더하는 시야(도)
        [SerializeField, Min(0f)] private float blackHold = 0.9f;

        // 연출 중에만 쓰는 볼륨 두 개를 실행 중에 만들어 씀 (에셋을 남기지 않음)
        [Title("Depth Of Field")]
        [SerializeField] private bool useDepthOfField = true;
        [SerializeField] private float focalLength = 60f; // mm, 클수록 앞뒤가 많이 흐려짐
        [SerializeField] private float aperture = 4f; // f값, 작을수록 많이 흐려짐
        [SerializeField, Min(0f)] private float focusSpeed = 6f; // 보스를 볼 때 초점이 따라가는 빠르기
        [SerializeField] private float bossFocusHeight = 3f; // 보스 몸 어디에 초점을 둘지(m)

        [Title("Impact (받아치는 순간)")]
        [SerializeField, Range(0f, 1f)] private float impactChromatic = 1f;
        [SerializeField, Range(0f, 1f)] private float impactVignette = 0.45f;
        [SerializeField] private float impactZoom = -8f; // FOV 변화(도), 음수면 줌인
        [SerializeField, Min(0.01f)] private float impactDecay = 0.4f; // 효과가 빠지는 시간(초, 실제 시간)
        [SerializeField] private float impactExposure = 0.8f; // 맞는 순간 화면이 번쩍 밝아지는 정도(노출)
        [SerializeField, Range(0f, 1f)] private float impactShake = 0.4f;
        [SerializeField] private float impactRoll = 6f; // 카메라가 비스듬히 기우는 각도(도), 좌우는 무작위
        [SerializeField] private float impactPush = 0.35f; // 보스 쪽으로 확 끌려가는 거리(m)
        [Tooltip("멈칫 뒤 바로 정상 속도로 돌아가지 않고 이 배속에서 서서히 풀림")]
        [SerializeField, Range(0.01f, 1f)] private float impactSlowScale = 0.15f;
        [SerializeField, Min(0.01f)] private float impactRampDuration = 0.3f; // 느린 배속이 풀리는 시간(초, 실제 시간)
        [SerializeField] private float impactWhip = 6f; // 풀리면서 시야가 넓어지며 뒤로 빠지는 FOV(도)

        [Title("Screen")]
        [SerializeField] private CanvasGroup blackout;

        [Title("Menu")]
        [SerializeField] private GameObject cinematicWorld; // 암전 뒤 숨길 맵·보스·물체
        [SerializeField] private GameObject menuWorld; // 메뉴에서만 보일 먼지 등
        [SerializeField] private TitleHologramPulse pulse;
        [Tooltip("원본 보스 프리팹에 홀로그램 머티리얼을 입혀 씬에 배치해 둔 것, 메뉴에서 켬")]
        [SerializeField] private GameObject menuHologram;
        [Tooltip("글리치 때 보스 대신 잠깐 비치는 플레이어 홀로그램, 비워 두면 바뀌지 않음")]
        [SerializeField] private GameObject menuPlayerHologram;
        [SerializeField, Range(0f, 1f)] private float menuScreenX = 0.72f; // 홀로그램 가운데가 올 화면 가로 위치, 왼쪽 제목·메뉴의 오른쪽
        [Tooltip("메뉴가 뜰 때 연출용 볼륨(아레나 노출 +1.44 등)을 모두 끄고 대신 쓸 후처리, 로비 홀로그램과 같은 Mission Lobby Profile을 써야 홀로그램 표면이 하얗게 타지 않음")]
        [SerializeField] private VolumeProfile menuProfile;
        [SerializeField, Range(0f, 0.3f)] private float menuScreenMargin = 0.03f; // 돌아가는 팔 끝이 화면 오른쪽 가장자리에서 떨어질 여유(화면 폭 비율) // 홀로그램 가운데가 올 화면 가로 위치, 메뉴 항목 오른쪽
        [SerializeField] private CanvasGroup menuGroup; // 입력을 받는 버튼 묶음
        [SerializeField] private CanvasGroup titleGroup;
        [SerializeField] private CanvasGroup[] buttons;
        [SerializeField] private CanvasGroup footer;
        [SerializeField] private float menuFov = 40f;
        [SerializeField, Range(0.1f, 1f)] private float menuFill = 0.8f; // 홀로그램 키가 화면 높이에서 차지하는 비율
        [SerializeField] private float menuLookOffset = 0.25f; // 홀로그램을 화면 아래쪽으로 내리는 정도(반키 배수)
        [SerializeField] private float menuHandheldAngle = 0.15f;
        [SerializeField] private float menuWorldDistance = 6f;

        // 암전이 걷히면 먼지 입자만 떠 있는 어둠 → 홀로그램과 빛 번짐·박동 링 → 로고 → 메뉴 항목·버전 순서로 하나씩 나타남
        [Title("Menu Reveal (초)")]
        [SerializeField] private Vector2 blackoutFadeTime = new(0.2f, 1.2f); // (시작, 길이) 걷히면 먼지 입자만 보임
        [SerializeField] private Vector2 hologramTime = new(1.3f, 1.4f); // 홀로그램, 뒤의 빛 번짐·박동 링
        [SerializeField] private Vector2 titleTime = new(2.6f, 1.1f);
        [SerializeField] private Vector2 buttonTime = new(3.6f, 0.6f);
        [SerializeField] private float buttonStagger = 0.15f;
        [SerializeField] private Vector2 footerTime = new(3.9f, 0.7f);

        private bool isReleased;
        private MeshFilter projectileMesh; // 회전된 상자 기준으로 카메라에서 가장 가까운 표면까지 잼
        private Renderer[] projectileRenderers; // 발사 전에는 숨김 (걸어오는 동안 카메라가 움직여 화면에 잡히지 않게)
        private Light[] projectileLights;
        private Transform bossRoot;
        private bool isHovering;
        private bool isTracking;
        private Vector3 cameraBase; // 도약 전 카메라 자리
        private Vector3 cameraBack; // 보스 반대쪽 수평 방향
        private Vector3 walkTarget; // 도약할 자리
        private float walkProgress; // 걸음 진행도(0~1)
        private float jumpProgress; // 도약 카메라 이동 진행도(0~1), 웅크림으로 jumpCrouchShare까지, 나머지는 다 오를 때 1
        private Vector3 launchFrom; // 클로즈업에서 바로 도약을 따라갈 때 출발 자리
        private bool isLaunching;
        private Vector3 launchLookFrom; // 클로즈업에서 바라보던 점
        private float riseTop = float.MaxValue; // 다 오른 높이(지면 기준), 카메라 몸은 그 위의 넘침·출렁임을 따라가지 않음
        private Vector3 walkCarry; // 도착 순간 걸음 속도, 웅크리는 동안 서서히 줄며 이어짐
        private Vector3 walkDirection;
        private float walkSpeed; // 부드럽게 읽은 걷는 속도(m/s)
        private bool isIntroWalk; // 소개를 찍는 동안 걷는 중, 소개가 끝나는 자리가 곧 도약 자리
        private Coroutine walkRoutine;
        private Vector3 cameraVelocity;
        private Vector3 lookPoint; // 고개가 실제로 바라보는 점 (보스를 늦게 쫓음)
        private Vector3 lookVelocity;
        private Vector3 lastBossPosition;
        private Vector3 bossVelocity; // 부드럽게 읽은 보스 속도
        private float riseFov; // 솟구치는 속도만큼 넓어진 시야
        private Vector3 hoverBase;
        private float hoverStartTime; // 출렁임을 0에서 시작해 다 오른 순간 툭 튀지 않게
        private float levitation; // 공중부양 다리 자세 비중(0~1)
        private float levitationTarget;
        private float groundHeight; // 시작할 때 보스가 선 바닥 높이
        private float ankleRest = -1f; // 서 있는 자세의 발목 높이(바닥 기준), 첫 프레임에 잼
        private Vector3 cameraOffset; // 던질 때 몸을 젖혔다 내미는 카메라 위치 반동
        private Volume focusVolume;
        private Volume impactVolume;
        private DepthOfField depthOfField;
        private float focusDistance;
        private bool isFocusOnProjectile; // 받아친 물체가 다가오는 동안은 초점을 물체에 붙임
        private float impactPulse;

        // 손떨림·흔들림·반동은 매 프레임 카메라 로컬 회전으로 덮어씀
        private float currentHandheld;
        private float shake;
        private Vector3 recoil;
        private float fovKick;
        private float baseFov;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            FaceEachOther();
            projectileMesh = projectile.GetComponentInChildren<MeshFilter>();
            projectileRenderers = projectile.GetComponentsInChildren<Renderer>(true);
            projectileLights = projectile.GetComponentsInChildren<Light>(true);
            CreateVolumes();
            StartCoroutine(PlayCinematic());
        }

        private void LateUpdate()
        {
            if (isHovering)
            {
                float hoverTime = Time.time - hoverStartTime;
                float fadeIn = Mathf.SmoothStep(0f, 1f, hoverTime / hoverFadeIn);
                bossRoot.position = hoverBase + Vector3.up * (Mathf.Sin(hoverTime * hoverFrequency * Mathf.PI * 2f) * hoverAmplitude * fadeIn);
            }

            KeepFeetAboveGround();
            levitation = Mathf.MoveTowards(levitation, levitationTarget, Time.deltaTime / levitationBlend);
            ApplyLevitationPose();

            TrackBoss();
            UpdateCamera();
            UpdateFocus();
        }

        private void OnDisable()
        {
            if (bossEvents != null) bossEvents.OnOccursAnimStateEvent -= OnBossEvent;
            Time.timeScale = 1f;
        }

        private void OnDestroy()
        {
            releaseBurst.Dispose();
            if (focusVolume != null) Destroy(focusVolume.sharedProfile);
            if (impactVolume != null) Destroy(impactVolume.sharedProfile);
        }

        #region CINEMATIC

        private IEnumerator PlayCinematic()
        {
            isReleased = false;
            Time.timeScale = 1f;
            baseFov = cinematicFov;
            currentHandheld = handheldAngle;
            HideMenu();
            SetAlpha(blackout, 1f);

            bossAnimator.SetBool(BossAnimParams.IS_GUARDING, false);
            projectile.position = VirtualPlayer;
            if (projectileTrail != null) projectileTrail.emitting = false;
            SetProjectileVisible(false);

            bool isWalking = walkDistance > 0f;
            if (isWalking)
            {
                // 첫 프레임부터 이미 걷는 중, 소개도 걸어오는 보스를 따라가며 찍음
                bossAnimator.SetBool(BossAnimParams.IS_CHASING, true); // 걷기 상태가 대기로 돌아가지 않게
                bossAnimator.speed = walkAnimSpeed;
                bossAnimator.Play(WalkState, 0, walkStartPhase);
                walkRoutine = StartCoroutine(WalkIn());
                if (introStyle == IntroStyle.None) BeginTracking(true);
            }

            // 씬을 연 직후 몇 프레임은 로딩으로 한 프레임이 길어 페이드가 순식간에 끝날 수 있으므로 넘기고 시작
            for (int frame = 0; frame < 3; frame++) yield return null;
            switch (introStyle)
            {
                case IntroStyle.Reveal:
                    yield return RevealIntro();
                    EndIntroWalk();
                    BeginTracking(false);
                    BeginLaunch();
                    break;

                case IntroStyle.Cuts:
                    yield return CutsIntro();
                    EndIntroWalk();
                    BeginTracking(false);
                    break;

                default:
                    if (isWalking)
                    {
                        StartCoroutine(Fade(blackout, 1f, 0f, openFade));
                    }
                    else
                    {
                        yield return Fade(blackout, 1f, 0f, openFade);
                        BeginTracking(true);
                    }
                    break;
            }

            if (walkRoutine != null) yield return walkRoutine;
            else if (!isWalking) yield return new WaitForSecondsRealtime(openHold);

            yield return Rise();
            yield return Charge();
            yield return Throw();
            yield return WaitForStrike();
            yield return DeflectToCamera();

            yield return ToMenu();
        }

        // 땅을 차고 높이 떠올라 대기 동작으로 떠 있음, 카메라는 고개가 먼저, 몸이 늦게 따라감 (TrackBoss)
        // 점프 클립은 높이가 0인 판이라 높이는 여기서 올림 (빠르게 솟구쳤다 천천히 멈춤)
        // 걷기 애니메이션의 발 이동량만큼 도약 자리로 다가옴, 웅크리는 동안 미끄러질 몫(walkGlide)을 남기고 도약으로 넘어감
        // 소개가 있으면 도착을 기다리지 않고 소개가 끝날 때까지 걷다가 그 자리에서 도약 (EndIntroWalk)
        private IEnumerator WalkIn()
        {
            Vector3 start = bossRoot.position;
            walkDirection = Vector3.ProjectOnPlane(walkTarget - start, Vector3.up).normalized;
            float total = Vector3.Distance(start, walkTarget);
            walkSpeed = walkFallbackSpeed;

            while (true)
            {
                float remaining = Vector3.Dot(walkTarget - bossRoot.position, walkDirection);
                if (!isIntroWalk && remaining <= walkSpeed * walkGlide) break;

                float deltaTime = Mathf.Min(Time.deltaTime, 1f / 30f); // 로딩으로 긴 첫 프레임에 훅 다가오지 않게
                float step = Vector3.Dot(bossAnimator.deltaPosition, walkDirection);
                if (step <= 0.0001f) step = walkFallbackSpeed * walkAnimSpeed * deltaTime;
                if (deltaTime > 0f) walkSpeed = Mathf.Lerp(walkSpeed, step / deltaTime, 1f - Mathf.Exp(-deltaTime / 0.2f));

                bossRoot.position += walkDirection * (isIntroWalk ? step : Mathf.Min(step, remaining));
                walkProgress = Mathf.Clamp01(1f - (remaining - step) / total);
                yield return null;
            }

            FinishWalk();
        }

        private void FinishWalk()
        {
            walkCarry = walkDirection * walkSpeed;
            bossAnimator.SetBool(BossAnimParams.IS_CHASING, false);
            bossAnimator.speed = 1f;
            walkRoutine = null;
        }

        // 소개가 끝난 순간 걷기를 끊고, 웅크리며 미끄러져 멈출 자리를 도약 자리로 삼아 처음 구도도 그만큼 옮김
        private void EndIntroWalk()
        {
            if (!isIntroWalk) return;

            isIntroWalk = false;
            if (walkRoutine != null) StopCoroutine(walkRoutine);
            FinishWalk();

            Vector3 shift = Vector3.ProjectOnPlane(IntroWalkLanding - walkTarget, Vector3.up);
            walkTarget += shift;
            cameraBase += shift;
            walkProgress = 1f;
        }

        // 0이면 땅을 차기 전, 1이면 다 오름 (넘쳤다 내려앉는 몫은 제외)
        private float RiseFraction => Mathf.Clamp01((jumpProgress - jumpCrouchShare) / Mathf.Max(1f - jumpCrouchShare, 0.0001f));

        private IEnumerator Rise()
        {
            bossAnimator.CrossFadeInFixedTime(riseState, walkDistance > 0f ? walkToJumpBlend : 0.1f);
            yield return WaitForTakeoff();

            // 상승은 따로 돌리고, throwAt만큼 오르면 다 오르기를 기다리지 않고 다음 단계(발사)로 넘어감
            float risen = 0f;
            StartCoroutine(RiseMotion(progress => risen = progress));
            while (risen < throwAt) yield return null;
            yield return new WaitForSeconds(afterRiseHold);
        }

        // 점프 시작 동작이 땅을 차는 순간까지 기다림, 전이 중이면 다음 상태를 봄 (상태를 못 찾으면 3초 뒤 진행)
        private IEnumerator WaitForTakeoff()
        {
            int riseHash = Animator.StringToHash(riseState);
            float elapsed = 0f;
            bossAnimator.speed = takeoffAnimSpeed;

            while (elapsed < 3f)
            {
                AnimatorStateInfo info = bossAnimator.IsInTransition(0)
                    ? bossAnimator.GetNextAnimatorStateInfo(0)
                    : bossAnimator.GetCurrentAnimatorStateInfo(0);
                if (info.shortNameHash == riseHash)
                {
                    if (info.normalizedTime >= takeoffAt) break;
                    jumpProgress = Mathf.Max(jumpProgress, jumpCrouchShare * Mathf.Clamp01(info.normalizedTime / takeoffAt));
                }

                // 걸어오던 힘이 남아 웅크리는 동안 서서히 멈춤
                bossRoot.position += walkCarry * Time.deltaTime;
                walkCarry *= Mathf.Exp(-Time.deltaTime / walkGlide);
                walkProgress = Mathf.MoveTowards(walkProgress, 1f, Time.deltaTime / walkGlide);

                elapsed += Time.deltaTime;
                yield return null;
            }

            bossAnimator.speed = 1f;
            walkCarry = Vector3.zero;
            walkProgress = 1f;
            jumpProgress = Mathf.Max(jumpProgress, jumpCrouchShare);
        }

        // 실제 점프처럼 박차는 순간 가장 빠르고(시작 기울기 3) 올라갈수록 감속해 0.65 지점에서 6% 넘친 뒤,
        // 염동력에 붙잡히듯 부드럽게 제 높이로 내려앉음
        private static AnimationCurve CreateRiseCurve() => new(
            new Keyframe(0f, 0f, 3f, 3f),
            new Keyframe(0.65f, 1.06f, 0f, 0f),
            new Keyframe(1f, 1f, 0f, 0f));

        private IEnumerator RiseMotion(System.Action<float> onProgress)
        {
            Vector3 start = bossRoot.position;
            riseTop = start.y - groundHeight + riseHeight;
            // 이륙은 흙먼지만 (입자·흔들림 없음)
            if (takeoffDust != null) takeoffDust.PlayLanding(takeoffDustAmount);
            bool isIdle = false;
            float elapsed = 0f;

            while (elapsed < riseDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / riseDuration);
                float height = riseHeight * riseCurve.Evaluate(progress);
                bossRoot.position = start + Vector3.up * height;
                onProgress(progress);
                jumpProgress = Mathf.Max(jumpProgress, Mathf.Lerp(jumpCrouchShare, 1f, Mathf.Min(riseCurve.Evaluate(progress), 1f)));
                if (height >= levitateHeight) levitationTarget = 1f;

                // 반격 대기 자세 없이 평소 대기 동작으로 떠 있다가, 물체가 날아오는 순간 바로 반격 베기
                if (isIdle == false && progress >= idleBlendAt)
                {
                    isIdle = true;
                    bossAnimator.CrossFadeInFixedTime(IdleState, 0.25f);
                }

                yield return null;
            }

            jumpProgress = 1f;
            hoverBase = bossRoot.position;
            hoverStartTime = Time.time;
            isHovering = true;
        }

        // 발사 직전 잠깐 멈춤, 물체는 카메라 뒤 가상 플레이어 자리에서 대기 (카메라 반동 없음)
        private IEnumerator Charge()
        {
            float elapsed = 0f;

            while (elapsed < chargeDuration)
            {
                elapsed += Time.deltaTime;
                projectile.position = VirtualPlayer;
                yield return null;
            }
        }

        // 보스 칼 앞 받아칠 자리까지 날아감
        // 베기는 도착 strikeLead초 전에 시작하고, 시작한 뒤에는 애니메이터의 남은 시간에 맞춰 물체 속도를 조절해 베는 순간 정확히 닿게 함
        // 날아가는 시간이 베기 준비보다 짧으면 던지는 순간 바로 베기를 시작하고 그만큼 천천히 날아감
        private IEnumerator Throw()
        {
            Vector3 start = VirtualPlayer;
            float duration = Mathf.Max(Vector3.Distance(start, StrikePoint) / throwSpeed, strikeLead, 0.05f);
            Vector3 spinAxis = Random.onUnitSphere;
            bool isSwinging = false;
            float elapsed = 0f;
            float progress = 0f;

            isReleased = false;
            bossEvents.OnOccursAnimStateEvent += OnBossEvent;

            // 뒤의 가상 플레이어가 발사해 물체가 카메라 옆을 스쳐 지나감 (카메라는 흔들지 않음)
            projectile.position = start;
            SetProjectileVisible(true);
            if (projectileTrail != null)
            {
                projectileTrail.Clear();
                projectileTrail.emitting = true;
            }

            while (progress < 1f && isReleased == false)
            {
                float deltaTime = Time.deltaTime;
                elapsed += deltaTime;

                if (isSwinging == false && elapsed >= duration - strikeLead) isSwinging = Swing();

                if (isSwinging && TryGetTimeToStrike(out float timeToStrike))
                    progress += (1f - progress) * Mathf.Clamp01(deltaTime / Mathf.Max(timeToStrike, deltaTime));
                else
                    progress = Mathf.Min(progress + deltaTime / duration, 0.99f);

                // 보스가 떠 있어 자리가 출렁이므로 매 프레임 받아칠 자리를 다시 잼
                projectile.position = Vector3.Lerp(start, StrikePoint, progress) + Vector3.up * (throwArc * 4f * progress * (1f - progress));
                projectile.Rotate(spinAxis, spinSpeed * deltaTime, Space.World);
                yield return null;
            }

            if (isSwinging == false) Swing();
        }

        // 전이 중이면 다음 상태가 GuardAttack인지 보고, 베는 순간까지 남은 시간(초)을 잼
        private bool TryGetTimeToStrike(out float time)
        {
            AnimatorStateInfo info = bossAnimator.IsInTransition(0)
                ? bossAnimator.GetNextAnimatorStateInfo(0)
                : bossAnimator.GetCurrentAnimatorStateInfo(0);

            time = 0f;
            if (info.shortNameHash != GuardAttackState) return false;

            time = Mathf.Max((strikeNormalizedTime - info.normalizedTime) * info.length, 0f);
            return true;
        }

        // 대기 동작에서 반격 베기(GuardAttack)를 바로 시작
        private bool Swing()
        {
            bossAnimator.CrossFadeInFixedTime(GuardAttackState, 0.05f);
            return true;
        }

        // 물체가 먼저 도착하면 베는 신호(OnDeflectRelease)가 올 때까지 칼 앞에 멈칫한 채 기다림
        private IEnumerator WaitForStrike()
        {
            Vector3 spinAxis = Random.onUnitSphere;
            float elapsed = 0f;

            while (isReleased == false && elapsed < strikeTimeout)
            {
                elapsed += Time.deltaTime;
                projectile.position = Vector3.MoveTowards(projectile.position, StrikePoint, throwSpeed * Time.deltaTime);
                projectile.Rotate(spinAxis, spinSpeed * 0.3f * Time.deltaTime, Space.World);
                yield return null;
            }

            bossEvents.OnOccursAnimStateEvent -= OnBossEvent;
            isReleased = true;
        }

        // 받아치는 순간 짧게 멈칫한 뒤, 물체가 가상 플레이어 쪽으로 돌아오며 그 앞의 카메라 눈높이로 곧장 날아옴
        // 가까워질수록 느려지고 조금 어두워지다가, 카메라에 맞는 순간 멈칫·흔들림 뒤 완전히 암전
        private IEnumerator DeflectToCamera()
        {
            // 받아치는 순간 1) 멈칫: 번쩍임·색수차·비네트·줌인, 보스 쪽으로 끌려가며 기울고 짧게 흔들림, 초점은 물체로
            releaseBurst.Play(projectile.position);
            impactPulse = 1f;
            fovKick = impactZoom;
            isFocusOnProjectile = true;
            shake = Mathf.Max(shake, impactShake);
            recoil += new Vector3(-2f, Random.Range(-1.5f, 1.5f), (Random.value < 0.5f ? -1f : 1f) * impactRoll);
            cameraOffset += new Vector3(0f, 0f, impactPush);
            yield return HitStop(releaseHitStop);

            // 2) 느리게 풀림: 튕겨 나가는 첫 순간이 보이도록 느린 배속에서 서서히 정상으로, 그동안 시야가 넓어지며 뒤로 빠짐
            fovKick = impactWhip;
            Vector3 spinAxis = Random.onUnitSphere;
            float rampElapsed = 0f;

            while (true)
            {
                rampElapsed += Time.unscaledDeltaTime;
                float ramp = Mathf.Lerp(impactSlowScale, 1f, EaseOutCubic(rampElapsed / impactRampDuration));

                Vector3 eye = viewCamera.transform.position;
                Vector3 toEye = eye - projectile.position;
                float distance = toEye.magnitude;
                float surface = SurfaceDistance(eye) - StopDistance;

                // 다가올수록 느려짐 (slowMoDistance에서 그 절반 거리까지 서서히)
                float slow = Mathf.InverseLerp(slowMoDistance, slowMoDistance * 0.5f, distance);
                Time.timeScale = Mathf.Min(ramp, Mathf.Lerp(1f, slowMoScale, slow));

                // 맞는 순간이 보이도록 완전히 어둡게 하지 않음
                float cover = Mathf.InverseLerp(coverFadeLength, 0f, surface);
                SetAlpha(blackout, cover * cover * approachDarken);

                // 비스듬히 다가오면 남은 거리만큼 움직여도 표면 거리가 0에 딱 닿지 않으므로 여유를 두고, 만일을 위해 시간 제한
                if (surface <= 0.01f || rampElapsed > approachTimeout) break;

                // 표면이 멈출 자리를 넘지 않게 남은 만큼만 다가감, 가까워질수록 회전을 줄임
                projectile.position += toEye / distance * Mathf.Min(deflectSpeed * Time.deltaTime, surface);
                float spin = Mathf.Clamp01(surface / spinStopDistance);
                projectile.Rotate(spinAxis, spinSpeed * spin * Time.deltaTime, Space.World);
                // 모서리가 아닌 가장 넓은 면으로 덮도록, 회전이 멈춰 가는 만큼 면을 카메라 쪽으로 돌림
                FaceCamera(1f - spin);
                yield return null;
            }

            // 카메라에 맞음: 물체가 화면을 덮은 채 흔들리고, 붙은 그대로 부드럽게 암전
            if (projectileTrail != null) projectileTrail.emitting = false;
            FitCoverSize();
            Time.timeScale = 1f;
            shake = 1f;
            recoil += new Vector3(-6f, Random.Range(-3f, 3f), Random.Range(-6f, 6f));
            cameraOffset += new Vector3(0f, 0f, -0.25f);

            float startAlpha = blackout.alpha;
            float elapsed = 0f;

            while (elapsed < coverBlackout)
            {
                elapsed += Time.unscaledDeltaTime;
                KeepCovering();
                SetAlpha(blackout, Mathf.Lerp(startAlpha, 1f, Mathf.SmoothStep(0f, 1f, elapsed / coverBlackout)));
                yield return null;
            }

            SetAlpha(blackout, 1f);
            shake = 0f;
        }

        // 카메라가 흔들리거나 뒤로 밀려도 물체가 넓은 면을 정면으로 화면 한가운데를 덮은 채 표면이 멈출 자리에 붙어 있게 함
        private void KeepCovering()
        {
            Transform view = viewCamera.transform;
            Vector3 eye = view.position;

            // 비스듬히 들어와 한쪽으로 치우친 물체를 화면 한가운데로 빠르게 모음
            Vector3 offset = CoverCenter - eye;
            Vector3 lateral = offset - view.forward * Vector3.Dot(offset, view.forward);
            projectile.position -= lateral * (1f - Mathf.Exp(-coverCenterSpeed * Time.unscaledDeltaTime));
            FaceCamera(1f);

            Vector3 toEye = eye - projectile.position;
            float gap = SurfaceDistance(eye) - StopDistance;
            if (toEye.sqrMagnitude > 0.0001f) projectile.position += toEye.normalized * gap;
        }

        private Vector3 CoverCenter => projectileMesh != null && projectileMesh.sharedMesh != null
            ? projectileMesh.transform.TransformPoint(projectileMesh.sharedMesh.bounds.center)
            : projectile.position;

        // 상자의 가장 얇은 축(= 가장 넓은 면의 법선), 메시 기준
        private Vector3 CoverAxis
        {
            get
            {
                Vector3 extents = Vector3.Scale(projectileMesh.sharedMesh.bounds.extents, projectileMesh.transform.lossyScale);
                extents = new Vector3(Mathf.Abs(extents.x), Mathf.Abs(extents.y), Mathf.Abs(extents.z));
                if (extents.x <= extents.y && extents.x <= extents.z) return Vector3.right;
                return extents.y <= extents.z ? Vector3.up : Vector3.forward;
            }
        }

        // 가장 넓은 면이 카메라를 정면으로 보게 돌림 (weight 1이면 완전히)
        private void FaceCamera(float weight)
        {
            if (weight <= 0f || projectileMesh == null || projectileMesh.sharedMesh == null) return;

            Vector3 toCamera = -viewCamera.transform.forward;
            Vector3 normal = projectileMesh.transform.TransformDirection(CoverAxis);
            if (Vector3.Dot(normal, toCamera) < 0f) normal = -normal;
            Quaternion delta = Quaternion.FromToRotation(normal, toCamera);
            projectile.rotation = Quaternion.Slerp(Quaternion.identity, delta, Mathf.Clamp01(weight)) * projectile.rotation;
        }

        // 맞닿은 거리에서 그 면이 화면 대각선(흔들림 여유 포함)보다 작으면 그만큼 키움
        private void FitCoverSize()
        {
            if (projectileMesh == null || projectileMesh.sharedMesh == null) return;

            Vector3 extents = Vector3.Scale(projectileMesh.sharedMesh.bounds.extents, projectileMesh.transform.lossyScale);
            extents = new Vector3(Mathf.Abs(extents.x), Mathf.Abs(extents.y), Mathf.Abs(extents.z));
            Vector3 axis = CoverAxis;
            float faceHalf = axis == Vector3.right ? Mathf.Min(extents.y, extents.z)
                : axis == Vector3.up ? Mathf.Min(extents.x, extents.z)
                : Mathf.Min(extents.x, extents.y);

            float halfHeight = StopDistance * Mathf.Tan((viewCamera.fieldOfView + coverShakeMargin) * 0.5f * Mathf.Deg2Rad);
            float needed = halfHeight * Mathf.Sqrt(1f + viewCamera.aspect * viewCamera.aspect) * 1.2f;
            if (faceHalf > 0.0001f && faceHalf < needed) projectile.localScale *= needed / faceHalf;
        }

        private void SetProjectileVisible(bool isVisible)
        {
            foreach (Renderer projectileRenderer in projectileRenderers) projectileRenderer.enabled = isVisible;
            foreach (Light projectileLight in projectileLights) projectileLight.enabled = isVisible;
        }

        private float StopDistance => viewCamera.nearClipPlane + surfaceGap;

        // 물체 메시의 상자(회전·크기 포함)에서 point에 가장 가까운 표면까지 거리, 메시가 없으면 중심까지
        private float SurfaceDistance(Vector3 point)
        {
            if (projectileMesh == null || projectileMesh.sharedMesh == null) return Vector3.Distance(point, projectile.position) - 0.5f;

            Transform meshTransform = projectileMesh.transform;
            Vector3 local = meshTransform.InverseTransformPoint(point);
            Vector3 closest = projectileMesh.sharedMesh.bounds.ClosestPoint(local);
            return Vector3.Distance(point, meshTransform.TransformPoint(closest));
        }

        private void OnBossEvent(string eventName, string parameter)
        {
            if (eventName == DeflectReleaseEvent) isReleased = true;
        }

        #endregion

        #region MENU

        // 암전 동안 연출 월드를 끄고 메뉴 보스를 켜 메뉴 구도로 옮긴 뒤 하나씩 나타냄
        private IEnumerator ToMenu()
        {
            isHovering = false;
            isTracking = false; // 메뉴 구도는 FrameMenu가 정함
            Time.timeScale = 1f;
            shake = 0f;
            recoil = Vector3.zero;
            fovKick = 0f;
            cameraOffset = Vector3.zero;
            impactPulse = 0f;
            isFocusOnProjectile = false;
            if (focusVolume != null) focusVolume.weight = 0f; // 메뉴 홀로그램은 흐리지 않음

            yield return new WaitForSecondsRealtime(blackHold * 0.5f);

            cinematicWorld.SetActive(false);
            RenderSettings.fog = false;
            UseMenuVolume();
            ShowMenuHologram();
            if (menuWorld != null)
            {
                menuWorld.transform.SetPositionAndRotation(cameraRig.position + cameraRig.forward * menuWorldDistance, cameraRig.rotation);
                menuWorld.SetActive(true);
            }

            yield return new WaitForSecondsRealtime(blackHold * 0.5f);
            yield return RevealMenu();
        }

        // 배치해 둔 홀로그램 보스를 켜고, 메뉴 항목 오른쪽(menuScreenX)에 오도록 카메라를 옆으로 비켜 세움
        private void ShowMenuHologram()
        {
            if (menuHologram == null)
            {
                Debug.LogWarning("[Title] 메뉴 홀로그램이 지정되지 않았습니다.");
                return;
            }

            menuHologram.SetActive(true);
            Renderer[] renderers = System.Array.FindAll(menuHologram.GetComponentsInChildren<Renderer>(),
                item => item.enabled && item is MeshRenderer or SkinnedMeshRenderer);
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer item in renderers) bounds.Encapsulate(item.bounds);

            FrameMenu(menuHologram.transform, bounds);
            pulse.Bind(menuHologram.transform, renderers, bounds); // 메뉴와 함께 서서히 나타나 늘 떠 있고, 무작위로 지직거림

            if (menuPlayerHologram == null) return;
            menuPlayerHologram.SetActive(true);
            pulse.BindAlternate(menuPlayerHologram.transform, System.Array.FindAll(menuPlayerHologram.GetComponentsInChildren<Renderer>(),
                item => item.enabled && item is MeshRenderer or SkinnedMeshRenderer));
        }

        // 홀로그램 정면에서 키가 화면을 menuFill만큼 채우는 거리로 물러선 뒤, 정면을 그대로 본 채 왼쪽으로 비켜 홀로그램이 오른쪽에 오게 함
        private void FrameMenu(Transform target, Bounds bounds)
        {
            Vector3 front = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
            float halfHeight = bounds.extents.y;
            float horizontalFov = Camera.VerticalToHorizontalFieldOfView(menuFov, viewCamera.aspect);
            float tanHalfWidth = Mathf.Tan(horizontalFov * 0.5f * Mathf.Deg2Rad);

            // 키가 화면을 menuFill만큼 채우는 거리, 다만 T-포즈로 돌며 팔이 가장 멀리 뻗는 반지름까지 화면 오른쪽 안에 들어오도록 더 물러설 수 있음
            float radius = new Vector2(bounds.size.x, bounds.size.z).magnitude * 0.5f;
            float rightRoom = Mathf.Max(1f - menuScreenMargin - menuScreenX, 0.05f);
            float distance = Mathf.Max(
                halfHeight / (Mathf.Tan(menuFov * 0.5f * Mathf.Deg2Rad) * menuFill),
                radius / (2f * tanHalfWidth * rightRoom));
            float side = distance * tanHalfWidth * (menuScreenX * 2f - 1f);

            Quaternion rotation = Quaternion.LookRotation(-front);
            Vector3 position = bounds.center + front * distance - rotation * Vector3.right * side + Vector3.up * (halfHeight * menuLookOffset);
            cameraRig.SetPositionAndRotation(position, rotation);

            baseFov = menuFov;
            currentHandheld = menuHandheldAngle;
            viewCamera.clearFlags = CameraClearFlags.SolidColor;
            viewCamera.backgroundColor = Color.black;
        }

        private IEnumerator RevealMenu()
        {
            float end = Mathf.Max(
                blackoutFadeTime.x + blackoutFadeTime.y,
                hologramTime.x + hologramTime.y,
                titleTime.x + titleTime.y,
                buttonTime.x + buttonStagger * Mathf.Max(buttons.Length - 1, 0) + buttonTime.y,
                footerTime.x + footerTime.y);
            float elapsed = 0f;

            while (elapsed < end)
            {
                elapsed += Time.unscaledDeltaTime;

                SetAlpha(blackout, 1f - Segment(elapsed, blackoutFadeTime));
                pulse.Visibility = Segment(elapsed, hologramTime);
                SetAlpha(titleGroup, Segment(elapsed, titleTime));
                SetAlpha(footer, Segment(elapsed, footerTime));

                for (int index = 0; index < buttons.Length; index++)
                    SetAlpha(buttons[index], Segment(elapsed, new Vector2(buttonTime.x + buttonStagger * index, buttonTime.y)));

                yield return null;
            }

            SetInput(menuGroup, true);
            blackout.blocksRaycasts = false;
        }

        private void HideMenu()
        {
            if (menuWorld != null) menuWorld.SetActive(false);

            SetAlpha(titleGroup, 0f);
            SetAlpha(footer, 0f);
            foreach (CanvasGroup button in buttons) SetAlpha(button, 0f);
            SetInput(menuGroup, false);

            pulse.Visibility = 0f;
        }

        #endregion

        #region CAMERA

        // 씬에서 보스나 Camera Rig 위치만 옮겨도 되도록 시작할 때 서로 마주 보게 맞춤
        private void FaceEachOther()
        {
            // 애니메이터가 자식에 있을 수 있어 보스 최상위(연출 월드 바로 아래)를 돌림
            Transform boss = bossAnimator.transform;
            while (boss.parent != null && boss.parent.gameObject != cinematicWorld) boss = boss.parent;
            bossRoot = boss;
            groundHeight = boss.position.y;

            Vector3 toCamera = Vector3.ProjectOnPlane(cameraRig.position - boss.position, Vector3.up);
            if (toCamera.sqrMagnitude > 0.0001f) boss.rotation = Quaternion.LookRotation(toCamera);

            Vector3 cameraPosition = cameraRig.position;
            cameraPosition.y = boss.position.y + cameraHeight; // 처음 높이는 보스 y 기준 오프셋
            cameraRig.position = cameraPosition;
            lookPoint = boss.position + Vector3.up * lookHeight;
            cameraRig.rotation = Quaternion.LookRotation(lookPoint - cameraRig.position);
            cameraBase = cameraRig.position;
            cameraBack = -Vector3.ProjectOnPlane(cameraRig.forward, Vector3.up).normalized;

            // 도약 자리는 씬에 배치한 위치, 보스는 그 뒤쪽에서 걸어옴
            walkTarget = boss.position;
            boss.position -= Vector3.ProjectOnPlane(boss.forward, Vector3.up).normalized * Mathf.Max(walkDistance, 0f);
            walkProgress = walkDistance > 0f ? 0f : 1f;
            jumpProgress = 0f;
            isIntroWalk = walkDistance > 0f && introStyle != IntroStyle.None;
            cameraRig.SetPositionAndRotation(TrackingPosition, TrackingRotation);
        }

        // 걸음 진행도(0 시작 ~ 1 도착)에 따른 카메라 자리, 도착하면 처음 구도
        private Vector3 WalkCameraPosition(float progress)
        {
            float remaining = 1f - Smootherstep(progress);
            return cameraBase - cameraBack * (walkDolly * remaining) + Vector3.down * (walkDrop * remaining);
        }

        // 소개가 끝나 처음 구도에 선 순간부터 도약 카메라가 보스를 따라감
        // snap이면 따라가기 구도로 바로 옮기고, 아니면 소개가 끝난 자리에서 그대로 이어받음
        private void BeginTracking(bool snap)
        {
            if (snap)
            {
                cameraRig.SetPositionAndRotation(TrackingPosition, TrackingRotation);
                baseFov = cinematicFov;
                lookPoint = bossRoot.position + Vector3.up * lookHeight;
            }
            else
            {
                // 지금 보던 방향 그대로에서 시작해 시선·시야가 따라가기 구도로 서서히 옮겨감
                float distance = Vector3.Distance(cameraRig.position, bossRoot.position + Vector3.up * lookHeight);
                lookPoint = cameraRig.position + cameraRig.forward * distance;
            }
            lastBossPosition = bossRoot.position;
            cameraVelocity = lookVelocity = bossVelocity = Vector3.zero;
            isTracking = true;
        }

        // 지금 자리에서 도약이 진행되는 만큼 도약 카메라 경로로 포물선을 그리며 옮겨붙음
        private void BeginLaunch()
        {
            launchFrom = cameraRig.position;
            launchLookFrom = lookPoint;
            isLaunching = true;
        }

        #region INTRO

        private Vector3 FeetPoint
        {
            get
            {
                Transform left = bossAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform right = bossAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
                return left != null && right != null ? (left.position + right.position) * 0.5f : bossRoot.position;
            }
        }

        private Vector3 HeadPoint
        {
            get
            {
                Transform head = bossAnimator.GetBoneTransform(HumanBodyBones.Head);
                return head != null ? head.position : bossRoot.position + Vector3.up * lookHeight * 2f;
            }
        }

        private float BossHeight => Mathf.Max(HeadPoint.y - bossRoot.position.y, 0.5f);

        // 소개가 끝나 넘어갈 따라가기 구도, 보스가 움직여도 매 프레임 새로 구함
        // 소개 중 걷고 있으면 지금 멈춰 설 자리를 도약 자리로 본 처음 구도, 아니면 그 순간 걸음 진행도의 자리
        private Vector3 TrackingPosition => isIntroWalk
            ? WalkCameraPosition(1f - jumpDollyShare) + Vector3.ProjectOnPlane(IntroWalkLanding - walkTarget, Vector3.up)
            : WalkCameraPosition(CameraApproach);

        // 걸음으로 (1 - jumpDollyShare)까지, 나머지는 도약하며 1까지 가는 카메라 뒷걸음질 진행도
        private float CameraApproach => walkProgress * (1f - jumpDollyShare) + jumpDollyShare * jumpProgress;
        private Quaternion TrackingRotation => Quaternion.LookRotation(bossRoot.position + Vector3.up * lookHeight - TrackingPosition);

        // 지금 걷기를 끊으면 웅크리며 미끄러져 멈출 자리 (WaitForTakeoff에서 속도가 walkGlide 시간으로 줄어듦)
        private Vector3 IntroWalkLanding => bossRoot.position + walkDirection * (walkSpeed * walkGlide);

        // 발끝 가까이에서 시작해 카메라가 올라가며 머리까지 훑음, 이어서 바로 도약을 따라감 (BeginLaunch)
        // 위치는 모두 보스 최상위 기준이라 걸어오는 중이면 카메라가 보스 앞에서 같이 뒷걸음질치며 훑음
        private IEnumerator RevealIntro()
        {
            float height = BossHeight;
            Vector3 front = Vector3.ProjectOnPlane(bossRoot.forward, Vector3.up).normalized;
            Vector3 side = Vector3.Cross(front, Vector3.up); // 보스를 바라보는 카메라 기준 오른쪽
            // 걸음에 흔들리는 뼈를 매 프레임 쫓지 않고, 시작할 때 최상위 기준 높이만 재 둠
            Vector3 feet = Vector3.ProjectOnPlane(FeetPoint - bossRoot.position, Vector3.up) + Vector3.up * (height * 0.04f);
            Vector3 head = HeadPoint - bossRoot.position;
            Vector3 offset = front * (revealDistance * height) + side * (revealSide * height);
            Vector3 lowPosition = feet + Vector3.up * (height * 0.02f) + offset;
            Vector3 highPosition = head + Vector3.down * (height * 0.05f) + offset;

            StartCoroutine(Fade(blackout, 1f, 0f, Mathf.Min(openFade, revealDuration * 0.5f)));
            baseFov = revealFov;

            float elapsed = 0f;
            while (elapsed < revealDuration)
            {
                elapsed += Time.deltaTime;
                float eased = Smootherstep(Mathf.Clamp01(elapsed / revealDuration));
                Vector3 root = bossRoot.position;
                Vector3 position = root + Vector3.Lerp(lowPosition, highPosition, eased);
                Vector3 look = root + Vector3.Lerp(feet, head, eased);
                cameraRig.SetPositionAndRotation(position, Quaternion.LookRotation(look - position));
                yield return null;
            }
        }

        // 정해 둔 부위를 차례로 끊어 보여주고(컷마다 천천히 흐름), 마지막에 따라가기 구도로 바로 넘어감
        // 컷 안에서도 보스 최상위를 따라 움직여 걸어오는 보스가 화면 밖으로 빠져나가지 않음
        private IEnumerator CutsIntro()
        {
            float height = BossHeight;
            Quaternion facing = Quaternion.LookRotation(Vector3.ProjectOnPlane(bossRoot.forward, Vector3.up).normalized);
            bool isFirst = true;

            foreach (IntroCut cut in introCuts)
            {
                Transform bone = bossAnimator.GetBoneTransform(cut.target);
                Vector3 target = (bone != null ? bone.position : bossRoot.position + Vector3.up * lookHeight) - bossRoot.position;
                Vector3 start = target + facing * (cut.offset * height);
                Vector3 end = start + facing * (cut.drift * height);
                baseFov = cut.fov;
                focusDistance = Vector3.Distance(start, target); // 컷이 바뀌자마자 초점이 맞게

                if (isFirst) StartCoroutine(Fade(blackout, 1f, 0f, 0.2f));
                else shake = Mathf.Max(shake, cutShake);
                isFirst = false;

                float elapsed = 0f;
                while (elapsed < cut.duration)
                {
                    elapsed += Time.deltaTime;
                    Vector3 root = bossRoot.position;
                    Vector3 position = root + Vector3.Lerp(start, end, Mathf.Clamp01(elapsed / cut.duration));
                    cameraRig.SetPositionAndRotation(position, Quaternion.LookRotation(root + target - position));
                    yield return null;
                }
            }

            if (isFirst) yield return Fade(blackout, 1f, 0f, openFade);
            else shake = Mathf.Max(shake, cutShake);
            baseFov = cinematicFov;
            cameraRig.SetPositionAndRotation(TrackingPosition, TrackingRotation);
            focusDistance = Vector3.Distance(TrackingPosition, BossFocusPoint);
        }

        #endregion

        // 보스 뼈가 아닌 최상위 위치만 보고 따라가 웅크림·다리 자세에 카메라가 흔들리지 않음
        // 바라보는 점과 몸 위치를 각각 다른 빠르기로 부드럽게 따라붙여, 사람이 든 카메라처럼 고개가 먼저 움직이고 몸이 뒤따름
        private void TrackBoss()
        {
            if (isTracking == false) return;

            // 카메라는 다 오른 높이까지만 따라감, 꼭대기에서 살짝 넘쳤다 내려앉는 것과 공중 출렁임은 화면 속 보스 움직임으로만 남김
            // (따라붙는 시간을 짧게 하면 이 작은 오르내림까지 그대로 따라가 꼭대기에서 화면이 떨려 보임)
            // 위치와 바라보는 점이 같은 순간 멈추고 같은 빠르기로 따라붙어, 움직임이 함께 끝남
            Vector3 anchor = bossRoot.position;
            anchor.y = Mathf.Min(anchor.y, groundHeight + riseTop);
            float crane = Mathf.Max(anchor.y - groundHeight, 0f);

            float deltaTime = Time.deltaTime;
            if (deltaTime > 0f)
            {
                Vector3 velocity = (anchor - lastBossPosition) / deltaTime;
                bossVelocity = Vector3.Lerp(bossVelocity, velocity, 1f - Mathf.Exp(-deltaTime / leadSmooth));
                lastBossPosition = anchor;
            }

            Vector3 targetPosition = WalkCameraPosition(CameraApproach) + Vector3.up * (crane * craneRatio) + cameraBack * (cranePullBack * Mathf.Clamp01(crane / riseHeight));
            // 클로즈업에서 바로 따라가는 중이면 수평으로만 서서히 물러나 경로에 붙고, 높이는 처음 자리에서 살짝 내려앉다가
            // 보스가 오르기 시작하면 그 높이를 곧바로 따라 올라감 → 아래로 오목한 포물선, 보스를 놓치지 않음
            if (isLaunching)
            {
                Vector3 launch = Vector3.Lerp(launchFrom, targetPosition, jumpProgress);
                launch.y = Mathf.Max(Mathf.Lerp(launchFrom.y, targetPosition.y, jumpProgress), targetPosition.y);
                targetPosition = launch;
            }

            Vector3 targetLook = anchor + Vector3.up * lookHeight + bossVelocity * lookLead;
            if (isLaunching)
            {
                // 땅을 차는 자리의 발밑 (보스가 떠도 땅에 남음)
                Vector3 groundLook = new(anchor.x, groundHeight + revealLaunchLook, anchor.z);
                if (RiseFraction <= 0f)
                {
                    float crouch = jumpCrouchShare > 0f ? Mathf.Clamp01(jumpProgress / jumpCrouchShare) : 1f;
                    targetLook = Vector3.Lerp(launchLookFrom, groundLook, Smootherstep(crouch));
                }
                else
                {
                    float follow = RiseFraction;
                    targetLook = Vector3.Lerp(groundLook, targetLook, follow * follow * (3f - 2f * follow));
                }
            }
            float targetFov = riseFovKick * Mathf.Clamp01(Mathf.Max(bossVelocity.y, 0f) / riseFovSpeed);
            riseFov = Mathf.Lerp(riseFov, targetFov, 1f - Mathf.Exp(-deltaTime / craneSmooth));
            baseFov = Mathf.Lerp(baseFov, cinematicFov, 1f - Mathf.Exp(-deltaTime / craneSmooth));

            Vector3 position = Vector3.SmoothDamp(cameraRig.position, targetPosition, ref cameraVelocity, craneSmooth);
            lookPoint = Vector3.SmoothDamp(lookPoint, targetLook, ref lookVelocity, craneSmooth);
            cameraRig.SetPositionAndRotation(position, Quaternion.LookRotation(lookPoint - position));
        }

        // 점프 시작 클립은 높이가 0으로 고정돼 웅크릴 때 발이 바닥 아래로 내려갈 수 있음
        // 서 있는 자세의 발목 높이보다 내려가면 그만큼 골반을 올려 발이 바닥에 묻히지 않게 함 (떠오른 뒤엔 해당 없음)
        private void KeepFeetAboveGround()
        {
            if (bossRoot == null || bossAnimator.isHuman == false) return;

            Transform left = bossAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform right = bossAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
            Transform hips = bossAnimator.GetBoneTransform(HumanBodyBones.Hips);
            if (left == null || right == null || hips == null) return;

            float lowest = Mathf.Min(left.position.y, right.position.y) - groundHeight;
            if (ankleRest < 0f)
            {
                ankleRest = lowest;
                return;
            }

            float sink = ankleRest - lowest;
            if (sink > 0f) hips.position += Vector3.up * sink;
        }

        // 부모 뼈부터 굽혀야 자식이 따라 움직인 뒤 자기 자리에서 다시 굽혀짐
        private void ApplyLevitationPose()
        {
            if (levitation <= 0f || bossAnimator.isHuman == false) return;

            Vector3 axis = bossRoot.right;
            float sway = Mathf.Sin(Time.time * hoverFrequency * Mathf.PI * 2f + 1f) * legSwayAngle;

            Bend(HumanBodyBones.LeftUpperLeg, thighAngles.x + sway, axis);
            Bend(HumanBodyBones.LeftLowerLeg, kneeAngles.x - sway * 0.5f, axis);
            PointToes(HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes, axis);
            Bend(HumanBodyBones.RightUpperLeg, thighAngles.y - sway, axis);
            Bend(HumanBodyBones.RightLowerLeg, kneeAngles.y + sway * 0.5f, axis);
            PointToes(HumanBodyBones.RightFoot, HumanBodyBones.RightToes, axis);
        }

        // 발끝(발가락 뼈 쪽)을 보스 정면에서 footAngle만큼 아래로 향하게 돌림, 발가락 뼈가 없으면 좌우축으로만 굽힘
        private void PointToes(HumanBodyBones footId, HumanBodyBones toesId, Vector3 axis)
        {
            Transform foot = bossAnimator.GetBoneTransform(footId);
            Transform toes = bossAnimator.GetBoneTransform(toesId);
            if (foot == null) return;

            if (toes == null)
            {
                Bend(footId, footAngle, axis);
                return;
            }

            Vector3 current = toes.position - foot.position;
            if (current.sqrMagnitude < 0.000001f) return;

            Vector3 forward = Vector3.ProjectOnPlane(bossRoot.forward, Vector3.up).normalized;
            Vector3 desired = Quaternion.AngleAxis(footAngle, axis) * forward;
            Quaternion turn = Quaternion.FromToRotation(current.normalized, desired);
            foot.rotation = Quaternion.Slerp(Quaternion.identity, turn, levitation) * foot.rotation;
        }

        private void Bend(HumanBodyBones boneId, float angle, Vector3 axis)
        {
            Transform bone = bossAnimator.GetBoneTransform(boneId);
            if (bone != null) bone.rotation = Quaternion.AngleAxis(angle * levitation, axis) * bone.rotation;
        }

        // 카메라가 보스를 올려다봐도 가상 플레이어는 수평 방향 기준으로 카메라 뒤에 둠
        private Vector3 VirtualPlayer => cameraRig.position + Quaternion.Euler(0f, cameraRig.eulerAngles.y, 0f) * virtualPlayerOffset;

        private void OnDrawGizmosSelected()
        {
            if (cameraRig == null) return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(VirtualPlayer, 0.3f);
            if (bossRoot != null) Gizmos.DrawLine(VirtualPlayer, StrikePoint); // 보스 최상위는 재생 중에만 잡혀 있음
        }

        // 보스 모델(애니메이터)은 3.25배로 키워져 있어 TransformPoint를 쓰면 거리가 같이 커짐 → 스케일 없이 m 단위로 (HumanoidGuardCounter와 같은 방식)
        private Vector3 StrikePoint => bossRoot.position + bossRoot.rotation * strikeOffset;

        // 슬로모션에 손떨림도 같이 느려지도록 게임 시간으로 진행, 흔들림·반동·FOV 반동은 실제 시간으로 가라앉음
        private void UpdateCamera()
        {
            if (viewCamera == null) return;

            float deltaTime = Time.unscaledDeltaTime;
            float noiseTime = Time.time * handheldFrequency;
            Vector3 handheld = new Vector3(
                Mathf.PerlinNoise(noiseTime, 0.3f) - 0.5f,
                Mathf.PerlinNoise(0.7f, noiseTime) - 0.5f,
                (Mathf.PerlinNoise(noiseTime, noiseTime + 5f) - 0.5f) * 0.5f) * (2f * currentHandheld);

            Vector3 shaking = Random.insideUnitSphere * (shakeAngle * shake * shake);
            shake = Mathf.MoveTowards(shake, 0f, shakeDecay * shake * deltaTime + 0.1f * deltaTime);

            float settle = 1f - Mathf.Exp(-recoilReturn * deltaTime);
            recoil = Vector3.Lerp(recoil, Vector3.zero, settle);
            fovKick = Mathf.Lerp(fovKick, 0f, settle);
            cameraOffset = Vector3.Lerp(cameraOffset, Vector3.zero, settle);

            viewCamera.transform.localRotation = Quaternion.Euler(handheld + recoil + shaking);
            viewCamera.transform.localPosition = cameraOffset;
            viewCamera.fieldOfView = baseFov + fovKick + (isTracking ? riseFov : 0f);
        }

        private void CreateVolumes()
        {
            focusVolume = CreateVolume(100f, out VolumeProfile focusProfile);
            depthOfField = focusProfile.Add<DepthOfField>(true);
            depthOfField.mode.value = DepthOfFieldMode.Bokeh;
            depthOfField.focalLength.value = focalLength;
            depthOfField.aperture.value = aperture;
            focusVolume.weight = useDepthOfField ? 1f : 0f;
            focusDistance = Vector3.Distance(viewCamera.transform.position, BossFocusPoint);

            // 가중치 0~1로 원래 볼륨 값과 섞이므로 평소엔 0으로 두고 받아칠 때만 올림
            impactVolume = CreateVolume(101f, out VolumeProfile impactProfile);
            impactProfile.Add<ChromaticAberration>(true).intensity.value = impactChromatic;
            // 가중치로 원래 볼륨 값과 섞이므로 노출만 덮고, 원래 노출(Hangars 1.44 등)에 더한 값으로 둠
            impactProfile.Add<ColorAdjustments>().postExposure.Override(FindBaseExposure() + impactExposure);
            Vignette vignette = impactProfile.Add<Vignette>(true);
            vignette.intensity.value = impactVignette;
            vignette.smoothness.value = 0.5f;
            vignette.color.value = Color.black;
            impactVolume.weight = 0f;
        }

        private float FindBaseExposure()
        {
            float exposure = 0f;
            float priority = float.MinValue;

            foreach (Volume volume in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (volume == focusVolume || volume == impactVolume || volume.isGlobal == false || volume.sharedProfile == null) continue;
                if (volume.priority < priority || volume.sharedProfile.TryGet(out ColorAdjustments adjustments) == false) continue;
                if (adjustments.postExposure.overrideState == false) continue;

                exposure = adjustments.postExposure.value;
                priority = volume.priority;
            }

            return exposure;
        }

        // 아레나에서 가져온 볼륨은 남은 효과(색수차·감마 등)가 섞이지 않게 아예 끄고 로비 후처리 하나만 씀
        private void UseMenuVolume()
        {
            if (menuProfile == null) return;

            foreach (Volume volume in FindObjectsByType<Volume>(FindObjectsSortMode.None))
                if (volume != focusVolume && volume != impactVolume) volume.enabled = false;

            Volume menuVolume = gameObject.AddComponent<Volume>();
            menuVolume.isGlobal = true;
            menuVolume.priority = 102f;
            menuVolume.sharedProfile = menuProfile;
        }

        private Volume CreateVolume(float priority, out VolumeProfile profile)
        {
            Volume volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = priority;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;
            return volume;
        }

        // 평소엔 보스 몸에 초점을 부드럽게 맞추고, 받아친 물체가 날아오는 동안은 물체에 바로 붙임 (50m/s라 따라가면 늦음)
        private void UpdateFocus()
        {
            if (impactVolume != null)
            {
                impactVolume.weight = impactPulse;
                impactPulse = Mathf.MoveTowards(impactPulse, 0f, Time.unscaledDeltaTime / impactDecay);
            }

            if (depthOfField == null || bossRoot == null) return;

            Vector3 eye = viewCamera.transform.position;
            if (isFocusOnProjectile)
                focusDistance = Vector3.Distance(eye, projectile.position);
            else
                focusDistance = Mathf.Lerp(focusDistance, Vector3.Distance(eye, BossFocusPoint), 1f - Mathf.Exp(-focusSpeed * Time.unscaledDeltaTime));

            depthOfField.focusDistance.value = Mathf.Max(focusDistance, 0.1f);
        }

        private Vector3 BossFocusPoint => bossRoot.position + Vector3.up * bossFocusHeight;

        private IEnumerator HitStop(float duration)
        {
            if (duration <= 0f) yield break;

            float previous = Time.timeScale;
            Time.timeScale = 0.02f;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = previous;
        }

        #endregion

        #region UTILITIES

        // 한 프레임이 길어도(로딩 등) 1/30초씩만 진행해 페이드가 건너뛰어지지 않음
        private static IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
                SetAlpha(group, Mathf.Lerp(from, to, elapsed / duration));
                yield return null;
            }

            SetAlpha(group, to);
        }

        private static float Smootherstep(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        private static float Segment(float time, Vector2 range) => Mathf.Clamp01((time - range.x) / Mathf.Max(range.y, 0.0001f));

        private static float EaseOutCubic(float t)
        {
            t = 1f - Mathf.Clamp01(t);
            return 1f - t * t * t;
        }

        private static void SetAlpha(CanvasGroup group, float alpha)
        {
            if (group != null) group.alpha = alpha;
        }

        private static void SetInput(CanvasGroup group, bool enabled)
        {
            if (group == null) return;

            group.interactable = enabled;
            group.blocksRaycasts = enabled;
        }

        #endregion
    }
}
