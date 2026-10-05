using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.Modifiers.Common
{
    /// <summary>[선제 방벽] 전투 시작 시 임시 방어도 5/중첩 (최대 15).</summary>
    [System.Serializable]
    public class OpeningBarrierModifier : StackableCommonModifier
    {
        private const int ArmorPerStack = 5;

        public override string ModifierName => "선제 방벽";
        public override ModifierFamily Family => ModifierFamily.Survival;
        public override string Description
            => $"전투 시작 시 방어도 +{ArmorPerStack}";

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null || !IsPrimary()) return;
            if (context.Phase != EventPhase.CombatStart) return;
            if (trigger != CombatTrigger.OnPreAction) return;   // 방송 1회당 1번만
            if (context.SourceUnit != owner || context.IsSimulation) return;

            GrantArmor(ArmorPerStack * StackCount());
        }
    }

    /// <summary>[불굴] 현재 체력이 50% 이하면 받는 공격 피해 -8%/중첩 (최대 -24%).</summary>
    [System.Serializable]
    public class UnyieldingModifier : StackableCommonModifier
    {
        private const float PercentPerStack = 8f;

        public override string ModifierName => "불굴";
        public override ModifierFamily Family => ModifierFamily.Survival;
        public override string Description
            => $"받는 공격 피해 -{PercentPerStack:0.#}%\n체력 50% 이하일 때";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null || !IsPrimary()) return;
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.Target != owner || context.IsDirectHpLoss) return;
            if (owner.Stats == null || owner.Stats.HPRatio > 0.5f) return;

            context.OutputValue *= UnityEngine.Mathf.Max(0f, 1f - PercentPerStack / 100f * StackCount());
        }
    }

    /// <summary>
    /// [앙갚음] 적의 공격으로 체력 피해를 받으면, 다음 자동공격 행동의 피해 +10%/중첩 (최대 +30%).
    /// 방어도가 전부 흡수해 체력이 깎이지 않았으면 발동하지 않는다 (IsEffected 기준).
    /// 한 번의 자동공격 행동 전체에 적용된 뒤 소모된다.
    /// </summary>
    [System.Serializable]
    public class PaybackModifier : StackableCommonModifier
    {
        private const float PercentPerStack = 10f;

        private bool _armed;
        private int _appliedActionId;

        public override string ModifierName => "앙갚음";
        public override ModifierFamily Family => ModifierFamily.Survival;
        public override string Description
            => $"다음 자동공격 피해 +{PercentPerStack:0.#}%\n적 공격에 체력을 잃었을 때";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null || !IsPrimary()) return;

            // 무장 — 적의 공격으로 실제 체력 피해를 받았을 때 (중독 등 직접 손실 제외)
            if (trigger == CombatTrigger.OnPostAction
                && context.Target == owner
                && context.SourceUnit is Monster
                && !context.IsDirectHpLoss
                && !context.IsSimulation
                && context.IsEffected)
            {
                _armed = true;
                return;
            }

            // 발동 — 다음 자동공격 행동의 모든 타격
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.SourceUnit != owner || context.IsDirectHpLoss) return;
            if (!_armed) return;
            if (!AttackActionScope.IsActive || AttackActionScope.CurrentSource != owner) return;
            if (!AttackActionScope.CurrentInfo.IsAutoAttack) return;

            int actionId = AttackActionScope.CurrentActionId;
            if (_appliedActionId != 0 && actionId != _appliedActionId)
            {
                // 이미 한 행동에 다 썼다 — 새 행동에는 적용하지 않고 소모 처리
                _armed = false;
                _appliedActionId = 0;
                return;
            }

            context.OutputValue *= 1f + PercentPerStack / 100f * StackCount();
            if (!context.IsSimulation)
            {
                if (_appliedActionId == 0) UI.CombatNotifier.NotifyPassive(owner, ModifierName);
                _appliedActionId = actionId;
            }
        }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            // 전투 시작 시 상태 초기화 — 이전 전투의 무장이 이월되지 않는다
            if (context.Phase != EventPhase.CombatStart) return;
            if (trigger != CombatTrigger.OnPreAction) return;
            if (context.SourceUnit != owner) return;

            _armed = false;
            _appliedActionId = 0;
        }
    }
}
