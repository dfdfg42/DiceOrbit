using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.Modifiers.Common
{
    /// <summary>[안전장치] 조건 불일치로 콤보가 끊기고 일반 자동공격이 나가면 임시 방어도 4/중첩 (최대 12).</summary>
    [System.Serializable]
    public class SafetyNetModifier : StackableCommonModifier, IComboBreakListener
    {
        private const int ArmorPerStack = 4;

        public override string ModifierName => "안전장치";
        public override ModifierFamily Family => ModifierFamily.Combo;
        public override string Description
            => $"방어도 +{ArmorPerStack}\n콤보가 끊겨 일반 자동공격이 나갈 때";

        public void OnComboBrokenBasicAttack(Character character)
        {
            if (character != owner) return;
            if (!IsPrimary()) return;
            GrantArmor(ArmorPerStack * StackCount());
        }
    }

    /// <summary>[연쇄 반응] 콤보 2단계 공격의 피해 +10%/중첩 (최대 +30%).</summary>
    [System.Serializable]
    public class ChainReactionModifier : StackableCommonModifier
    {
        private const float PercentPerStack = 10f;

        public override string ModifierName => "연쇄 반응";
        public override ModifierFamily Family => ModifierFamily.Combo;
        public override string Description
            => $"콤보 2단계 피해 +{PercentPerStack:0.#}%";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null || !IsPrimary()) return;
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.SourceUnit != owner || context.IsDirectHpLoss) return;
            if (!AttackActionScope.IsActive || AttackActionScope.CurrentSource != owner) return;
            if (AttackActionScope.CurrentInfo.ComboStage != 1) return;

            context.OutputValue *= 1f + PercentPerStack / 100f * StackCount();
        }
    }

    /// <summary>[대단원] 콤보 3단계 공격의 피해 +15%/중첩 (최대 +45%).</summary>
    [System.Serializable]
    public class GrandFinaleModifier : StackableCommonModifier
    {
        private const float PercentPerStack = 15f;

        public override string ModifierName => "대단원";
        public override ModifierFamily Family => ModifierFamily.Combo;
        public override string Description
            => $"콤보 3단계 피해 +{PercentPerStack:0.#}%";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null || !IsPrimary()) return;
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.SourceUnit != owner || context.IsDirectHpLoss) return;
            if (!AttackActionScope.IsActive || AttackActionScope.CurrentSource != owner) return;
            if (AttackActionScope.CurrentInfo.ComboStage != 2) return;

            context.OutputValue *= 1f + PercentPerStack / 100f * StackCount();
        }
    }
}
