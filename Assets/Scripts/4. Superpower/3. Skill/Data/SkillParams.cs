using System.Collections.Generic;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 스킬마다 종류도 개수도 다른 매개변수. SkillChip.tsv의 Params 칸에 "Radius=6;Force=15;Power=300"처럼 적는다.
    /// 어떤 키를 어떤 타입으로 읽을지는 스킬마다 다르므로 여기서는 문자열 그대로 들고 있고,
    /// SkillFactory가 스킬을 만들 때 그 스킬에 맞는 키를 맞는 타입으로 꺼낸다.
    /// 키가 없거나 값 형식이 틀리면 예외가 난다. 테이블을 읽을 때 한 번 만들어 보므로 게임 시작할 때 바로 알 수 있다.
    /// </summary>
    public class SkillParams
    {
        private Dictionary<string, string> values = new Dictionary<string, string>();

        // "Radius=6;Force=15" -> { "Radius": "6", "Force": "15" }. 빈 칸이면 매개변수가 없는 스킬
        public SkillParams(string text)
        {
            foreach (string pair in text.Split(';'))
            {
                if (pair == "")
                    continue;

                string[] keyValue = pair.Split('=');
                values[keyValue[0]] = keyValue[1];
            }
        }

        // 꺼낼 때 원하는 타입으로 바꾼다
        public int GetInt(string key) => int.Parse(values[key]);
        public float GetFloat(string key) => float.Parse(values[key]);
        public bool GetBool(string key) => bool.Parse(values[key]);
        public string GetString(string key) => values[key];
    }
}
