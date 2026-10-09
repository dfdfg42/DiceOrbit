using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using UnityEngine;

namespace DiceOrbit.Systems.Effects
{
    /// <summary>
    /// 충전 (시트 「버프·디버프」, 2026-10-09 B). 스택형 — Value가 곧 스택 수, 지속시간 없음(-1).
    /// - 강화 공격(행동 스코프의 ComboStage ≥ 0, 주체가 자신)의 피해 +15%/스택. 행동 예고(시뮬레이션)에도 같은 보정(부수효과 없음).
    /// - 강화 공격 행동을 마친 뒤 AutoAttackSystem이 ConsumeOne으로 1스택 소비한다.
    /// - 최대 3스택(획득 경로 합계) — 상한은 Grant 한 곳에서만 보장한다.
    /// - 전투 시작 방송 전·전투 종료 시 CombatManager가 Clear로 제거한다(전투를 넘어 이월되지 않음).
    /// </summary>
    public class ChargeStatus : StatusEffect
    {
        public const int MaxStacks = 3;
        public const float PercentPerStack = 15f;

        public ChargeStatus(int stacks)
            : base(EffectType.Charge, Mathf.Clamp(stacks, 0, MaxStacks), -1, isStackable: true) { }

        /// <summary>스택 수에 따른 피해 배율 (1 + 0.15 × 스택).</summary>
        public static float Multiplier(int stacks) => 1f + PercentPerStack / 100f * Mathf.Clamp(stacks, 0, MaxStacks);

        /// <summary>지금 열린 행동 스코프가 owner의 강화 공격인가 (실제 행동·예고 시뮬레이션 공통).</summary>
        public static bool IsEmpoweredAttackOf(Unit owner)
            => owner != null
               && AttackActionScope.IsActive
               && AttackActionScope.CurrentSource == owner
               && AttackActionScope.CurrentInfo.ComboStage >= 0;

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (Owner == null || Value <= 0) return;
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.SourceUnit != Owner || context.IsDirectHpLoss) return;
            if (!IsEmpoweredAttackOf(Owner)) return;

            context.OutputValue *= Multiplier(Value);
        }

        /// <summary>
        /// 상태 매니저에 충전 stacks를 더한다 — 합계가 3을 넘지 않게 자른다. 실제로 더한 스택 수를 반환한다(버블 없음, 테스트용 진입점).
        /// </summary>
        public static int Grant(StatusEffectManager effects, int stacks)
        {
            if (effects == null || stacks <= 0) return 0;

            int current = effects.GetEffectValue(EffectType.Charge);
            int add = Mathf.Min(stacks, MaxStacks - current);
            if (add <= 0) return 0;

            effects.AddEffect(new ChargeStatus(add));
            return add;
        }

        /// <summary>유닛에 충전 부여 + 획득 버블. 실제로 더한 스택 수를 반환한다.</summary>
        public static int Grant(Unit unit, int stacks)
        {
            if (unit == null || !unit.IsAlive) return 0;

            int added = Grant(unit.StatusEffects, stacks);
            if (added > 0)
            {
                var data = UI.TooltipKeywordFormatter.BuildStatusDisplayData(EffectType.Charge.ToString(), added, -1);
                UI.CombatNotifier.NotifyStatus(unit, $"{data.Name} +{added}", data.Color);
            }
            return added;
        }

        /// <summary>충전 1스택 소비. 0이 되면 제거. 충전이 없으면 아무 일도 없다.</summary>
        public static void ConsumeOne(StatusEffectManager effects)
        {
            if (effects == null) return;
            effects.ReduceStack(EffectType.Charge, 1);
        }

        public static void ConsumeOne(Unit unit)
        {
            if (unit == null) return;
            ConsumeOne(unit.StatusEffects);
        }

        /// <summary>충전을 전부 제거한다 (전투 경계).</summary>
        public static void Clear(Unit unit)
        {
            if (unit == null || unit.StatusEffects == null) return;
            unit.StatusEffects.RemoveEffect(EffectType.Charge);
        }
    }
}
