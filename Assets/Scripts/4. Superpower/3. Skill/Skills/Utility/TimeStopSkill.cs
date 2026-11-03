using System.Collections;
using TeamOdd.TimeControl;
using UnityEngine;

namespace TeamOdd.Superpower
{
    // 플레이어를 제외한 모든 TimeFreezable(보스, 염동력 오브젝트, 파편)의 시간을 멈춘다. 매개변수 1개: Duration(float)
    // - 멈출 때 시전음(SkillChip.tsv의 Cast), 다시 흐를 때 탈출음(End)을 낸다
    public class TimeStopSkill : SuperpowerSkill
    {
        private readonly float duration; // 멈추는 시간 (초)

        public TimeStopSkill(string name, float cooldown, float duration) : base(name, cooldown)
        {
            this.duration = duration;
        }

        public override string Describe() => $"시간 정지 {duration}초";

        protected override void Execute(SkillContext context)
        {
            TimeFreezeManager.Instance.FreezeWorld(duration);
            Sounds.Play("Cast");

            // 스킬은 MonoBehaviour가 아니므로 정지를 맡은 TimeFreezeManager가 돌린다
            TimeFreezeManager.Instance.StartCoroutine(PlayEndSound());
        }

        // FreezeWorld와 같은 방식(WaitForSeconds)으로 같은 시간을 기다려, 정지가 풀리는 순간에 탈출음을 낸다
        private IEnumerator PlayEndSound()
        {
            yield return new WaitForSeconds(duration);
            Sounds.Play("End");
        }
    }
}
