using UnityEngine;
using UnityEngine.InputSystem;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// X키로 증강칩 패널을 열고 닫는다. 패널 바깥(항상 켜져 있는 Canvas 루트)에 붙여서 쓴다.
    /// 열려 있는 동안에는 커서를 풀고, 플레이어 조작처럼 패널과 겹치는 입력은 꺼둔다.
    /// </summary>
    public class AugmentationChipToggle : MonoBehaviour
    {
        [SerializeField] private AugmentationChipPanel panel; // 비워두면 자식에서 찾는다
        [SerializeField] private Behaviour[] disableWhileOpen; // 열려 있는 동안 끌 조작 컴포넌트 (플레이어, 카메라 등)

        private CursorLockMode prevLockState;
        private bool prevCursorVisible;

        public bool IsOpen => panel.gameObject.activeSelf;

        private void Awake()
        {
            if (panel == null)
                panel = GetComponentInChildren<AugmentationChipPanel>(true);

            // 닫힌 채로 시작한다. 패널의 Start(칩 생성)는 처음 열 때 실행된다
            panel.gameObject.SetActive(false);
        }

        private void Update()
        {
            // 드래그 중에 닫으면 칩이 루트에 붙은 채 남으므로 드래그가 끝난 뒤에만 토글한다
            if (Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame && panel.IsDragging == false)
                SetOpen(IsOpen == false);
        }

        public void SetOpen(bool open)
        {
            if (open == IsOpen)
                return;

            if (open)
            {
                prevLockState = Cursor.lockState;
                prevCursorVisible = Cursor.visible;
            }

            foreach (Behaviour behaviour in disableWhileOpen)
                if (behaviour != null)
                    behaviour.enabled = open == false;

            panel.gameObject.SetActive(open);

            // 열면 커서를 풀고, 닫으면 열기 전 상태로 되돌린다
            Cursor.lockState = open ? CursorLockMode.None : prevLockState;
            Cursor.visible = open || prevCursorVisible;
        }
    }
}
