using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Systems.Effects;
using UnityEngine;

namespace DiceOrbit.Data.Modifiers.Common
{
    /// <summary>[대단원] 콤보 3단계 강화 공격 피해 +25%, 그 공격을 마친 뒤 방어도 4 (3단계가 실제로 실행돼야 함).</summary>
    [System.Serializable]
    public class GrandFinaleModifier : CommonModifier, IAttackActionFinishedListener
    {
        private const float Percent = 25f;
        private const int Armor = 4;

        public override string ModifierName => "대단원";
        public override ModifierFamily Family => ModifierFamily.Combo;
        public override string Description
            => $"3단계 강화 공격의 피해가 {Percent:0.#}% 증가하고, 그 공격을 마친 뒤 방어도를 {Armor}만큼 얻습니다.";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (!IsOwnAttackCalculation(trigger, context)) return;
            if (!AttackActionScope.IsActive || AttackActionScope.CurrentSource != owner) return;
            if (AttackActionScope.CurrentInfo.ComboStage != 2) return;

            context.OutputValue *= 1f + Percent / 100f;
        }

        public void OnAttackActionFinished(Character character, AttackActionResult result)
        {
            if (character != owner || result.ComboStage != 2 || result.Targets.Count == 0) return;
            GrantArmor(Armor);
        }
    }

    /// <summary>
    /// [균열] 공격 전 첫 대상의 일시 방어도 4 제거(행동당 1회). 그 대상에게 처음부터 방어도가 없었으면 그 대상에 대한 피해 +15%.
    /// 이 효과로 0이 된 경우는 +15% 없음. "첫 대상" = 행동에서 처음 OnPreAction에 들어온 대상.
    /// 행동 예고(시뮬레이션)는 상태를 바꾸지 않고, 대상의 현재 방어도가 0이면 +15%로 근사한다.
    /// </summary>
    [System.Serializable]
    public class FractureModifier : CommonModifier
    {
        private const int ArmorBreak = 4;
        private const float Percent = 15f;

        [System.NonSerialized] private int _actionId;
        [System.NonSerialized] private Unit _firstTarget;
        [System.NonSerialized] private bool _firstTargetHadNoArmor;

        public override string ModifierName => "균열";
        public override ModifierFamily Family => ModifierFamily.Combo;
        public override string Description
            => $"공격 전 첫 대상의 방어도를 {ArmorBreak} 제거합니다. 그 대상에게 처음부터 방어도가 없었다면 피해가 {Percent:0.#}% 증가합니다.";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null || context.SourceUnit != owner || context.IsDirectHpLoss) return;
            if (!AttackActionScope.IsActive || AttackActionScope.CurrentSource != owner) return;

            if (trigger == CombatTrigger.OnPreAction)
            {
                if (!context.IsSimulation) BreakFirstTarget(context.Target);
                return;
            }

            if (trigger != CombatTrigger.OnCalculateOutput) return;

            if (context.IsSimulation)
            {
                if (context.Target != null && context.Target.Stats != null && context.Target.Stats.TempArmor <= 0)
                    context.OutputValue *= 1f + Percent / 100f;
                return;
            }

            if (AttackActionScope.CurrentActionId == _actionId && context.Target == _firstTarget && _firstTargetHadNoArmor)
                context.OutputValue *= 1f + Percent / 100f;
        }

        /// <summary>이 행동의 첫 대상을 기록하고 방어도를 깎는다 — 행동 ID가 같으면 이미 처리됐다.</summary>
        private void BreakFirstTarget(Unit target)
        {
            int id = AttackActionScope.CurrentActionId;
            if (id == _actionId) return;

            _actionId = id;
            _firstTarget = target;
            _firstTargetHadNoArmor = false;
            if (target == null || target.Stats == null) return;

            int armor = target.Stats.TempArmor;
            _firstTargetHadNoArmor = armor <= 0;
            if (armor <= 0) return;

            int removed = Mathf.Min(ArmorBreak, armor);
            target.Stats.TempArmor -= removed;
            UI.CombatNotifier.Notify(target, $"{ModifierName} 방어도 -{removed}", new Color(1f, 0.7f, 0.45f));
        }
    }

    /// <summary>
    /// [독 바른 무기] 공격을 마친 뒤 공격한 몬스터마다 중독 1 (광역이면 대상마다).
    /// 공격 종료 시점에 이미 죽은 대상은 건너뛴다. 공격 도중 owner가 쓰러졌으면 발동하지 않는다.
    /// </summary>
    [System.Serializable]
    public class PoisonedWeaponModifier : CommonModifier, IAttackActionFinishedListener
    {
        private const int PoisonStacks = 1;

        public override string ModifierName => "독 바른 무기";
        public override ModifierFamily Family => ModifierFamily.Combo;
        public override string Description
            => $"공격을 마친 뒤 공격한 몬스터마다 중독을 {PoisonStacks} 부여합니다.";

        public void OnAttackActionFinished(Character character, AttackActionResult result)
        {
            if (owner == null || character != owner || !owner.IsAlive) return;

            foreach (var target in result.Targets)
                if (target is Monster && target != null && target.IsAlive)
                    GrantStatusTo(target, EffectType.Poison, PoisonStacks);
        }
    }

    /// <summary>[기본기 단련] 기본 공격력 +2 — 유효 공격력(CharacterStats.EffectiveAttack)으로 반영, 저장 필드는 그대로(D6).</summary>
    [System.Serializable]
    public class BasicTrainingModifier : CommonModifier
    {
        private const int Bonus = 2;

        public override string ModifierName => "기본기 단련";
        public override ModifierFamily Family => ModifierFamily.Combo;
        public override string Description
            => $"기본 공격력이 {Bonus} 증가합니다.";

        public override int AttackBonus => Bonus;
    }
}
