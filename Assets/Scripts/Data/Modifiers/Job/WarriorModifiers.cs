using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Modifiers.Common;
using DiceOrbit.Systems.Effects;
using UnityEngine;

namespace DiceOrbit.Data.Modifiers.Job
{
    /// <summary>[견고한 방진] [방진]의 같은 구역 다른 아군 1명당 받는 피해 감소 10 → 15%. WarriorGuardPassive가 조회한다(D3).</summary>
    [System.Serializable]
    public class SteadfastFormationModifier : JobModifier
    {
        public const float PerAllyPercent = 15f;

        protected override string JobId => JobIds.Warrior;
        public override string ModifierName => "견고한 방진";
        public override ModifierFamily Family => ModifierFamily.Position;
        public override string Description
            => $"[방진]으로 같은 구역의 다른 아군 1명마다 줄어드는 받는 피해가 10%에서 {PerAllyPercent:0.#}%로 커집니다.";
    }

    /// <summary>
    /// [전우애] 같은 구역에 다른 살아 있는 아군이 2명 이상이면 기본 공격력 +1 (미중첩).
    /// 저장 없이 AttackBonus 게터에서 지금 구역 아군 수를 센다 — 조건이 깨지면 즉시 해제되고,
    /// 행동 예고(PretendAt)·정보 패널·실제 피해가 같은 값을 본다. 저장 필드(Attack)는 바꾸지 않는다(B의 D6).
    /// </summary>
    [System.Serializable]
    public class ComradeshipModifier : JobModifier
    {
        public const int Bonus = 1;
        public const int RequiredAllies = 2;

        protected override string JobId => JobIds.Warrior;
        public override string ModifierName => "전우애";
        public override ModifierFamily Family => ModifierFamily.Position;
        public override string Description
            => $"같은 구역에 다른 아군이 {RequiredAllies}명 이상 있으면 기본 공격력이 {Bonus} 증가합니다. 조건이 깨지면 바로 사라집니다.";

        public override int AttackBonus
            => owner != null ? BonusFor(JobModifierQueries.CountOtherAlliesInMyZone(owner)) : 0;

        /// <summary>같은 구역 다른 아군 수 → 공격력 보너스 (순수 — 자가 테스트 대상).</summary>
        public static int BonusFor(int otherAllies) => otherAllies >= RequiredAllies ? Bonus : 0;
    }
}
