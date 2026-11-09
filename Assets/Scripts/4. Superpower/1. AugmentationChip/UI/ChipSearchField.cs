using System;
using System.Collections.Generic;
using TeamOdd.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 보유 칩 트레이 위쪽, "보유 칩" 제목 오른쪽의 칩 이름 검색 칸과 그 아래에 뜨는 자동 완성 목록.
    /// 글자를 칠 때마다(한글은 조합 중인 글자까지) QueryChanged로 검색어를 알리고, 트레이가 넘겨준 이름 가운데 맞는 것을 목록에 띄운다.
    /// 이름 맞추기는 KoreanSearch를 쓴다 (띄어쓰기 무시, 초성 검색, 치고 있는 마지막 글자).
    /// 목록은 마우스로 누르거나 ↑↓로 고른 뒤 Enter·Tab으로 넣는다. Esc는 목록을 닫고 입력만 끝낸다(검색어는 남긴다). 칸 오른쪽 ×는 검색어를 지운다.
    /// 입력하는 동안 E·F·Esc 단축키는 TextInputFocus로 막는다.
    /// 프리팹을 고치지 않도록 트레이가 깨어날 때(ChipTrayView.Awake) 정렬 버튼을 복제해서 만든다. 테두리·마우스 반응·글꼴은 정렬 버튼과 같고,
    /// 버튼과 구별되도록 바탕만 입력 칸처럼 어둡게 칠한다. 입력 중에는 테두리 선만 밝혀 글자가 묻히지 않게 한다.
    /// </summary>
    public class ChipSearchField : MonoBehaviour
    {
        // 칩 캔버스(3840x2160) 기준 크기
        private const float MaxWidth = 720f;
        private const float TitleGap = 36f;          // "보유 칩" 글자 끝 ~ 검색 칸
        private const float FallbackTop = 24f;       // 제목을 못 찾았을 때 트레이 위쪽 ~ 검색 칸
        private const float TextInset = 24f;         // 칸 왼쪽 ~ 글자
        private const float ClearButtonWidth = 72f;
        private const float ClearStrokeLength = 28f; // × 선 하나의 길이
        private const float ClearStrokeThickness = 4f;
        private const float ListGap = 6f;            // 칸 아래 ~ 목록
        private const float ListPadding = 6f;
        private const float RowHeight = 66f;
        private const int CaretWidth = 3;
        private const int MaxSuggestions = 6;
        private const int CharacterLimit = 20;
        private const string PlaceholderText = "칩 이름 검색";

        // 목록은 아래 필터 토글·정렬 버튼을 덮으므로 거의 불투명하게 칠한다. 테두리는 정렬 버튼 테두리와 같은 색
        private static readonly Color ListColor = new(0.055f, 0.06f, 0.07f, 0.98f);
        private static readonly Color ListBorderColor = new(0.84705883f, 0.8235294f, 0.76862746f, 0.22f);
        private static readonly Color RowHighlightColor = new(0.84705883f, 0.8235294f, 0.76862746f, 0.12f);
        // 칸 바탕. 정렬 버튼(옅은 회색 바탕)보다 어둡게 눌러 입력 칸으로 보이게 하고, 테두리 선은 그대로 둔다
        private static readonly Color FieldColor = new(0.03f, 0.035f, 0.04f, 0.8f);
        private const float DimAlpha = 0.55f; // 안내 글자와 자동 완성에서 검색어와 맞지 않는 부분

        private struct Suggestion
        {
            public string Name;
            public int Start; // 검색어와 맞는 구간 [Start, End)
            public int End;
        }

        private sealed class Row
        {
            public GameObject GameObject;
            public Image Background;
            public Text Label;
        }

        private readonly List<Row> rows = new();
        private readonly List<Suggestion> suggestions = new();
        private InputField field;
        private VectorPanel frame; // 정렬 버튼에서 복제해 온 테두리
        private bool litByFocus;   // 입력 중이라 테두리를 밝혀 두었는지
        private GameObject clearButton;
        private Graphic[] clearStrokes; // ×를 이루는 선 두 개
        private Color clearColor;       // 마우스를 올렸을 때 × 색 (검색 글자색)
        private RectTransform list;
        private Func<IEnumerable<string>> candidates;
        private string dimColorTag; // 자동 완성 글자에서 흐리게 칠할 부분의 색 (#RRGGBBAA)
        private int highlighted = -1;
        private bool wasFocused;

        public event Action<string> QueryChanged;

        // 칸에 보이는 그대로의 검색어. 한글은 조합 중인 글자까지 들어 있다
        public string Query { get; private set; } = string.Empty;

        public bool Matches(string name) => Query.Length == 0 || KoreanSearch.TryMatch(name, Query, out _, out _);

        /// <param name="tray">검색 칸과 목록을 놓을 트레이</param>
        /// <param name="template">생김새를 따올 정렬 버튼</param>
        /// <param name="title">"보유 칩" 제목. 이 줄 높이에 두고 글자와 겹치지 않게 한다. 없으면 null</param>
        /// <param name="right">칸 오른쪽 끝(트레이 왼쪽 기준). 정렬 버튼 줄 오른쪽 끝에 맞춘다</param>
        /// <param name="candidates">자동 완성 후보 이름. 목록을 새로 그릴 때마다 부른다</param>
        public static ChipSearchField Create(RectTransform tray, Button template, Text title, float right, Func<IEnumerable<string>> candidates)
        {
            GameObject go = Instantiate(template.gameObject, tray, false);
            go.name = "Search";
            // 버튼 기능만 떼고 테두리(VectorPanel)와 글자는 그대로 쓴다
            DestroyImmediate(go.GetComponent<Button>());

            ChipSearchField search = go.AddComponent<ChipSearchField>();
            search.candidates = candidates;
            search.Build(template, title, right);
            return search;
        }

        // 검색어를 지우고 목록을 닫는다. 패널을 다시 열 때와 × 버튼이 부른다
        public void Clear()
        {
            if (field.isFocused)
                field.DeactivateInputField();

            field.SetTextWithoutNotify(string.Empty);
            SetQuery(string.Empty);
            Hide();
        }

        private void Update()
        {
            bool focused = field.isFocused;
            if (focused)
            {
                TextInputFocus.MarkTyping();

                string live = LiveText();
                if (live != Query)
                    SetQuery(live);
                else if (wasFocused == false)
                    RefreshSuggestions(); // 칸을 다시 눌러 입력을 시작하면 남아 있던 검색어로 목록을 다시 띄운다
            }

            wasFocused = focused;
        }

        // 입력 칸은 EventSystem(Update)에서 키를 받으므로 같은 프레임의 ↑↓·Tab 뒷정리는 그 뒤인 여기서 한다
        private void LateUpdate()
        {
            bool focused = field.isFocused;

            // 입력 중에는 마우스를 올린 것처럼 테두리 선을 밝혀 둔다(바탕은 어두운 채라 글자가 잘 보인다). 마우스가 나가면 VectorPanel이 끄므로 매 프레임 다시 켠다
            // 입력이 끝나면 마우스가 칸 위에 있는지에 따라 돌려놓는다
            if (frame != null)
            {
                if (focused)
                    frame.Highlighted = true;
                else if (litByFocus)
                    frame.Highlighted = IsPointerOver((RectTransform)transform);
                litByFocus = focused;
            }

            if (clearButton.activeSelf)
                SetClearHover(IsPointerOver((RectTransform)clearButton.transform));

            if (focused)
            {
                HandleListKeys();
            }
            // 입력이 끝나면 목록을 닫는다. 목록을 누르는 중이면 입력이 먼저 끝나므로 누른 이름이 들어갈 때(Pick)까지 기다린다
            else if (list.gameObject.activeSelf && IsPointerOver(list) == false)
            {
                Hide();
            }
        }

        private void OnDisable()
        {
            wasFocused = false;
            litByFocus = false; // VectorPanel도 꺼질 때 밝힌 테두리를 지운다
            if (list != null)
                Hide();
        }

        #region Build

        private void Build(Button template, Text title, float right)
        {
            RectTransform rect = (RectTransform)transform;
            float height = ((RectTransform)template.transform).sizeDelta.y;
            float top = FallbackTop;
            float left = right - MaxWidth;
            if (title != null)
            {
                // 제목 줄과 같은 높이에 두고, 제목 글자와 겹치지 않을 만큼만 넓힌다 (제목은 좌상단 기준)
                RectTransform titleRect = title.rectTransform;
                top = -titleRect.anchoredPosition.y;
                height = titleRect.sizeDelta.y;
                left = Mathf.Max(left, titleRect.anchoredPosition.x + title.preferredWidth + TitleGap);
            }

            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(right - left, height);

            Text label = GetComponentInChildren<Text>(true);
            Color textColor = label.color;
            dimColorTag = "#" + ColorUtility.ToHtmlStringRGBA(WithAlpha(textColor, textColor.a * DimAlpha));

            label.name = "Text";
            label.text = string.Empty;
            label.supportRichText = false; // InputField는 서식 없는 글자만 다룬다
            label.alignment = TextAnchor.MiddleLeft;
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.offsetMin = new Vector2(TextInset, 0f);
            labelRect.offsetMax = new Vector2(-ClearButtonWidth, 0f);

            Text placeholder = Instantiate(label, transform);
            placeholder.name = "Placeholder";
            placeholder.text = PlaceholderText;
            placeholder.color = WithAlpha(textColor, textColor.a * DimAlpha);
            placeholder.raycastTarget = false;

            clearButton = CreateClearButton(textColor);
            list = CreateList(rect, label);

            frame = GetComponent<VectorPanel>();
            if (frame != null)
                frame.TrackColor = FieldColor;
            field = gameObject.AddComponent<InputField>();
            field.transition = Selectable.Transition.None; // 마우스 반응은 복제해 온 VectorPanel이 한다
            field.targetGraphic = GetComponent<Graphic>();
            field.navigation = new Navigation { mode = Navigation.Mode.None }; // ↑↓는 다른 버튼으로 옮겨 가지 않고 목록을 고른다
            field.textComponent = label;
            field.placeholder = placeholder;
            field.lineType = InputField.LineType.SingleLine;
            field.characterLimit = CharacterLimit;
            field.customCaretColor = true;
            field.caretColor = textColor;
            field.caretWidth = CaretWidth;
            field.selectionColor = WithAlpha(textColor, 0.25f);
            field.onSubmit.AddListener(OnSubmit);
            field.onEndEdit.AddListener(OnEndEdit);
        }

        // 칸 오른쪽 끝의 ×. 검색어가 있을 때만 보인다
        // 글꼴의 × 글자는 줄 높이 가운데보다 아래에 찍혀 치우쳐 보이므로, 창 X 버튼처럼 선 두 개를 칸 가운데에 겹쳐 그린다
        private GameObject CreateClearButton(Color color)
        {
            RectTransform rect = NewUI("Clear", transform);
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(1f, 0.5f);
            rect.offsetMin = new Vector2(-ClearButtonWidth, 0f);
            rect.offsetMax = Vector2.zero;

            // 누르는 영역은 투명하게 칠한 칸 전체
            Image hit = rect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            clearColor = color;
            float[] angles = { 45f, -45f };
            clearStrokes = new Graphic[angles.Length];
            for (int i = 0; i < angles.Length; i++)
            {
                RectTransform stroke = NewUI("Stroke", rect);
                stroke.anchorMin = stroke.anchorMax = stroke.pivot = new Vector2(0.5f, 0.5f);
                stroke.anchoredPosition = Vector2.zero;
                stroke.sizeDelta = new Vector2(ClearStrokeLength, ClearStrokeThickness);
                stroke.localRotation = Quaternion.Euler(0f, 0f, angles[i]);
                Image image = stroke.gameObject.AddComponent<Image>();
                image.raycastTarget = false;
                clearStrokes[i] = image;
            }
            SetClearHover(false);

            // 선이 둘이라 색 전환은 버튼 대신 LateUpdate에서 마우스 위치로 한다
            Button button = rect.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(Clear);

            rect.gameObject.SetActive(false);
            return rect.gameObject;
        }

        // 마우스를 올리면 글자색 그대로, 아니면 안내 글자처럼 흐리게
        private void SetClearHover(bool hover)
        {
            Color color = hover ? clearColor : WithAlpha(clearColor, clearColor.a * DimAlpha);
            foreach (Graphic stroke in clearStrokes)
                stroke.color = color;
        }

        // 검색 칸 바로 아래 목록. 트레이 맨 위에 그려서 필터 토글·정렬 버튼·보유 칩을 덮는다
        private RectTransform CreateList(RectTransform searchRect, Text label)
        {
            RectTransform listRect = NewUI("Search Suggestions", searchRect.parent);
            listRect.SetAsLastSibling();
            listRect.anchorMin = listRect.anchorMax = listRect.pivot = new Vector2(0f, 1f);
            listRect.anchoredPosition = searchRect.anchoredPosition - new Vector2(0f, searchRect.sizeDelta.y + ListGap);
            listRect.sizeDelta = new Vector2(searchRect.sizeDelta.x, ListPadding * 2f);

            Image background = listRect.gameObject.AddComponent<Image>();
            background.color = ListColor;
            Outline border = listRect.gameObject.AddComponent<Outline>();
            border.effectColor = ListBorderColor;
            border.effectDistance = new Vector2(2f, -2f);

            for (int i = 0; i < MaxSuggestions; i++)
                rows.Add(CreateRow(listRect, i, label));

            listRect.gameObject.SetActive(false);
            return listRect;
        }

        private Row CreateRow(RectTransform listRect, int index, Text label)
        {
            RectTransform rowRect = NewUI($"Suggestion {index}", listRect);
            rowRect.anchorMin = rowRect.anchorMax = rowRect.pivot = new Vector2(0f, 1f);
            rowRect.anchoredPosition = new Vector2(ListPadding, -ListPadding - index * RowHeight);
            rowRect.sizeDelta = new Vector2(listRect.sizeDelta.x - ListPadding * 2f, RowHeight);

            Image background = rowRect.gameObject.AddComponent<Image>();
            background.color = Color.clear; // 투명해도 마우스는 받는다

            Text text = Instantiate(label, rowRect);
            text.name = "Label";
            text.supportRichText = true; // 검색어와 맞는 부분만 밝게 칠한다
            text.raycastTarget = false;
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            // 글자 시작을 검색 칸 글자와 맞춘다
            textRect.offsetMin = new Vector2(TextInset - ListPadding, 0f);
            textRect.offsetMax = new Vector2(-(TextInset - ListPadding), 0f);

            EventTrigger trigger = rowRect.gameObject.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, _ => SetHighlighted(index));
            AddTrigger(trigger, EventTriggerType.PointerClick, data =>
            {
                if (((PointerEventData)data).button == PointerEventData.InputButton.Left)
                    Pick(index);
            });

            return new Row { GameObject = rowRect.gameObject, Background = background, Label = text };
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
        {
            EventTrigger.Entry entry = new() { eventID = type };
            entry.callback.AddListener(action);
            trigger.triggers.Add(entry);
        }

        private RectTransform NewUI(string objectName, Transform parent)
        {
            GameObject go = new(objectName, typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        #endregion

        #region Input

        // ↑↓로 목록을 고르고 Tab으로 고른 이름(없으면 첫 이름)을 넣는다. Enter는 InputField가 받아 OnSubmit으로 온다
        private void HandleListKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.downArrowKey.wasPressedThisFrame)
            {
                MoveHighlight(1);
            }
            else if (keyboard.upArrowKey.wasPressedThisFrame)
            {
                MoveHighlight(-1);
                // 한 줄 입력 칸은 ↑에 커서를 맨 앞으로 옮긴다. 이어서 친 글자가 앞에 붙지 않게 끝으로 돌려놓는다
                field.MoveTextEnd(false);
            }
            else if (keyboard.tabKey.wasPressedThisFrame && list.gameObject.activeSelf)
            {
                Pick(Mathf.Max(highlighted, 0));
            }
        }

        // Enter. 목록에서 고른 이름이 있으면 그 이름으로, 없으면 친 글자 그대로 검색한다
        private void OnSubmit(string text)
        {
            if (highlighted >= 0)
                Pick(highlighted);
            else
                Hide();
        }

        private void OnEndEdit(string text)
        {
            // InputField는 Esc를 받으면 입력을 시작할 때의 글자로 되돌린다. 검색 칸은 목록만 닫고 친 글자를 남긴다
            if (field.wasCanceled)
            {
                field.SetTextWithoutNotify(Query);
                Hide();
                return;
            }

            SetQuery(text);
        }

        // InputField.text에는 조합이 끝난 글자만 들어 있다. 조합 중인 글자를 커서 자리에 끼워 칸에 보이는 그대로 검색한다
        private string LiveText()
        {
            string text = field.text;
            EventSystem eventSystem = EventSystem.current;
            BaseInput input = eventSystem != null && eventSystem.currentInputModule != null ? eventSystem.currentInputModule.input : null;
            string composition = input != null ? input.compositionString : string.Empty;
            if (string.IsNullOrEmpty(composition))
                return text;

            // caretPosition에는 조합 중인 글자 수가 더해져 있다
            int caret = Mathf.Clamp(field.caretPosition - composition.Length, 0, text.Length);
            return text.Insert(caret, composition);
        }

        private static bool IsPointerOver(RectTransform rect)
        {
            // Screen Space - Overlay 캔버스 전제라 카메라 없이(null) 잰다 (AugmentationChipPanel과 같은 방식)
            Mouse mouse = Mouse.current;
            return mouse != null && RectTransformUtility.RectangleContainsScreenPoint(rect, mouse.position.ReadValue(), null);
        }

        #endregion

        #region Suggestions

        private void SetQuery(string value)
        {
            value ??= string.Empty;
            if (value == Query)
                return;

            Query = value;
            clearButton.SetActive(Query.Length > 0);
            RefreshSuggestions();
            QueryChanged?.Invoke(Query);
        }

        // 후보 이름 가운데 검색어와 맞는 것을 앞에서 맞는 이름, 짧은 이름, 가나다 순으로 MaxSuggestions개까지 보여준다
        private void RefreshSuggestions()
        {
            suggestions.Clear();
            highlighted = -1;

            if (Query.Length > 0 && candidates != null)
            {
                foreach (string name in candidates())
                {
                    if (KoreanSearch.TryMatch(name, Query, out int start, out int end))
                        suggestions.Add(new Suggestion { Name = name, Start = start, End = end });
                }

                suggestions.Sort(CompareSuggestions);
                if (suggestions.Count > MaxSuggestions)
                    suggestions.RemoveRange(MaxSuggestions, suggestions.Count - MaxSuggestions);

                // 검색어가 이미 이름 하나와 똑같으면 고를 것이 없다
                if (suggestions.Count == 1 && KoreanSearch.Normalize(suggestions[0].Name) == KoreanSearch.Normalize(Query))
                    suggestions.Clear();
            }

            for (int i = 0; i < rows.Count; i++)
            {
                bool used = i < suggestions.Count;
                rows[i].GameObject.SetActive(used);
                if (used)
                    rows[i].Label.text = Format(suggestions[i]);
            }

            list.sizeDelta = new Vector2(list.sizeDelta.x, suggestions.Count * RowHeight + ListPadding * 2f);
            RefreshRowColors();

            // 입력 중일 때만 띄운다. 입력이 끝난 뒤 목록을 누르는 중이면 보이던 그대로 두고 LateUpdate가 닫는다
            if (suggestions.Count == 0)
                Hide();
            else if (field.isFocused)
                list.gameObject.SetActive(true);
        }

        private static int CompareSuggestions(Suggestion a, Suggestion b)
        {
            int result = a.Start.CompareTo(b.Start);
            if (result == 0)
                result = a.Name.Length.CompareTo(b.Name.Length);
            if (result == 0)
                result = string.CompareOrdinal(a.Name, b.Name);
            return result;
        }

        // 검색어와 맞는 부분은 원래 색, 나머지는 흐리게
        private string Format(Suggestion suggestion)
        {
            string name = suggestion.Name;
            string before = name.Substring(0, suggestion.Start);
            string match = name.Substring(suggestion.Start, suggestion.End - suggestion.Start);
            string after = name.Substring(suggestion.End);
            return $"<color={dimColorTag}>{before}</color>{match}<color={dimColorTag}>{after}</color>";
        }

        private void MoveHighlight(int delta)
        {
            if (list.gameObject.activeSelf == false)
                return;

            // 맨 위에서 ↑를 누르면 고른 것이 없는 상태(-1)로 돌아간다
            highlighted = Mathf.Clamp(highlighted + delta, -1, suggestions.Count - 1);
            RefreshRowColors();
        }

        private void SetHighlighted(int index)
        {
            highlighted = index;
            RefreshRowColors();
        }

        private void RefreshRowColors()
        {
            for (int i = 0; i < rows.Count; i++)
                rows[i].Background.color = i == highlighted ? RowHighlightColor : Color.clear;
        }

        private void Pick(int index)
        {
            if (index < 0 || index >= suggestions.Count)
                return;

            string name = suggestions[index].Name;
            field.SetTextWithoutNotify(name);
            if (field.isFocused)
                field.MoveTextEnd(false);

            SetQuery(name);
            Hide();
        }

        private void Hide()
        {
            highlighted = -1;
            list.gameObject.SetActive(false);
        }

        #endregion

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
