using TeamOdd.Superpower;
using TeamOdd.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.Reward
{
    [DisallowMultipleComponent]
    public sealed class RewardIconView : MonoBehaviour, ITooltipSource, ITooltipAccessory
    {
        [SerializeField] private Image icon;
        [Tooltip("칩 대표 아이콘(IconSprite), 비워 두면 칸 그림만 보인다.")]
        [SerializeField] private Image chipIcon;
        [Tooltip("칩 색 외곽선, 칸 그림(UnitSprite) 가운데 한 점의 색으로 칠한다. 연결하면 칸 그림은 그리지 않는다.")]
        [SerializeField] private RawImage colorBorder;

        public OwnedChip Chip { get; private set; }

        public void Show(OwnedChip chip)
        {
            Chip = chip;
            Sprite unit = chip != null ? chip.Def.UnitSprite : null;
            if (colorBorder != null)
            {
                icon.sprite = null;
                colorBorder.texture = unit != null ? unit.texture : null;
                colorBorder.uvRect = new Rect(GetCenterUV(unit), Vector2.zero);
                colorBorder.enabled = unit != null;
            }
            else icon.sprite = unit;
            if (chipIcon != null)
            {
                chipIcon.sprite = chip != null ? chip.Def.IconSprite : null;
                chipIcon.enabled = chipIcon.sprite != null;
            }
            gameObject.SetActive(chip != null);
        }

        public bool TryGetTooltip(out TooltipContent content)
        {
            if (Chip == null)
            {
                content = default;
                return false;
            }

            content = ChipTooltip.CreateContent(Chip);
            return true;
        }

        // 아틀라스에 들어가도 맞도록 텍스처 안 스프라이트 영역의 가운데를 쓴다(ChipShapeGraphic과 같은 방식).
        private static Vector2 GetCenterUV(Sprite sprite)
        {
            if (sprite == null) return Vector2.zero;
            Vector2 sum = Vector2.zero;
            foreach (Vector2 point in sprite.uv)
                sum += point;
            return sum / sprite.uv.Length;
        }

        // 지급할 때 정해진 모양을 툴팁 헤더 오른쪽에 그린다.
        public bool FillAccessory(RectTransform slot) => ChipTooltip.FillShape(Chip, slot);
    }
}
