using System.Collections.Generic;
using UnityEngine;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 플레이어가 보유한 증강칩 목록. Player 오브젝트에 붙인다.
    /// 데이터만 들고 있고, 테이블이나 UI는 모른다. 칩을 얻는 쪽(보상, 드랍 등)이 Add로 넣어준다.
    /// </summary>
    public class AugmentationChipInventory : MonoBehaviour
    {
        private readonly List<AugmentationChipDef> chips = new();

        public IReadOnlyList<AugmentationChipDef> Chips => chips;

        public void Add(AugmentationChipDef def) => chips.Add(def);
    }
}
