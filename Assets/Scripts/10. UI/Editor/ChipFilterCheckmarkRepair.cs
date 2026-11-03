using TeamOdd.Superpower;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // 증강 칩 창의 필터 토글(전체·공격·방어…) 체크 표시를 체크 상자 안에 꽉 찬 사각형으로 만든다.
    // 체크 표시가 거의 투명한 VectorPanel로 바뀌어 켜고 꺼도 보이지 않던 것을 고친다. 다시 실행해도 된다.
    public static class ChipFilterCheckmarkRepair
    {
        private const string ChipPanelPath = "Assets/Prefabs/4. Superpower/Augmentation Chip Panel.prefab";
        private static readonly Color CheckColor = new(0.84705883f, 0.8235294f, 0.76862746f, 1f);
        private const float FillRatio = 0.7f;

        [MenuItem("Tools/Team Odd/Repair Chip Filter Checkmarks")]
        private static void Repair()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ChipPanelPath);
            try
            {
                ChipTrayView tray = root.GetComponentInChildren<ChipTrayView>(true);
                if (tray == null)
                {
                    Debug.LogError("[ChipFilter] Augmentation Chip Panel 프리팹에서 ChipTrayView를 찾지 못했습니다.");
                    return;
                }

                int repaired = 0;
                for (int i = 0; i < tray.FilterToggleCount; i++)
                    repaired += RepairToggle(tray.GetFilterToggle(i)) ? 1 : 0;
                if (tray.AllToggle != null) repaired += RepairToggle(tray.AllToggle) ? 1 : 0;

                PrefabUtility.SaveAsPrefabAsset(root, ChipPanelPath);
                Debug.Log($"[ChipFilter] 필터 토글 {repaired}개의 체크 표시를 사각형으로 바꿨습니다.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool RepairToggle(Toggle toggle)
        {
            if (toggle == null || toggle.graphic == null) return false;

            GameObject mark = toggle.graphic.gameObject;
            if (toggle.graphic is not Image)
            {
                Object.DestroyImmediate(toggle.graphic);
                mark.AddComponent<Image>();
            }

            Image image = mark.GetComponent<Image>();
            image.sprite = null;
            image.color = CheckColor;
            image.raycastTarget = false;
            toggle.graphic = image;

            // 체크 상자 가운데에 상자 크기의 70%만 채운다
            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.one * (0.5f - FillRatio * 0.5f);
            rect.anchorMax = Vector2.one * (0.5f + FillRatio * 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            // 저장된 켜짐 상태대로 보이게 둔다 (실행하면 ChipTrayView가 모두 켠다)
            image.canvasRenderer.SetAlpha(toggle.isOn ? 1f : 0f);
            return true;
        }
    }
}
