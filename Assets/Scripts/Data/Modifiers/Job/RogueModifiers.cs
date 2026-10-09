using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Modifiers.Common;
using DiceOrbit.Systems.Effects;
using UnityEngine;

namespace DiceOrbit.Data.Modifiers.Job
{
    /// <summary>
    /// [여분의 비수] 3단계 [죽음의 촉매] 타격 4 → 5회. RogueAmbushActive의 3단계 계산이 조회한다(D3) —
    /// 다른 타격 증가보다 먼저, 일제 타격 전환보다 먼저 적용된다(스펙 §3.4).
    /// </summary>
    [System.Serializable]
    public class SpareDaggerModifier : JobModifier
    {
        public const int ExtraHits = 1;

        protected override string JobId => JobIds.Rogue;
        public override string ModifierName => "여분의 비수";
        public override ModifierFamily Family => ModifierFamily.Combo;
        public override string Description
            => $"3단계 강화 공격 [죽음의 촉매]의 타격 횟수가 {ExtraHits}회 늘어납니다.";
    }

    /// <summary>
    /// [일제 타격] 3단계 타격을 1회로 바꾸고, 줄어든 타격 1회당 피해 +110% — 최종 타격 수 n → 1회, 배율 × (1 + 1.1 × (n − 1)).
    /// 4→1 = ×4.3, 5→1(여분의 비수) = ×5.4. RogueAmbushActive의 3단계 계산이 조회한다(D3).
    /// </summary>
    [System.Serializable]
    public class VolleyStrikeModifier : JobModifier
    {
        /// <summary>줄어든 타격 1회당 배율 가산 (+110%).</summary>
        public const float BonusPerRemovedHit = 1.1f;

        protected override string JobId => JobIds.Rogue;
        public override string ModifierName => "일제 타격";
        public override ModifierFamily Family => ModifierFamily.Combo;
        public override string Description
            => $"3단계 강화 공격 [죽음의 촉매]가 한 번의 타격으로 바뀌고, 줄어든 타격 1회마다 그 타격의 피해가 {BonusPerRemovedHit * 100f:0.#}% 증가합니다.";
    }

    /// <summary>[노련한 협공] [협공]의 같은 구역 다른 아군 1명당 피해 증가 20 → 25%. RogueFlankPassive가 조회한다(D3).</summary>
    [System.Serializable]
    public class VeteranFlankModifier : JobModifier
    {
        public const float PerAllyPercent = 25f;

        protected override string JobId => JobIds.Rogue;
        public override string ModifierName => "노련한 협공";
        public override ModifierFamily Family => ModifierFamily.Position;
        public override string Description
            => $"[협공]으로 같은 구역의 다른 아군 1명마다 늘어나는 피해가 20%에서 {PerAllyPercent:0.#}%로 커집니다.";
    }

    /// <summary>
    /// [독침] 공격 직전 대상의 중독이 10 이상이면, 그 강화 공격의 실제 타격 직후마다 중독 1.
    /// - 조건은 행동(행동 ID)마다 대상의 첫 OnPreAction에서 기록한다 — 행동 중 10 도달은 소급하지 않는다.
    /// - 부여는 그 대상 타격의 OnPostAction(실제 처리, 시뮬레이션 아님). 회피된 타격은 OnPostAction이 오지 않는다.
    /// - 타격으로 쓰러진 대상엔 부여하지 않는다. 타격 수는 늘리지 않는다.
    /// - 도적 3단계 '중독 2배'(OnStageCompleted, 스코프 종료 후)보다 먼저 들어가므로 독침 중독도 2배가 된다.
    /// </summary>
    [System.Serializable]
    public class VenomStingModifier : JobModifier
    {
        public const int PoisonThreshold = 10;
        public const int PoisonPerHit = 1;

        [System.NonSerialized] private int _actionId;
        [System.NonSerialized] private Dictionary<Unit, bool> _qualified;

        protected override string JobId => JobIds.Rogue;
        public override string ModifierName => "독침";
        public override ModifierFamily Family => ModifierFamily.Combo;
        public override string Description
            => $"공격 직전 대상의 중독이 {PoisonThreshold} 이상이면 그 강화 공격이 적중할 때마다 중독을 {PoisonPerHit} 부여합니다.";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null || context.SourceUnit != owner) return;
            if (context.IsDirectHpLoss || context.IsSimulation) return;
            if (trigger != CombatTrigger.OnPreAction && trigger != CombatTrigger.OnPostAction) return;
            if (!AttackActionScope.IsActive || AttackActionScope.CurrentSource != owner) return;
            if (AttackActionScope.CurrentInfo.ComboStage < 0) return;   // 강화 공격만
            if (!(context.Target is Monster target)) return;

            int id = AttackActionScope.CurrentActionId;
            if (_qualified == null) _qualified = new Dictionary<Unit, bool>();
            if (id != _actionId)
            {
                _actionId = id;
                _qualified.Clear();
            }

            if (trigger == CombatTrigger.OnPreAction)
            {
                if (!_qualified.ContainsKey(target))
                    _qualified[target] = target.StatusEffects != null
                                         && target.StatusEffects.GetEffectValue(EffectType.Poison) >= PoisonThreshold;
                return;
            }

            // OnPostAction — 실제로 처리된 타격 직후
            if (_qualified.TryGetValue(target, out bool qualified) && qualified && target.IsAlive)
                GrantStatusTo(target, EffectType.Poison, PoisonPerHit);
        }
    }
}
