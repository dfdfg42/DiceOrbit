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

    /// <summary>
    /// [앙갚음(전사)] 직전 라운드(이전 플레이어 턴 시작 ~ 몬스터 턴 종료)에 체력을 잃었으면 이번 라운드 전사의 공격 행동 피해 +15%.
    /// - 손실 기록: 주인이 대상인 타격의 OnPostAction에서 IsEffected(체력이 실제로 줄었음)면 지금 라운드 번호를 남긴다.
    ///   원인 무관(중독 등 직접 체력 손실 포함 — 파이프라인은 직접 손실도 OnPostAction을 통지한다), 방어도만 깎인 경우 제외.
    /// - 라운드 번호 = CombatManager.TurnCount (StartPlayerTurn이 캐릭터 턴 시작 처리보다 먼저 올린다). 손실 라운드 = 지금 − 1이면 활성.
    /// - 보정: 행동 스코프 주체가 주인인 공격 행동(기본/강화)만 — 유물·디버프 피해 제외. 전투 시작 판정은 없다(CombatStart에서 기록 삭제).
    /// - 공용 [앙갚음](충전)과 다른 종류라 함께 장착할 수 있어 표시 이름에 "(전사)"를 붙인다.
    /// </summary>
    [System.Serializable]
    public class WarriorPaybackModifier : JobModifier
    {
        public const float Percent = 15f;

        [System.NonSerialized] private bool _hasLoss;
        [System.NonSerialized] private int _lossRound;

        protected override string JobId => JobIds.Warrior;
        public override string ModifierName => "앙갚음(전사)";
        public override ModifierFamily Family => ModifierFamily.Survival;
        public override string Description
            => $"직전 라운드에 체력을 잃었다면 이번 라운드 동안 공격 피해가 {Percent:0.#}% 증가합니다. 방어도만 깎인 경우는 세지 않습니다.";

        /// <summary>round 라운드에 체력을 잃었다고 기록한다.</summary>
        public void RecordLoss(int round)
        {
            _hasLoss = true;
            _lossRound = round;
        }

        /// <summary>round 라운드에 보너스가 켜져 있나 — 직전 라운드에 손실 기록이 있으면 (순수 — 자가 테스트 대상).</summary>
        public bool IsActiveInRound(int round) => _hasLoss && _lossRound == round - 1;

        /// <summary>기록 삭제 (전투 시작).</summary>
        public void ResetRounds()
        {
            _hasLoss = false;
            _lossRound = 0;
        }

        private static int CurrentRound => CombatManager.Instance != null ? CombatManager.Instance.TurnCount : 0;

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null) return;
            if (context.Phase != EventPhase.CombatStart) return;
            if (trigger != CombatTrigger.OnPreAction) return;   // 방송 1회당 1번만
            if (context.SourceUnit != owner || context.IsSimulation) return;

            ResetRounds();
        }

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null) return;

            if (trigger == CombatTrigger.OnPostAction)
            {
                if (context.Target == owner && context.IsEffected && !context.IsSimulation)
                    RecordLoss(CurrentRound);
                return;
            }

            if (!IsOwnAttackCalculation(trigger, context)) return;
            if (!AttackActionScope.IsActive || AttackActionScope.CurrentSource != owner) return;
            if (!IsActiveInRound(CurrentRound)) return;

            context.OutputValue *= 1f + Percent / 100f;
        }
    }

    /// <summary>
    /// [대지의 룬] 3단계 [대지 가르기] 피해 +15%, 아군 일시 방어도 5 → 8.
    /// 피해는 OnAttack(3단계 조건, 스펙 §3.3), 방어도는 WarriorGreatswordActive가 조회해 +3 (D3).
    /// </summary>
    [System.Serializable]
    public class EarthRuneModifier : JobModifier
    {
        public const float Percent = 15f;
        public const int ExtraAllyArmor = 3;

        protected override string JobId => JobIds.Warrior;
        public override string ModifierName => "대지의 룬";
        public override ModifierFamily Family => ModifierFamily.Combo;
        public override string Description
            => $"3단계 강화 공격 [대지 가르기]의 피해가 {Percent:0.#}% 증가하고, 아군에게 주는 일시 방어도가 {ExtraAllyArmor} 늘어납니다.";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (!IsOwnStage3Calculation(trigger, context)) return;
            context.OutputValue *= 1f + Percent / 100f;
        }
    }
}
