using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using DiceOrbit.Data;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 주사위 UI 컨테이너
    /// </summary>
    public class DiceUI : MonoBehaviour
    {
        public static DiceUI Instance { get; private set; }

        [Header("References")]
        [SerializeField] private Transform diceContainer;
        [SerializeField] private GameObject diceElementPrefab;
        [SerializeField] private DiceRollAnimator rollAnimator;
        
        [Header("Settings")]
        [SerializeField] private bool useRollAnimation = true;
    [SerializeField] private CanvasGroup panelCanvasGroup;

        /// <summary>튜토리얼 하이라이트용 — 주사위 손패 패널 Rect.</summary>
        public RectTransform PanelRect => panelCanvasGroup != null ? panelCanvasGroup.transform as RectTransform : null;

        // Runtime
        private List<DiceElement> diceElements = new List<DiceElement>();
        private DiceElement selectedElement;
        private bool panelVisible = false;
        private int? _tutorialLockValue;   // 튜토리얼: 이 눈 값 주사위만 선택 가능(null=제한없음)

        /// <summary>튜토리얼: 지정 눈 값 주사위만 선택 가능하게 잠금. null이면 해제.</summary>
        public void SetTutorialDiceLock(int? value)
        {
            _tutorialLockValue = value;
            foreach (var element in diceElements)
            {
                if (element == null || element.Data == null) continue;
                bool locked = value.HasValue && element.Data.Value != value.Value;
                element.SetLockedTint(locked);   // 완전 잠금 = 붉은 몸통 (스킬 배지와 구분)
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (panelCanvasGroup == null)
            {
                panelCanvasGroup = GetComponent<CanvasGroup>();
                if (panelCanvasGroup == null)
                {
                    panelCanvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            SetPanelVisible(false);
        }
        
        private void Start()
        {
            // DiceManager에 자신을 등록
            var diceManager = Core.DiceManager.Instance;
            if (diceManager != null)
            {
                diceManager.SetDiceUI(this);
            }
        }
        
        /// <summary>
        /// 주사위 표시
        /// </summary>
        public void DisplayDice(List<DiceData> diceList)
        {
            // 기존 주사위 UI 제거
            ClearDice();
            
            // 새로운 주사위 UI 생성
            foreach (var diceData in diceList)
            {
                CreateDiceElement(diceData);
            }
            
            // 애니메이션 실행
            if (useRollAnimation && rollAnimator != null && diceElements.Count > 0)
            {
                rollAnimator.PlayRollAnimation(new List<DiceElement>(diceElements), diceContainer);
            }
        }
        
        /// <summary>
        /// 주사위 UI 요소 생성
        /// </summary>
        private void CreateDiceElement(DiceData diceData)
        {
            if (diceElementPrefab == null || diceContainer == null)
            {
                Debug.LogError("DiceElementPrefab or DiceContainer is null!");
                return;
            }
            
            GameObject obj = Instantiate(diceElementPrefab, diceContainer);
            DiceElement element = obj.GetComponent<DiceElement>();
            
            if (element != null)
            {
                element.SetDiceData(diceData);
                diceElements.Add(element);
            }
        }
        
        /// <summary>
        /// 주사위 UI 제거
        /// </summary>
        public void ClearDice()
        {
            rollAnimator?.CancelAnimation();
            selectedElement = null;

            foreach (var element in diceElements)
            {
                if (element != null)
                {
                    Destroy(element.gameObject);
                }
            }
            
            diceElements.Clear();
        }

        /// <summary>
        /// 특정 주사위 사용됨 표시
        /// </summary>
        public void MarkDiceAsUsed(DiceData diceData)
        {
            if (diceData == null) return;
            // 상태(Used) 세팅은 DiceManager.MarkUsed로 중앙화 — 여기선 시각 갱신/제거만.

            var element = diceElements.Find(e => e.Data == diceData);
            if (element != null)
            {
                if (selectedElement == element)
                {
                    selectedElement = null;
                }
                element.UpdateVisual();

                // 사용된 주사위는 1초 후 제거 (애니메이션용)
                StartCoroutine(RemoveDiceAfterDelay(element, 0.5f));
            }
        }

        public void HandleDiceElementClicked(DiceElement element)
        {
            if (rollAnimator != null && rollAnimator.IsPlaying) return;
            if (element == null || element.Data == null || element.Data.State != DiceState.Available) return;
            // 튜토리얼 잠금: 지정 눈 외에는 선택 불가
            if (_tutorialLockValue.HasValue && element.Data.Value != _tutorialLockValue.Value) return;

            if (selectedElement != null && selectedElement != element)
            {
                selectedElement.SetSelected(false);
            }

            bool isSame = selectedElement == element;
            if (isSame)
            {
                element.SetSelected(false);
                selectedElement = null;
                CharacterActionUI.Instance?.OnDiceDeselected(element.Data);
                return;
            }

            selectedElement = element;
            selectedElement.SetSelected(true);
            CharacterActionUI.Instance?.OnDiceDropped(element.Data);
        }

        public void ClearSelectedDice()
        {
            if (selectedElement != null)
            {
                selectedElement.SetSelected(false);
                selectedElement = null;
            }
        }

        /// <summary>
        /// 캐릭터 선택 중: 해당 캐릭터의 강화 공격 조건을 못 맞추는 주사위에 배지 표시 —
        /// 그 눈을 배정하면 기본공격만 나간다. 몸통 색은 그대로 둔다 (이동·기본공격은 되므로.
        /// 붉은 몸통 = 완전 잠금 전용).
        /// (CharacterActionUI.Show에서 호출, Hide에서 ClearSkillUsabilityHint로 해제)
        /// </summary>
        public void ShowSkillUsabilityHint(Core.Character character)
        {
            var skillIcon = GetPrimarySkillIcon(character);
            foreach (var element in diceElements)
            {
                if (element == null || element.Data == null) continue;
                element.SetSkillUnusableHint(!CanUseAnySkill(character, element.Data.Value), skillIcon);
            }
        }

        /// <summary>스킬 사용 불가 힌트를 전부 해제.</summary>
        public void ClearSkillUsabilityHint()
        {
            foreach (var element in diceElements)
                element?.SetSkillUnusableHint(false);
        }

        private static bool CanUseAnySkill(Core.Character character, int diceValue)
        {
            var slots = character?.Stats?.ActiveAbilities;
            if (slots == null || slots.Count == 0) return true;   // 정보 없으면 힌트를 띄우지 않음

            foreach (var slot in slots)
                if (slot != null && slot.CanUse(diceValue)) return true;
            return false;
        }

        /// <summary>배지에 넣을 아이콘 — 체력바 위 "공격 가능" 아이콘(칼)과 같은 스프라이트.
        /// (스킬 icon 필드는 실제로 비어 있는 경우가 많아 폴백으로만 사용)</summary>
        private static Sprite GetPrimarySkillIcon(Core.Character character)
        {
            if (CharacterUI.SharedActiveIconSprite != null) return CharacterUI.SharedActiveIconSprite;

            var slots = character?.Stats?.ActiveAbilities;
            if (slots == null) return null;

            foreach (var slot in slots)
            {
                var icon = slot?.BaseSkill?.icon;
                if (icon != null) return icon;
            }
            return null;
        }

        /// <summary>
        /// 특정 주사위의 UI 표시를 새로고침합니다.
        /// </summary>
        public void RefreshDiceVisual(DiceData diceData)
        {
            if (diceData == null) return;
            var element = diceElements.Find(e => e.Data == diceData);
            element?.UpdateVisual();
        }

        /// <summary>
        /// 현재 UI에서 선택된 주사위 데이터를 반환합니다.
        /// </summary>
        public DiceData GetSelectedDiceData()
        {
            if (selectedElement == null) return null;
            if (selectedElement.Data == null || selectedElement.Data.State != DiceState.Available) return null;
            return selectedElement.Data;
        }

        public void SetPanelVisible(bool visible)
        {
            if (!visible) rollAnimator?.CancelAnimation();
            panelVisible = visible;

            if (panelCanvasGroup == null)
            {
                return;
            }

            panelCanvasGroup.alpha = visible ? 1f : 0f;
            panelCanvasGroup.interactable = visible;
            panelCanvasGroup.blocksRaycasts = visible;
        }

        public bool IsPanelVisible => panelVisible;
        
        /// <summary>
        /// 주사위 제거 (지연)
        /// </summary>
        private System.Collections.IEnumerator RemoveDiceAfterDelay(DiceElement element, float delay)
        {
            yield return new UnityEngine.WaitForSeconds(delay);
            
            if (element != null)
            {
                diceElements.Remove(element);
                Destroy(element.gameObject);
                Debug.Log("Used dice removed from UI");
            }
        }
    }
}
