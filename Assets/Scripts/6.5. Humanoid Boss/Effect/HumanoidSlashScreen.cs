using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd
{
    // 차원 가르기 화면 연출: 멈춘 순간의 카메라 화면을 한 장 찍어 세로선 기준 두 장으로 나눠 위아래로 어긋나게 보여 줌
    // 세상이 멈춰 있어 화면이 변하지 않으므로 렌더러 기능 없이 UI로 처리, HUD(다른 오버레이 캔버스)는 이보다 위에 그려져 갈라지지 않음
    public class HumanoidSlashScreen : MonoBehaviour
    {
        private const int CRACK_COUNT = 10;

        private RenderTexture capture;
        private Image background;
        private RawImage leftHalf;
        private RawImage rightHalf;
        private Image cutLine;
        private Image flash;
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
        }

        // 지금 카메라 화면(월드만, 오버레이 UI 제외)을 찍어 둠
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

        // viewportX: 가르는 세로선의 화면 x(0~1)
        public void ShowSplit(float viewportX)
        {
            cutX = Mathf.Clamp(viewportX, 0.05f, 0.95f);

            SetHalf(leftHalf, 0f, cutX);
            SetHalf(rightHalf, cutX, 1f);
            PlaceCracks();

            // 금이 반쪽의 자식이라 컴포넌트가 아닌 오브젝트를 켜고 끔
            background.enabled = true;
            leftHalf.gameObject.SetActive(true);
            rightHalf.gameObject.SetActive(true);
            SetSplit(0f, 0f);
        }

        // offset: 왼쪽 반이 위로, 오른쪽 반이 아래로 어긋나는 픽셀 / crackProgress: 금이 퍼진 정도(0~1)
        public void SetSplit(float offset, float crackProgress)
        {
            leftHalf.rectTransform.anchoredPosition = new Vector2(0f, offset);
            rightHalf.rectTransform.anchoredPosition = new Vector2(0f, -offset);

            for (int index = 0; index < cracks.Count; index++)
                cracks[index].sizeDelta = new Vector2(crackLengths[index] * crackProgress, cracks[index].sizeDelta.y);
        }

        public void HideSplit()
        {
            background.enabled = false;
            leftHalf.gameObject.SetActive(false);
            rightHalf.gameObject.SetActive(false);
        }

        public void SetCutLine(float viewportX, float alpha)
        {
            RectTransform line = cutLine.rectTransform;
            line.anchorMin = new Vector2(viewportX, 0f);
            line.anchorMax = new Vector2(viewportX, 1f);
            cutLine.enabled = alpha > 0f;
            cutLine.color = new Color(1f, 1f, 1f, alpha);
        }

        public void SetFlash(float alpha)
        {
            flash.enabled = alpha > 0f;
            flash.color = new Color(1f, 1f, 1f, alpha);
        }

        public void HideAll()
        {
            HideSplit();
            SetCutLine(cutX, 0f);
            SetFlash(0f);
        }

        private void Build()
        {
            background = CreateImage("Background", Color.black);
            leftHalf = CreateRawImage("Left Half");
            rightHalf = CreateRawImage("Right Half");

            for (int index = 0; index < CRACK_COUNT; index++)
            {
                RawImage half = index % 2 == 0 ? leftHalf : rightHalf;
                Image crack = CreateImage("Crack", new Color(1f, 1f, 1f, 0.85f), half.transform);
                cracks.Add(crack.rectTransform);
                crackLengths.Add(0f);
            }

            cutLine = CreateImage("Cut Line", Color.white);
            cutLine.rectTransform.sizeDelta = new Vector2(6f, 0f);
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
                crack.sizeDelta = new Vector2(0f, Random.Range(2f, 4f));

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

        private RawImage CreateRawImage(string objectName)
        {
            GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(RawImage));
            imageObject.transform.SetParent(transform, false);

            RawImage image = imageObject.GetComponent<RawImage>();
            image.raycastTarget = false;
            Stretch(image.rectTransform);
            return image;
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
