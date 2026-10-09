using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Core.Zones;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.Modifiers.Common
{
    /// <summary>[합류 전술] 공격할 때 같은 구역에 다른 살아 있는 아군이 있으면 피해 +15%.</summary>
    [System.Serializable]
    public class JointTacticsModifier : CommonModifier
    {
        private const float Percent = 15f;

        public override string ModifierName => "합류 전술";
        public override ModifierFamily Family => ModifierFamily.Position;
        public override string Description
            => $"공격할 때 같은 구역에 다른 아군이 있으면 피해가 {Percent:0.#}% 증가합니다.";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (!IsOwnAttackCalculation(trigger, context)) return;
            if (!ZoneAllyCounter.HasOtherAllyInMyZone(owner)) return;

            context.OutputValue *= 1f + Percent / 100f;
        }
    }

    /// <summary>[독립 행동] 공격할 때 같은 구역에 다른 아군이 없으면 피해 +15%.</summary>
    [System.Serializable]
    public class LoneWolfModifier : CommonModifier
    {
        private const float Percent = 15f;

        public override string ModifierName => "독립 행동";
        public override ModifierFamily Family => ModifierFamily.Position;
        public override string Description
            => $"공격할 때 같은 구역에 다른 아군이 없으면 피해가 {Percent:0.#}% 증가합니다.";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (!IsOwnAttackCalculation(trigger, context)) return;

            var zones = CombatZoneManager.Instance;
            if (zones == null || zones.GetZoneOf(owner) < 0) return;   // 구역 밖이면 판정 불가
            if (ZoneAllyCounter.HasOtherAllyInMyZone(owner)) return;

            context.OutputValue *= 1f + Percent / 100f;
        }
    }

    /// <summary>같은 구역의 '다른 아군' 존재 판정 (합류 전술/독립 행동 공용).</summary>
    internal static class ZoneAllyCounter
    {
        public static bool HasOtherAllyInMyZone(Character self)
        {
            var zones = CombatZoneManager.Instance;
            var party = PartyManager.Instance;
            if (zones == null || party == null || self == null) return false;

            int myZone = zones.GetZoneOf(self);
            if (myZone < 0) return false;

            foreach (var ally in party.GetAliveCharacters())
            {
                if (ally == null || ally == self) continue;
                if (zones.GetZoneOf(ally) == myZone) return true;
            }
            return false;
        }
    }
}
