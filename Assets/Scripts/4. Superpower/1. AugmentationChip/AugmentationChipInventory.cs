using System.Collections.Generic;
using UnityEngine;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 플레이어가 보유한 증강칩 목록. Player 오브젝트에 붙인다.
    /// 데이터만 들고 있고, 테이블이나 UI는 모른다. 칩을 얻는 쪽(보상, 드랍 등)이 Add로 넣어준다.
    /// 같은 칩을 여러 장 얻어도 각각 다른 OwnedChip으로 들고 있는다.
    /// </summary>
    public class AugmentationChipInventory : MonoBehaviour
    {
        private readonly List<OwnedChip> chips = new();

        public IReadOnlyList<OwnedChip> Chips => chips;

        public OwnedChip Add(AugmentationChipDef def)
        {
            OwnedChip chip = new OwnedChip(def);
            chips.Add(chip);
            return chip;
        }
    }
}
