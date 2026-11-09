using System.Collections.Generic;
using System.Text;

namespace TeamOdd.UI
{
    // 한글 이름 검색. 띄어쓰기와 대소문자를 무시하고 이름 어디에 있어도 찾는다. 증강 칩 창의 검색 칸(ChipSearchField)이 쓴다.
    //   초성 검색: "ㅅㄱ" → 시간 정지, 시간 역행
    //   치고 있는 마지막 글자: 한글은 한 글자를 다 조합하기 전에도 검색어가 바뀌므로, 검색어 마지막 글자는 그 글자로 가는 중이면 맞는 것으로 본다
    //     받침 전 "수" → 순간 이동, 겹모음 전 "호" → 회복, 다음 글자 초성이 잠깐 받침으로 붙은 "상" → 사이, 겹받침 전 "달" → 닭
    public static class KoreanSearch
    {
        private const int SyllableFirst = 0xAC00; // 가
        private const int SyllableLast = 0xD7A3;  // 힣
        private const int JungCount = 21;
        private const int JongCount = 28;

        // 사용자가 낱자로 치는 자모(호환용 자모)로 적은 초성 19자와 종성 28자. 종성 첫 칸(공백)은 받침 없음
        private const string Choseong = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";
        private const string Jongseong = " ㄱㄲㄳㄴㄵㄶㄷㄹㄺㄻㄼㄽㄾㄿㅀㅁㅂㅄㅅㅆㅇㅈㅊㅋㅌㅍㅎ";

        // TryMatch가 이름에서 띄어쓰기를 뺀 글자 위치를 모아 두는 곳. 메인 스레드에서만 부른다
        private static readonly List<int> positions = new();

        // 띄어쓰기를 빼고 소문자로 바꾼 글자. 검색어와 이름이 똑같은지 볼 때 쓴다
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            StringBuilder builder = new(text.Length);
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c) == false)
                    builder.Append(char.ToLowerInvariant(c));
            }
            return builder.ToString();
        }

        // name에서 query와 맞는 첫 구간 [start, end) (name의 글자 위치). 검색어가 비어 있으면 늘 맞고 구간은 비어 있다
        public static bool TryMatch(string name, string query, out int start, out int end)
        {
            start = end = 0;
            string q = Normalize(query);
            if (q.Length == 0) return true;
            if (string.IsNullOrEmpty(name)) return false;

            positions.Clear();
            for (int i = 0; i < name.Length; i++)
            {
                if (char.IsWhiteSpace(name[i]) == false)
                    positions.Add(i);
            }

            for (int s = 0; s + q.Length <= positions.Count; s++)
            {
                bool matched = true;
                for (int j = 0; j < q.Length && matched; j++)
                {
                    bool last = j == q.Length - 1;
                    char next = s + j + 1 < positions.Count ? name[positions[s + j + 1]] : '\0';
                    matched = CharMatches(q[j], name[positions[s + j]], last, next);
                }

                if (matched)
                {
                    start = positions[s];
                    end = positions[s + q.Length - 1] + 1;
                    return true;
                }
            }

            return false;
        }

        // q: 검색어 글자(소문자), n: 이름 글자, next: 이름에서 n 다음 글자(없으면 '\0')
        private static bool CharMatches(char q, char n, bool last, char next)
        {
            n = char.ToLowerInvariant(n);
            if (q == n) return true;

            // 초성만 친 글자
            if (IsSyllable(n) && Choseong[Cho(n)] == q) return true;

            return last && IsSyllable(q) && IsSyllable(n) && IsTypingToward(q, n, next);
        }

        // 조합 중인 글자 q가 계속 치면 n이 되는지
        private static bool IsTypingToward(char q, char n, char next)
        {
            if (Cho(q) != Cho(n)) return false;

            int qv = Jung(q), nv = Jung(n);
            int qj = Jong(q), nj = Jong(n);

            // 받침이나 겹모음의 뒷모음을 아직 안 친 글자
            if (qj == 0) return qv == nv || qv == FirstVowel(nv);
            if (qv != nv) return false;

            // 겹받침의 뒷자음을 아직 안 친 글자
            if (nj != 0) return FirstConsonant(nj) == qj;

            // 다음 글자의 초성이 잠깐 받침으로 붙어 있는 글자 (사 + ㅇ = 상, 이어서 ㅣ를 치면 사이)
            return IsSyllable(next) && Choseong[Cho(next)] == Jongseong[qj];
        }

        private static bool IsSyllable(char c) => c >= SyllableFirst && c <= SyllableLast;
        private static int Cho(char c) => (c - SyllableFirst) / (JungCount * JongCount);
        private static int Jung(char c) => (c - SyllableFirst) / JongCount % JungCount;
        private static int Jong(char c) => (c - SyllableFirst) % JongCount;

        // 겹모음의 앞모음 (중성 번호). ㅘ·ㅙ·ㅚ ← ㅗ, ㅝ·ㅞ·ㅟ ← ㅜ, ㅢ ← ㅡ
        private static int FirstVowel(int jung) => jung switch
        {
            9 or 10 or 11 => 8,
            14 or 15 or 16 => 13,
            19 => 18,
            _ => jung,
        };

        // 겹받침의 앞자음 (종성 번호). ㄳ ← ㄱ, ㄵ·ㄶ ← ㄴ, ㄺ~ㅀ ← ㄹ, ㅄ ← ㅂ
        private static int FirstConsonant(int jong) => jong switch
        {
            3 => 1,
            5 or 6 => 4,
            >= 9 and <= 15 => 8,
            18 => 17,
            _ => jong,
        };
    }
}
