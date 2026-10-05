using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Zones;
using DiceOrbit.Data;
using UnityEngine;

namespace DiceOrbit.Data.Passives
{
    /// <summary>
    /// [마력 회로] 마법사의 공격 가능 구역 = 자기 구역 + 살아 있는 다른 아군들의 구역 (2026-08-28 개편).
    /// 아군이 퍼질수록 회로가 넓어져, 셋이 서로 다른 구역에 서면 전장 전체가 사거리가 된다.
    /// 기본공격도 회로 안에서 가장 가까운 몬스터를 찾는다 (AutoAttackSystem이 IAttackZoneProvider를 읽음).
    /// 저장하지 않고 질의 시점에 계산 — 아군 사망·이동이 즉시 반영된다.
    /// 클래스명은 .asset SerializeReference 호환을 위해 유지한다 (구 [원거리]).
    /// </summary>
    [System.Serializable]
    public class MageRangedPassive : CharacterPassiveSkill, IAttackZoneProvider, IPassiveZoneProvider
    {
        public override int Priority => 50;

        public override string GetDynamicDescription()
            => "자기 구역 + 아군이 있는 모든 구역 공격 가능\n아군이 흩어질수록 범위 확대";

        /// <summary>공격 가능 구역 집합 = 마력 회로.</summary>
        public List<int> GetAttackableZones(Character source)
            => MageCircuit.GetCircuitZones(source != null ? source : owner as Character);

        /// <summary>패시브 구역 = 회로로 연결된 모든 구역 (첫 번째가 마법사 자신의 구역).</summary>
        public void CollectPassiveZones(List<int> zones)
        {
            if (!(owner is Character mage)) return;
            zones.AddRange(MageCircuit.GetCircuitZones(mage));
        }

        public PassiveZoneStatus GetPassiveZoneStatus()
            => FormatZoneStatus(owner is Character mage ? MageCircuit.GetCircuitZones(mage).Count : 0);

        /// <summary>지금 효과 한 줄 (순수 — PassiveZoneSelfTests). 자기 구역뿐이면 회로가 넓혀 준 것이 없다 → 꺼짐.</summary>
        public static PassiveZoneStatus FormatZoneStatus(int circuitZoneCount)
        {
            if (circuitZoneCount <= 1) return new PassiveZoneStatus("자기 구역만", false);
            return new PassiveZoneStatus($"{circuitZoneCount}구역 연결", true);
        }
    }
}
