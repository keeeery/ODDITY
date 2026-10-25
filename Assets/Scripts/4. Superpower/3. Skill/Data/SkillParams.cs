using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 스킬마다 종류도 개수도 다른 매개변수. 테이블 SkillParams 칸에 "Radius=6;Force=15;Damage=10"처럼 적는다.
    /// 어떤 키를 어떤 타입으로 읽을지는 스킬마다 다르므로 여기서는 문자열 그대로 들고 있고,
    /// SkillFactory가 스킬을 만들 때 그 스킬에 맞는 키를 맞는 타입으로 꺼낸다.
    /// 키가 없거나 값 형식이 틀리면 에러 로그를 남기고 기본값(0, false, "")을 돌려준다.
    /// </summary>
    public class SkillParams
    {
        private readonly Dictionary<string, string> values = new();
        private readonly int chipId; // 에러 로그에 어느 칩의 값인지 보여준다
        private readonly string text;

        // 형식이 틀렸거나 없는 키를 읽은 적이 있는지. 테이블이 읽을 때 한 번 만들어 보고 확인한다
        public bool HasError { get; private set; }

        // "Key=Value;Key=Value" -> 키별 문자열. 빈 칸이면 매개변수가 없는 스킬
        public SkillParams(int chipId, string text)
        {
            this.chipId = chipId;
            this.text = text;

            foreach (string pair in text.Split(';'))
            {
                if (string.IsNullOrWhiteSpace(pair))
                    continue;

                string[] keyValue = pair.Split('=');
                if (keyValue.Length != 2)
                {
                    HasError = true;
                    Debug.LogError($"[SkillParams] {chipId}번 칩: \"{pair}\"는 Key=Value 형식이 아닙니다.");
                    continue;
                }

                // 같은 키를 두 번 적으면 어느 값을 쓸지 모호하므로 틀린 값으로 본다
                string key = keyValue[0].Trim();
                if (values.ContainsKey(key))
                {
                    HasError = true;
                    Debug.LogError($"[SkillParams] {chipId}번 칩: {key}가 두 번 적혀 있습니다. (SkillParams: \"{text}\")");
                    continue;
                }

                values[key] = keyValue[1].Trim();
            }
        }

        public int GetInt(string key)
        {
            if (TryGet(key, out string value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
                return result;

            LogInvalid(key, "정수");
            return 0;
        }

        // 소수점은 PC 지역 설정과 상관없이 '.'으로 읽는다
        public float GetFloat(string key)
        {
            if (TryGet(key, out string value) && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
                return result;

            LogInvalid(key, "실수");
            return 0f;
        }

        public bool GetBool(string key)
        {
            if (TryGet(key, out string value) && bool.TryParse(value, out bool result))
                return result;

            LogInvalid(key, "true/false");
            return false;
        }

        public string GetString(string key)
        {
            if (TryGet(key, out string value))
                return value;

            LogInvalid(key, "문자열");
            return "";
        }

        private bool TryGet(string key, out string value) => values.TryGetValue(key, out value);

        private void LogInvalid(string key, string expected)
        {
            HasError = true;
            string state = values.TryGetValue(key, out string value) ? $"\"{value}\"을(를) {expected}(으)로 읽을 수 없습니다" : "키가 없습니다";
            Debug.LogError($"[SkillParams] {chipId}번 칩의 {key}: {state}. (SkillParams: \"{text}\")");
        }
    }
}
