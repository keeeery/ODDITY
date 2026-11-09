using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // 글자 입력 칸(InputField, TMP_InputField)이 키보드 입력을 받고 있는지.
    // 검색 칸에 글자를 치는 동안 E(증강 칩 창)·F(상호작용)·Esc(창 닫기) 같은 단축키가 같이 눌리지 않도록 단축키 쪽에서 확인한다.
    // 한글을 쳐도 물리 키는 그대로 눌리므로(ㄷ = E, ㄹ = F) 입력 칸이 막아 주지 못한다.
    // 입력 칸은 Esc·Enter를 받은 그 프레임 안에 입력을 끝내 버려서, 단축키가 입력 칸보다 늦게 읽으면 이미 끝난 것으로 보인다.
    // 그래서 바로 앞 프레임까지 입력 중이었으면 이번 프레임도 입력 중으로 본다.
    public static class TextInputFocus
    {
        private static int lastTypingFrame = -10;

        public static bool IsTyping
        {
            get
            {
                if (IsFieldFocused()) MarkTyping();
                return Time.frameCount - lastTypingFrame <= 1;
            }
        }

        // 입력 칸이 입력을 받는 동안 매 프레임 부른다. 이번 프레임에 단축키 쪽이 아무도 묻지 않았어도 다음 프레임의 Esc를 막는다
        public static void MarkTyping() => lastTypingFrame = Time.frameCount;

        private static bool IsFieldFocused()
        {
            EventSystem eventSystem = EventSystem.current;
            GameObject selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            if (selected == null) return false;

            if (selected.TryGetComponent(out InputField field)) return field.isFocused;
            if (selected.TryGetComponent(out TMP_InputField tmpField)) return tmpField.isFocused;
            return false;
        }
    }
}
