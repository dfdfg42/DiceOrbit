using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Core.Zones;
using DiceOrbit.Data;
using DiceOrbit.Data.Modifiers.Job;
using UnityEngine;

namespace DiceOrbit.Data.Passives
{
    /// <summary>
    /// [방진] 전사와 같은 구역에 모인 '다른 아군' 수만큼 그 구역 전원이 단단해진다 (2026-08-28 개편).
    /// 다른 아군 1명당 받는 공격 피해 -10% (1/2/3명 = -10/-20/-30%). 전사 자신도 감쇄를 받지만
    /// 중첩 수 계산에서는 제외된다. 저장 없이 공격 시점에 계산하므로 진입·이탈·사망이 즉시 반영된다.
    /// 중독 같은 직접 체력 손실에는 적용되지 않는다 (파이프라인이 통지를 건너뜀 + 이중 방어).
    /// 반격·추가 공격은 없다 — 순수 감쇄만.
    /// </summary>
    [System.Serializable]
    public class WarriorGuardPassive : CharacterPassiveSkill, IPassiveZoneProvider
    {
        [Header("Designer Tuning")]
        [Tooltip("같은 구역의 '다른 아군' 1명당 받는 공격 피해 감소율(%). 예: 10이면 3명일 때 -30%")]
        [SerializeField] private float perAllyReductionPercent = 10f;

        public override int Priority => 100;

        /// <summary>지금 아군 1명당 감소 % — 견고한 방진 보유 시 15 (2026-10-09 C, D3). 피해 계산·구역 상태·설명이 같은 값을 쓴다.</summary>
        private float PerAllyPercent
            => EffectivePerAllyPercent(perAllyReductionPercent,
                                       owner is Character guard && JobModifierQueries.Has<SteadfastFormationModifier>(guard));

        /// <summary>견고한 방진이면 max(기본, 15) (순수 — 자가 테스트 대상).</summary>
        public static float EffectivePerAllyPercent(float basePercent, bool steadfast)
            => steadfast ? Mathf.Max(basePercent, SteadfastFormationModifier.PerAllyPercent) : basePercent;

        public override string GetDynamicDescription()
            => $"전사와 같은 구역에 있는 다른 아군 1명마다 해당 구역의 모든 아군(전사 포함)이 받는 공격 피해가 {PerAllyPercent:0.#}% 감소합니다.";

        /// <summary>패시브 구역 = 전사가 선 구역. 조회 시 구역 테두리에 쓰인다.</summary>
        public void CollectPassiveZones(List<int> zones)
        {
            var manager = CombatZoneManager.Instance;
            if (manager == null || !(owner is Character guard)) return;
            int zone = manager.GetZoneOf(guard);
            if (zone >= 0) zones.Add(zone);
        }

        public PassiveZoneStatus GetPassiveZoneStatus()
        {
            var manager = CombatZoneManager.Instance;
            int allyCount = 0;
            if (manager != null && owner is Character guard)
            {
                int zone = manager.GetZoneOf(guard);
                if (zone >= 0) allyCount = CountOtherAlliesInZone(guard, zone, manager);
            }
            return FormatZoneStatus(allyCount, PerAllyPercent);
        }

        /// <summary>같은 구역 '다른 아군' 수에 따른 감쇄율(%). 피해 계산과 정보 패널 표시가 같은 식을 쓴다.</summary>
        public static float ReductionPercent(int allyCount, float perAllyPercent)
            => Mathf.Clamp(perAllyPercent * Mathf.Max(0, allyCount), 0f, 90f);

        /// <summary>지금 효과 한 줄 (순수 — PassiveZoneSelfTests).</summary>
        public static PassiveZoneStatus FormatZoneStatus(int allyCount, float perAllyPercent)
        {
            if (allyCount <= 0) return new PassiveZoneStatus("같은 구역 아군 없음", false);
            return new PassiveZoneStatus($"받는 피해 -{ReductionPercent(allyCount, perAllyPercent):0.#}%", true);
        }

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.IsDirectHpLoss) return;   // 중독 등 직접 체력 손실 제외 (이중 방어)
            if (!(owner is Character guard) || !guard.IsAlive) return;

            // 아군이 맞을 때만 개입한다 (아군의 공격에는 관여하지 않는다).
            if (!(context.Target is Character victim)) return;

            var zones = CombatZoneManager.Instance;
            if (zones == null) return;

            int guardZone = zones.GetZoneOf(guard);
            if (guardZone < 0 || zones.GetZoneOf(victim) != guardZone) return;

            int allyCount = CountOtherAlliesInZone(guard, guardZone, zones);
            if (allyCount <= 0) return;

            float reduction = ReductionPercent(allyCount, PerAllyPercent);
            context.OutputValue *= 1f - reduction / 100f;
            if (!context.IsSimulation) Notify($"{PassiveName} -{reduction:0.#}%");
        }

        /// <summary>전사 구역에 있는 '전사가 아닌' 살아 있는 아군 수 (중첩 수의 기준).</summary>
        private static int CountOtherAlliesInZone(Character guard, int guardZone, CombatZoneManager zones)
        {
            var party = PartyManager.Instance;
            if (party == null) return 0;

            int count = 0;
            foreach (var ally in party.GetAliveCharacters())
            {
                if (ally == null || ally == guard) continue;
                if (zones.GetZoneOf(ally) == guardZone) count++;
            }
            return count;
        }
    }
}
