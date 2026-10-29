using UnityEngine;

namespace TeamOdd.UI
{
    // 월드 화면 전체에 거는 글리치·채도·가장자리 어둡게·화면 꺼짐 값, ScreenGlitchFeature가 매 프레임 읽어 그린다.
    // 모두 0이면 패스를 넣지 않는다. 연출 쪽이 DOTween으로 값을 바꾸고 끝나면 Clear로 되돌린다.
    // 화면 공간 오버레이 UI는 이 패스 뒤에 그려지므로 영향을 받지 않는다(UI는 UIGlitch로 따로 흔든다).
    public static class ScreenGlitch
    {
        // 가로 띠 밀림·RGB 색 분리·깜빡임 세기(0~1)
        public static float Glitch { get; set; }
        // 0이면 원래 색, 1이면 흑백
        public static float Desaturate { get; set; }
        // 가장자리 어두워짐(0~1)
        public static float Vignette { get; set; }
        // 화면 꺼짐: 0~1 위아래로 접혀 가로 선, 1~2 선이 가운데 점으로 줄어듦, 2~3 점이 사라짐
        public static float PowerOff { get; set; }

        public static bool IsActive => Glitch > 0f || Desaturate > 0f || Vignette > 0f || PowerOff > 0f;

        public static void Clear()
        {
            Glitch = 0f;
            Desaturate = 0f;
            Vignette = 0f;
            PowerOff = 0f;
        }

        // 도메인 리로드 없이 플레이할 때를 위한 초기화
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Clear();
    }
}
