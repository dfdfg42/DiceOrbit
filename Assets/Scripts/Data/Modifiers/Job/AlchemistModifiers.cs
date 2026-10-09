using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Modifiers.Common;
using DiceOrbit.Systems.Effects;
using UnityEngine;

namespace DiceOrbit.Data.Modifiers.Job
{
    /// <summary>
    /// [고순도 촉매] 촉매 25 → 40% (출처 무관). StatusEnhancement가 촉매 계산·부여 때 조회한다(D5).
    /// ReagentPrepPassive.CatalystPercent도 같은 값을 돌려줘 시약 타일 부여·행동 예고·설명이 일치한다.
    /// </summary>
    [System.Serializable]
    public class HighPurityCatalystModifier : JobModifier
    {
        protected override string JobId => JobIds.Alchemist;
        public override string ModifierName => "고순도 촉매";
        public override ModifierFamily Family => ModifierFamily.Combo;
        public override string Description
            => $"촉매로 늘어나는 다음 자동공격 피해가 25%에서 {StatusEnhancement.EnhancedPercent}%로 강화됩니다. 누가 얻은 촉매든 적용되며, 이 연금술사가 쓰러지면 원래대로 돌아갑니다.";
    }
}
