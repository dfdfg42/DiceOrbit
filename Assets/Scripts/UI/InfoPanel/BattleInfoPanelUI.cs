using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.Data;
using DiceOrbit.UI.Skin;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 화면 오른쪽 상시 정보 패널 (스펙: Docs/superpowers/specs/2026-07-05-battle-info-panel-design.md).
    ///
    /// ── 에디터 소유 레이아웃 ─────────────────────────────────────
    /// 씬의 `InfoPanelCanvas/Panel/Body`가 세로 흐름(VerticalLayoutGroup)이고, 헤더·액티브·패시브·모디파이어
    /// 섹션이 그 안에 순서대로 쌓인다. 이 컴포넌트는 슬롯 참조에 "내용만" 채워 넣고, 각 섹션은 내용 높이만큼
    /// 자라며 비면 통째로 숨겨 아래 섹션이 당겨 올라온다 (2026-09-25 — 구 고정 슬롯은 내용이 넘치면 잘렸다).
    ///
    /// 슬롯은 씬에서 배치하고 인스펙터 슬롯에 배선한다.
    ///
    /// 행 모양 커스텀: cardPrefab에 GlossaryCardUI 프리팹을 꽂으면 항목이 카드로 렌더링된다.
    /// 비워두면 기본 텍스트 행(InfoPanelRows)으로 렌더링.
    /// </summary>
    public class BattleInfoPanelUI : MonoBehaviour
    {
        public static BattleInfoPanelUI Instance { get; private set; }

        [SerializeField] private RectTransform highlightRect;   // 튜토리얼 스포트라이트 대상(오른쪽 패널 배경). 미배선 시 transform 폴백.

        /// <summary>튜토리얼 하이라이트용 — 정보 패널 rect(오른쪽 패널). transform이 RectTransform이 아닐 수 있어 배선 필수.</summary>
        public RectTransform PanelRect => highlightRect != null ? highlightRect : transform as RectTransform;

        [Header("헤더 슬롯 (씬에서 배치)")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private TextMeshProUGUI flavorText;
        private bool _flavorHoverRegistered;   // 상태이상 줄의 키워드 링크 호버 등록 1회 가드

        [Header("섹션 컨테이너 슬롯 (씬에서 배치 — 내용 행이 이 안에 생성됨)")]
        [SerializeField] private TextMeshProUGUI activesTitle;      // 액티브 섹션 제목 (캐릭터 "액티브" / 몬스터 "다음 행동"으로 교체됨)
        [SerializeField] private RectTransform activesContainer;
        [SerializeField] private RectTransform passivesContainer;
        [SerializeField] private RectTransform modifiersContainer;   // 장착 모디파이어
        // (상태이상 섹션 철거 — 헤더의 flavorText 한 줄로 요약)
        // (키워드 섹션 철거 — 정의는 텍스트 링크 호버 시 커서 옆 툴팁. KeywordLinkHover)
        // (타일 섹션 철거 — 왼쪽 위 독립 패널로 분리. TileInfoPanelUI)

        [Header("기타 슬롯")]
        [SerializeField] private GameObject emptyState;             // 대상 없을 때 표시할 오브젝트

        [Header("행 프리팹 (선택 — 비우면 기본 텍스트 행)")]
        [SerializeField] private GlossaryCardUI cardPrefab;

        [Header("스킨")]
        [SerializeField] private TileAttributeVisualDatabase attributeVisuals;
        [SerializeField] private float refreshInterval = 0.5f;

        [Header("정렬")]
        [Tooltip("사이드바는 배경 레이어 — 일반 UI(0)와 팝업(캐릭터 액션 패널 등)이 항상 위에 그려지도록 음수 유지")]
        [SerializeField] private int panelSortingOrder = -5;

        private InfoPanelSelectionController _selection;
        private float _nextRefresh;
        private object _lastTargetKey;          // 대상 변경 감지용

        // ═══════════════════════════════════════════════════════
        // 초기화
        // ═══════════════════════════════════════════════════════

        public static void EnsureInstance()
        {
            if (Instance != null) return;

            Instance = FindFirstObjectByType<BattleInfoPanelUI>();
            if (Instance == null)
                Debug.LogWarning("[BattleInfoPanelUI] 씬에 인스턴스가 없습니다. 씬에 패널을 배치하고 슬롯을 배선해주세요.");
        }

        /// <summary>
        /// 패널 전체 표시/숨김. 전체 화면 UI(보상/모집/결과 등)가 열릴 때 false, 닫힐 때 true.
        /// 숨김 중에는 호버/핀 갱신도 멈춘다.
        /// </summary>
        public static void SetVisible(bool visible)
        {
            if (Instance == null) return;
            Instance.SetPanelVisible(visible);
        }

        private GameObject _canvasRoot;

        private void SetPanelVisible(bool visible)
        {
            if (_canvasRoot == null)
                _canvasRoot = transform.Find("InfoPanelCanvas")?.gameObject;

            if (_canvasRoot != null) _canvasRoot.SetActive(visible);
            if (_selection != null) _selection.enabled = visible;   // 호버/핀 갱신 중지
            enabled = visible;                                       // 렌더 루프 중지

            if (!visible)
            {
                // 패널 숨김 시 월드 인디케이터/타일 패널도 제거
                Visuals.PassiveZoneIndicator.Instance?.Hide();
                Visuals.IntentTileLiftEffect.Instance?.Hide();
                Visuals.MonsterThreatOutline.Instance?.Hide();
                TileInfoPanelUI.Instance?.Hide();
            }
            if (visible) _lastTargetKey = new object();              // 다시 켜질 때 강제 리렌더
        }

        /// <summary>
        /// 조회 대상별 월드 인디케이터 동기화.
        /// 캐릭터 → 패시브 구역 테두리 / 몬스터 → 공격 예정 타일 외곽선 / 그 외 → 모두 숨김.
        /// </summary>
        private static void SyncWorldIndicators(IBattleInfoProvider unit)
        {
            Visuals.PassiveZoneIndicator.EnsureInstance();
            Visuals.IntentTileLiftEffect.EnsureInstance();
            Visuals.MonsterThreatOutline.EnsureInstance();
            var passiveZones = Visuals.PassiveZoneIndicator.Instance;
            var lift = Visuals.IntentTileLiftEffect.Instance;
            var outline = Visuals.MonsterThreatOutline.Instance;

            if (unit is Core.Character ch)
            {
                passiveZones?.Show(ch);
                lift?.Hide();
                outline?.Hide();
            }
            else if (unit is Core.Monster m)
            {
                // 몬스터 → 공격 예정 타일 '둘레 외곽선'(정체성 색). 기존 리프트 대신 사용.
                var tiles = Visuals.IntentTileLiftEffect.CollectIntentTiles(m);
                Color col = Visuals.MonsterIdentityManager.Instance != null
                    ? Visuals.MonsterIdentityManager.Instance.GetColor(m)
                    : new Color(1f, 0.45f, 0.3f, 1f);
                outline?.ShowTiles(tiles, col);
                lift?.Hide();
                passiveZones?.Hide();
            }
            else
            {
                passiveZones?.Hide();
                lift?.Hide();
                outline?.Hide();
            }
        }

        /// <summary>패널 캔버스 정렬값 적용. 액션 패널 등 팝업이 항상 패널 위에 그려지게 한다.</summary>
        private void ApplyPanelSortingOrder()
        {
            var canvasTr = transform.Find("InfoPanelCanvas");
            var canvas = canvasTr != null ? canvasTr.GetComponent<Canvas>() : null;
            if (canvas != null) canvas.sortingOrder = panelSortingOrder;
        }

        /// <summary>UI 요소(파티 로스터 등)가 특정 유닛을 패널에 임시 표시. 벗어나면 ClearUnitExternal 호출.</summary>
        public void ShowUnitExternal(IBattleInfoProvider unit) => _selection?.SetExternalHover(unit);

        /// <summary>ShowUnitExternal로 지정한 표시를 해제.</summary>
        public void ClearUnitExternal(IBattleInfoProvider unit) => _selection?.ClearExternalHover(unit);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _selection = GetComponent<InfoPanelSelectionController>();
            if (_selection == null) _selection = gameObject.AddComponent<InfoPanelSelectionController>();

            // 패널 텍스트 속 키워드 링크 호버 → 커서 옆 정의 툴팁
            if (GetComponent<KeywordLinkHover>() == null) gameObject.AddComponent<KeywordLinkHover>();

            if (attributeVisuals == null)
                attributeVisuals = Resources.Load<TileAttributeVisualDatabase>("UI/TileAttributeVisualDatabase");

            // 씬에 저장된 캔버스에도 최신 정렬값 강제 (기존 생성 레이아웃의 100 등 옛 값 교정)
            ApplyPanelSortingOrder();
        }

        private void Update()
        {
            object key = (object)_selection.CurrentUnit ?? _selection.CurrentTile;
            bool targetChanged = !ReferenceEquals(key, _lastTargetKey);
            if (!targetChanged && Time.unscaledTime < _nextRefresh) return;

            _lastTargetKey = key;
            _nextRefresh = Time.unscaledTime + refreshInterval;
            Render();
        }

        // ═══════════════════════════════════════════════════════
        // 렌더링 — 고정 슬롯에 내용만 채운다
        // ═══════════════════════════════════════════════════════

        private void Render()
        {
            ClearAllSlots();

            var unit = _selection.CurrentUnit;
            if (unit is Component c && c == null) unit = null;   // 파괴된 유닛 방어

            // 월드 인디케이터 연동: 캐릭터 → 패시브 구역 테두리 / 몬스터 → 공격 타일 외곽선
            SyncWorldIndicators(unit);

            if (unit != null)
            {
                if (emptyState != null) emptyState.SetActive(false);
                var d = unit.GetBattleInfo();
                SetSectionVisibility(unitSections: true,
                    activesSection: HasAny(d.Actives),
                    passivesSection: HasAny(d.Passives),
                    modifiersSection: HasAny(d.Modifiers));
                RenderUnit(d);

                // 밟은 타일 → 왼쪽 위 독립 타일 패널
                SyncTilePanel(d.CurrentTile);
                return;
            }

            var tile = _selection.CurrentTile;
            if (tile != null)
            {
                // 타일 단독 뷰: 오른쪽 패널은 비우고 왼쪽 위 타일 패널만
                if (emptyState != null) emptyState.SetActive(false);
                SetSectionVisibility(unitSections: false, activesSection: false, passivesSection: false, modifiersSection: false);
                SyncTilePanel(tile.GetTileInfo());
                return;
            }

            SetSectionVisibility(unitSections: false, activesSection: false, passivesSection: false, modifiersSection: false);      // 빈 상태: 안내 문구만
            if (emptyState != null) emptyState.SetActive(true);
            SyncTilePanel(null);
        }

        /// <summary>
        /// 패널 왼쪽에 도킹된 독립 타일 패널 동기화.
        /// 말할 거리가 있는 타일(레벨업 타일이거나 속성이 붙은 타일)만 띄운다 — 맨 일반 타일은 그림만 남아 소음이라 숨김 (2026-09-25).
        /// </summary>
        private static void SyncTilePanel(TileInfoData? tile)
        {
            if (tile.HasValue && TileInfoPanelUI.HasContent(tile.Value))
            {
                TileInfoPanelUI.EnsureInstance();
                TileInfoPanelUI.Instance?.Show(tile.Value);
            }
            else
            {
                TileInfoPanelUI.Instance?.Hide();
            }
        }

        private static bool HasAny<T>(IReadOnlyList<T> list) => list != null && list.Count > 0;

        private void ClearAllSlots()
        {
            SetText(nameText, "");
            SetText(hpText, "");
            SetText(flavorText, "");
            SetText(ResolveActivesTitle(), InfoPanelRows.FormatSectionTitle("액티브"));   // 기본 라벨로 복원
            ClearContainer(activesContainer);
            ClearContainer(passivesContainer);
            ClearContainer(modifiersContainer);
        }

        /// <summary>상태이상 줄(flavorText)을 키워드 링크 호버 대상으로 1회 등록.</summary>
        private void RegisterFlavorHover()
        {
            if (_flavorHoverRegistered || flavorText == null) return;
            KeywordLinkHover.Register(flavorText);
            _flavorHoverRegistered = true;
        }

        private void RenderUnit(UnitInfoData d)
        {
            // ── Header ──
            SetText(nameText, d.Name);
            SetText(hpText, $"HP {d.CurrentHp}/{d.MaxHp}"
                + (d.Attack > 0 ? $"   공격력 {d.Attack}" : "")
                + (d.Armor > 0 ? $"   방어도 {d.Armor}" : ""));

            // 상태이상 라인 (체력 바로 아래): 별도 섹션 대신 헤더에 요약 — 없으면 "상태이상 없음"
            // 설명이 있는 상태이상은 키워드 링크(<link="kw:...">)로 감싸 KeywordLinkHover가
            // 호버 시 커서 옆 툴팁으로 설명을 띄운다 (스킬 설명 속 키워드와 같은 언어).
            string statusLine = "";
            if (d.Statuses != null)
            {
                foreach (var s in d.Statuses)
                {
                    string stack = string.IsNullOrWhiteSpace(s.StackText) ? "" : $" {s.StackText}";
                    string colorHex = ColorUtility.ToHtmlStringRGB(InfoPanelRows.OnLight(s.Color));
                    string one = string.IsNullOrWhiteSpace(s.Description)
                        ? $"<color=#{colorHex}>{s.Name}{stack}</color>"
                        : $"<link=\"kw:{s.Name}\"><color=#{colorHex}><u>{s.Name}</u>{stack}</color></link>";
                    statusLine = statusLine.Length == 0 ? one : $"{statusLine}    {one}";
                }
            }
            SetText(flavorText, statusLine.Length == 0 ? "상태이상 없음" : statusLine);
            RegisterFlavorHover();

            // 액티브 섹션 제목: 캐릭터 "액티브" / 몬스터 "다음 행동" (빌더가 결정)
            if (!string.IsNullOrWhiteSpace(d.ActivesLabel))
                SetText(ResolveActivesTitle(), InfoPanelRows.FormatSectionTitle(d.ActivesLabel));

            // ── Actives ──
            if (d.Actives != null)
            {
                foreach (var a in d.Actives)
                {
                    string title = a.Name;

                    // 메타: 주사위 조건 + 유효 대상 (모디파이어 반영값 — 광역 참격 장착 시 "적 2명")
                    string meta = a.DiceCondition ?? "";
                    if (!string.IsNullOrWhiteSpace(a.TargetLabel))
                        meta = string.IsNullOrWhiteSpace(meta) ? a.TargetLabel : $"{meta} · {a.TargetLabel}";

                    // 설명 + 이 스킬에 적용 중인 모디파이어 효과 라인 (보라색)
                    string desc = string.IsNullOrWhiteSpace(a.DynamicDescription) ? "" : a.DynamicDescription;
                    if (a.ModifierLines != null)
                        foreach (var line in a.ModifierLines)
                            desc = JoinLines(desc, $"<color={InfoPanelRows.ModifierColorHex}>{line}</color>");

                    AddEntry(activesContainer, title, meta, desc, UiSkin.Current.Ink);
                }
            }

            // ── Passives ──
            if (d.Passives != null)
            {
                foreach (var p in d.Passives)
                {
                    // 동적 설명이 있으면 그것만 — 에셋의 정적 설명(FlavorText)은 같은 규칙 문구라
                    // 둘을 이어붙이면 내용이 두 번 반복된다 (2026-08-28). 정적은 동적이 없을 때의 폴백.
                    string desc = string.IsNullOrWhiteSpace(p.DynamicEffect) ? p.FlavorText : p.DynamicEffect;
                    AddEntry(passivesContainer, p.Name, FormatLiveEffect(p), desc, UiSkin.Current.Passive);
                }
            }

            // ── Modifiers (장착 모디파이어) ──
            if (d.Modifiers != null)
            {
                foreach (var m in d.Modifiers)
                {
                    string title = m.Count > 1 ? $"{m.Name} ×{m.Count}" : m.Name;
                    AddEntry(modifiersContainer, title, "", m.Description, UiSkin.Current.Modifier);
                }
            }

            // ── Statuses ── (헤더의 flavorText 한 줄로 요약 — 별도 섹션 없음)

            // (타일 정보는 왼쪽 위 독립 패널(TileInfoPanelUI), 키워드 정의는 링크 호버 툴팁(KeywordLinkHover))
        }

        // ═══════════════════════════════════════════════════════
        // 슬롯 채우기 헬퍼
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 조회 대상에 맞는 섹션만 표시.
        /// 유닛 뷰: 헤더 + 내용이 있는 섹션만 / 빈 상태·타일 뷰: 전부 숨김.
        /// 섹션은 세로 흐름이라 숨기면 아래 섹션이 당겨 올라온다 — 빈 섹션 제목만 덩그러니 남지 않는다.
        /// </summary>
        private void SetSectionVisibility(bool unitSections, bool activesSection, bool passivesSection, bool modifiersSection)
        {
            // 헤더(이름/HP/상태이상 줄): nameText가 속한 헤더 그룹을 통째로 토글
            if (nameText != null && nameText.transform.parent != null)
                SetActiveIfChanged(HeaderRoot(nameText.transform).gameObject, unitSections);

            ToggleSection(activesContainer, unitSections && activesSection);
            ToggleSection(passivesContainer, unitSections && passivesSection);
            ToggleSection(modifiersContainer, unitSections && modifiersSection);
        }

        /// <summary>헤더 그룹 루트 — 이름 칩(NameRow/NameChip) 안에 있어도 "Header"까지 올라간다.</summary>
        private static Transform HeaderRoot(Transform nameTextTr)
        {
            var t = nameTextTr.parent;
            while (t != null && t.name != "Header" && t.parent != null && t.parent.name != "Panel") t = t.parent;
            return t != null ? t : nameTextTr.parent;
        }

        /// <summary>컨테이너의 부모(섹션 루트: 타이틀 포함)를 토글.</summary>
        private static void ToggleSection(RectTransform container, bool active)
        {
            if (container == null) return;
            var go = container.parent != null ? container.parent.gameObject : container.gameObject;
            SetActiveIfChanged(go, active);
        }

        private static void SetActiveIfChanged(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active) go.SetActive(active);
        }

        private static void SetText(TextMeshProUGUI target, string value)
        {
            if (target == null) return;
            target.text = value ?? "";
        }

        private static void ClearContainer(RectTransform container)
        {
            if (container == null) return;
            InfoPanelRows.Clear(container);
        }

        /// <summary>
        /// 항목 1개(제목/메타/설명)를 컨테이너에 추가. cardPrefab이 있으면 카드로, 없으면 텍스트 행으로.
        /// icon이 있으면 제목 행 왼쪽에 아이콘 표시 (타일 속성 등).
        /// </summary>
        /// <summary>패시브 제목 옆 '지금 효과' — 걸려 있으면 굵은 잉크, 꺼져 있으면 메타 기본색(흐린 회색).</summary>
        private static string FormatLiveEffect(PassiveInfoData p)
        {
            if (string.IsNullOrWhiteSpace(p.LiveEffect)) return "";
            if (!p.LiveActive) return p.LiveEffect;
            return $"<b><color=#{ColorUtility.ToHtmlStringRGB(UiSkin.Current.Ink)}>{p.LiveEffect}</color></b>";
        }

        private void AddEntry(RectTransform container, string title, string meta, string desc, Color titleColor,
            Sprite icon = null, Color? iconTint = null)
        {
            if (container == null) return;

            if (cardPrefab != null)
            {
                var card = Instantiate(cardPrefab, container);
                // 설명 속 키워드를 링크로 감싸 호버 시 커서 옆 툴팁으로 정의 표시
                string linkedDesc = TooltipKeywordFormatter.InsertKeywordLinks(desc, onLightBackground: true);
                card.SetStatus(title, meta, "", linkedDesc, titleColor);
                if (icon != null) card.SetIcon(icon, iconTint ?? Color.white);
                KeywordLinkHover.Register(card.DescText);
                return;
            }

            // 항목 = 세로 그룹 1개(제목 행 + 설명). 컨테이너 spacing이 항목 사이, 그룹 spacing이 제목↔설명 간격.
            // 제목은 볼드·색, 메타(주사위 조건·대상)는 작고 흐리게, 설명은 보통 굵기 잉크 — 위계가 한눈에 읽히게 (2026-09-25).
            var entry = InfoPanelRows.AddEntry(container);
            string line = string.IsNullOrWhiteSpace(meta)
                ? title
                : $"{title}  <size={InfoPanelRows.EntryMetaSize}><color={InfoPanelRows.MutedColorHex}>{meta}</color></size>";
            InfoPanelRows.AddIconTextRow(entry, icon, iconTint ?? Color.white, line, InfoPanelRows.EntryTitleSize, titleColor, FontStyles.Bold);
            if (!string.IsNullOrWhiteSpace(desc))
                InfoPanelRows.AddText(entry, desc, InfoPanelRows.EntryBodySize, UiSkin.Current.Ink, FontStyles.Normal, linkKeywords: true);
        }

        /// <summary>액티브 섹션 타이틀 TMP (씬 배선 필수 — 비어 있으면 에러로 알리고 제목 교체를 건너뛴다).</summary>
        private TextMeshProUGUI ResolveActivesTitle()
        {
            if (activesTitle == null && !_activesTitleMissingReported)
            {
                _activesTitleMissingReported = true;
                Debug.LogError("[BattleInfoPanelUI] activesTitle 슬롯이 비어 있습니다 — 씬 InfoPanelCanvas/Panel/Body/ActivesSection/TitleRow/TitleChip/Title을 배선하세요.", this);
            }
            return activesTitle;
        }
        private bool _activesTitleMissingReported;

        private static string JoinLines(string a, string b)
        {
            bool hasA = !string.IsNullOrWhiteSpace(a);
            bool hasB = !string.IsNullOrWhiteSpace(b);
            if (hasA && hasB) return $"{a}\n{b}";
            if (hasA) return a;
            if (hasB) return b;
            return "";
        }

    }
}
