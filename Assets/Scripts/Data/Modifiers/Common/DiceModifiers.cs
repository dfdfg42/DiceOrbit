using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.Modifiers.Common
{
    /// <summary>[저속 방호] 3 이하 주사위를 사용하면 임시 방어도 3/중첩 (최대 9). 공격 성사 여부와 무관.</summary>
    [System.Serializable]
    public class LowRollGuardModifier : StackableCommonModifier, IDiceUseListener
    {
        private const int ArmorPerStack = 3;

        public override string ModifierName => "저속 방호";
        public override ModifierFamily Family => ModifierFamily.Dice;
        public override string Description
            => $"방어도 +{ArmorPerStack}\n주사위 눈 3 이하를 쓸 때";

        public void OnDiceUsed(Character character, int diceValue)
        {
            if (character != owner || diceValue > 3) return;
            if (!IsPrimary()) return;
            GrantArmor(ArmorPerStack * StackCount());
        }
    }

    /// <summary>[관성 타격] 4 이상 주사위 사용 시 해당 자동공격 피해 +8%/중첩 (최대 +24%).</summary>
    [System.Serializable]
    public class MomentumStrikeModifier : StackableCommonModifier
    {
        private const float PercentPerStack = 8f;

        public override string ModifierName => "관성 타격";
        public override ModifierFamily Family => ModifierFamily.Dice;
        public override string Description
            => $"이동 후 자동공격 피해 +{PercentPerStack:0.#}%\n주사위 눈 4 이상을 쓸 때";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null || !IsPrimary()) return;
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.SourceUnit != owner || context.IsDirectHpLoss) return;
            if (!AttackActionScope.IsActive || AttackActionScope.CurrentSource != owner) return;
            if (!AttackActionScope.CurrentInfo.IsAutoAttack || AttackActionScope.CurrentInfo.DiceValue < 4) return;

            context.OutputValue *= 1f + PercentPerStack / 100f * StackCount();
        }
    }

    /// <summary>[극점 공명] 정확히 1 또는 6 사용 시 해당 자동공격 피해 +15%/중첩 (최대 +45%).</summary>
    [System.Serializable]
    public class ExtremeResonanceModifier : StackableCommonModifier
    {
        private const float PercentPerStack = 15f;

        public override string ModifierName => "극점 공명";
        public override ModifierFamily Family => ModifierFamily.Dice;
        public override string Description
            => $"이동 후 자동공격 피해 +{PercentPerStack:0.#}%\n주사위 눈 1 또는 6을 쓸 때";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null || !IsPrimary()) return;
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.SourceUnit != owner || context.IsDirectHpLoss) return;
            if (!AttackActionScope.IsActive || AttackActionScope.CurrentSource != owner) return;
            if (!AttackActionScope.CurrentInfo.IsAutoAttack) return;

            int dice = AttackActionScope.CurrentInfo.DiceValue;
            if (dice != 1 && dice != 6) return;

            context.OutputValue *= 1f + PercentPerStack / 100f * StackCount();
        }
    }
}
