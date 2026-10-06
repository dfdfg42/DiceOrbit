using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Visuals;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.UI.Skin;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 체력바 아래 상태이상 아이콘 줄 (스펙 2026-07-29 §③). CharacterUI/MonsterUI 공용.
    /// 유닛 StatusEffects.OnChanged 구독 → 아이콘 + 우하단 스택 수(값>0, 숫자만) 나열.
    ///
    /// 호버 설명: 이 월드 캔버스는 레이캐스트가 의도적으로 꺼져 있으므로(캐릭터 클릭 보호)
    /// IPointerEnter 대신 KeywordLinkHover 방식의 폴링 히트테스트로 커서 옆 툴팁을 띄운다.
    /// </summary>
    public class StatusIconRow : MonoBehaviour
    {
        [Header("배치 (캔버스 자식 좌표 = 픽셀 스케일 — HP바 높이 14~20 기준)")]
        [SerializeField] private float iconSize = 22f;     // 그림 아이콘(2026-10-06)은 글자 칩보다 조금 커야 실루엣이 읽힌다
        [SerializeField] private float spacing = 2f;
        [SerializeField] private float stackFontScale = 0.42f;   // 우하단 스택 수 — 그림을 덮지 않게 작게

        private Unit _unit;
        private Camera _cam;
        private TMP_FontAsset _font;

        // 히트테스트용: (아이콘 rect, 툴팁 텍스트)
        private readonly List<(RectTransform rect, string tooltip)> _icons = new();
        private bool _hoverShown;

        /// <summary>아이콘 줄 생성 + 유닛 구독. CharacterUI/MonsterUI가 HP바 아래 좌표를 지정해 호출.
        /// (캔버스 rect는 런타임에 덮어써지는 명목값이라 형제들처럼 중앙 앵커 + 픽셀 좌표를 쓴다.)</summary>
        public static StatusIconRow Attach(Canvas canvas, Unit unit, Camera cam, Vector2 anchoredPos)
        {
            if (canvas == null || unit == null) return null;
            if (canvas.GetComponentInChildren<StatusIconRow>(true) != null) return null;   // 중복 방지

            var go = new GameObject("StatusIconRow", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);   // 형제들(HPBar 등)과 같은 중앙 앵커
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 1f);           // 좌상단 피벗 — HP바 좌측 끝에서 오른쪽으로 하나씩
            rect.anchoredPosition = anchoredPos;

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var row = go.AddComponent<StatusIconRow>();
            row.Init(unit, cam);
            return row;
        }

        private void Init(Unit unit, Camera cam)
        {
            _unit = unit;
            _cam = cam;
            _font = TMP_Settings.defaultFontAsset;

            var layout = GetComponent<HorizontalLayoutGroup>();
            if (layout != null) layout.spacing = spacing;

            if (_unit.StatusEffects != null)
                _unit.StatusEffects.OnChanged += Rebuild;
            Rebuild();
        }

        private void OnDestroy()
        {
            if (_unit != null && _unit.StatusEffects != null)
                _unit.StatusEffects.OnChanged -= Rebuild;
            HideHover();
        }

        // ── 아이콘 재구성 ─────────────────────────────────────

        private void Rebuild()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
            _icons.Clear();

            if (_unit == null || _unit.StatusEffects == null) return;

            foreach (var effect in _unit.StatusEffects.GetActiveEffects())
            {
                if (effect == null) continue;

                var data = TooltipKeywordFormatter.BuildStatusDisplayData(
                    effect.Type.ToString(), effect.Value, effect.Duration);

                StatusVisualLibrary.TryGet(effect.Type, out var visual);
                var chip = CreateChip(data, visual);

                string tooltip = string.IsNullOrWhiteSpace(data.Description)
                    ? $"<b>[{data.Name}]</b>"
                    : $"<b>[{data.Name}]</b>\n{data.Description}";
                _icons.Add(((RectTransform)chip.transform, tooltip));
            }

            gameObject.SetActive(_icons.Count > 0);
        }

        private GameObject CreateChip(TooltipKeywordFormatter.StatusDisplayData data, StatusVisualLibrary.Entry visual)
        {
            var go = new GameObject("StatusIcon", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = iconSize; le.preferredHeight = iconSize;

            if (visual != null && visual.Icon != null)
            {
                var img = go.AddComponent<Image>();
                img.sprite = visual.Icon;
                img.color = visual.Tint;
                img.preserveAspect = true;
                img.raycastTarget = false;   // 클릭 보호 (캔버스 규약)
            }
            else
            {
                // 폴백: 상태색 원형 칩 + 이름 첫 글자
                var bg = go.AddComponent<Image>();
                UiSkin.Current.ApplyCircle(bg, new Color(data.Color.r, data.Color.g, data.Color.b, 0.9f));
                bg.raycastTarget = false;

                var letter = CreateLabel(go, data.Name.Substring(0, 1), iconSize * 0.55f, Color.black);
                var lr = letter.rectTransform;
                lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
                lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
            }

            // 스택 수 (값>0일 때, 우하단 — 정보 패널과 같은 표기: 숫자만)
            // 주의: 갓 생성한 TMP에 outlineWidth를 설정하면 내부 머티리얼 미초기화로 NRE —
            // 가독성은 그림자 라벨(검정, 1px 오프셋)로 대신한다.
            if (!string.IsNullOrEmpty(data.StackText))
            {
                float fontSize = iconSize * stackFontScale;
                float hang = iconSize * 0.14f;   // 숫자는 아이콘 우하단 모서리에 살짝 걸쳐 그림을 덜 가린다
                var shadow = CreateLabel(go, data.StackText, fontSize, Color.black);
                shadow.alignment = TextAlignmentOptions.BottomRight;
                shadow.fontStyle = FontStyles.Bold;
                var shr = shadow.rectTransform;
                shr.anchorMin = Vector2.zero; shr.anchorMax = Vector2.one;
                shr.offsetMin = new Vector2(1f, -hang - 1f);
                shr.offsetMax = new Vector2(hang + 1f, -1f);

                var stack = CreateLabel(go, data.StackText, fontSize, Color.white);
                stack.alignment = TextAlignmentOptions.BottomRight;
                stack.fontStyle = FontStyles.Bold;
                var sr = stack.rectTransform;
                sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one;
                sr.offsetMin = new Vector2(0f, -hang);
                sr.offsetMax = new Vector2(hang, 0f);
            }

            return go;
        }

        private TextMeshProUGUI CreateLabel(GameObject parent, string text, float size, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.raycastTarget = false;
            if (_font != null) tmp.font = _font;
            return tmp;
        }

        // ── 호버 (폴링 히트테스트 — 레이캐스터 불필요) ─────────

        private void Update()
        {
            if (_icons.Count == 0) return;

            Vector2 mousePos = UnityEngine.InputSystem.Mouse.current != null
                ? UnityEngine.InputSystem.Mouse.current.position.ReadValue()
                : (Vector2)Input.mousePosition;

            string hit = null;
            foreach (var (rect, tooltip) in _icons)
            {
                if (rect == null) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(rect, mousePos, _cam))
                {
                    hit = tooltip;
                    break;
                }
            }

            if (hit != null)
            {
                HoverTooltipUI.EnsureInstance();
                HoverTooltipUI.Instance?.ShowPinned(hit);
                _hoverShown = true;
            }
            else
            {
                HideHover();
            }
        }

        private void HideHover()
        {
            if (!_hoverShown) return;
            _hoverShown = false;
            HoverTooltipUI.Instance?.HidePinned();
        }
    }
}
