using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.Data.Modifiers;
using DiceOrbit.UI.Skin;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 강화 박자의 모디파이어 카드 뷰 — 계열 아이콘·이름·설명·상태 줄. 씬 템플릿에 붙어 복제된다.
    /// 그리기만 한다: 누가 받을 수 있는지, 무엇이 선택됐는지는 RewardUI가 정해 SetStatus/SetSelected/SetDimmed로 알려 준다.
    /// </summary>
    public class RewardModifierCard : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image familyIcon;
        [SerializeField] private TextMeshProUGUI familyLabel;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private CanvasGroup group;

        public CharacterModifier Modifier { get; private set; }
        public RectTransform Rect => (RectTransform)transform;
        public CanvasGroup Group => group;

        public void Bind(CharacterModifier modifier, Action onClick)
        {
            if (modifier == null) throw new ArgumentNullException(nameof(modifier));
            Modifier = modifier;

            familyIcon.sprite = UiSkin.Current.GetFamilyIcon(modifier.Family);   // 계열이 없으면 예외 — 보상 카드는 계열이 있어야 한다
            familyLabel.text = modifier.Family.Label();
            nameText.text = modifier.ModifierName;
            descText.text = modifier.Description;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick());

            SetSelected(false);
            SetDimmed(false);
        }

        /// <summary>선택된 캐릭터 기준 상태 줄 ("전사 · 신규", "전사 · 보유 중", "전사 · 장착 불가").</summary>
        public void SetStatus(string text, bool equippable)
        {
            var skin = UiSkin.Current;
            statusText.text = text;
            statusText.color = equippable ? skin.Modifier : skin.InkMuted;
        }

        public void SetSelected(bool selected)
        {
            UiSkin.Current.ApplyButtonSelected(button, SkinButton.RewardChoiceCard, selected);
            Rect.localScale = Vector3.one * (selected ? 1.04f : 1f);
        }

        /// <summary>다른 카드가 선택됐을 때 물러나 보이게.</summary>
        public void SetDimmed(bool dimmed) => group.alpha = dimmed ? 0.6f : 1f;
    }
}
