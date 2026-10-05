using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data.Skills;
using UnityEngine;

namespace DiceOrbit.Data.CharacterActives
{
    /// <summary>
    /// [전사 대검 콤보] 4 이상 주사위를 연속 사용할 때마다 강해지는 3단계 콤보 (2026-08-28 개편).
    /// 1단계 균열 베기(단일) → 2단계 지진파(현+양옆) → 3단계 대지 가르기(전장 + 아군 방어도).
    /// 클래스명은 .asset SerializeReference 호환을 위해 유지한다.
    /// </summary>
    [System.Serializable]
    public class WarriorGreatswordActive : ComboActiveSkill
    {
        [Header("Designer Tuning — 단계 배율 (피해 = 공격력 × 배율)")]
        [Tooltip("1단계 [균열 베기] 현재 구역 몬스터 하나")]
        [SerializeField] private float stage1Multiplier = 1.5f;
        [Tooltip("2단계 [지진파] 현재 구역과 양옆 인접 구역의 모든 몬스터")]
        [SerializeField] private float stage2Multiplier = 1.1f;
        [Tooltip("3단계 [대지 가르기] 전장의 모든 몬스터")]
        [SerializeField] private float stage3Multiplier = 1.8f;
        [Tooltip("3단계 후 모든 아군에게 부여할 임시 방어도")]
        [SerializeField] private int stage3AllyArmor = 10;

        public override string GetStageName(int stage)
            => stage switch { 0 => "균열 베기", 1 => "지진파", _ => "대지 가르기" };

        public override float GetStageMultiplier(int stage)
            => stage switch { 0 => stage1Multiplier, 1 => stage2Multiplier, _ => stage3Multiplier };

        public override List<Unit> ResolveStageTargets(Character source, int stage, IReadOnlyList<int> passedZones)
            => stage switch
            {
                0 => OwnersOfCurrentZone(source),
                1 => OwnersOfCurrentAndAdjacent(source, 1),
                _ => AllLivingMonsters(),
            };

        public override void OnStageCompleted(Character source, List<Unit> targets, int stage)
        {
            if (stage != StageCount - 1 || stage3AllyArmor <= 0) return;

            // 대지 가르기 — 살아 있는 모든 아군에게 임시 방어도. 캐릭터 방어도는 소모될 때까지 유지되므로
            // '최소한 다음 적 공격까지'가 자연히 보장된다.
            foreach (var ally in AllLivingAllies())
            {
                if (ally == null || ally.Stats == null) continue;
                ally.Stats.TempArmor += stage3AllyArmor;
                UI.CombatNotifier.NotifyStatus(ally, $"방어도 +{stage3AllyArmor}", new Color(0.65f, 0.8f, 1f));
            }
        }

        public override string GetSelectionSummary()
            => $"{FormatDiceCondition()} 연속 → 3단계 콤보\n한 명 → 양옆 구역까지 → 전체 + 아군 방어도";

        public override string GetStageDescription(int stage)
            => stage switch
            {
                0 => $"[균열 베기] 공격력 {stage1Multiplier * 100f:0.#}% 피해\n현재 구역 1명",
                1 => $"[지진파] 공격력 {stage2Multiplier * 100f:0.#}% 피해\n현재·양옆 구역 전부",
                _ => $"[대지 가르기] 공격력 {stage3Multiplier * 100f:0.#}% 피해\n전장 전체 + 아군 방어도 {stage3AllyArmor}",
            };
    }
}
