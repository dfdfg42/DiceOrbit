using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DiceOrbit.Data;
using System.Collections;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 몬스터 UI (HP 바, 이름, 공격 의도)
    /// World Space Canvas로 몬스터 위에 표시
    /// </summary>
    public class MonsterUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Core.Monster monster;
        
        [Header("UI Elements")]
        [SerializeField] private Canvas worldCanvas;
        [SerializeField] private Slider hpSlider;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private RectTransform intentBubbleRoot;
        [SerializeField] private Image intentBubbleBg;
        [SerializeField] private Image intentIcon;
        [SerializeField] private TextMeshProUGUI intentText;

        [Header("의도 설명 — 말풍선 옆에 다음 기술을 짧은 글로 (MonsterIntentText)")]
        [Tooltip("설명 칩 루트 (말풍선 오른쪽). 요약이 없는 기술이면 숨긴다")]
        [SerializeField] private RectTransform intentSummaryRoot;
        [SerializeField] private TextMeshProUGUI intentSummaryText;
        [Tooltip("글이 이 폭을 넘으면 줄을 바꾼다 (캔버스 단위)")]
        [SerializeField] private float intentSummaryMaxWidth = 160f;
        [Tooltip("예상 피해를 다시 계산하는 간격(초) — 캐릭터가 움직이거나 상태가 바뀌면 숫자가 달라진다")]
        [SerializeField] private float intentSummaryRefresh = 0.3f;
        [Tooltip("설명 칩이 떠 있는 동안 이름표는 칩 바로 위로 비켜 선다 — 칩 윗변과 이름표 사이 간격 (캔버스 단위)")]
        [SerializeField] private float nameGapAboveSummary = 1f;

        [Header("Armor UI")]
        [SerializeField] private RectTransform armorRoot;
        [SerializeField] private Image armorIcon;
        [SerializeField] private TextMeshProUGUI armorText;
        [SerializeField] private Sprite armorIconSprite;
        [SerializeField] private bool hideArmorWhenZero = true;
        
        [Header("Intent Colors")]
        [SerializeField] private Color attackColor = Color.red;
        [SerializeField] private Color defendColor = Color.blue;
        [SerializeField] private Color buffColor = Color.yellow;
        [SerializeField] private Color specialColor = Color.magenta;
        [SerializeField] private Color neutralBubbleColor = new Color(1f, 1f, 1f, 0.92f);
        
        [Header("Settings")]
        [SerializeField] private Vector3 uiOffset = new Vector3(0, 2f, 0);
        [SerializeField] private bool autoFindMonster = true;
        [Tooltip("체력바 캔버스 정렬 순서. 스프라이트보다 높아야 항상 앞에 보인다.")]
        [SerializeField] private int canvasSortingOrder = 500;
        [Tooltip("체력바 캔버스 크기(월드 스케일). 키우려면 값을 올린다.")]
        [SerializeField] private float canvasScale = 0.1f;
        [SerializeField] private bool hideBubbleWhenNoIntent = true;
        [SerializeField] private bool tintBubbleByIntent = false;
        [SerializeField] private bool showIntentText = true;
        
        [Header("행동 예고 — 깎일 체력 미리 보기")]
        [Tooltip("깎일 구간이 깜빡이는 빠르기 (초당 왕복 수)")]
        [SerializeField] private float forecastBlinkPerSecond = 1.6f;
        [SerializeField] private Color forecastColor = new Color(1f, 0.96f, 0.72f, 1f);
        [SerializeField, Range(0f, 1f)] private float forecastMinAlpha = 0.45f;
        [SerializeField, Range(0f, 1f)] private float forecastMaxAlpha = 1f;

        private Image forecastSegment;   // 체력바 채움 위에 겹치는 '깎일 구간'

        private string shownSummary;             // 지금 칩에 적힌 글 (같으면 다시 배치하지 않는다)
        private float summaryRefreshAt;
        private LayoutElement summaryLayout;
        private float nameBaseY;
        private bool nameBaseCaptured;
        private static readonly System.Collections.Generic.HashSet<string> ReportedMissingSummary = new System.Collections.Generic.HashSet<string>();
        private int forecastLoss;

        [Header("Animation")]
        [SerializeField] private bool animateOnIntentChange = true;
        [SerializeField] private float popScale = 1.12f;
        [SerializeField] private float popDuration = 0.12f;
        
        private Camera mainCamera;
        private int lastIntentVisualKey = int.MinValue;
        private Coroutine popRoutine;
        
        private void Awake()
        {
            // 몬스터 자동 찾기
            if (autoFindMonster && monster == null)
            {
                monster = GetComponentInParent<Core.Monster>();
                
                if (monster == null)
                {
                    Debug.LogWarning("MonsterUI: Monster not found!");
                }
            }
            
            mainCamera = Camera.main;
            AutoResolveIntentRefs();
            AutoResolveArmorRefs();
            EnsureArmorUIExists();
        }
        
        private void Start()
        {
            SetupCanvas();
            UpdateUI();
        }
        
        private void Update()
        {
            UpdateUI();
            UpdateForecastSegment();
            // Canvas 회전은 부모 Monster의 Billboard가 처리함
        }
        
        // ── 행동 예고: 깎일 체력 미리 보기 ─────────────────────

        /// <summary>
        /// 행동 예고 — 이번 행동으로 깎일 체력을 체력바에 깜빡이는 구간으로 미리 보여 준다 (ActionForecastView가 호출).
        /// 0 이하면 지운다. 구간은 지금 체력의 오른쪽 끝에서 왼쪽으로 hpLoss만큼이다.
        /// </summary>
        public void SetForecastLoss(int hpLoss)
        {
            forecastLoss = Mathf.Max(0, hpLoss);
            UpdateForecastSegment();
        }

        /// <summary>깎일 구간이 체력바에서 차지하는 범위 (0~1 비율). 순수 — 자가 테스트.</summary>
        public static void ForecastRange(int currentHp, int maxHp, int hpLoss, out float from, out float to)
        {
            if (maxHp <= 0 || currentHp <= 0 || hpLoss <= 0)
            {
                from = to = 0f;
                return;
            }
            to = Mathf.Clamp01((float)currentHp / maxHp);
            from = Mathf.Clamp01((float)Mathf.Max(0, currentHp - hpLoss) / maxHp);
        }

        private void UpdateForecastSegment()
        {
            bool show = forecastLoss > 0 && monster != null && hpSlider != null && hpSlider.fillRect != null;
            float from = 0f, to = 0f;
            if (show)
            {
                ForecastRange(monster.Stats.CurrentHP, monster.Stats.MaxHP, forecastLoss, out from, out to);
                show = to > from;
            }

            if (!show)
            {
                if (forecastSegment != null && forecastSegment.enabled) forecastSegment.enabled = false;
                return;
            }

            if (forecastSegment == null) forecastSegment = CreateForecastSegment();
            forecastSegment.enabled = true;

            // 구간은 채움(Fill)의 자식 — 채움의 오른쪽 끝에 정확히 붙는다 (Fill Area 기준으로 놓으면 채움이 여백만큼 삐져나온다).
            // 채움 사각형은 지금 체력만큼만 뻗어 있으므로, 그 안에서의 비율로 바꾼다. Filled 방식이면 채움 사각형이 막대 전체다.
            var fillImage = hpSlider.fillRect.GetComponent<Image>();
            bool fullWidthFill = fillImage != null && fillImage.type == Image.Type.Filled;
            if (!fullWidthFill)
            {
                from = from / to;
                to = 1f;
            }

            var rect = forecastSegment.rectTransform;
            rect.anchorMin = new Vector2(from, 0f);
            rect.anchorMax = new Vector2(to, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * forecastBlinkPerSecond * 2f * Mathf.PI);
            var color = forecastColor;
            color.a = Mathf.Lerp(forecastMinAlpha, forecastMaxAlpha, wave);
            forecastSegment.color = color;
        }

        private Image CreateForecastSegment()
        {
            var fill = hpSlider.fillRect;
            var go = new GameObject("ForecastLoss", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(fill, false);   // 채움의 자식 — 채움 위에 그려지고 채움 끝에 맞는다

            // 스프라이트 없이 단색 — 채움 스프라이트(붉은 막대)를 쓰면 색이 곱해져 붉은색에 묻힌다
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// World Space Canvas 설정
        /// </summary>
        private void SetupCanvas()
        {
            if (worldCanvas == null)
            {
                Debug.LogWarning("MonsterUI: WorldCanvas not assigned!");
                return;
            }
            
            worldCanvas.renderMode = RenderMode.WorldSpace;
            worldCanvas.worldCamera = mainCamera;

            // 항상 스프라이트보다 앞에 표시
            worldCanvas.overrideSorting = true;
            worldCanvas.sortingOrder = canvasSortingOrder;

            // 크기 조정
            RectTransform rectTransform = worldCanvas.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.sizeDelta = new Vector2(3, 1f);
                rectTransform.localScale = Vector3.one * canvasScale;
            }

            ConfigureNonBlockingRaycasts();

            // 상태이상 시각화: 체력바 아래 아이콘 줄 + 유닛 위 오버레이 (스펙 2026-07-29)
            // HPBar(중심 x=2.95, 실폭 117) 좌측 끝 x=-56, 바 하단(27.8) 바로 아래 y=26
            StatusIconRow.Attach(worldCanvas, monster, mainCamera, new Vector2(-56f, 26f));
            Visuals.StatusOverlayStack.Attach(monster);
        }

        private void ConfigureNonBlockingRaycasts()
        {
            if (worldCanvas == null) return;

            var raycaster = worldCanvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                raycaster.enabled = false;
            }

            var graphics = worldCanvas.GetComponentsInChildren<Graphic>(true);
            foreach (var graphic in graphics)
            {
                if (graphic == null) continue;
                graphic.raycastTarget = false;
            }

            var canvasGroups = worldCanvas.GetComponentsInChildren<CanvasGroup>(true);
            foreach (var group in canvasGroups)
            {
                if (group == null) continue;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
        }
        
        private void AutoResolveIntentRefs()
        {
            if (intentBubbleRoot == null && intentIcon != null)
            {
                intentBubbleRoot = intentIcon.rectTransform;
            }
            
            if (intentBubbleBg == null && intentBubbleRoot != null)
            {
                intentBubbleBg = intentBubbleRoot.GetComponent<Image>();
            }
        }
        
        /// <summary>
        /// UI 업데이트
        /// </summary>
        private void UpdateUI()
        {
            if (monster == null) return;
            
            MonsterStats stats = monster.Stats;
            
            // HP 슬라이더
            if (hpSlider != null)
            {
                hpSlider.maxValue = stats.MaxHP;
                hpSlider.value = stats.CurrentHP;
            }
            
            // 이름
            if (nameText != null)
            {
                nameText.text = stats.MonsterName;
            }
            
            // HP 텍스트
            if (hpText != null)
            {
                hpText.text = $"{stats.CurrentHP}/{stats.MaxHP}";
            }
            
            // 공격 의도
            UpdateIntent();
            UpdateArmor();
        }
        
        /// <summary>
        /// 공격 의도 UI 업데이트
        /// </summary>
        private void UpdateIntent()
        {
            if (monster == null)
            {
                SetIntentVisible(false);
                return;
            }

            AttackIntent intent = monster.CurrentIntent;
            if (intent == null)
            {
                if (hideBubbleWhenNoIntent)
                {
                    SetIntentVisible(false);
                }
                return;
            }

            SetIntentVisible(true);

            IntentType type = intent.Type;
            int visualKey = BuildIntentVisualKey(intent);
            bool changed = visualKey != lastIntentVisualKey;
            lastIntentVisualKey = visualKey;

            if (intentBubbleBg != null)
            {
                intentBubbleBg.color = tintBubbleByIntent ? GetIntentColor(type) : neutralBubbleColor;
            }

            if (intentIcon != null)
            {
                if (intent.Icon != null)
                {
                    intentIcon.sprite = intent.Icon;
                    intentIcon.color = Color.white;
                    intentIcon.enabled = true;
                }
                else
                {
                    // 기본 스프라이트를 유지하고 색상만 의도 타입에 맞춤
                    intentIcon.color = GetIntentColor(type);
                    intentIcon.enabled = true;
                }
            }

            if (intentText != null)
            {
                if (showIntentText)
                {
                    intentText.text = GetIntentLabel(type, intent);
                    intentText.gameObject.SetActive(true);
                }
                else
                {
                    intentText.gameObject.SetActive(false);
                }
            }

            UpdateIntentSummary(changed);

            if (animateOnIntentChange && changed)
            {
                PlayIntentPop();
            }
        }

        /// <summary>
        /// 말풍선 옆 설명 칩 — 다음 기술의 요약 글. 의도가 바뀌었거나 일정 시간이 지났을 때만 다시 계산한다
        /// ({피해}는 대상·상태에 따라 달라지지만 매 프레임 시뮬레이션할 일은 아니다).
        /// </summary>
        private void UpdateIntentSummary(bool intentChanged)
        {
            if (intentSummaryRoot == null || intentSummaryText == null) return;
            if (!intentChanged && Time.unscaledTime < summaryRefreshAt) return;
            summaryRefreshAt = Time.unscaledTime + intentSummaryRefresh;

            string summary = MonsterIntentText.RichSummary(monster);
            if (string.IsNullOrEmpty(summary))
            {
                ReportMissingSummary();
                ShowSummary(false);
                return;
            }

            ShowSummary(true);
            if (summary == shownSummary) return;
            shownSummary = summary;

            intentSummaryText.text = summary;
            // 짧은 글은 글 폭만큼, 긴 글은 최대 폭에서 줄바꿈
            if (summaryLayout == null) summaryLayout = intentSummaryText.GetComponent<LayoutElement>();
            if (summaryLayout != null)
            {
                float natural = intentSummaryText.GetPreferredValues(summary, intentSummaryMaxWidth, 0f).x;
                float width = Mathf.Min(natural, intentSummaryMaxWidth);
                summaryLayout.preferredWidth = width;
                // 높이도 직접 준다 — 자동 계산은 칩이 막 켜진 프레임의 옛 폭으로 줄을 세어 몇 배로 커진다
                summaryLayout.preferredHeight = intentSummaryText.GetPreferredValues(summary, width, 0f).y;
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(intentSummaryRoot);
            PlaceNameAboveSummary(true);
        }

        private void ShowSummary(bool visible)
        {
            if (intentSummaryRoot != null && intentSummaryRoot.gameObject.activeSelf != visible)
                intentSummaryRoot.gameObject.SetActive(visible);
            if (!visible)
            {
                shownSummary = null;
                PlaceNameAboveSummary(false);
            }
        }

        /// <summary>이름표 자리 — 칩이 떠 있으면 칩 윗변 바로 위, 아니면 프리팹에 놓인 원래 자리. (이름표와 칩은 둘 다 말풍선 오른쪽에 있다.)</summary>
        private void PlaceNameAboveSummary(bool summaryVisible)
        {
            if (nameText == null || intentSummaryRoot == null) return;
            var nameRect = nameText.rectTransform;
            if (!nameBaseCaptured) { nameBaseY = nameRect.anchoredPosition.y; nameBaseCaptured = true; }

            float y = nameBaseY;
            if (summaryVisible)
            {
                float summaryTop = intentSummaryRoot.anchoredPosition.y + intentSummaryRoot.rect.height * (1f - intentSummaryRoot.pivot.y);
                y = Mathf.Max(nameBaseY, summaryTop + nameGapAboveSummary + nameRect.rect.height * nameRect.pivot.y);
            }
            if (!Mathf.Approximately(nameRect.anchoredPosition.y, y))
                nameRect.anchoredPosition = new Vector2(nameRect.anchoredPosition.x, y);
        }

        /// <summary>요약이 비어 있는 기술 — 칩을 띄우지 않고 한 번 알린다 (기술 이름 같은 다른 글로 대체하지 않는다).</summary>
        private void ReportMissingSummary()
        {
            var data = monster != null ? monster.NextSkill?.skillData : null;
            if (data == null) return;
            string key = monster.name + "/" + data.SkillName;
            if (ReportedMissingSummary.Add(key))
                Debug.LogError($"[MonsterUI] '{monster.name}'의 기술 '{data.SkillName}'에 의도 요약(intentSummary)이 없습니다 — 몬스터 프리셋에서 채우세요.", monster);
        }
        
        private void SetIntentVisible(bool visible)
        {
            if (!visible) ShowSummary(false);

            if (intentBubbleRoot != null)
            {
                intentBubbleRoot.gameObject.SetActive(visible);
            }
            else
            {
                if (intentIcon != null) intentIcon.gameObject.SetActive(visible);
                if (intentText != null) intentText.gameObject.SetActive(visible && showIntentText);
            }
        }

        private void UpdateArmor()
        {
            if (monster == null) return;
            int armor = Mathf.Max(0, monster.Stats.TempArmor);

            if (armorRoot != null)
            {
                armorRoot.gameObject.SetActive(!hideArmorWhenZero || armor > 0);
            }

            if (armorText != null)
            {
                armorText.text = armor.ToString();
            }
        }

        private void AutoResolveArmorRefs()
        {
            if (worldCanvas == null) return;

            if (armorRoot == null)
            {
                var rects = worldCanvas.GetComponentsInChildren<RectTransform>(true);
                foreach (var rect in rects)
                {
                    if (rect == null) continue;
                    var n = rect.name.ToLowerInvariant();
                    if (n.Contains("armor"))
                    {
                        armorRoot = rect;
                        break;
                    }
                }
            }

            if (armorIcon == null && armorRoot != null)
            {
                armorIcon = armorRoot.GetComponent<Image>();
                if (armorIcon == null)
                {
                    armorIcon = armorRoot.GetComponentInChildren<Image>(true);
                }
            }

            if (armorText == null && armorRoot != null)
            {
                armorText = armorRoot.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }

        private void EnsureArmorUIExists()
        {
            if (worldCanvas == null || armorRoot != null) return;

            GameObject rootGo = new GameObject("ArmorBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rootGo.transform.SetParent(worldCanvas.transform, false);
            armorRoot = rootGo.GetComponent<RectTransform>();
            armorRoot.anchorMin = new Vector2(0.5f, 0.5f);
            armorRoot.anchorMax = new Vector2(0.5f, 0.5f);
            armorRoot.pivot = new Vector2(0.5f, 0.5f);
            armorRoot.anchoredPosition = new Vector2(120f, 24f);
            armorRoot.sizeDelta = new Vector2(40f, 40f);
            armorRoot.localScale = Vector3.one;

            armorIcon = rootGo.GetComponent<Image>();
            armorIcon.raycastTarget = false;
            armorIcon.color = Color.white;
            if (armorIconSprite != null)
            {
                armorIcon.sprite = armorIconSprite;
                armorIcon.type = Image.Type.Simple;
                armorIcon.preserveAspect = true;
            }
            else
            {
                // 아이콘 스프라이트가 없으면 배지 배경색으로 대체
                armorIcon.sprite = null;
                armorIcon.color = new Color(0.2f, 0.45f, 1f, 0.9f);
            }

            GameObject textGo = new GameObject("ArmorText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(armorRoot, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            armorText = textGo.GetComponent<TextMeshProUGUI>();
            armorText.alignment = TextAlignmentOptions.Center;
            armorText.fontSize = 18f;
            armorText.color = Color.white;
            armorText.raycastTarget = false;
            armorText.text = "0";
        }
        
        private int BuildIntentVisualKey(AttackIntent intent)
        {
            unchecked
            {
                int key = 17;
                key = key * 31 + (int)intent.Type;
                key = key * 31 + (int)intent.TargetType;
                key = key * 31 + intent.AreaRadius;
                key = key * 31 + (intent.Icon != null ? intent.Icon.GetInstanceID() : 0);
                key = key * 31 + (intent.Targets != null ? intent.Targets.Count : 0);
                return key;
            }
        }
        
        private string GetIntentLabel(IntentType type, AttackIntent intent)
        {
            switch (type)
            {
                case IntentType.Attack:
                    return "공격";
                case IntentType.Multi:
                    return intent.Targets != null && intent.Targets.Count > 1 ? $"연타 x{intent.Targets.Count}" : "연타";
                case IntentType.Defend:
                    return "방어";
                case IntentType.Buff:
                    return "강화";
                case IntentType.Special:
                    return "특수";
                default:
                    return "행동";
            }
        }
        
        private void PlayIntentPop()
        {
            if (intentBubbleRoot == null) return;
            if (popRoutine != null) StopCoroutine(popRoutine);
            popRoutine = StartCoroutine(CoPlayIntentPop());
        }
        
        private IEnumerator CoPlayIntentPop()
        {
            Vector3 baseScale = Vector3.one;
            float half = Mathf.Max(0.01f, popDuration * 0.5f);
            float t = 0f;
            
            while (t < half)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / half);
                intentBubbleRoot.localScale = Vector3.Lerp(baseScale, baseScale * popScale, k);
                yield return null;
            }

            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / half);
                intentBubbleRoot.localScale = Vector3.Lerp(baseScale * popScale, baseScale, k);
                yield return null;
            }
            
            intentBubbleRoot.localScale = baseScale;
            popRoutine = null;
        }
        
        /// <summary>
        /// 의도 타입별 색상
        /// </summary>
        private Color GetIntentColor(IntentType type)
        {
            switch (type)
            {
                case IntentType.Attack:
                case IntentType.Multi:
                    return attackColor;
                case IntentType.Defend:
                    return defendColor;
                case IntentType.Buff:
                    return buffColor;
                case IntentType.Special:
                    return specialColor;
                default:
                    return Color.white;
            }
        }
        
        /// <summary>
        /// 몬스터 참조 설정
        /// </summary>
        public void SetMonster(Core.Monster newMonster)
        {
            monster = newMonster;
            UpdateUI();
        }

        // ── 호버 툴팁 연동: 의도 버블 히트테스트용 노출 ──

        /// <summary>의도 버블 RectTransform (호버 히트테스트용). 없으면 null.</summary>
        public RectTransform IntentBubbleRect => intentBubbleRoot;

        /// <summary>의도 버블이 현재 표시 중이고 유효한 의도가 있는지.</summary>
        public bool IsIntentBubbleActive =>
            intentBubbleRoot != null && intentBubbleRoot.gameObject.activeInHierarchy
            && monster != null && monster.CurrentIntent != null;

        /// <summary>의도 버블(월드 캔버스)을 렌더하는 카메라 (RectangleContainsScreenPoint용).</summary>
        public Camera IntentWorldCamera =>
            worldCanvas != null && worldCanvas.worldCamera != null ? worldCanvas.worldCamera : mainCamera;
    }
}
