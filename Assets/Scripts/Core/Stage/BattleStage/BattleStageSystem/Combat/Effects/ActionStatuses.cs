using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using DiceOrbit.Data.Modifiers.Job;

namespace DiceOrbit.Systems.Effects
{
    /// <summary>
    /// '다음 공격 행동' 1회에만 적용되는 1회성 상태의 공통 골격 (2026-08-28). 현재 사용처: 촉매.
    /// - 행동 스코프(AttackActionScope) 안: 행동의 모든 타격에 적용하고 End()에서 제거된다.
    /// - 스코프 밖(지연 발사체 등 폴백): 첫 적용 컨텍스트의 OnPostAction에서 스스로 제거한다.
    /// - 직접 체력 손실(중독)엔 절대 개입하지 않는다 (파이프라인이 통지를 건너뛰지만 이중 방어).
    /// - 비중첩: 이미 있으면 StatusEffectManager.AddEffect가 갱신만 한다.
    /// </summary>
    public abstract class OneShotActionStatus : StatusEffect
    {
        private bool _consumedOutsideScope;

        protected OneShotActionStatus(EffectType type, int value)
            : base(type, value, -1, isStackable: false) { }

        /// <summary>이 컨텍스트에 개입하는가 (소스/대상 매칭은 자식이 정의).</summary>
        protected abstract bool AppliesTo(AttackContext context);

        /// <summary>수치 보정 (자식이 정의).</summary>
        protected abstract void Modify(AttackContext context);

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (Owner == null || context.IsDirectHpLoss) return;
            if (!AppliesTo(context)) return;

            if (trigger == CombatTrigger.OnCalculateOutput)
            {
                Modify(context);
                if (context.IsSimulation) return;   // 미리보기: 수치만 반영, 소비 없음

                if (AttackActionScope.IsActive) AttackActionScope.MarkConsumed(this, Owner);
                else _consumedOutsideScope = true;
            }
            else if (trigger == CombatTrigger.OnPostAction && _consumedOutsideScope && !context.IsSimulation)
            {
                Owner.StatusEffects?.RemoveEffect(Type);
            }
        }
    }

    /// <summary>
    /// '부여된 라운드 동안' 모든 해당 공격에 적용되는 상태의 공통 골격 (2026-10-09, 시트 "이번 턴 동안").
    /// 라운드 = 플레이어 턴 + 몬스터 턴. 소모되지 않으며, 제거는 CombatManager가
    /// 몬스터 턴 종료·전투 시작에 StatusEffectManager.RemoveRoundStatuses()로 일괄 처리한다.
    /// 직접 체력 손실(중독)엔 개입하지 않는다. 비중첩 — 재부여 시 큰 값을 유지한다(StatusEffectManager.AddEffect).
    /// </summary>
    public abstract class RoundStatus : StatusEffect
    {
        protected RoundStatus(EffectType type, int value)
            : base(type, value, -1, isStackable: false) { }

        /// <summary>이 컨텍스트에 개입하는가 (소스/대상 매칭은 자식이 정의).</summary>
        protected abstract bool AppliesTo(AttackContext context);

        /// <summary>수치 보정 (자식이 정의).</summary>
        protected abstract void Modify(AttackContext context);

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (Owner == null || context.IsDirectHpLoss) return;
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (!AppliesTo(context)) return;
            Modify(context);
        }
    }

    /// <summary>
    /// 감전 — 이번 라운드 동안 이 유닛이 '받는' 공격 피해 +Value% (기본 25). 디버프 피해엔 적용 안 됨.
    /// 실효 %는 계산할 때마다 StatusEnhancement가 정한다 — 고전압 보유 마법사가 살아 있으면 max(Value, 40) (2026-10-09 C, D5).
    /// </summary>
    public class ShockStatus : RoundStatus
    {
        // 부여 시점에도 강화 중이면 40으로 부여해 아이콘 수치를 맞춘다 (D5)
        public ShockStatus(int value) : base(EffectType.Shock, StatusEnhancement.EffectivePercent(EffectType.Shock, value)) { }

        protected override bool AppliesTo(AttackContext context)
            => context.Target == Owner && context.SourceUnit != null;

        protected override void Modify(AttackContext context)
            => context.OutputValue *= 1f + StatusEnhancement.EffectivePercent(Type, Value) / 100f;
    }

    /// <summary>
    /// 촉매 — 시약 타일에서 얻는 버프. 이 유닛의 다음 '자동공격 행동' 피해 +Value% (기본 25).
    /// 자동공격 행동(기본/강화 불문)은 항상 스코프로 감싸이므로, 스코프의 주체가 자신일 때만 반응한다 —
    /// 포션 등 행동 밖 피해에는 쓰이지 않고 남는다.
    /// </summary>
    public class CatalystStatus : OneShotActionStatus
    {
        // 부여 시점에도 강화 중이면 40으로 부여 (고순도 촉매, 2026-10-09 C D5)
        public CatalystStatus(int value) : base(EffectType.Catalyst, StatusEnhancement.EffectivePercent(EffectType.Catalyst, value)) { }

        protected override bool AppliesTo(AttackContext context)
            => AttackActionScope.IsActive
               && AttackActionScope.CurrentSource == Owner
               && context.SourceUnit == Owner
               && Owner is Character;

        // 실효 % — 고순도 촉매 보유 연금술사가 살아 있으면 max(Value, 40) (D5)
        protected override void Modify(AttackContext context)
            => context.OutputValue *= 1f + StatusEnhancement.EffectivePercent(Type, Value) / 100f;
    }

    /// <summary>고양 — 이번 라운드 동안 이 유닛이 주는 공격 피해 +Value% (기본 25).</summary>
    public class InspireStatus : RoundStatus
    {
        public InspireStatus(int value) : base(EffectType.Inspire, value) { }

        protected override bool AppliesTo(AttackContext context)
            => context.SourceUnit == Owner;

        protected override void Modify(AttackContext context)
            => context.OutputValue *= 1f + Value / 100f;
    }

    /// <summary>약화 — 이번 라운드 동안 이 유닛이 주는 공격 피해 −Value% (기본 25).</summary>
    public class WeakenStatus : RoundStatus
    {
        public WeakenStatus(int value) : base(EffectType.Weaken, value) { }

        protected override bool AppliesTo(AttackContext context)
            => context.SourceUnit == Owner;

        protected override void Modify(AttackContext context)
            => context.OutputValue *= UnityEngine.Mathf.Max(0f, 1f - Value / 100f);
    }
}
