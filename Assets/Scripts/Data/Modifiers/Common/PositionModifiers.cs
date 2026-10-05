using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Core.Zones;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.Modifiers.Common
{
    /// <summary>[합류 전술] 공격 시 같은 구역에 다른 아군이 있으면 공격 피해 +10%/중첩 (최대 +30%).</summary>
    [System.Serializable]
    public class JointTacticsModifier : StackableCommonModifier
    {
        private const float PercentPerStack = 10f;

        public override string ModifierName => "합류 전술";
        public override ModifierFamily Family => ModifierFamily.Position;
        public override string Description
            => $"공격 피해 +{PercentPerStack:0.#}%\n같은 구역에 다른 아군이 있을 때";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null || !IsPrimary()) return;
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.SourceUnit != owner || context.IsDirectHpLoss) return;
            if (!ZoneAllyCounter.HasOtherAllyInMyZone(owner)) return;

            context.OutputValue *= 1f + PercentPerStack / 100f * StackCount();
        }
    }

    /// <summary>[독립 행동] 공격 시 같은 구역에 다른 아군이 없으면 공격 피해 +12%/중첩 (최대 +36%).</summary>
    [System.Serializable]
    public class LoneWolfModifier : StackableCommonModifier
    {
        private const float PercentPerStack = 12f;

        public override string ModifierName => "독립 행동";
        public override ModifierFamily Family => ModifierFamily.Position;
        public override string Description
            => $"공격 피해 +{PercentPerStack:0.#}%\n같은 구역에 다른 아군이 없을 때";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null || !IsPrimary()) return;
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.SourceUnit != owner || context.IsDirectHpLoss) return;

            var zones = CombatZoneManager.Instance;
            if (zones == null || zones.GetZoneOf(owner) < 0) return;   // 구역 밖이면 판정 불가
            if (ZoneAllyCounter.HasOtherAllyInMyZone(owner)) return;

            context.OutputValue *= 1f + PercentPerStack / 100f * StackCount();
        }
    }

    /// <summary>[경계 돌파] 이동 시작 구역과 도착 구역이 다르면 해당 자동공격 피해 +10%/중첩 (최대 +30%).</summary>
    [System.Serializable]
    public class ZoneCrossModifier : StackableCommonModifier
    {
        private const float PercentPerStack = 10f;

        public override string ModifierName => "경계 돌파";
        public override ModifierFamily Family => ModifierFamily.Position;
        public override string Description
            => $"이번 자동공격 피해 +{PercentPerStack:0.#}%\n다른 구역으로 이동했을 때";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null || !IsPrimary()) return;
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.SourceUnit != owner || context.IsDirectHpLoss) return;
            if (!AttackActionScope.IsActive || AttackActionScope.CurrentSource != owner) return;
            if (!AttackActionScope.CurrentInfo.IsAutoAttack || !AttackActionScope.CurrentInfo.CrossedZone) return;

            context.OutputValue *= 1f + PercentPerStack / 100f * StackCount();
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
