using System.Collections.Generic;
using UnityEngine;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 스킬마다 종류도 개수도 다른 사운드. SkillChip.tsv의 Sounds 칸에 "Hum=Skill/PsychoOrb/Hum/scifi_drone_13,0.5;Tick=..."처럼 적는다.
    /// 이름=클립 경로(Resources/Audios 기준, 확장자 제외),볼륨. 볼륨(0~1)을 빼면 1이다.
    /// 어떤 이름을 언제 틀지는 스킬마다 다르므로 스킬 코드가 정한다. (SkillParams와 같은 방식)
    /// 재생은 SoundManager에 맡긴다. 표에 없는 이름은 틀지 않으므로, 소리를 빼고 싶으면 그 이름을 지우면 된다
    /// 클립 경로가 틀리면 SoundManager가 경고를 남기고 틀지 않는다
    /// </summary>
    public class SkillSounds
    {
        private class Entry
        {
            public string ClipName;
            public float Volume;
        }

        private Dictionary<string, Entry> entries = new Dictionary<string, Entry>();

        // "Hum=Skill/Orb/Hum,0.5;Tick=Skill/Orb/Tick" -> { "Hum": (Skill/Orb/Hum, 0.5), "Tick": (Skill/Orb/Tick, 1) }. 빈 칸이면 소리가 없는 스킬
        public SkillSounds(string text)
        {
            foreach (string pair in text.Split(';'))
            {
                if (pair == "")
                    continue;

                string[] keyValue = pair.Split('=');
                string[] clipAndVolume = keyValue[1].Split(',');

                Entry entry = new Entry();
                entry.ClipName = clipAndVolume[0];
                entry.Volume = clipAndVolume.Length > 1 ? float.Parse(clipAndVolume[1]) : 1f;
                entries[keyValue[0]] = entry;
            }
        }

        // 한 번 재생한다
        public void Play(string key)
        {
            // 게임 종료 중에는 싱글톤이 null을 돌려준다
            if (entries.ContainsKey(key) == false || SoundManager.Instance == null)
                return;

            SoundManager.Instance.Play(entries[key].ClipName, entries[key].Volume);
        }

        // 반복 재생한다. 받은 AudioSource로 빠르기(pitch)를 바꿀 수 있고, 다 쓰면 StopLoop로 멈춰야 한다
        // 표에 없거나 클립을 못 찾으면 null. 받은 쪽은 null인지 보고 써야 한다
        public AudioSource PlayLoop(string key)
        {
            if (entries.ContainsKey(key) == false || SoundManager.Instance == null)
                return null;

            return SoundManager.Instance.PlayLoop(entries[key].ClipName, entries[key].Volume);
        }

        // PlayLoop로 튼 소리를 fadeTime초 동안 줄이며 멈춘다. 틀지 못해 null이면 아무 일도 없다
        public static void StopLoop(AudioSource loop, float fadeTime = 0f)
        {
            // 게임 종료 중에는 싱글톤이 null을 돌려준다
            if (loop == null || SoundManager.Instance == null)
                return;

            SoundManager.Instance.StopLoop(loop, fadeTime);
        }
    }
}
