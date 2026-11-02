using System.Collections;
using System.Collections.Generic;
using FantasyGarden.FSM;
using Sirenix.OdinInspector;
using TeamOdd.Boss;
using TeamOdd.Combat;
using TeamOdd.Player;
using TeamOdd.TimeControl;
using TeamOdd.UI;
using UnityEngine;

namespace TeamOdd
{
    // 차원 가르기 (A15): 체력이 일정 비율 이하가 되면 한 번, 세상을 멈추고 플레이어 정면 멀리 나타나 천천히 걸어온 뒤
    // 검을 천천히 들어 잠깐 멈췄다가 빠르게 내려베고(세로 또는 사선) 화면을 가른 뒤 확정 피해
    // 다른 담당 코드를 고치지 않으려고 기존 기능을 씀
    // - 세상 정지: TimeFreezable.FreezeWorld(아주 작은 배율), 0이면 시간 정지 홀로그램 막이 덮이므로 / 보스만 SetWorldExempt
    // - 플레이어 감속: SlowMotion.AddPlayerOnly(아주 작은 배율)로 플레이어만 느리게 해 사실상 움직이지 못하게 함, 염동력·스킬은 SetSuperpowerLocked로 막음
    // - 순간이동: 사망 연출과 같은 홀로그램(HumanoidHologramWarp)으로 떠오르며 사라졌다가 플레이어 정면에서 내려오며 나타남
    // - 회색·깨짐: ScreenGlitch(Desaturate, Glitch), 화면 가르기: HumanoidSlashScreen
    public class HumanoidDimensionSlash_2 : HumanoidDimensionSlash {}
}
