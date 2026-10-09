using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data.Skills;
using DiceOrbit.Data.Modifiers.Job;
using UnityEngine;

namespace DiceOrbit.Data.CharacterActives
{
    /// <summary>
    /// [전사 대검 콤보] 4 이상 주사위를 연속 사용할 때마다 강해지는 3단계 콤보 (2026-08-28 개편).
    /// 1단계 대검 베기(현재 구역) → 2단계 균열 베기(현+양옆) → 3단계 대지 가르기(전장 + 아군 방어도). 수치는 마스터 시트 261003.
    /// 클래스명은 .asset SerializeReference 호환을 위해 유지한다.
    /// </summary>
    [System.Serializable]
    public class WarriorGreatswordActive : ComboActiveSkill
    {
        [Header("Designer Tuning — 단계 배율 (피해 = 공격력 × 배율)")]
        [Tooltip("1단계 [대검 베기] 현재 구역의 몬스터")]
        [SerializeField] private float stage1Multiplier = 1.2f;
        [Tooltip("2단계 [균열 베기] 현재 구역과 양옆 구역의 몬스터")]
        [SerializeField] private float stage2Multiplier = 1.2f;
        [Tooltip("3단계 [대지 가르기] 전장의 모든 몬스터")]
        [SerializeField] private float stage3Multiplier = 1.5f;
        [Tooltip("3단계 후 모든 아군에게 부여할 임시 방어도")]
        [SerializeField] private int stage3AllyArmor = 5;

        public override string GetStageName(int stage)
            => stage switch { 0 => "대검 베기", 1 => "균열 베기", _ => "대지 가르기" };

        public override float GetStageMultiplier(int stage)
            => stage switch { 0 => stage1Multiplier, 1 => stage2Multiplier, _ => stage3Multiplier };

        public override List<Unit> ResolveStageTargets(Character source, int stage, IReadOnlyList<int> passedZones)
            => stage switch
            {
                0 => OwnersOfCurrentZone(source),
                1 => OwnersOfCurrentAndAdjacent(source, 1),
                _ => AllLivingMonsters(),
            };

        /// <summary>3단계 후 아군 일시 방어도 — 대지의 룬 보유 시 +3 (2026-10-09 C, D3). 부여·설명이 같은 값을 쓴다.</summary>
        public int GetStage3AllyArmor(Character source)
            => AllyArmorFor(stage3AllyArmor, JobModifierQueries.Has<EarthRuneModifier>(source));

        /// <summary>기본 방어도 + 대지의 룬 (순수 — 자가 테스트 대상). 기본이 0 이하(부여 꺼짐)면 0.</summary>
        public static int AllyArmorFor(int baseArmor, bool earthRune)
            => baseArmor <= 0 ? 0 : baseArmor + (earthRune ? EarthRuneModifier.ExtraAllyArmor : 0);

        public override void OnStageCompleted(Character source, List<Unit> targets, int stage)
        {
            if (stage != StageCount - 1) return;
            int armor = GetStage3AllyArmor(source);
            if (armor <= 0) return;

            // 대지 가르기 — 살아 있는 모든 아군에게 임시 방어도. 캐릭터 방어도는 소모될 때까지 유지되므로
            // '최소한 다음 적 공격까지'가 자연히 보장된다.
            foreach (var ally in AllLivingAllies())
            {
                if (ally == null || ally.Stats == null) continue;
                ally.Stats.TempArmor += armor;
                UI.CombatNotifier.NotifyStatus(ally, $"방어도 +{armor}", new Color(0.65f, 0.8f, 1f));
            }
        }

        public override string GetSelectionSummary()
            => $"{FormatDiceConditionNounPhrase()}를 연속으로 사용하면 공격 범위가 단일 대상에서 인접 구역, 전장 전체로 넓어지며 마지막 공격은 모든 아군에게 방어도를 부여합니다.";

        public override string GetStageDescription(int stage)
            => stage switch
            {
                0 => $"[대검 베기] 현재 구역의 몬스터에게 공격력의 {stage1Multiplier * 100f:0.#}%만큼 피해를 줍니다.",
                1 => $"[균열 베기] 현재 구역과 양옆 구역의 몬스터에게 공격력의 {stage2Multiplier * 100f:0.#}%만큼 피해를 줍니다.",
                _ => FormatStage3Description(stage3AllyArmor),
            };

        /// <summary>주인 기준 3단계 설명 — 대지의 룬 방어도 반영 (정보 패널).</summary>
        public override string GetStageDescription(Character source, int stage)
            => stage == StageCount - 1 ? FormatStage3Description(GetStage3AllyArmor(source)) : GetStageDescription(stage);

        private string FormatStage3Description(int allyArmor)
            => $"[대지 가르기] 모든 몬스터에게 공격력의 {stage3Multiplier * 100f:0.#}%만큼 피해를 주고 모든 아군에게 일시 방어도 {allyArmor}을 부여합니다.";
    }
}
