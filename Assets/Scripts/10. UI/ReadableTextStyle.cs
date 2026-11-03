using TMPro;
using UnityEngine;

namespace TeamOdd.UI
{
    // 밝은 배경 위에서도 읽히도록 바깥 외곽선과 그림자를 켠 글자 스타일. 시스템 메시지와 조작 안내가 함께 쓴다.
    // 외곽선·그림자는 셰이더 키워드라 실행 중에 켜면 빌드에서 변형이 빠질 수 있어, 키워드를 켠 머티리얼 에셋을 Resources로 묶어 둔다.
    [CreateAssetMenu(menuName = "Team Odd/UI/Readable Text Style")]
    public sealed class ReadableTextStyle : ScriptableObject
    {
        private const string ResourcePath = "ReadableTextStyle";

        [Tooltip("머티리얼이 쓰는 아틀라스의 폰트. 폰트를 따로 정하지 않은 조작 안내도 이 폰트로 쓴다")]
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("font의 아틀라스에 바깥 외곽선과 그림자(Underlay)를 켠 머티리얼")]
        [SerializeField] private Material material;

        private static ReadableTextStyle loaded;
        private static bool loadTried;

        public static ReadableTextStyle Instance
        {
            get
            {
                if (loadTried == false)
                {
                    loaded = Resources.Load<ReadableTextStyle>(ResourcePath);
                    loadTried = true;
                }
                return loaded;
            }
        }

        public static TMP_FontAsset Font => Instance != null ? Instance.font : null;

        // 머티리얼이 이 스타일 폰트의 아틀라스를 쓰므로 같은 폰트인 글자에만 입힌다
        public static void Apply(TMP_Text label)
        {
            ReadableTextStyle style = Instance;
            if (label == null || style == null || style.material == null || label.font != style.font) return;

            label.fontSharedMaterial = style.material;
        }
    }
}
