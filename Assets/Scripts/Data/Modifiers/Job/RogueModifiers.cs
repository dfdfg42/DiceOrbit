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
}
