using System.Collections.Generic;
using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TeamOdd.Player.Development
{
    // 구르기 속도 커브 디버깅용 그래프
    // - 하늘색: 커브로 계산한 목표 속도, 주황색: 실제 이동 속도(이번/마지막 구르기)
    // - 세로선: 회색 = moveEndTime(이동 끝), 노랑 = 현재 진행도, 빨강 = 구르기가 끝난 진행도
    // 조작: F6 커브 켜기/끄기(끄면 기존 고정 속도), F7 기록 지우기, F8 패널 표시
    public class RollCurveDebugger : MonoBehaviour
    {
        private const int CurveDotCount = 120;

        [SerializeField] private PlayerRollCurve rollCurve; // 비어 있으면 씬에서 찾음

        [Header("패널")]
        [SerializeField] private bool showPanel = true;
        [SerializeField] private bool alignRight = true; // 화면 오른쪽 기준으로 배치
        [SerializeField] private Vector2 panelOffset = new(10f, 10f);
        [SerializeField] private Vector2 graphSize = new(420f, 200f);
        [SerializeField, Min(8)] private int fontSize = 14;
        [SerializeField, Min(1f)] private float minGraphMaxSpeed = 5f; // 그래프 Y축 최소 상한(m/s)

        private struct Sample
        {
            public float NormalizedTime;
            public float Speed;
        }

        private Player player;
        private PlayerController playerController;

        private readonly List<Sample> samples = new();
        private bool wasRolling;
        private Vector3 lastPosition;
        private Vector3 rollStartPosition;

        private float lastRollDistance = -1f;
        private float lastRollEndTime = -1f; // 구르기가 끝난 시점의 Roll 진행도
        private float peakActualSpeed;

        private GUIStyle textStyle;

        private void Start()
        {
            player = Player.Instance;
            playerController = player.PlayerController;
            if (rollCurve == null) rollCurve = FindAnyObjectByType<PlayerRollCurve>();
        }

        private void Update()
        {
            if (Key.F6.GetKeyDown() && rollCurve != null) rollCurve.enabled = rollCurve.enabled == false;
            if (Key.F7.GetKeyDown()) ClearSamples();
            if (Key.F8.GetKeyDown()) showPanel = showPanel == false;
        }

        // PlayerController.Update에서 이동한 뒤의 위치로 실제 속도를 계산
        private void LateUpdate()
        {
            bool isRolling = playerController.IsRolling;
            Vector3 position = player.transform.position;

            if (isRolling && wasRolling == false)
            {
                // 구르기 요청이 들어온 프레임은 아직 구르기 이동을 하지 않았으므로 기록하지 않음
                ClearSamples();
                rollStartPosition = position;
            }
            else if (isRolling && Time.deltaTime > 0f)
            {
                Vector3 delta = position - lastPosition;
                delta.y = 0f;

                float speed = delta.magnitude / Time.deltaTime;
                peakActualSpeed = Mathf.Max(peakActualSpeed, speed);

                if (rollCurve != null && rollCurve.enabled)
                    samples.Add(new Sample { NormalizedTime = rollCurve.CurrentNormalizedTime, Speed = speed });
            }

            if (isRolling == false && wasRolling)
            {
                Vector3 total = position - rollStartPosition;
                total.y = 0f;
                lastRollDistance = total.magnitude;
                lastRollEndTime = rollCurve != null && rollCurve.enabled ? rollCurve.CurrentNormalizedTime : -1f;
            }

            lastPosition = position;
            wasRolling = isRolling;
        }

        private void ClearSamples()
        {
            samples.Clear();
            peakActualSpeed = 0f;
        }

        #region 패널

        private void OnGUI()
        {
            if (showPanel == false) return;

            textStyle ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, richText = true };
            textStyle.fontSize = fontSize;

            float x = alignRight ? Screen.width - graphSize.x - panelOffset.x : panelOffset.x;
            Rect graphRect = new(x, panelOffset.y, graphSize.x, graphSize.y);

            if (rollCurve == null)
            {
                GUI.Box(graphRect, "<color=red>PlayerRollCurve를 찾지 못함</color>", textStyle);
                return;
            }

            DrawGraph(graphRect);

            string info = BuildInfoText();
            float infoHeight = textStyle.CalcHeight(new GUIContent(info), graphSize.x);
            GUI.Box(new Rect(x, graphRect.yMax + 4f, graphSize.x, infoHeight), info, textStyle);
        }

        private void DrawGraph(Rect rect)
        {
            GUI.Box(rect, GUIContent.none);

            float maxSpeed = Mathf.Max(minGraphMaxSpeed, GetMaxExpectedSpeed() * 1.2f, peakActualSpeed * 1.1f);

            // 이동 끝, 현재 진행도, 구르기가 끝난 진행도 세로선
            DrawVerticalLine(rect, rollCurve.MoveEndTime, Color.grey);
            if (playerController.IsRolling) DrawVerticalLine(rect, rollCurve.CurrentNormalizedTime, Color.yellow);
            else if (lastRollEndTime >= 0f) DrawVerticalLine(rect, lastRollEndTime, Color.red);

            // 목표 속도
            for (int i = 0; i <= CurveDotCount; i++)
            {
                float t = (float)i / CurveDotCount;
                DrawDot(rect, t, rollCurve.GetSpeedAt(t) / maxSpeed, 2f, Color.cyan);
            }

            // 실제 속도
            foreach (Sample sample in samples)
                DrawDot(rect, sample.NormalizedTime, sample.Speed / maxSpeed, 4f, new Color(1f, 0.55f, 0.1f));

            GUI.Label(new Rect(rect.x + 4f, rect.y + 2f, 120f, 20f), $"{maxSpeed:F1} m/s");
            GUI.Label(new Rect(rect.xMax - 70f, rect.yMax - 20f, 70f, 20f), "진행도 1.0");
        }

        private string BuildInfoText()
        {
            float moveSeconds = rollCurve.RollStateLength * rollCurve.MoveEndTime;
            string curveState = rollCurve.enabled
                ? "<color=lime>커브 사용</color>"
                : "<color=grey>커브 꺼짐 (고정 속도)</color>";

            string text =
                $"<b>구르기 속도 커브</b>  {curveState}\n" +
                $"목표 거리 {rollCurve.RollDistance:F2}m   이동 구간 0~{rollCurve.MoveEndTime:F2} ({moveSeconds:F2}s)   Roll 길이 {rollCurve.RollStateLength:F2}s\n" +
                $"목표 최고 속도 {GetMaxExpectedSpeed():F2}m/s   실제 최고 속도 {peakActualSpeed:F2}m/s";

            if (playerController.IsRolling)
            {
                string stateText = rollCurve.IsInRollState ? "Roll" : "<color=yellow>Roll 진입 전</color>";
                text += $"\n진행도 {rollCurve.CurrentNormalizedTime:F2} ({stateText})   현재 목표 속도 {rollCurve.CurrentSpeed:F2}m/s";
            }

            if (lastRollDistance >= 0f)
            {
                text += $"\n마지막 구르기: 실제 거리 {lastRollDistance:F2}m";
                if (lastRollEndTime >= 0f) text += $", 끝난 진행도 {lastRollEndTime:F2}";

                // 이동 구간이 끝나기 전에 IsRolling이 꺼지면 커브 뒷부분이 잘려 거리가 모자람
                if (lastRollEndTime >= 0f && lastRollEndTime < rollCurve.MoveEndTime)
                    text += $"\n<color=red>[!] 이동 구간({rollCurve.MoveEndTime:F2}) 전에 구르기가 끝남 -> " +
                            "EndRolling 이벤트 시점을 늦추거나 moveEndTime을 줄이세요</color>";
                else if (rollCurve.enabled && Mathf.Abs(lastRollDistance - rollCurve.RollDistance) > 0.3f)
                    text += "\n<color=yellow>목표 거리와 0.3m 넘게 차이 (벽/경사, 시간 배속 변경 확인)</color>";
            }

            text += "\nF6 커브 켜기/끄기 | F7 기록 지우기 | F8 패널";
            return text;
        }

        private float GetMaxExpectedSpeed()
        {
            float max = 0f;
            for (int i = 0; i <= CurveDotCount; i++)
                max = Mathf.Max(max, rollCurve.GetSpeedAt((float)i / CurveDotCount));

            return max;
        }

        private static void DrawDot(Rect rect, float normalizedX, float normalizedY, float size, Color color)
        {
            float px = rect.x + Mathf.Clamp01(normalizedX) * rect.width;
            float py = rect.yMax - Mathf.Clamp01(normalizedY) * rect.height;

            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(px - size * 0.5f, py - size * 0.5f, size, size), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void DrawVerticalLine(Rect rect, float normalizedX, Color color)
        {
            float px = rect.x + Mathf.Clamp01(normalizedX) * rect.width;

            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(px - 1f, rect.y, 2f, rect.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        #endregion
    }
}
