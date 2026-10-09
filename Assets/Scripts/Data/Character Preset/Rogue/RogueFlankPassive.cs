using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Core.Zones;
using DiceOrbit.Data;
using UnityEngine;

namespace DiceOrbit.Data.Passives
{
    /// <summary>
    /// [협공] 같은 구역의 '다른 아군' 수만큼 도적의 공격이 깊어진다 (2026-08-28 개편 — 인원 비례).
    /// 다른 아군 1명당 도적이 주는 공격 피해 +20% (1/2/3명 = +20/+40/+60%). 도적 자신은 제외.
    /// 비수 콤보·기본공격 같은 직접 공격 피해에만 적용되고, 중독 피해에는 적용되지 않는다
    /// (직접 체력 손실은 파이프라인이 리액터 통지를 건너뜀 + 이중 방어).
    /// </summary>
    [System.Serializable]
    public class RogueFlankPassive : CharacterPassiveSkill, IPassiveZoneProvider
    {
        [Header("Designer Tuning")]
        [Tooltip("같은 구역의 '다른 아군' 1명당 피해 증가율(%). 예: 20이면 3명일 때 +60%")]
        [SerializeField] private float perAllyBonusPercent = 20f;

        public override int Priority => 99;

        public override string GetDynamicDescription()
            => $"같은 구역에 있는 다른 아군 1명마다 도적이 주는 공격 피해가 {perAllyBonusPercent:0.#}% 증가합니다.";

        /// <summary>패시브 구역 = 도적이 선 구역. 조회 시 구역 테두리에 쓰인다.</summary>
        public void CollectPassiveZones(List<int> zones)
        {
            var manager = CombatZoneManager.Instance;
            if (manager == null || !(owner is Character rogue)) return;
            int zone = manager.GetZoneOf(rogue);
            if (zone >= 0) zones.Add(zone);
        }

        public PassiveZoneStatus GetPassiveZoneStatus()
            => FormatZoneStatus(owner is Character rogue ? CountOtherAlliesInMyZone(rogue) : 0, perAllyBonusPercent);

        /// <summary>지금 효과 한 줄 (순수 — PassiveZoneSelfTests).</summary>
        public static PassiveZoneStatus FormatZoneStatus(int allyCount, float perAllyPercent)
        {
            if (allyCount <= 0) return new PassiveZoneStatus("같은 구역 아군 없음", false);
            return new PassiveZoneStatus($"주는 피해 +{perAllyPercent * allyCount:0.#}%", true);
        }

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.IsDirectHpLoss) return;   // 중독 피해엔 적용 없음 (이중 방어)
            if (context.SourceUnit != owner) return;
            if (!(owner is Character rogue)) return;

            int allyCount = CountOtherAlliesInMyZone(rogue);
            if (allyCount <= 0) return;

            float percent = perAllyBonusPercent * allyCount;
            context.OutputValue *= 1f + percent / 100f;
            if (!context.IsSimulation) Notify($"{PassiveName} +{percent:0.#}%");
        }

        /// <summary>도적 구역에 있는 '도적이 아닌' 살아 있는 아군 수.</summary>
        private int CountOtherAlliesInMyZone(Character rogue)
        {
            var zones = CombatZoneManager.Instance;
            var party = PartyManager.Instance;
            if (zones == null || party == null) return 0;

            int myZone = zones.GetZoneOf(rogue);
            if (myZone < 0) return 0;

            int count = 0;
            foreach (var ally in party.GetAliveCharacters())
            {
                if (ally == null || ally == rogue) continue;
                if (zones.GetZoneOf(ally) == myZone) count++;
            }
            return count;
        }

        // (구 시그니처 모디파이어 연동은 2026-08-28 공용 모디파이어 전면 교체로 폐기됐다)
    }
}
