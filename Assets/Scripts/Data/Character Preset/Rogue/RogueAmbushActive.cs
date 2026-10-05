using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data.Skills;
using UnityEngine;

namespace DiceOrbit.Data.CharacterActives
{
    /// <summary>
    /// [도적 비수 콤보] 3 이하 주사위를 연속 사용해 중독을 쌓고 증폭하는 3단계 콤보 (2026-08-28 개편).
    /// 1단계 쌍비수(2타) → 2단계 맹독비수(3타+중독) → 3단계 죽음의 촉매(5타 후 중독 2배, 소비 없음).
    /// 2단계와 3단계의 표적이 다르면 그 표적의 중독만 2배가 된다 — 위치·표적 유지가 콤보의 핵심.
    /// 클래스명은 .asset SerializeReference 호환을 위해 유지한다.
    /// </summary>
    [System.Serializable]
    public class RogueAmbushActive : ComboActiveSkill
    {
        [Header("Designer Tuning — 단계 배율/타수 (피해 = 공격력 × 배율, 타격마다)")]
        [Tooltip("1단계 [쌍비수] 타격 1회 배율")]
        [SerializeField] private float stage1Multiplier = 0.7f;
        [Tooltip("1단계 [쌍비수] 타격 수")]
        [SerializeField] private int stage1Hits = 2;
        [Tooltip("2단계 [맹독비수] 타격 1회 배율")]
        [SerializeField] private float stage2Multiplier = 0.6f;
        [Tooltip("2단계 [맹독비수] 타격 수")]
        [SerializeField] private int stage2Hits = 3;
        [Tooltip("2단계 적중 1회당 부여할 중독 중첩")]
        [SerializeField] private int stage2PoisonPerHit = 2;
        [Tooltip("3단계 [죽음의 촉매] 타격 1회 배율")]
        [SerializeField] private float stage3Multiplier = 0.5f;
        [Tooltip("3단계 [죽음의 촉매] 타격 수")]
        [SerializeField] private int stage3Hits = 5;

        public override string GetStageName(int stage)
            => stage switch { 0 => "쌍비수", 1 => "맹독비수", _ => "죽음의 촉매" };

        public override float GetStageMultiplier(int stage)
            => stage switch { 0 => stage1Multiplier, 1 => stage2Multiplier, _ => stage3Multiplier };

        public override int GetStageHits(int stage)
            => stage switch { 0 => Mathf.Max(1, stage1Hits), 1 => Mathf.Max(1, stage2Hits), _ => Mathf.Max(1, stage3Hits) };

        /// <summary>모든 단계가 단일 대상 — 현재 구역의 주인.</summary>
        public override List<Unit> ResolveStageTargets(Character source, int stage, IReadOnlyList<int> passedZones)
            => OwnersOfCurrentZone(source);

        public override void OnStageHitLanded(Character source, Unit target, int stage, int hitIndex)
        {
            // 맹독비수 — 적중마다 중독 부여 (죽은 대상 무시는 GrantStatus가 처리)
            if (stage == 1 && stage2PoisonPerHit > 0)
                GrantStatus(target, EffectType.Poison, stage2PoisonPerHit);
        }

        public override void OnStageCompleted(Character source, List<Unit> targets, int stage)
        {
            if (stage != StageCount - 1) return;

            // 죽음의 촉매 — 공격이 끝난 뒤 대상의 현재 중독을 2배로 (소비하지 않는다).
            // AddEffect는 기존 중첩에 합산하므로 '현재치만큼 추가' = 정확히 2배.
            foreach (var target in targets)
            {
                if (target == null || !target.IsAlive || target.StatusEffects == null) continue;

                int current = target.StatusEffects.GetEffectValue(EffectType.Poison);
                if (current <= 0) continue;

                target.StatusEffects.AddEffect(
                    Systems.Effects.StatusEffectManager.CreateEffect(EffectType.Poison, current, -1));
                UI.CombatNotifier.NotifyStatus(target, $"중독 {current * 2} (2배!)", new Color(0.55f, 0.9f, 0.35f));
            }
        }

        public override string GetSelectionSummary()
            => $"{FormatDiceCondition()} 연속 → 3단계 콤보\n{Mathf.Max(1, stage1Hits)}회 → {Mathf.Max(1, stage2Hits)}회 + 중독 → {Mathf.Max(1, stage3Hits)}회 + 중독 2배";

        public override string GetStageDescription(int stage)
            => stage switch
            {
                0 => $"[쌍비수] 공격력 {stage1Multiplier * 100f:0.#}% 피해 × {Mathf.Max(1, stage1Hits)}회",
                1 => $"[맹독비수] 공격력 {stage2Multiplier * 100f:0.#}% 피해 × {Mathf.Max(1, stage2Hits)}회\n적중마다 중독 {stage2PoisonPerHit}",
                _ => $"[죽음의 촉매] 공격력 {stage3Multiplier * 100f:0.#}% 피해 × {Mathf.Max(1, stage3Hits)}회\n끝나면 대상 중독 2배",
            };
    }
}
