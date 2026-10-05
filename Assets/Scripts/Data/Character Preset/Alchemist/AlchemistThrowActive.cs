using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data.Skills;
using UnityEngine;

namespace DiceOrbit.Data.CharacterActives
{
    /// <summary>
    /// [연금술사 연성 콤보] 짝수 주사위를 연속 사용해 적을 약화시키고 파티를 끌어올리는 3단계 콤보 (2026-08-28 개편).
    /// 1단계 산성 플라스크(단일+약화) → 2단계 촉매 살포(현+양옆+약화) → 3단계 대연성: 현자의 폭탄(전장+아군 고양).
    /// 시약 타일 촉매(+25%)를 폭탄 직전에 밟으면 폭탄 전체가 강화된다 — 경로 계획의 정점.
    /// 클래스명은 .asset SerializeReference 호환을 위해 유지한다.
    /// </summary>
    [System.Serializable]
    public class AlchemistThrowActive : ComboActiveSkill
    {
        [Header("Designer Tuning — 단계 배율 (피해 = 공격력 × 배율)")]
        [Tooltip("1단계 [산성 플라스크] 현재 구역 몬스터 하나 + 약화")]
        [SerializeField] private float stage1Multiplier = 1.0f;
        [Tooltip("2단계 [촉매 살포] 현재 구역과 양옆 구역의 모든 몬스터 + 약화")]
        [SerializeField] private float stage2Multiplier = 0.9f;
        [Tooltip("3단계 [대연성: 현자의 폭탄] 전장의 모든 몬스터")]
        [SerializeField] private float stage3Multiplier = 1.8f;
        [Tooltip("양옆으로 몇 구역까지 퍼질지 (2단계)")]
        [SerializeField] private int spreadZones = 1;
        [Tooltip("약화 수치 — 대상의 다음 공격 행동 피해 -%")]
        [SerializeField] private int weakenPercent = 25;
        [Tooltip("고양 수치 — 3단계 후 아군 전원의 다음 공격 행동 피해 +%")]
        [SerializeField] private int inspirePercent = 30;

        public override string GetStageName(int stage)
            => stage switch { 0 => "산성 플라스크", 1 => "촉매 살포", _ => "대연성: 현자의 폭탄" };

        public override float GetStageMultiplier(int stage)
            => stage switch { 0 => stage1Multiplier, 1 => stage2Multiplier, _ => stage3Multiplier };

        public override List<Unit> ResolveStageTargets(Character source, int stage, IReadOnlyList<int> passedZones)
            => stage switch
            {
                0 => OwnersOfCurrentZone(source),
                1 => OwnersOfCurrentAndAdjacent(source, Mathf.Max(0, spreadZones)),
                _ => AllLivingMonsters(),
            };

        public override void OnStageHitLanded(Character source, Unit target, int stage, int hitIndex)
        {
            // 1·2단계 — 적중한 모든 대상에게 약화 (다음 공격 행동 피해 감소, 행동 전체 적용 후 제거)
            if (stage <= 1 && weakenPercent > 0)
                GrantStatus(target, EffectType.Weaken, weakenPercent);
        }

        public override void OnStageCompleted(Character source, List<Unit> targets, int stage)
        {
            if (stage != StageCount - 1 || inspirePercent <= 0) return;

            // 현자의 폭탄 — 공격 후 살아 있는 모든 아군(자신 포함)에게 고양.
            foreach (var ally in AllLivingAllies())
                GrantStatus(ally, EffectType.Inspire, inspirePercent);
        }

        public override string GetSelectionSummary()
            => $"{FormatDiceCondition()} 연속 → 3단계 콤보\n한 명 + 약화 → 양옆 구역까지 + 약화 → 전체 + 아군 고양";

        public override string GetStageDescription(int stage)
            => stage switch
            {
                0 => $"[산성 플라스크] 공격력 {stage1Multiplier * 100f:0.#}% 피해\n현재 구역 1명 + 약화 {weakenPercent}%",
                1 => $"[촉매 살포] 공격력 {stage2Multiplier * 100f:0.#}% 피해\n현재·양옆 구역 + 약화 {weakenPercent}%",
                _ => $"[대연성: 현자의 폭탄] 공격력 {stage3Multiplier * 100f:0.#}% 피해\n전장 전체 + 아군 고양 {inspirePercent}%",
            };
    }
}
