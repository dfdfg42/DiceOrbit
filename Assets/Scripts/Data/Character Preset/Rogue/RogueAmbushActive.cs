using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data.Skills;
using DiceOrbit.Data.Modifiers.Job;
using UnityEngine;

namespace DiceOrbit.Data.CharacterActives
{
    /// <summary>
    /// [도적 비수 콤보] 3 이하 주사위를 연속 사용해 중독을 쌓고 증폭하는 3단계 콤보 (2026-08-28 개편).
    /// 1단계 쌍비수(2타) → 2단계 맹독비수(3타 후 중독 5) → 3단계 죽음의 촉매(4타 후 중독 2배, 소비 없음). 수치는 마스터 시트 261003.
    /// 2단계와 3단계의 표적이 다르면 그 표적의 중독만 2배가 된다 — 위치·표적 유지가 콤보의 핵심.
    /// 클래스명은 .asset SerializeReference 호환을 위해 유지한다.
    /// </summary>
    [System.Serializable]
    public class RogueAmbushActive : ComboActiveSkill
    {
        [Header("Designer Tuning — 단계 배율/타수 (피해 = 공격력 × 배율, 타격마다)")]
        [Tooltip("1단계 [쌍비수] 타격 1회 배율")]
        [SerializeField] private float stage1Multiplier = 0.6f;
        [Tooltip("1단계 [쌍비수] 타격 수")]
        [SerializeField] private int stage1Hits = 2;
        [Tooltip("2단계 [맹독비수] 타격 1회 배율")]
        [SerializeField] private float stage2Multiplier = 0.75f;
        [Tooltip("2단계 [맹독비수] 타격 수")]
        [SerializeField] private int stage2Hits = 3;
        [Tooltip("2단계 행동을 마친 뒤 대상에게 부여할 중독 중첩 (타격 수와 무관)")]
        [SerializeField] private int stage2Poison = 5;
        [Tooltip("3단계 [죽음의 촉매] 타격 1회 배율")]
        [SerializeField] private float stage3Multiplier = 1.0f;
        [Tooltip("3단계 [죽음의 촉매] 타격 수")]
        [SerializeField] private int stage3Hits = 4;

        public override string GetStageName(int stage)
            => stage switch { 0 => "쌍비수", 1 => "맹독비수", _ => "죽음의 촉매" };

        public override float GetStageMultiplier(int stage)
            => stage switch { 0 => stage1Multiplier, 1 => stage2Multiplier, _ => stage3Multiplier };

        public override int GetStageHits(int stage)
            => stage switch { 0 => Mathf.Max(1, stage1Hits), 1 => Mathf.Max(1, stage2Hits), _ => Mathf.Max(1, stage3Hits) };

        // ── 3단계 주인 기준 (2026-10-09 C — 여분의 비수·일제 타격, 스펙 §3.4) ──
        // 실행(AttackPlan.Hits)·피해(CalculateStageDamage)·행동 예고·미리보기·정보 패널이 모두 아래 한 계산을 쓴다.

        public override int GetStageHits(Character source, int stage)
        {
            if (stage != StageCount - 1) return GetStageHits(stage);
            ResolveStage3For(source, out int hits, out _);
            return hits;
        }

        public override float GetStageMultiplier(Character source, int stage)
        {
            if (stage != StageCount - 1) return GetStageMultiplier(stage);
            ResolveStage3For(source, out _, out float scale);
            return GetStageMultiplier(stage) * scale;
        }

        public override string GetStageDescription(Character source, int stage)
        {
            if (stage != StageCount - 1) return GetStageDescription(stage);
            ResolveStage3For(source, out int hits, out float scale);
            return FormatStage3Description(stage3Multiplier * scale, hits);
        }

        /// <summary>
        /// 3단계 타격 수·배율 (순수 — 자가 테스트 대상). 순서: 기본 → 여분의 비수(+1) → (다른 타격 증가 자리) → 일제 타격 전환.
        /// 일제 타격이면 최종 타격 수 n을 1회로 바꾸고 배율 × (1 + 1.1 × (n − 1)).
        /// </summary>
        public static void ResolveStage3(int baseHits, bool spareDagger, bool volley, out int hits, out float multiplierScale)
        {
            int n = Mathf.Max(1, baseHits);
            if (spareDagger) n += SpareDaggerModifier.ExtraHits;
            // 다른 타격 증가가 생기면 여기에 — 일제 타격 전환보다 먼저.

            if (volley)
            {
                hits = 1;
                multiplierScale = 1f + VolleyStrikeModifier.BonusPerRemovedHit * (n - 1);
            }
            else
            {
                hits = n;
                multiplierScale = 1f;
            }
        }

        private void ResolveStage3For(Character source, out int hits, out float multiplierScale)
            => ResolveStage3(stage3Hits,
                             JobModifierQueries.Has<SpareDaggerModifier>(source),
                             JobModifierQueries.Has<VolleyStrikeModifier>(source),
                             out hits, out multiplierScale);

        private static string FormatStage3Description(float multiplier, int hits)
            => $"[죽음의 촉매] 공격력의 {multiplier * 100f:0.#}%만큼 {hits}회 피해를 준 뒤 대상의 중독을 2배로 늘립니다.";

        /// <summary>모든 단계가 단일 대상 — 현재 구역의 주인.</summary>
        public override List<Unit> ResolveStageTargets(Character source, int stage, IReadOnlyList<int> passedZones)
            => OwnersOfCurrentZone(source);

        public override void OnStageCompleted(Character source, List<Unit> targets, int stage)
        {
            // 맹독비수 — 행동을 마친 뒤 대상마다 중독 (시트: 3회 공격 후 중독 5)
            if (stage == 1)
            {
                if (stage2Poison <= 0) return;
                foreach (var target in targets)
                    GrantStatus(target, EffectType.Poison, stage2Poison);
                return;
            }

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
            => $"{FormatDiceConditionNounPhrase()}를 연속으로 사용하면 공격 횟수가 2회, 3회, 4회로 늘어나며 중독을 부여하고 마지막 공격으로 중독을 2배로 늘립니다.";

        public override string GetStageDescription(int stage)
            => stage switch
            {
                0 => $"[쌍비수] 공격력의 {stage1Multiplier * 100f:0.#}%만큼 {Mathf.Max(1, stage1Hits)}회 피해를 줍니다.",
                1 => $"[맹독비수] 공격력의 {stage2Multiplier * 100f:0.#}%만큼 {Mathf.Max(1, stage2Hits)}회 피해를 주고 중독을 {stage2Poison} 부여합니다.",
                _ => FormatStage3Description(stage3Multiplier, Mathf.Max(1, stage3Hits)),
            };
    }
}
