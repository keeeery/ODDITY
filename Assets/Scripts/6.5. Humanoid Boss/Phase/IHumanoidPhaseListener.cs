namespace TeamOdd
{
    // 페이즈 2로 넘어갈 때 HumanoidPhaseController가 보스 아래에서 찾아 알림
    public interface IHumanoidPhaseListener
    {
        void EnterPhase2();
    }
}
