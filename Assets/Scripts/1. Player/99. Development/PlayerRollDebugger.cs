using System.Collections.Generic;
using System.Reflection;
using Study.Utilities;
using Study3D.FpsGame;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TeamOdd.Player.Development
{
    // 구르기 디버깅용 오버레이
    // - 구르기 시작/종료 시점, 이동 거리, 소요 시간, 애니메이션 진행도를 기록
    // - 애니메이션 상태 이벤트(EndRolling 등)를 시간순으로 기록
    // - 구르기가 끝나지 않거나 Roll 상태로 진입하지 못하면 경고
    // 조작: F1 HUD, F2 스태미나 채우기, F3 무한 스태미나, F4 위치 초기화, F5 구르기 강제 종료, [ ] 시간 배속, \ 배속 초기화
    public class PlayerRollDebugger : MonoBehaviour
    {
        private static readonly int RollingParam = Animator.StringToHash("Rolling");

        [Header("HUD")]
        [SerializeField] private bool showHud = true;
        [SerializeField] private Vector2 hudPosition = new(10f, 10f);
        [SerializeField, Min(100f)] private float hudWidth = 720f;
        [SerializeField, Min(0f)] private float hudHeight; // 0이면 화면 아래 끝까지
        [SerializeField, Min(8)] private int hudFontSize = 14;
        [SerializeField] private bool infiniteStamina;
        [SerializeField, Min(1)] private int maxRecordCount = 6;
        [SerializeField, Min(1)] private int maxEventLogCount = 10;

        [Header("입력 표시")]
        [SerializeField] private bool showInputPanel = true;
        [SerializeField, Min(0.05f)] private float keyDownHighlightTime = 0.3f; // 눌린 순간을 이 시간 동안 강조
        [SerializeField, Min(1)] private int maxRollRequestLogCount = 6;

        [Header("경고")]
        [SerializeField, Min(0.1f)] private float stuckRollWarningTime = 2f; // IsRolling이 이 시간 이상 유지되면 경고
        [SerializeField, Min(0.05f)] private float rollStateEnterTimeout = 0.3f; // 이 시간 안에 Roll 상태로 진입하지 못하면 경고

        [Header("애니메이터 상태 이름 (HUD 표시용)")]
        [SerializeField] private string rollStateName = "Roll";
        [SerializeField] private string[] stateNames =
        {
            "Anim_Idle", "Walk", "Run", "Roll", "Dead",
            "Unarmed-Knockback-Back1", "Unarmed-Knockdown1", "Unarmed-Revive1"
        };

        private struct RollRecord
        {
            public float Distance;
            public float Duration;
            public float StaminaBefore;
            public float EndNormalizedTime;
            public bool EnteredRollState;
            public string EndReason;
        }

        private class KeyState
        {
            public readonly Key Key;
            public readonly string Label;
            public float LastDownTime = float.NegativeInfinity;
            public int DownCount;

            public KeyState(Key key, string label)
            {
                Key = key;
                Label = label;
            }
        }

        // 구르기 판정에 쓰이는 키들 (이동은 SimpleInput.GetMoveAxisRaw 기준 WASD + 방향키)
        private readonly KeyState[] keyStates =
        {
            new(Key.LeftShift, "LShift"), new(Key.G, "G"),
            new(Key.W, "W"), new(Key.A, "A"), new(Key.S, "S"), new(Key.D, "D"),
            new(Key.UpArrow, "Up"), new(Key.LeftArrow, "Left"), new(Key.DownArrow, "Down"), new(Key.RightArrow, "Right")
        };

        private readonly List<string> rollRequestLog = new();
        private int rollRequestCount;
        private int rollAcceptCount;

        private Player player;
        private PlayerController playerController;
        private Animator animator;
        private AnimStateEventListener eventListener;

        private readonly Dictionary<int, string> stateNameByHash = new();
        private readonly List<RollRecord> records = new();
        private readonly List<string> eventLog = new();
        private readonly List<Vector3> rollPath = new();

        private float expectedRollDistance = -1f;
        private float expectedRollDuration = -1f;

        private Vector3 resetPosition;
        private Quaternion resetRotation;

        private bool wasRolling;
        private float rollStartTime;
        private float rollStartStamina;
        private Vector3 rollStartPosition;
        private bool enteredRollState;
        private float lastRollNormalizedTime;
        private string lastRollEvent;

        private GUIStyle boxStyle;

        private void Start()
        {
            player = Player.Instance;
            playerController = player.PlayerController;
            animator = player.GetComponent<Animator>();
            eventListener = player.GetComponent<AnimStateEventListener>();

            eventListener.OnOccursAnimStateEvent += HandleAnimStateEvent;
            // Player가 OnEnable에서 먼저 구독하므로, 이 핸들러가 불릴 때는 수락 여부가 이미 결정돼 있음
            playerController.OnRollRequested += HandleRollRequested;

            foreach (string stateName in stateNames)
                stateNameByHash[Animator.StringToHash(stateName)] = stateName;

            ReadRollSettings();

            resetPosition = player.transform.position;
            resetRotation = player.transform.rotation;
        }

        private void OnDestroy()
        {
            if (eventListener != null) eventListener.OnOccursAnimStateEvent -= HandleAnimStateEvent;
            if (playerController != null) playerController.OnRollRequested -= HandleRollRequested;
            Time.timeScale = 1f;
        }

        private void Update()
        {
            HandleHotkeys();
            TrackKeys();

            if (infiniteStamina) RefillStamina();

            TrackRoll();
        }

        #region 구르기 추적

        private void TrackRoll()
        {
            bool isRolling = playerController.IsRolling;

            if (isRolling && wasRolling == false) BeginRecord();

            if (isRolling)
            {
                AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
                if (info.IsName(rollStateName))
                {
                    enteredRollState = true;
                    lastRollNormalizedTime = info.normalizedTime;
                }

                rollPath.Add(player.transform.position);
            }

            if (isRolling == false && wasRolling) EndRecord(lastRollEvent ?? "IsRolling 해제");

            wasRolling = isRolling;
        }

        private void BeginRecord()
        {
            rollStartTime = Time.time;
            // HandleRollRequested에서 스태미나를 먼저 소모하므로 소모량을 더해 직전 값으로 복원
            rollStartStamina = player.Stamina + playerController.NeedRollStamina;
            rollStartPosition = player.transform.position;
            enteredRollState = false;
            lastRollNormalizedTime = 0f;
            lastRollEvent = null;

            rollPath.Clear();
            rollPath.Add(rollStartPosition);
        }

        private void EndRecord(string reason)
        {
            Vector3 delta = player.transform.position - rollStartPosition;
            delta.y = 0f;

            records.Insert(0, new RollRecord
            {
                Distance = delta.magnitude,
                Duration = Time.time - rollStartTime,
                StaminaBefore = rollStartStamina,
                EndNormalizedTime = lastRollNormalizedTime,
                EnteredRollState = enteredRollState,
                EndReason = reason
            });

            if (records.Count > maxRecordCount) records.RemoveAt(records.Count - 1);
        }

        private void HandleAnimStateEvent(string eventName, string parameter)
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            string log = $"{Time.time:F2}s  {eventName}  ({GetStateName(info)} {info.normalizedTime:F2})";

            eventLog.Insert(0, log);
            if (eventLog.Count > maxEventLogCount) eventLog.RemoveAt(eventLog.Count - 1);

            if (eventName == "EndRolling") lastRollEvent = "EndRolling 이벤트";
        }

        #endregion

        #region 입력 추적

        private void TrackKeys()
        {
            foreach (KeyState state in keyStates)
            {
                if (state.Key.GetKeyDown() == false) continue;

                state.LastDownTime = Time.unscaledTime;
                state.DownCount++;
            }
        }

        private void HandleRollRequested()
        {
            rollRequestCount++;

            string result;
            if (playerController.IsRolling)
            {
                rollAcceptCount++;
                result = "<color=lime>수락</color>";
            }
            else
            {
                // Player.HandleRollRequested의 거부 조건과 맞춰야 함
                result = player.Stamina < 100f
                    ? $"<color=red>거부: 스태미나 {player.Stamina:F1} < 100</color>"
                    : "<color=red>거부: 원인 불명</color>";
            }

            string log = $"{Time.time:F2}s  Shift {(Key.LeftShift.GetKey() ? "O" : "X")}  " +
                         $"이동 {SimpleInput.GetMoveAxisRaw()}  달리기 {(playerController.IsSprinting ? "O" : "X")}  {result}";

            rollRequestLog.Insert(0, log);
            if (rollRequestLog.Count > maxRollRequestLogCount) rollRequestLog.RemoveAt(rollRequestLog.Count - 1);
        }

        #endregion

        #region 단축키

        private void HandleHotkeys()
        {
            if (Key.F1.GetKeyDown()) showHud = showHud == false;
            if (Key.F2.GetKeyDown()) RefillStamina();
            if (Key.F3.GetKeyDown()) infiniteStamina = infiniteStamina == false;
            if (Key.F4.GetKeyDown()) ResetPlayer();
            if (Key.F5.GetKeyDown() && playerController.IsRolling)
            {
                lastRollEvent = "F5 강제 종료";
                playerController.EndRoll();
            }

            if (Key.LeftBracket.GetKeyDown()) Time.timeScale = Mathf.Max(0.1f, Time.timeScale - 0.1f);
            if (Key.RightBracket.GetKeyDown()) Time.timeScale = Mathf.Min(1f, Time.timeScale + 0.1f);
            if (Key.Backslash.GetKeyDown()) Time.timeScale = 1f;
        }

        private void RefillStamina()
        {
            float missing = player.MaxStamina - player.Stamina;
            if (missing > 0f) player.ConsumeStamina(-missing);
        }

        private void ResetPlayer()
        {
            // CharacterController가 켜져 있으면 위치를 직접 바꿔도 덮어써짐
            CharacterController controller = player.CharacterController;
            controller.enabled = false;
            player.transform.SetPositionAndRotation(resetPosition, resetRotation);
            controller.enabled = true;

            rollPath.Clear();
        }

        #endregion

        #region HUD

        private void OnGUI()
        {
            if (showHud == false) return;

            boxStyle ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, richText = true };
            boxStyle.fontSize = hudFontSize; // 플레이 중 인스펙터에서 바꿔도 바로 반영

            float height = hudHeight > 0f ? hudHeight : Screen.height - hudPosition.y - 10f;

            GUILayout.BeginArea(new Rect(hudPosition.x, hudPosition.y, hudWidth, height));
            GUILayout.Box(BuildStatusText(), boxStyle);
            if (showInputPanel) GUILayout.Box(BuildInputText(), boxStyle);
            GUILayout.Box(BuildRecordText(), boxStyle);
            GUILayout.Box(BuildEventLogText(), boxStyle);
            GUILayout.Box(
                "F1 HUD | F2 스태미나 채우기 | F3 무한 스태미나 | F4 위치 초기화\n" +
                "F5 구르기 강제 종료 | [ ] 시간 배속 | \\ 배속 초기화 | G 구르기", boxStyle);
            GUILayout.EndArea();
        }

        private string BuildStatusText()
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            string transition = animator.IsInTransition(0)
                ? $" -> {GetStateName(animator.GetNextAnimatorStateInfo(0))}"
                : string.Empty;

            // Player.HandleRollRequested의 조건과 동일하게 맞춰야 함
            bool hasStamina = player.Stamina >= 100f;
            string staminaColor = hasStamina ? "lime" : "red";

            string text =
                "<b>구르기 상태</b>\n" +
                $"IsRolling: {Colored(playerController.IsRolling)}   IsInvincible: {Colored(playerController.IsInvincible)}   isHit: {Colored(playerController.isHit)}\n" +
                $"스태미나: <color={staminaColor}>{player.Stamina:F1}</color> / {player.MaxStamina:F0}   소모량: {playerController.NeedRollStamina:F0}   (구르기 조건: 100 이상)\n" +
                $"무한 스태미나: {Colored(infiniteStamina)}   시간 배속: {Time.timeScale:F1}   컨트롤러 활성: {Colored(playerController.enabled)}\n" +
                $"애니메이터: {GetStateName(info)} {info.normalizedTime:F2}{transition}   Rolling 트리거 대기: {Colored(animator.GetBool(RollingParam))}";

            if (expectedRollDistance > 0f)
                text += $"\n설정값: 거리 {expectedRollDistance:F2}m / 시간 {expectedRollDuration:F2}s -> 속도 {expectedRollDistance / expectedRollDuration:F2}m/s";

            if (playerController.IsRolling)
            {
                float elapsed = Time.time - rollStartTime;
                text += $"\n진행 중: {elapsed:F2}s";

                if (elapsed > stuckRollWarningTime)
                    text += $"\n<color=red>[!] {stuckRollWarningTime:F1}초 넘게 IsRolling 유지 (EndRolling 이벤트 미수신?)</color>";
                if (enteredRollState == false && elapsed > rollStateEnterTimeout)
                    text += $"\n<color=red>[!] '{rollStateName}' 상태로 진입하지 못함 (트리거/전이 확인)</color>";
            }

            if (playerController.enabled == false)
                text += "\n<color=yellow>커서 잠금 해제로 PlayerController가 꺼져 있음 (화면 클릭)</color>";

            return text;
        }

        // 키 하드웨어 입력(눌림) -> 구르기 요청(OnRollRequested) -> 수락(IsRolling) 단계를 나눠서 보여줌
        private string BuildInputText()
        {
            string text = "<b>입력</b>  (<color=lime>초록</color> 누르는 중  <color=yellow>노랑</color> 방금 눌림  <color=grey>회색</color> 안 눌림)\n";

            foreach (KeyState state in keyStates)
            {
                bool isPressed = state.Key.GetKey();
                bool justDown = Time.unscaledTime - state.LastDownTime <= keyDownHighlightTime;
                string color = justDown ? "yellow" : isPressed ? "lime" : "grey";

                text += $"<color={color}>[{state.Label}]</color> ";
            }

            KeyState gKey = keyStates[1];
            text += $"\n이동 축: {SimpleInput.GetMoveAxisRaw()}   달리기(IsSprinting): {Colored(playerController.IsSprinting)}" +
                    $"\nG 눌림 {gKey.DownCount}회 -> 구르기 요청 {rollRequestCount}회 -> 수락 {rollAcceptCount}회";

            if (gKey.DownCount > rollRequestCount)
                text += "\n<color=yellow>G가 눌렸는데 요청이 안 간 적 있음 (구르기/피격 중이거나 컨트롤러 비활성)</color>";

            if (rollRequestLog.Count > 0)
            {
                text += "\n<b>구르기 요청 기록</b> (최신순)";
                foreach (string log in rollRequestLog)
                    text += "\n" + log;
            }

            return text;
        }

        private string BuildRecordText()
        {
            string text = "<b>최근 구르기 기록</b> (최신순)";
            if (records.Count == 0) return text + "\n없음";

            foreach (RollRecord record in records)
            {
                // 실제 이동 거리는 '설정 속도 x 실제 구른 시간'이라 애니메이션 길이가 rollDuration과 다르면 어긋남
                string distanceColor = expectedRollDistance > 0f &&
                                       Mathf.Abs(record.Distance - expectedRollDistance) > 0.5f
                    ? "yellow"
                    : "white";

                text += $"\n<color={distanceColor}>{record.Distance:F2}m</color>  {record.Duration:F2}s  " +
                        $"애니 {record.EndNormalizedTime:F2}  스태미나 {record.StaminaBefore:F0}  " +
                        $"{(record.EnteredRollState ? string.Empty : "<color=red>Roll 미진입</color>  ")}{record.EndReason}";
            }

            return text;
        }

        private string BuildEventLogText()
        {
            string text = "<b>애니메이션 상태 이벤트</b>";
            if (eventLog.Count == 0) return text + "\n없음";

            foreach (string log in eventLog)
                text += "\n" + log;

            return text;
        }

        private string GetStateName(AnimatorStateInfo info)
        {
            return stateNameByHash.TryGetValue(info.shortNameHash, out string stateName) ? stateName : "(알 수 없음)";
        }

        private static string Colored(bool value)
        {
            return value ? "<color=lime>true</color>" : "<color=grey>false</color>";
        }

        #endregion

        // rollDistance/rollDuration은 private 필드라 디버그 표시용으로만 리플렉션으로 읽음
        private void ReadRollSettings()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;

            FieldInfo distanceField = typeof(PlayerController).GetField("rollDistance", flags);
            FieldInfo durationField = typeof(PlayerController).GetField("rollDuration", flags);

            if (distanceField != null) expectedRollDistance = (float)distanceField.GetValue(playerController);
            if (durationField != null) expectedRollDuration = (float)durationField.GetValue(playerController);
        }

        private void OnDrawGizmos()
        {
            if (rollPath.Count < 2) return;

            Gizmos.color = Color.cyan;
            for (int i = 1; i < rollPath.Count; i++)
                Gizmos.DrawLine(rollPath[i - 1], rollPath[i]);

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(rollPath[0], 0.15f);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(rollPath[^1], 0.15f);
        }
    }
}
