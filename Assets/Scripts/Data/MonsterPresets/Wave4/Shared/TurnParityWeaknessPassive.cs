using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.MonsterPresets.Wave4.Shared
{
    /// <summary>
    /// 리듬 약화 패시브 (양력/음력 공용).
    /// 매 턴 시작, 현재 턴의 홀짝이 지정과 맞으면 "받는 피해 +percent%"를 자신에게 durationTurns턴 부여한다.
    /// 태양 유닛 = triggerOnOddTurn(홀수턴에 약화), 달 유닛 = triggerOnOddTurn 해제(짝수턴에 약화).
    /// 받는 피해 증가는 기존 VulnerableStatus(받는 피해 +Value%, 중첩 불가)를 재사용한다.
    /// </summary>
    [System.Serializable]
    public class TurnParityWeaknessPassive : PassiveAbility
    {
        [Header("Rhythm Settings")]
        [Tooltip("체크 시 홀수 턴에 약화, 해제 시 짝수 턴에 약화")]
        [SerializeField] private bool triggerOnOddTurn = true;
        [Tooltip("약화 시 받는 피해 증가 퍼센트")]
        [SerializeField] private int percent = 30;
        [Tooltip("약화 지속 턴 (플레이에서 다음 플레이어 턴을 못 덮으면 2로 올릴 것)")]
        [SerializeField] private int durationTurns = 1;

        public TurnParityWeaknessPassive()
        {
            passiveName = "양력";
            description = "받는 피해 증가\n정해진 홀수 또는 짝수 턴에";
            priority = 10;
            isStackable = false;
        }

        public override string GetDynamicDescription()
            => $"{(triggerOnOddTurn ? "홀수" : "짝수")} 턴에 받는 피해 +{percent}% ({durationTurns}턴)";

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null) return;
            if (trigger != CombatTrigger.OnPreAction) return;
            if (context.Phase != EventPhase.TurnStart) return;
            if (context.SourceUnit != owner) return;

            var cm = CombatManager.Instance;
            if (cm == null) return;

            bool isOddTurn = (cm.TurnCount % 2) == 1;
            if (isOddTurn != triggerOnOddTurn) return;

            if (owner.StatusEffects == null) return;
            owner.StatusEffects.AddEffect(new VulnerableStatus(percent, durationTurns));
            Debug.Log($"[{PassiveName}] {owner.name} 받는 피해 +{percent}% ({durationTurns}턴) — turn {cm.TurnCount}");
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }
}
