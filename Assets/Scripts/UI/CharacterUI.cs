using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DiceOrbit.Data;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 개별 캐릭터 UI (HP 바, 이름)
    /// World Space Canvas로 캐릭터 위에 표시
    /// </summary>
    public class CharacterUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Core.Character character;
        
        [Header("UI Elements")]
        [SerializeField] private Canvas worldCanvas;
        [SerializeField] private Slider hpSlider;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private TextMeshProUGUI levelText;
        
        [Header("Settings")]
        [SerializeField] private Vector3 uiOffset = new Vector3(0, 1.2f, 0);
        [SerializeField] private bool autoFindCharacter = true;
        [Tooltip("체력바 캔버스 정렬 순서. 스프라이트(hover 시 최대 +100)보다 높아야 항상 앞에 보인다.")]
        [SerializeField] private int canvasSortingOrder = 500;
        [Tooltip("체력바 캔버스 크기(월드 스케일). 키우려면 값을 올린다.")]
        [SerializeField] private float canvasScale = 0.07f;

        [Header("Overlap Stacking")]
        [Tooltip("같은 타일에 여러 명이면 체력바를 인덱스별로 Y를 올려 서로 겹치지 않게 세로로 쌓는다")]
        [SerializeField] private bool stackSameTile = true;
        [SerializeField] private float perIndexYStep = 0.35f; // 같은 타일 캐릭터 1명당 올릴 Y(월드)

        [Header("행동 표시 — 이동 토큰 + 콤보 보석 (CharacterActionStrip)")]
        [Tooltip("표시줄 왼쪽 끝 가운데 — 체력바 바로 위 (캔버스 단위)")]
        [SerializeField] private Vector2 actionStripAnchor = new Vector2(-49.3f, 8f);

        // 주사위의 '강화 불가' 배지가 빌려 쓰는 공격 아이콘 (체력바 위 상시 아이콘은 2026-08-21에 철거)
        [SerializeField, HideInInspector] private Sprite activeIconSprite;

        /// <summary>체력바 위 "공격 가능" 아이콘과 같은 스프라이트를 다른 UI(주사위 스킬불가 배지 등)가 쓰도록 공유.</summary>
        public static Sprite SharedActiveIconSprite { get; private set; }

        private Camera mainCamera;
        
        private void Awake()
        {
            // 캐릭터 자동 찾기
            if (autoFindCharacter && character == null)
            {
                character = GetComponentInParent<Core.Character>();
                
                if (character == null)
                {
                    Debug.LogWarning("CharacterUI: Character not found! Assign manually or place as child of Character.");
                }
            }
            
            mainCamera = Camera.main;
        }
        
        private void Start()
        {
            SetupCanvas();
            UpdateUI();
        }
        
        private void Update()
        {
            UpdateUI();
            UpdatePosition();
            // Canvas 회전은 부모 Character의 Billboard가 처리함
        }

        /// <summary>체력바 위치 갱신. 같은 타일에 여러 명이면 인덱스만큼 Y를 올려 세로로 쌓아 겹침을 막는다.
        /// (옆으로 벌어진 캐릭터들은 카메라 거리가 거의 같아 거리 기반 분리가 안 되므로 인덱스로 처리)</summary>
        private void UpdatePosition()
        {
            if (worldCanvas == null) return;

            Transform anchor = character != null ? character.transform : transform;
            Vector3 pos = anchor.position + uiOffset;

            if (stackSameTile)
            {
                pos += Vector3.up * (GetSameTileIndex() * perIndexYStep);
            }

            worldCanvas.transform.position = pos;
        }

        /// <summary>같은 타일에 있는 캐릭터들 중 이 캐릭터의 순서(InstanceID 정렬 = 포메이션 순서)를 0부터 반환.</summary>
        private int GetSameTileIndex()
        {
            if (character == null || character.CurrentTile == null) return 0;

            var party = Core.PartyManager.Instance?.Party;
            if (party == null) return 0;

            int myId = character.GetInstanceID();
            int index = 0;
            foreach (var other in party)
            {
                if (other == null || other == character) continue;
                if (other.CurrentTile == character.CurrentTile && other.GetInstanceID() < myId) index++;
            }
            return index;
        }
        
        /// <summary>
        /// World Space Canvas 설정
        /// </summary>
        private void SetupCanvas()
        {
            if (worldCanvas == null)
            {
                Debug.LogWarning("CharacterUI: WorldCanvas not assigned!");
                return;
            }
            
            worldCanvas.renderMode = RenderMode.WorldSpace;
            worldCanvas.worldCamera = mainCamera;

            // 항상 스프라이트보다 앞에 표시 (hover 시 sortingOrder +100 되는 캐릭터에 가리지 않도록)
            worldCanvas.overrideSorting = true;
            worldCanvas.sortingOrder = canvasSortingOrder;

            // 위치 설정
            worldCanvas.transform.position = transform.position + uiOffset;

            // 크기 조정
            RectTransform rectTransform = worldCanvas.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.sizeDelta = new Vector2(2, 0.5f);
                rectTransform.localScale = Vector3.one * canvasScale;
            }

            // 체력 텍스트가 체력바(슬라이더) 그래픽에 가려지지 않도록 맨 앞(마지막 형제)으로
            if (hpText != null) hpText.transform.SetAsLastSibling();

            ConfigureNonBlockingRaycasts();

            // 상태이상 시각화: 체력바 아래 아이콘 줄 + 유닛 위 오버레이 (스펙 2026-07-29)
            // HPBar(중심 x=1.35, 폭 101) 좌측 끝 x=-49, HP텍스트(-24까지) 바로 아래 y=-25
            StatusIconRow.Attach(worldCanvas, character, mainCamera, new Vector2(-49f, -25f));
            Visuals.StatusOverlayStack.Attach(character);

            if (activeIconSprite != null) SharedActiveIconSprite = activeIconSprite;
            CharacterActionStrip.Attach(worldCanvas, character, actionStripAnchor);
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
        
        /// <summary>
        /// UI 업데이트
        /// </summary>
        private void UpdateUI()
        {
            if (character == null) return;
            
            CharacterStats stats = character.Stats;
            
            // HP 슬라이더
            if (hpSlider != null)
            {
                hpSlider.maxValue = stats.MaxHP;
                hpSlider.value = stats.CurrentHP;
            }
            
            // 이름
            if (nameText != null)
            {
                nameText.text = stats.CharacterName;
            }
            
            // HP 텍스트
            if (hpText != null)
            {
                hpText.text = $"{stats.CurrentHP}/{stats.MaxHP}";
            }
            
            // (레벨 표기는 철거됨 — 성장은 전부 모디파이어로. 텍스트는 비워서 숨김)
            if (levelText != null)
            {
                levelText.text = string.Empty;
            }

        }

        /// <summary>
        /// 캐릭터 참조 설정
        /// </summary>
        public void SetCharacter(Core.Character newCharacter)
        {
            character = newCharacter;
            UpdateUI();
        }
    }
}
