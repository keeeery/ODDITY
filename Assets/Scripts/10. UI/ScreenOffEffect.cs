using DG.Tweening;

namespace TeamOdd.UI
{
    public static class ScreenOffEffect
    {
        // 기존 시퀀스에 글리치 → 가로 선 → 점 → 소멸을 넣고, 모두 꺼지는 시각을 반환한다.
        // 시간 기준·중단·완료 처리는 시퀀스를 소유한 쪽에서 정한다.
        public static float Insert(Sequence sequence, float startTime, float glitchDuration, float foldDuration,
            float shrinkDuration, float dotFadeDuration)
        {
            float glitchEnd = startTime + glitchDuration;
            float foldEnd = glitchEnd + foldDuration;
            float shrinkEnd = foldEnd + shrinkDuration;

            sequence.Insert(startTime, DOTween.To(() => ScreenGlitch.Glitch, value => ScreenGlitch.Glitch = value, 1f, glitchDuration)
                .SetEase(Ease.InQuad));
            sequence.InsertCallback(glitchEnd, () => ScreenGlitch.Glitch = 0f);
            sequence.Insert(glitchEnd, DOTween.To(() => ScreenGlitch.PowerOff, value => ScreenGlitch.PowerOff = value, 1f, foldDuration)
                .SetEase(Ease.InCubic));
            sequence.Insert(foldEnd, DOTween.To(() => ScreenGlitch.PowerOff, value => ScreenGlitch.PowerOff = value, 2f, shrinkDuration)
                .SetEase(Ease.InCubic));
            sequence.Insert(shrinkEnd, DOTween.To(() => ScreenGlitch.PowerOff, value => ScreenGlitch.PowerOff = value, 3f, dotFadeDuration)
                .SetEase(Ease.OutQuad));

            return shrinkEnd + dotFadeDuration;
        }
    }
}
