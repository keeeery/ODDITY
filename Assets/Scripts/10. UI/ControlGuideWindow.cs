using TeamOdd.Superpower;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // 로비 조작 콘솔(Control Guide NPC)이 여는 조작 안내 창. 게임의 조작 키를 이동·염동력 두 단에 늘어놓는다.
    // 한 줄은 조준점 위 상호작용 안내와 같은 [키캡] 설명 생김새(KeyHintBuilder)이고, 키캡 칸 너비를 맞춰 설명이 세로로 줄을 선다.
    // 창 틀(바탕·제목·X)은 Tools/Team Odd/Set Up Lobby Control Guide 메뉴가 만들고, 안쪽 줄은 처음 열 때 한 번 만든다.
    // 콘솔의 MissionNPC가 이 오브젝트를 켜고 끄며, X·Esc(일시정지 메뉴가 X를 대신 누름)로도 닫힌다.
    public sealed class ControlGuideWindow : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [Tooltip("비워두면 ReadableTextStyle의 폰트(Pretendard-Medium SDF)")]
        [SerializeField] private TMP_FontAsset font;

        // 1920x1080 기준
        private const float ColumnGap = 56f;
        private const float RowSpacing = 10f;
        // 가장 긴 키 묶음([W][A][S][D])이 들어가는 키캡 칸 너비
        private const float KeyColumnWidth = 140f;
        private const float ActionNoteGap = 4f;

        private readonly struct Entry
        {
            public readonly string[] Keys;
            // 키캡 사이에 흐리게 넣는 글자(스킬 [1] ~ [5]의 "~")
            public readonly string Joiner;
            public readonly string Action;
            public readonly string Note;

            public Entry(string action, string note, string joiner, params string[] keys)
            {
                Keys = keys;
                Joiner = joiner;
                Action = action;
                Note = note;
            }
        }

        private bool built;

        public void Close() => gameObject.SetActive(false);

        private void Awake() => Build();

        private void Build()
        {
            if (built || content == null) return;
            built = true;

            HorizontalLayoutGroup columns = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            columns.spacing = ColumnGap;
            columns.childAlignment = TextAnchor.UpperLeft;
            columns.childControlWidth = columns.childControlHeight = true;
            columns.childForceExpandWidth = columns.childForceExpandHeight = false;

            foreach (Entry[] column in BuildColumns())
            {
                RectTransform columnRect = NewUI("Column", content);
                VerticalLayoutGroup layout = columnRect.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.spacing = RowSpacing;
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = layout.childForceExpandHeight = false;

                foreach (Entry entry in column)
                    AddEntry(columnRect, entry);
            }
        }

        // 스킬 키는 슬롯 UI와 같이 SkillCaster.SlotKeys에서 읽어, 키를 바꾸면 안내도 따라간다
        private static Entry[][] BuildColumns()
        {
            string firstSkill = SkillCaster.GetKeyLabel(0);
            string lastSkill = SkillCaster.GetKeyLabel(SkillCaster.SlotKeys.Length - 1);

            return new[]
            {
                new[]
                {
                    new Entry("이동", null, null, "W", "A", "S", "D"),
                    new Entry("달리기", "누르는 동안", null, "Shift"),
                    new Entry("구르기", "구르는 동안 무적", null, "Space"),
                    new Entry("카메라 회전", null, null, "마우스"),
                },
                new[]
                {
                    new Entry("기본 공격 / 물체 던지기", null, null, "좌클릭"),
                    new Entry("물체 들기", "누르는 동안", null, "우클릭"),
                    new Entry("스킬", null, "~", firstSkill, lastSkill),
                    new Entry("빠른 기상", "쓰러졌을 때", null, "Space"),
                },
            };
        }

        private void AddEntry(Transform column, Entry entry)
        {
            RectTransform row = NewLayoutRow($"Row {entry.Action}", column);

            // 키캡 칸: 너비를 맞춰 두어 설명이 세로로 줄을 서게 한다
            RectTransform keys = NewLayoutRow("Keys", row);
            keys.gameObject.AddComponent<LayoutElement>().minWidth = KeyColumnWidth;
            for (int i = 0; i < entry.Keys.Length; i++)
            {
                if (i > 0 && string.IsNullOrEmpty(entry.Joiner) == false)
                    KeyHintBuilder.AddSubText(keys, "Joiner", entry.Joiner, font, 1f);
                KeyHintBuilder.AddKey(keys, entry.Keys[i], font, 1f);
            }

            KeyHintBuilder.AddText(row, "Action", entry.Action, font, KeyHintBuilder.DefaultFontSize, KeyHintBuilder.TextColor);
            if (string.IsNullOrEmpty(entry.Note)) return;

            AddSpacer(row, ActionNoteGap, horizontal: true);
            KeyHintBuilder.AddSubText(row, "Note", entry.Note, font, 1f);
        }

        // 부모 레이아웃이 크기를 정하므로 KeyHintBuilder 줄의 ContentSizeFitter는 끄고, 왼쪽부터 늘어놓는다
        private static RectTransform NewLayoutRow(string objectName, Transform parent)
        {
            RectTransform row = KeyHintBuilder.NewRow(objectName, parent, 1f);
            row.GetComponent<ContentSizeFitter>().enabled = false;
            row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
            return row;
        }

        private static void AddSpacer(Transform parent, float size, bool horizontal = false)
        {
            if (size <= 0f) return;

            LayoutElement spacer = NewUI("Spacer", parent).gameObject.AddComponent<LayoutElement>();
            if (horizontal) spacer.minWidth = size;
            else spacer.minHeight = size;
        }

        private static RectTransform NewUI(string objectName, Transform parent)
        {
            GameObject go = new(objectName, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
    }
}
