using TeamOdd.Superpower;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.Reward
{
    [DisallowMultipleComponent]
    public sealed class RewardIconView : MonoBehaviour
    {
        [SerializeField] private Image icon;

        public OwnedChip Chip { get; private set; }

        public void Show(OwnedChip chip)
        {
            Chip = chip;
            icon.sprite = chip != null ? chip.Def.UnitSprite : null;
            gameObject.SetActive(chip != null);
        }
    }
}
