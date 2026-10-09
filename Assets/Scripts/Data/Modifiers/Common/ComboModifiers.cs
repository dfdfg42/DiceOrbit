using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.Modifiers.Common
{
    /// <summary>[대단원] 콤보 3단계 강화 공격 피해 +25%.</summary>
    [System.Serializable]
    public class GrandFinaleModifier : CommonModifier
    {
        private const float Percent = 25f;

        public override string ModifierName => "대단원";
        public override ModifierFamily Family => ModifierFamily.Combo;
        public override string Description
            => $"3단계 강화 공격의 피해가 {Percent:0.#}% 증가합니다.";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (!IsOwnAttackCalculation(trigger, context)) return;
            if (!AttackActionScope.IsActive || AttackActionScope.CurrentSource != owner) return;
            if (AttackActionScope.CurrentInfo.ComboStage != 2) return;

            context.OutputValue *= 1f + Percent / 100f;
        }
    }
}
