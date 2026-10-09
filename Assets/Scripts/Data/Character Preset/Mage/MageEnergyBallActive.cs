using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Skills;
using UnityEngine;

namespace DiceOrbit.Data.CharacterActives
{
    /// <summary>
    /// [마법사 마력 콤보] 홀수 주사위를 연속 사용해 마력 회로로 연결된 구역을 공격하는 3단계 콤보 (2026-08-28 개편).
    /// 회로 = 자기 구역 + 살아 있는 아군들의 구역 (마력 회로 패시브와 공유).
    /// 1단계 비전 화살(단일) → 2단계 연쇄 번개(2체+감전) → 3단계 궤도 붕괴(회로 전 구역+감전). 수치는 마스터 시트 261003.
    /// 클래스명은 .asset SerializeReference 호환을 위해 유지한다.
    /// </summary>
    [System.Serializable]
    public class MageEnergyBallActive : ComboActiveSkill
    {
        [Header("Designer Tuning — 단계 배율 (피해 = 공격력 × 배율)")]
        [Tooltip("1단계 [비전 화살] 회로 안 가장 가까운 몬스터 하나")]
        [SerializeField] private float stage1Multiplier = 1.2f;
        [Tooltip("2단계 [연쇄 번개] 회로 안 가까운 몬스터 최대 2명 + 감전")]
        [SerializeField] private float stage2Multiplier = 1.2f;
        [Tooltip("2단계 최대 대상 수")]
        [SerializeField] private int stage2MaxTargets = 2;
        [Tooltip("3단계 [궤도 붕괴] 회로로 연결된 모든 구역의 몬스터")]
        [SerializeField] private float stage3Multiplier = 1.5f;
        [Tooltip("감전 수치 — 이번 라운드 동안 대상이 받는 공격 피해 +% (2·3단계 공통)")]
        [SerializeField] private int shockPercent = 25;

        public override string GetStageName(int stage)
            => stage switch { 0 => "비전 화살", 1 => "연쇄 번개", _ => "궤도 붕괴" };

        public override float GetStageMultiplier(int stage)
            => stage switch { 0 => stage1Multiplier, 1 => stage2Multiplier, _ => stage3Multiplier };

        public override List<Unit> ResolveStageTargets(Character source, int stage, IReadOnlyList<int> passedZones)
            => stage switch
            {
                0 => MageCircuit.OwnersInCircuit(source, 1),
                1 => MageCircuit.OwnersInCircuit(source, Mathf.Max(1, stage2MaxTargets)),
                _ => MageCircuit.OwnersInCircuit(source, 0),   // 0 = 회로 전 구역
            };

        public override void OnStageHitLanded(Character source, Unit target, int stage, int hitIndex)
        {
            // 연쇄 번개·궤도 붕괴 — 적중한 대상마다 감전 (이번 라운드 동안 받는 공격 피해 증가)
            if (stage >= 1 && shockPercent > 0)
                GrantStatus(target, EffectType.Shock, shockPercent);
        }

        public override string GetSelectionSummary()
            => $"{FormatDiceConditionNounPhrase()}를 연속으로 사용하면 단일 적, 가까운 적 2명, 마력 회로 전체 순서로 공격하며 두 번째와 마지막 공격은 감전을 부여합니다.";

        public override string GetStageDescription(int stage)
            => stage switch
            {
                0 => $"[비전 화살] 마력 회로 안의 몬스터 하나에게 공격력의 {stage1Multiplier * 100f:0.#}%만큼 피해를 줍니다.",
                1 => $"[연쇄 번개] 회로 안에서 가장 가까운 몬스터 최대 {Mathf.Max(1, stage2MaxTargets)}명에게 공격력의 {stage2Multiplier * 100f:0.#}%만큼 피해를 주고 감전 {shockPercent}%를 부여합니다.",
                _ => $"[궤도 붕괴] 회로에 연결된 모든 구역의 몬스터에게 공격력의 {stage3Multiplier * 100f:0.#}%만큼 피해를 주고 감전 {shockPercent}%를 부여합니다.",
            };
    }
}
