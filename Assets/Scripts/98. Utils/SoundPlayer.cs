using Sirenix.OdinInspector;
using UnityEngine;

namespace TeamOdd
{
    // 사운드 하나를 붙여 두고 UI 버튼 OnClick 같은 UnityEvent에서 Play를 연결해 쓰는 용도
    public class SoundPlayer : MonoBehaviour
    {
        [Title("Settings")]
        [SerializeField, InlineProperty, HideLabel] private SoundData sound;

        public void Play() => sound.Play();
    }
}
