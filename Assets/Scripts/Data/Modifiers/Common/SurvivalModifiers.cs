using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.Modifiers.Common
{
    /// <summary>[공방 일체] 공격 행동(기본/강화)을 마친 뒤 방어도 3.</summary>
    [System.Serializable]
    public class OffenseDefenseModifier : CommonModifier, IAttackActionFinishedListener
    {
        private const int Armor = 3;

        public override string ModifierName => "공방 일체";
        public override ModifierFamily Family => ModifierFamily.Survival;
        public override string Description
            => $"공격을 마친 뒤 방어도를 {Armor}만큼 얻습니다.";

        public void OnAttackActionFinished(Character character, AttackActionResult result)
        {
            if (character != owner || result.Targets.Count == 0) return;
            GrantArmor(Armor);
        }
    }

    /// <summary>[완강한 신체] 획득 시 1회 최대 체력·현재 체력 +10 (D7 — 세이브 복원에선 다시 적용하지 않음).</summary>
    [System.Serializable]
    public class SturdyBodyModifier : CommonModifier
    {
        private const int Amount = 10;

        public override string ModifierName => "완강한 신체";
        public override ModifierFamily Family => ModifierFamily.Survival;
        public override string Description
            => $"최대 체력과 현재 체력이 {Amount} 증가합니다.";

        public override void OnAcquired(Character acquiredBy)
        {
            if (acquiredBy == null || acquiredBy != owner) return;
            RaiseMaxHp(Amount);
        }
    }

    /// <summary>[자연 치유] 플레이어 턴 시작 시 체력 +2 (최대 체력 초과 없음). TurnStart의 OnPostAction 한 번만 — 방어도 초기화(OnPreAction) 뒤.</summary>
    [System.Serializable]
    public class NaturalHealingModifier : CommonModifier
    {
        private const int HealAmount = 2;

        public override string ModifierName => "자연 치유";
        public override ModifierFamily Family => ModifierFamily.Survival;
        public override string Description
            => $"플레이어 턴 시작 시 체력을 {HealAmount} 회복합니다.";

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null) return;
            if (context.Phase != EventPhase.TurnStart) return;
            if (trigger != CombatTrigger.OnPostAction) return;   // 한 트리거만 (방송 1회당 1번)
            if (context.SourceUnit != owner || context.IsSimulation) return;

            HealTarget(owner, HealAmount);
        }
    }

    /// <summary>
    /// [사냥] 공격 행동을 마친 뒤, 그 행동으로 처치한 몬스터 1명당 최대·현재 체력 +2 (영구).
    /// 처치 수는 AutoAttackSystem이 스코프 종료 직후에 센다 — 유물·디버프(중독) 피해 처치는 들어가지 않는다.
    /// </summary>
    [System.Serializable]
    public class HuntModifier : CommonModifier, IAttackActionFinishedListener
    {
        private const int PerKill = 2;

        public override string ModifierName => "사냥";
        public override ModifierFamily Family => ModifierFamily.Survival;
        public override string Description
            => $"공격으로 몬스터를 처치하면 처치 1명당 최대 체력과 현재 체력이 {PerKill} 증가합니다.";

        public void OnAttackActionFinished(Character character, AttackActionResult result)
        {
            if (character != owner || owner == null || !owner.IsAlive) return;
            if (result.Kills <= 0) return;
            RaiseMaxHp(PerKill * result.Kills);
        }
    }
}
