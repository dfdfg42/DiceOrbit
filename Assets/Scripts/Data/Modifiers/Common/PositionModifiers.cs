using System.Collections.Generic;
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

    /// <summary>
    /// [듀얼] 공격 대상의 양옆 구역(원형 wrap)에 다른 살아 있는 몬스터가 없으면 피해 +15%.
    /// 같은 구역은 보지 않는다(구역당 몬스터 1마리 규칙). 대상 구역을 모르면 보정 없음.
    /// </summary>
    [System.Serializable]
    public class DuelModifier : CommonModifier
    {
        private const float Percent = 15f;

        public override string ModifierName => "듀얼";
        public override ModifierFamily Family => ModifierFamily.Position;
        public override string Description
            => $"공격 대상의 양옆 구역에 다른 몬스터가 없으면 피해가 {Percent:0.#}% 증가합니다.";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (!IsOwnAttackCalculation(trigger, context)) return;
            if (!(context.Target is Monster target)) return;
            if (!IsIsolated(target)) return;

            context.OutputValue *= 1f + Percent / 100f;
        }

        /// <summary>대상의 양옆 구역에 대상이 아닌 살아 있는 주인이 하나도 없는가.</summary>
        public static bool IsIsolated(Monster target)
        {
            var zones = CombatZoneManager.Instance;
            if (zones == null || target == null) return false;

            int zone = zones.GetZoneOfMonster(target);
            if (zone < 0) return false;

            int n = zones.ZoneCount;
            int left = ((zone - 1) % n + n) % n;
            int right = (zone + 1) % n;
            return !HasOtherLivingOwner(zones, left, target) && !HasOtherLivingOwner(zones, right, target);
        }

        private static bool HasOtherLivingOwner(CombatZoneManager zones, int zone, Monster self)
        {
            var owner = zones.GetOwner(zone);   // 죽었거나 없으면 null
            return owner != null && owner != self;
        }
    }

    /// <summary>[기동 장갑] 다른 구역으로 이동을 마치면 방어도 4. 공격할 몬스터가 없어도 발동.</summary>
    [System.Serializable]
    public class MobileArmorModifier : CommonModifier, IMoveFinishedListener
    {
        private const int Armor = 4;

        public override string ModifierName => "기동 장갑";
        public override ModifierFamily Family => ModifierFamily.Position;
        public override string Description
            => $"다른 구역으로 이동을 마치면 방어도를 {Armor}만큼 얻습니다.";

        public void OnMoveFinished(Character character, int fromZone, int toZone)
        {
            if (character != owner || owner == null || !owner.IsAlive) return;
            if (fromZone < 0 || toZone < 0 || fromZone == toZone) return;
            GrantArmor(Armor);
        }
    }

    /// <summary>
    /// [구조대] 행동(이동만 해도 포함)을 마친 뒤 같은 구역에서 현재 체력이 가장 낮은 아군 1명(자신 포함, 동률 무작위) 체력 +2.
    /// 최대 체력인 아군은 대상에서 제외(초과 회복 없음). 회복은 HealContext로 파이프라인을 거친다.
    /// </summary>
    [System.Serializable]
    public class RescueTeamModifier : CommonModifier, IActionFinishedListener
    {
        private const int HealAmount = 2;

        public override string ModifierName => "구조대";
        public override ModifierFamily Family => ModifierFamily.Position;
        public override string Description
            => $"행동을 마친 뒤 같은 구역에서 체력이 가장 낮은 아군(자신 포함)의 체력을 {HealAmount} 회복합니다.";

        public void OnActionFinished(Character character)
        {
            if (character != owner || owner == null || !owner.IsAlive) return;

            var target = PickTarget(owner);
            if (target != null) HealTarget(target, HealAmount);
        }

        /// <summary>self와 같은 구역의 살아 있는 아군(자신 포함) 중 회복 대상.</summary>
        public static Character PickTarget(Character self)
        {
            var zones = CombatZoneManager.Instance;
            var party = PartyManager.Instance;
            if (zones == null || party == null || self == null) return null;

            int myZone = zones.GetZoneOf(self);
            if (myZone < 0) return null;

            var candidates = new List<Character>();
            foreach (var ally in party.GetAliveCharacters())
                if (ally != null && zones.GetZoneOf(ally) == myZone) candidates.Add(ally);

            return PickLowestHp(candidates);
        }

        /// <summary>최대 체력 미만인 생존자 중 현재 체력 최저 1명, 동률이면 무작위 (순수 — 자가 테스트 대상). 없으면 null.</summary>
        public static Character PickLowestHp(IReadOnlyList<Character> candidates)
        {
            if (candidates == null) return null;

            List<Character> lowest = null;
            int best = int.MaxValue;
            foreach (var c in candidates)
            {
                if (c == null || c.Stats == null || !c.IsAlive) continue;
                if (c.Stats.CurrentHP >= c.Stats.MaxHP) continue;

                if (c.Stats.CurrentHP < best)
                {
                    best = c.Stats.CurrentHP;
                    lowest = new List<Character> { c };
                }
                else if (c.Stats.CurrentHP == best)
                {
                    lowest.Add(c);
                }
            }

            if (lowest == null) return null;
            return lowest[UnityEngine.Random.Range(0, lowest.Count)];
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
