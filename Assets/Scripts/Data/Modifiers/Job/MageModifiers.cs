using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Core.Zones;
using DiceOrbit.Data.Modifiers.Common;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using UnityEngine;

namespace DiceOrbit.Data.Modifiers.Job
{
    /// <summary>
    /// [고전압] 감전 25 → 40% (출처 무관). 효과는 StatusEnhancement가 감전 계산·부여 때 이 모디파이어를 조회해 낸다(D5) —
    /// 이 클래스 자체엔 훅이 없다. 보유 마법사가 쓰러지면 조회에서 빠져 원래대로 돌아간다.
    /// </summary>
    [System.Serializable]
    public class HighVoltageModifier : JobModifier
    {
        protected override string JobId => JobIds.Mage;
        public override string ModifierName => "고전압";
        public override ModifierFamily Family => ModifierFamily.Combo;
        public override string Description
            => $"감전으로 늘어나는 받는 피해가 25%에서 {StatusEnhancement.EnhancedPercent}%로 강화됩니다. 누가 부여한 감전이든 적용되며, 이 마법사가 쓰러지면 원래대로 돌아갑니다.";
    }
}
