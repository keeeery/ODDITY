using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd
{
    // 차원 가르기 화면 연출: 카메라 화면을 찍어 가르는 선 기준 두 장으로 나눠 선을 따라 서로 어긋나게 보여 줌
    // 세상이 거의 멈춰 있어 렌더러 기능 없이 UI로 처리, HUD(다른 오버레이 캔버스)는 이보다 위에 그려져 갈라지지 않음
    // 가르는 선: 위에서 아래로 그어지는 흰 심이 번쩍인 뒤 검은 틈(양옆 흰 테두리)으로 남고, 그 순간 화면이 몇 px 어긋남, 둘레로 은은한 빛이 번짐
    // 선은 기울일 수 있음: 반쪽마다 선 각도로 돌린 마스크(한쪽 반평면)에 찍은 화면을 반대로 돌려 넣어 화면과 맞춤
    // 어긋난 만큼 화면 끝이 드러나 검은 띠가 생기지 않도록 두 반쪽을 가르는 선 위 기준점에서 함께 확대함
    // 끝이 가늘게 빠지도록 텍스처를 코드로 만들어 씀
    public class HumanoidSlashScreen : MonoBehaviour
    {
        private const int CRACK_COUNT = 10;

        // 화면 높이 1080 기준 픽셀
        private const float CORE_WIDTH = 12f;
        private const float FRINGE_OFFSET = 6f;
        private const float RIFT_WIDTH = 9f; // 다 벌어졌을 때 검은 틈 폭
        private const float RIM_WIDTH = 4.5f;
        private const float GLOW_WIDTH = 28f; // 선 둘레로 은은하게 번지는 빛 폭
        private const float GLOW_ALPHA = 0.1f; // 틈이 다 벌어졌을 때 빛 세기
        private const float GLOW_FLASH_ALPHA = 0.18f; // 그어지는 순간 빛 세기
        private const float LINE_OVERSHOOT = 0.06f; // 끝이 가늘게 빠지는 부분이 화면 밖에 걸리도록 양끝을 늘림(화면 높이 비율)

        // 회색으로 바랜 화면에 맞춰 무채색, 색수차만 번쩍이는 순간 짧게
        private static readonly Color CoreColor = Color.white;
        private static readonly Color RiftColor = Color.black;
        private static readonly Color RimColor = new(0.95f, 0.95f, 0.95f, 1f);
        private static readonly Color GlowColor = Color.white;
        private static readonly Color FringeRed = new(1f, 0.2f, 0.25f, 1f);
        private static readonly Color FringeBlue = new(0.2f, 0.6f, 1f, 1f);
        private static readonly Color CrackColor = new(0.92f, 0.92f, 0.92f, 1f);

        private RenderTexture capture;
        private Image background;
        private RectTransform splitRoot; // 두 반쪽 마스크의 부모, 갈라지는 동안만 켬
        private RectTransform leftMask; // 선 각도로 돌린 반평면 마스크
        private RectTransform rightMask;
        private RectTransform leftSlide; // 마스크 안에서 선을 따라 어긋나는 부분, 화면과 금이 함께 움직임
        private RectTransform rightSlide;
        private RawImage leftHalf;
        private RawImage rightHalf;
        private RectTransform lines; // 가르는 선들의 부모, 기준점에 놓고 선 각도로 돌림
        private RawImage glow;
        private RawImage rift;
        private RawImage leftRim;
        private RawImage rightRim;
        private RawImage cutCore;
        private RawImage cutFringeRed;
        private RawImage cutFringeBlue;
        private Image flash;
        private Texture2D streakTexture;
        private Texture2D glowTexture;
        private Texture2D crackTexture;
        private readonly List<RectTransform> cracks = new();
        private readonly List<float> crackLengths = new();
        private Vector2 cutPoint; // 가르는 선이 지나는 화면 픽셀 위치
        private float cutAngle; // 세로에서 반시계로 기운 각도, 양수면 위가 왼쪽(\)
        private float lineTop; // 선 방향(위쪽)으로 기준점에서 화면 끝까지 거리
        private float lineBottom; // 반대 방향(아래쪽) 거리, 음수

        public static HumanoidSlashScreen Create(int sortingOrder)
        {
            GameObject screenObject = new GameObject("Humanoid Slash Screen");
            Canvas canvas = screenObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            HumanoidSlashScreen screen = screenObject.AddComponent<HumanoidSlashScreen>();
            screen.Build();
            return screen;
        }

        private void OnDestroy()
        {
            if (capture != null) capture.Release();

            Destroy(streakTexture);
            Destroy(glowTexture);
            Destroy(crackTexture);
        }

        // 지금 카메라 화면(월드만, 오버레이 UI 제외)을 찍어 둠, 보스가 움직이는 동안은 매 프레임 불러 갱신
        public void Capture(Camera camera)
        {
            if (capture == null || capture.width != Screen.width || capture.height != Screen.height)
            {
                if (capture != null) capture.Release();
                capture = new RenderTexture(Screen.width, Screen.height, 24);
            }

            RenderTexture previous = camera.targetTexture;
            camera.targetTexture = capture;
            camera.Render();
            camera.targetTexture = previous;

            leftHalf.texture = capture;
            rightHalf.texture = capture;
        }

        // viewportPoint: 가르는 선이 지나는 화면 위치(0~1), angle: 세로에서 기운 각도(양수면 위가 왼쪽, \)
        // Capture로 찍은 화면을 두 반쪽으로 덮어 보여 주기 시작
        public void ShowSplit(Vector2 viewportPoint, float angle)
        {
            Vector2 size = new Vector2(Screen.width, Screen.height);
            cutPoint = new Vector2(Mathf.Clamp(viewportPoint.x, 0.05f, 0.95f) * size.x, Mathf.Clamp(viewportPoint.y, 0.1f, 0.9f) * size.y);
            cutAngle = Mathf.Clamp(angle, -80f, 80f);
            ClipLineToScreen(size);

            // 마스크는 화면 대각선 두 배 크기 정사각형, 한 변이 가르는 선에 붙음
            float maskSize = size.magnitude * 2f;
            Quaternion rotation = Quaternion.Euler(0f, 0f, cutAngle);
            PlaceAtCut(leftMask, new Vector2(1f, 0.5f), new Vector2(maskSize, maskSize), rotation);
            PlaceAtCut(rightMask, new Vector2(0f, 0.5f), new Vector2(maskSize, maskSize), rotation);
            PlaceAtCut(lines, new Vector2(0.5f, 0.5f), Vector2.zero, rotation);
            PlaceSlide(leftSlide, new Vector2(1f, 0.5f));
            PlaceSlide(rightSlide, new Vector2(0f, 0.5f));

            // 찍은 화면은 반대로 돌려 원래 자리에 맞추고, 확대 기준을 가르는 선 위 기준점에 둬 커져도 틈이 밀리지 않게 함
            Vector2 pivot = new Vector2(cutPoint.x / size.x, cutPoint.y / size.y);
            SetHalf(leftHalf, size, pivot);
            SetHalf(rightHalf, size, pivot);
            PlaceCracks();

            background.enabled = true;
            splitRoot.gameObject.SetActive(true);
            SetSplit(0f, 0f);
        }

        // offset: 선을 따라 왼쪽 반이 위로, 오른쪽 반이 아래로 어긋나는 픽셀 / crackProgress: 금이 퍼진 정도(0~1)
        // 어긋난 만큼 화면 끝이 비지 않도록 딱 그만큼 확대
        public void SetSplit(float offset, float crackProgress)
        {
            leftSlide.anchoredPosition = new Vector2(0f, offset);
            rightSlide.anchoredPosition = new Vector2(0f, -offset);

            float scale = GetCoverScale(Quaternion.Euler(0f, 0f, cutAngle) * new Vector2(0f, offset));
            leftHalf.rectTransform.localScale = new Vector3(scale, scale, 1f);
            rightHalf.rectTransform.localScale = new Vector3(scale, scale, 1f);

            for (int index = 0; index < cracks.Count; index++)
                cracks[index].sizeDelta = new Vector2(crackLengths[index] * crackProgress, cracks[index].sizeDelta.y);
        }

        public void HideSplit()
        {
            background.enabled = false;
            splitRoot.gameObject.SetActive(false);
            SetCut(0f, 0f, 0f);
        }

        // ShowSplit 뒤에 부름. draw: 위에서 아래로 그어진 정도(0~1)
        // flash: 흰 심과 색수차의 세기(0~1), 그어지는 순간 1에서 잦아듦 / riftOpen: 검은 틈이 벌어진 정도(0~1), 테두리도 함께 진해짐
        public void SetCut(float draw, float flash, float riftOpen)
        {
            bool isVisible = draw > 0f;
            SetLineVisible(glow, isVisible && (flash > 0f || riftOpen > 0f));
            SetLineVisible(cutCore, isVisible && flash > 0f);
            SetLineVisible(cutFringeRed, isVisible && flash > 0f);
            SetLineVisible(cutFringeBlue, isVisible && flash > 0f);
            SetLineVisible(rift, isVisible && riftOpen > 0f);
            SetLineVisible(leftRim, isVisible && riftOpen > 0f);
            SetLineVisible(rightRim, isVisible && riftOpen > 0f);
            if (isVisible == false) return;

            float pixel = Screen.height / 1080f;
            float top = lineTop;
            float bottom = Mathf.Lerp(top, lineBottom, draw);

            // 번쩍일 때는 넓고 밝게 퍼졌다가 틈이 남으면 테두리 둘레에 은은하게 남음
            PlaceLine(glow, top, bottom, GLOW_WIDTH * (1f + 0.2f * flash) * pixel, 0f);
            glow.color = WithAlpha(GlowColor, Mathf.Max(GLOW_ALPHA * riftOpen, GLOW_FLASH_ALPHA * flash));

            float riftWidth = RIFT_WIDTH * riftOpen * pixel;
            PlaceLine(rift, top, bottom, riftWidth * 2f, 0f); // 텍스처 가장자리가 옅어 실제 보이는 폭의 두 배로 둠
            PlaceLine(leftRim, top, bottom, RIM_WIDTH * pixel, -riftWidth * 0.5f);
            PlaceLine(rightRim, top, bottom, RIM_WIDTH * pixel, riftWidth * 0.5f);
            rift.color = WithAlpha(RiftColor, Mathf.Clamp01(riftOpen * 1.5f));
            leftRim.color = WithAlpha(RimColor, 0.9f * riftOpen);
            rightRim.color = WithAlpha(RimColor, 0.9f * riftOpen);

            float fringe = FRINGE_OFFSET * flash * pixel;
            PlaceLine(cutCore, top, bottom, CORE_WIDTH * Mathf.Lerp(0.3f, 1f, flash) * pixel, 0f);
            PlaceLine(cutFringeRed, top, bottom, CORE_WIDTH * pixel, -fringe);
            PlaceLine(cutFringeBlue, top, bottom, CORE_WIDTH * pixel, fringe);
            cutCore.color = WithAlpha(CoreColor, flash);
            cutFringeRed.color = WithAlpha(FringeRed, 0.5f * flash);
            cutFringeBlue.color = WithAlpha(FringeBlue, 0.5f * flash);
        }

        public void SetFlash(float alpha)
        {
            flash.enabled = alpha > 0f;
            flash.color = new Color(1f, 1f, 1f, alpha);
        }

        public void HideAll()
        {
            HideSplit();
            SetFlash(0f);
        }

        private void Build()
        {
            streakTexture = CreateStreakTexture(0.18f, 0.12f);
            glowTexture = CreateStreakTexture(0.2f, 0.2f);
            crackTexture = CreateCrackTexture();

            background = CreateImage("Background", Color.black);
            splitRoot = new GameObject("Split", typeof(RectTransform)).GetComponent<RectTransform>();
            splitRoot.SetParent(transform, false);
            Stretch(splitRoot);
            leftMask = CreateMask("Left Mask", out leftSlide, out leftHalf);
            rightMask = CreateMask("Right Mask", out rightSlide, out rightHalf);

            for (int index = 0; index < CRACK_COUNT; index++)
            {
                RectTransform slide = index % 2 == 0 ? leftSlide : rightSlide;
                RawImage crack = CreateRawImage("Crack", crackTexture, slide);
                crack.color = WithAlpha(CrackColor, 0.9f);
                cracks.Add(crack.rectTransform);
                crackLengths.Add(0f);
            }

            // 빛 → 틈 → 테두리 → 색수차 → 심 순서로 겹쳐 그림, 번쩍이는 동안은 심이 틈을 덮음
            lines = new GameObject("Cut Lines", typeof(RectTransform)).GetComponent<RectTransform>();
            lines.SetParent(transform, false);
            glow = CreateRawImage("Glow", glowTexture, lines);
            rift = CreateRawImage("Rift", streakTexture, lines);
            leftRim = CreateRawImage("Left Rim", streakTexture, lines);
            rightRim = CreateRawImage("Right Rim", streakTexture, lines);
            cutFringeRed = CreateRawImage("Cut Fringe Red", streakTexture, lines);
            cutFringeBlue = CreateRawImage("Cut Fringe Blue", streakTexture, lines);
            cutCore = CreateRawImage("Cut Core", streakTexture, lines);
            flash = CreateImage("Flash", Color.white);

            HideAll();
        }

        // 반평면 마스크 → 선을 따라 미끄러지는 부분 → 찍은 화면 순서로 만듦
        private RectTransform CreateMask(string objectName, out RectTransform slide, out RawImage half)
        {
            Image mask = CreateImage(objectName, Color.white, splitRoot);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            slide = new GameObject("Slide", typeof(RectTransform)).GetComponent<RectTransform>();
            slide.SetParent(mask.transform, false);
            half = CreateRawImage("Half", null, slide);
            return mask.rectTransform;
        }

        // 화면 왼쪽 아래 기준 cutPoint에 pivot을 맞춰 놓음
        private void PlaceAtCut(RectTransform rect, Vector2 pivot, Vector2 size, Quaternion rotation)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = cutPoint;
            rect.localRotation = rotation;
        }

        // 미끄러지는 부분은 마스크의 기준점(선 위)에 크기 없이 둠
        private static void PlaceSlide(RectTransform slide, Vector2 maskPivot)
        {
            slide.anchorMin = slide.anchorMax = maskPivot;
            slide.pivot = new Vector2(0.5f, 0.5f);
            slide.sizeDelta = Vector2.zero;
            slide.anchoredPosition = Vector2.zero;
        }

        // 찍은 화면 전체를 화면 크기로 두고 기준점을 pivot으로 맞춘 뒤, 마스크가 돈 만큼 반대로 돌림
        private void SetHalf(RawImage half, Vector2 size, Vector2 pivot)
        {
            RectTransform rect = half.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            rect.localRotation = Quaternion.Euler(0f, 0f, -cutAngle);
            half.uvRect = new Rect(0f, 0f, 1f, 1f);
        }

        // 기준점을 지나는 선이 화면 사각형과 만나는 구간을 선 방향 거리로 구해 둠(위쪽이 양수), 양끝은 조금 더 늘림
        private void ClipLineToScreen(Vector2 size)
        {
            Vector2 direction = Quaternion.Euler(0f, 0f, cutAngle) * Vector2.up;
            float top = float.PositiveInfinity;
            float bottom = float.NegativeInfinity;

            for (int axis = 0; axis < 2; axis++)
            {
                if (Mathf.Abs(direction[axis]) < 0.0001f) continue;
                float toMin = -cutPoint[axis] / direction[axis];
                float toMax = (size[axis] - cutPoint[axis]) / direction[axis];
                top = Mathf.Min(top, Mathf.Max(toMin, toMax));
                bottom = Mathf.Max(bottom, Mathf.Min(toMin, toMax));
            }

            float overshoot = LINE_OVERSHOOT * size.y;
            lineTop = top + overshoot;
            lineBottom = bottom - overshoot;
        }

        // 기준점에서 확대한 뒤 shift만큼 옮겨도 화면 네 끝이 찍은 화면 안에 들도록 하는 최소 배율(양쪽 반 모두 덮도록 절댓값)
        private float GetCoverScale(Vector2 shift)
        {
            if (shift == Vector2.zero) return 1f;

            Vector2 size = new Vector2(Screen.width, Screen.height);
            float scale = 1f;
            for (int axis = 0; axis < 2; axis++)
            {
                float margin = Mathf.Abs(shift[axis]) + 1f; // 1px은 반올림으로 생기는 틈 여유
                scale = Mathf.Max(scale, (cutPoint[axis] + margin) / cutPoint[axis]);
                scale = Mathf.Max(scale, (size[axis] - cutPoint[axis] + margin) / (size[axis] - cutPoint[axis]));
            }
            return scale;
        }

        // 가르는 선에서 바깥쪽으로 뻗는 금, 짝수는 왼쪽 반·홀수는 오른쪽 반에 붙어 함께 어긋남
        private void PlaceCracks()
        {
            float pixel = Screen.height / 1080f;
            for (int index = 0; index < cracks.Count; index++)
            {
                bool isLeft = index % 2 == 0;
                RectTransform crack = cracks[index];

                crack.anchorMin = crack.anchorMax = new Vector2(0.5f, 0.5f);
                crack.pivot = new Vector2(0f, 0.5f);
                crack.anchoredPosition = new Vector2(0f, Mathf.Lerp(lineBottom, lineTop, Random.Range(0.15f, 0.85f)));
                crack.sizeDelta = new Vector2(0f, Random.Range(5f, 9f) * pixel);

                float angle = Random.Range(-60f, 60f);
                crack.localRotation = Quaternion.Euler(0f, 0f, isLeft ? 180f + angle : angle);
                crackLengths[index] = Random.Range(80f, 320f) * pixel;
            }
        }

        private Image CreateImage(string objectName, Color color, Transform parent = null)
        {
            GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent != null ? parent : transform, false);

            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            Stretch(image.rectTransform);
            return image;
        }

        private RawImage CreateRawImage(string objectName, Texture texture = null, Transform parent = null)
        {
            GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(RawImage));
            imageObject.transform.SetParent(parent != null ? parent : transform, false);

            RawImage image = imageObject.GetComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            Stretch(image.rectTransform);
            return image;
        }

        private static void SetLineVisible(RawImage line, bool isVisible) => line.enabled = isVisible;

        // 선 부모(기준점에 놓고 선 각도로 돈 좌표) 안에 세로 빛줄기를 놓음, top·bottom은 선 방향 거리, 폭과 좌우 밀림은 픽셀
        private static void PlaceLine(RawImage line, float top, float bottom, float width, float offsetX)
        {
            RectTransform rect = line.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, top - bottom);
            rect.anchoredPosition = new Vector2(offsetX, (top + bottom) * 0.5f);
        }

        private static Color WithAlpha(Color color, float alpha) => new(color.r, color.g, color.b, alpha);

        // 가로로는 가운데가 진한 가우스, 세로로는 양끝이 가늘게 빠지는 세로 빛줄기
        // sigma: 가로 퍼짐(텍스처 폭 비율), taper: 끝이 빠지는 길이(텍스처 높이 비율)
        private static Texture2D CreateStreakTexture(float sigma, float taper)
        {
            const int WIDTH = 64;
            const int HEIGHT = 256;

            Texture2D texture = new Texture2D(WIDTH, HEIGHT, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            Color32[] pixels = new Color32[WIDTH * HEIGHT];

            for (int y = 0; y < HEIGHT; y++)
            {
                float v = y / (HEIGHT - 1f);
                float end = Mathf.SmoothStep(0f, 1f, Mathf.Min(v, 1f - v) / taper);

                for (int x = 0; x < WIDTH; x++)
                {
                    float u = x / (WIDTH - 1f) - 0.5f;
                    float alpha = Mathf.Exp(-u * u / (2f * sigma * sigma)) * end;

                    // 가장자리는 0으로 떨어뜨려 사각형 경계가 보이지 않게 함
                    alpha *= Mathf.Clamp01((0.5f - Mathf.Abs(u)) * 8f);
                    pixels[y * WIDTH + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        // 가르는 선(왼쪽 끝)에서 굵게 시작해 끝으로 갈수록 가늘고 옅어지는 금
        private static Texture2D CreateCrackTexture()
        {
            const int WIDTH = 128;
            const int HEIGHT = 16;

            Texture2D texture = new Texture2D(WIDTH, HEIGHT, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            Color32[] pixels = new Color32[WIDTH * HEIGHT];

            for (int x = 0; x < WIDTH; x++)
            {
                float u = x / (WIDTH - 1f);
                float thickness = Mathf.Lerp(0.35f, 0.06f, u);
                float fade = 1f - u * u;

                for (int y = 0; y < HEIGHT; y++)
                {
                    float v = y / (HEIGHT - 1f) - 0.5f;
                    float alpha = Mathf.Exp(-v * v / (2f * thickness * thickness * 0.25f)) * fade;
                    pixels[y * WIDTH + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
