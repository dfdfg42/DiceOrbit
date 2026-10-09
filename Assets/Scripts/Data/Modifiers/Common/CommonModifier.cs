using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Systems.Effects;
using UnityEngine;

namespace DiceOrbit.Data.Modifiers.Common
{
    /// <summary>
    /// 공용 모디파이어 공통 골격 (2026-08-28 도입, 2026-10-09 B 단순화).
    /// 모든 캐릭터가 장착할 수 있고, 같은 종류는 캐릭터당 1개다(CharacterModifier.CanApplyTo 기본 규칙) — 중첩 없음.
    /// 부여형 효과(방어도·충전·회복·최대 체력·상태)의 공용 도우미를 둔다.
    /// 이 클래스 이름은 세이브에 저장되지 않는다(세이브는 구체 타입명만 저장).
    /// </summary>
    [System.Serializable]
    public abstract class CommonModifier : CharacterModifier
    {
        public override ModifierCategory Category => ModifierCategory.Generic;

        /// <summary>owner가 주는 공격의 피해 계산 단계인가 (직접 체력 손실 제외).</summary>
        protected bool IsOwnAttackCalculation(CombatTrigger trigger, AttackContext context)
            => owner != null
               && trigger == CombatTrigger.OnCalculateOutput
               && context.SourceUnit == owner
               && !context.IsDirectHpLoss;

        /// <summary>임시 방어도 부여 + 버블.</summary>
        protected void GrantArmor(int amount)
        {
            if (owner == null || owner.Stats == null || !owner.IsAlive || amount <= 0) return;
            owner.Stats.TempArmor += amount;
            UI.CombatNotifier.Notify(owner, $"{ModifierName} 방어도 +{amount}", new Color(0.65f, 0.8f, 1f));
        }

        /// <summary>owner에게 충전 부여 (상한 3은 ChargeStatus.Grant가 보장, 버블 포함).</summary>
        protected void GrantCharge(int stacks)
        {
            if (owner == null || !owner.IsAlive) return;
            ChargeStatus.Grant(owner, stacks);
        }

        /// <summary>
        /// 대상 회복 — 파이프라인(HealContext) 경유, Unit.Heal이 최대 체력에서 자른다. 최대 체력이면 아무 일도 없다.
        /// 파이프라인이 없으면(테스트·특수 씬) 스탯에 직접 회복한다 (PartyManager.HealAllParty와 같은 폴백).
        /// </summary>
        protected void HealTarget(Character target, int amount)
        {
            if (target == null || target.Stats == null || !target.IsAlive || amount <= 0) return;
            if (target.Stats.CurrentHP >= target.Stats.MaxHP) return;

            if (CombatPipeline.Instance != null)
                CombatPipeline.Instance.Process(new HealContext(owner, target, ModifierName, amount));
            else
                target.Stats.Heal(amount);

            UI.CombatNotifier.Notify(target, $"{ModifierName} 체력 +{amount}", new Color(0.55f, 1f, 0.6f));
        }

        /// <summary>owner의 최대 체력·현재 체력을 amount만큼 영구히 올린다 + 버블 (완강한 신체·사냥).</summary>
        protected void RaiseMaxHp(int amount)
        {
            if (owner == null || owner.Stats == null) return;
            if (ApplyMaxHpGain(owner.Stats, amount) <= 0) return;
            UI.CombatNotifier.Notify(owner, $"{ModifierName} 최대 체력 +{amount}", new Color(0.55f, 1f, 0.6f));
        }

        /// <summary>최대 체력 +amount, 현재 체력 +amount(최대 체력 이하로). 실제로 올린 양을 반환 (순수 — 자가 테스트 대상).</summary>
        public static int ApplyMaxHpGain(UnitStats stats, int amount)
        {
            if (stats == null || amount <= 0) return 0;
            stats.MaxHP += amount;
            stats.CurrentHP = Mathf.Min(stats.CurrentHP + amount, stats.MaxHP);
            return amount;
        }

        /// <summary>대상에게 상태를 부여하고 상태 버블을 띄운다 (죽은 대상 무시). ComboActiveSkill.GrantStatus와 같은 방식.</summary>
        protected static void GrantStatusTo(Unit target, EffectType type, int value)
        {
            if (target == null || !target.IsAlive || target.StatusEffects == null || value <= 0) return;

            target.StatusEffects.AddEffect(StatusEffectManager.CreateEffect(type, value, -1));
            var data = UI.TooltipKeywordFormatter.BuildStatusDisplayData(type.ToString(), value, -1);
            UI.CombatNotifier.NotifyStatus(target, data.Name, data.Color);
        }
    }
}
