using UnityEngine;
using UnityEngine.InputSystem;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// X키로 증강칩 패널을 열고 닫는다. (데모용. 나중에는 정비소가 같은 방식으로 panel.Open(inventory)를 부른다)
    /// 패널 바깥(항상 켜져 있는 Canvas 루트)에 붙이고, 씬의 플레이어 인벤토리를 연결해 둔다.
    /// 열려 있는 동안에는 커서를 풀고, 플레이어 조작처럼 패널과 겹치는 입력은 꺼둔다.
    /// </summary>
    public class AugmentationChipToggle : MonoBehaviour
    {
        [SerializeField] private AugmentationChipPanel panel;         // 비워두면 자식에서 찾는다
        [SerializeField] private AugmentationChipInventory inventory; // 씬의 Player. 열 때 패널에 넘긴다
        [SerializeField] private Behaviour[] disableWhileOpen;        // 열려 있는 동안 끌 조작 컴포넌트 (플레이어, 카메라 등)

        private CursorLockMode prevLockState;
        private bool prevCursorVisible;

        private void Awake()
        {
            if (panel == null)
                panel = GetComponentInChildren<AugmentationChipPanel>(true);

            // 닫힌 채로 시작한다. 칩은 열 때 인벤토리를 읽어서 그린다
            panel.Close();
        }

        private void Update()
        {
            // 드래그 중에 닫으면 칩이 루트에 붙은 채 남으므로 드래그가 끝난 뒤에만 토글한다
            if (Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame && panel.IsDragging == false)
                SetOpen(panel.IsOpen == false);
        }

        public void SetOpen(bool open)
        {
            if (open == panel.IsOpen)
                return;

            if (open)
            {
                prevLockState = Cursor.lockState;
                prevCursorVisible = Cursor.visible;
            }

            foreach (Behaviour behaviour in disableWhileOpen)
                if (behaviour != null)
                    behaviour.enabled = open == false;

            if (open)
                panel.Open(inventory);
            else
                panel.Close();

            // 열면 커서를 풀고, 닫으면 열기 전 상태로 되돌린다
            Cursor.lockState = open ? CursorLockMode.None : prevLockState;
            Cursor.visible = open || prevCursorVisible;
        }
    }
}
