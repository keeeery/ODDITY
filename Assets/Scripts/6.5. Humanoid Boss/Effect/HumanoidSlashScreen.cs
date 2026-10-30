using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd
{
    // 차원 가르기 화면 연출: 카메라 화면을 찍어 세로선 기준 두 장으로 나눠 위아래로 어긋나게 보여 줌
    // 세상이 거의 멈춰 있어 렌더러 기능 없이 UI로 처리, HUD(다른 오버레이 캔버스)는 이보다 위에 그려져 갈라지지 않음
    // 가르는 선: 위에서 아래로 그어지는 흰 심이 번쩍인 뒤 검은 틈(양옆 흰 테두리)으로 남고, 그 순간 화면이 몇 px 어긋남
    // 어긋난 만큼 화면 끝이 드러나 검은 띠가 생기지 않도록 두 반쪽을 가르는 선 기준으로 함께 확대함
    // 끝이 가늘게 빠지도록 텍스처를 코드로 만들어 씀
    public class HumanoidSlashScreen : MonoBehaviour
    {
        private const int CRACK_COUNT = 10;

        // 화면 높이 1080 기준 픽셀
        private const float CORE_WIDTH = 8f;
        private const float FRINGE_OFFSET = 4f;
        private const float RIFT_WIDTH = 6f; // 다 벌어졌을 때 검은 틈 폭
        private const float RIM_WIDTH = 3f;
        private const float LINE_OVERSHOOT = 0.06f; // 끝이 가늘게 빠지는 부분이 화면 밖에 걸리도록 위아래로 늘림

        // 회색으로 바랜 화면에 맞춰 무채색, 색수차만 번쩍이는 순간 짧게
        private static readonly Color CoreColor = Color.white;
        private static readonly Color RiftColor = Color.black;
        private static readonly Color RimColor = new(0.95f, 0.95f, 0.95f, 1f);
        private static readonly Color FringeRed = new(1f, 0.2f, 0.25f, 1f);
        private static readonly Color FringeBlue = new(0.2f, 0.6f, 1f, 1f);
        private static readonly Color CrackColor = new(0.92f, 0.92f, 0.92f, 1f);

        private RenderTexture capture;
        private Image background;
        private RectTransform halves; // 두 반쪽의 부모, 가르는 선 기준으로 확대
        private RawImage leftHalf;
        private RawImage rightHalf;
        private RawImage rift;
        private RawImage leftRim;
        private RawImage rightRim;
        private RawImage cutCore;
        private RawImage cutFringeRed;
        private RawImage cutFringeBlue;
        private Image flash;
        private Texture2D streakTexture;
        private Texture2D crackTexture;
        private readonly List<RectTransform> cracks = new();
        private readonly List<float> crackLengths = new();
        private float cutX;

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

        // viewportX: 가르는 세로선의 화면 x(0~1), Capture로 찍은 화면을 두 반쪽으로 덮어 보여 주기 시작
        public void ShowSplit(float viewportX)
        {
            cutX = Mathf.Clamp(viewportX, 0.05f, 0.95f);

            // 확대 기준을 가르는 선에 두어 커져도 틈이 옆으로 밀리지 않게 함
            halves.pivot = new Vector2(cutX, 0.5f);
            halves.anchoredPosition = Vector2.zero;
            SetHalf(leftHalf, 0f, cutX);
            SetHalf(rightHalf, cutX, 1f);
            PlaceCracks();

            background.enabled = true;
            halves.gameObject.SetActive(true);
            SetSplit(0f, 0f);
        }

        // offset: 왼쪽 반이 위로, 오른쪽 반이 아래로 어긋나는 픽셀 / crackProgress: 금이 퍼진 정도(0~1)
        // 어긋난 만큼 위아래 끝이 비지 않도록 딱 그만큼 확대 (반 높이가 offset만큼 줄어든 것을 다시 채우는 배율)
        public void SetSplit(float offset, float crackProgress)
        {
            leftHalf.rectTransform.anchoredPosition = new Vector2(0f, offset);
            rightHalf.rectTransform.anchoredPosition = new Vector2(0f, -offset);

            float halfHeight = Screen.height * 0.5f;
            float scale = offset == 0f ? 1f : halfHeight / Mathf.Max(1f, halfHeight - Mathf.Abs(offset) - 1f); // 1px은 반올림으로 생기는 틈 여유
            halves.localScale = new Vector3(scale, scale, 1f);

            for (int index = 0; index < cracks.Count; index++)
                cracks[index].sizeDelta = new Vector2(crackLengths[index] * crackProgress, cracks[index].sizeDelta.y);
        }

        public void HideSplit()
        {
            background.enabled = false;
            halves.gameObject.SetActive(false);
            SetCut(0f, 0f, 0f);
        }

        // ShowSplit 뒤에 부름. draw: 위에서 아래로 그어진 정도(0~1)
        // flash: 흰 심과 색수차의 세기(0~1), 그어지는 순간 1에서 잦아듦 / riftOpen: 검은 틈이 벌어진 정도(0~1), 테두리도 함께 진해짐
        public void SetCut(float draw, float flash, float riftOpen)
        {
            bool isVisible = draw > 0f;
            SetLineVisible(cutCore, isVisible && flash > 0f);
            SetLineVisible(cutFringeRed, isVisible && flash > 0f);
            SetLineVisible(cutFringeBlue, isVisible && flash > 0f);
            SetLineVisible(rift, isVisible && riftOpen > 0f);
            SetLineVisible(leftRim, isVisible && riftOpen > 0f);
            SetLineVisible(rightRim, isVisible && riftOpen > 0f);
            if (isVisible == false) return;

            float pixel = Screen.height / 1080f;
            float top = 1f + LINE_OVERSHOOT;
            float bottom = Mathf.Lerp(top, -LINE_OVERSHOOT, draw);

            float riftWidth = RIFT_WIDTH * riftOpen * pixel;
            PlaceLine(rift, cutX, top, bottom, riftWidth * 2f, 0f); // 텍스처 가장자리가 옅어 실제 보이는 폭의 두 배로 둠
            PlaceLine(leftRim, cutX, top, bottom, RIM_WIDTH * pixel, -riftWidth * 0.5f);
            PlaceLine(rightRim, cutX, top, bottom, RIM_WIDTH * pixel, riftWidth * 0.5f);
            rift.color = WithAlpha(RiftColor, Mathf.Clamp01(riftOpen * 1.5f));
            leftRim.color = WithAlpha(RimColor, 0.9f * riftOpen);
            rightRim.color = WithAlpha(RimColor, 0.9f * riftOpen);

            float fringe = FRINGE_OFFSET * flash * pixel;
            PlaceLine(cutCore, cutX, top, bottom, CORE_WIDTH * Mathf.Lerp(0.3f, 1f, flash) * pixel, 0f);
            PlaceLine(cutFringeRed, cutX, top, bottom, CORE_WIDTH * pixel, -fringe);
            PlaceLine(cutFringeBlue, cutX, top, bottom, CORE_WIDTH * pixel, fringe);
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
            crackTexture = CreateCrackTexture();

            background = CreateImage("Background", Color.black);
            halves = new GameObject("Halves", typeof(RectTransform)).GetComponent<RectTransform>();
            halves.SetParent(transform, false);
            Stretch(halves);
            leftHalf = CreateRawImage("Left Half", null, halves);
            rightHalf = CreateRawImage("Right Half", null, halves);

            for (int index = 0; index < CRACK_COUNT; index++)
            {
                RawImage half = index % 2 == 0 ? leftHalf : rightHalf;
                RawImage crack = CreateRawImage("Crack", crackTexture, half.transform);
                crack.color = WithAlpha(CrackColor, 0.9f);
                cracks.Add(crack.rectTransform);
                crackLengths.Add(0f);
            }

            // 틈 → 테두리 → 색수차 → 심 순서로 겹쳐 그림, 번쩍이는 동안은 심이 틈을 덮음
            rift = CreateRawImage("Rift", streakTexture);
            leftRim = CreateRawImage("Left Rim", streakTexture);
            rightRim = CreateRawImage("Right Rim", streakTexture);
            cutFringeRed = CreateRawImage("Cut Fringe Red", streakTexture);
            cutFringeBlue = CreateRawImage("Cut Fringe Blue", streakTexture);
            cutCore = CreateRawImage("Cut Core", streakTexture);
            flash = CreateImage("Flash", Color.white);

            HideAll();
        }

        private void SetHalf(RawImage half, float fromX, float toX)
        {
            RectTransform rect = half.rectTransform;
            rect.anchorMin = new Vector2(fromX, 0f);
            rect.anchorMax = new Vector2(toX, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            half.uvRect = new Rect(fromX, 0f, toX - fromX, 1f);
        }

        // 가르는 선에서 바깥쪽으로 뻗는 금, 짝수는 왼쪽 반·홀수는 오른쪽 반에 붙어 함께 어긋남
        private void PlaceCracks()
        {
            for (int index = 0; index < cracks.Count; index++)
            {
                bool isLeft = index % 2 == 0;
                RectTransform crack = cracks[index];

                crack.anchorMin = crack.anchorMax = new Vector2(isLeft ? 1f : 0f, Random.Range(0.1f, 0.9f));
                crack.pivot = new Vector2(0f, 0.5f);
                crack.anchoredPosition = Vector2.zero;
                crack.sizeDelta = new Vector2(0f, Random.Range(5f, 9f) * Screen.height / 1080f);

                float angle = Random.Range(-60f, 60f);
                crack.localRotation = Quaternion.Euler(0f, 0f, isLeft ? 180f + angle : angle);
                crackLengths[index] = Random.Range(80f, 320f) * Screen.height / 1080f;
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

        // 세로 빛줄기를 화면 x에 놓음, 위아래는 화면 비율(0~1 밖으로 늘릴 수 있음)·폭과 좌우 밀림은 픽셀
        private static void PlaceLine(RawImage line, float viewportX, float top, float bottom, float width, float offsetX)
        {
            RectTransform rect = line.rectTransform;
            rect.anchorMin = new Vector2(viewportX, bottom);
            rect.anchorMax = new Vector2(viewportX, top);
            rect.offsetMin = new Vector2(offsetX - width * 0.5f, 0f);
            rect.offsetMax = new Vector2(offsetX + width * 0.5f, 0f);
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
