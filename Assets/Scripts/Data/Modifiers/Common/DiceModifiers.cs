using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Systems.Effects;
using UnityEngine;

namespace DiceOrbit.Data.Modifiers.Common
{
    /// <summary>[저속 방호] 눈 3 이하 주사위를 사용하면 방어도 4. 공격 성사 여부와 무관.</summary>
    [System.Serializable]
    public class LowRollGuardModifier : CommonModifier, IDiceUseListener
    {
        private const int Armor = 4;

        public override string ModifierName => "저속 방호";
        public override ModifierFamily Family => ModifierFamily.Dice;
        public override string Description
            => $"주사위 눈이 3 이하면 방어도를 {Armor}만큼 얻습니다.";

        public void OnDiceUsed(Character character, int diceValue)
        {
            if (character != owner || diceValue > 3) return;
            GrantArmor(Armor);
        }
    }

    /// <summary>
    /// [앙갚음] 적의 공격으로 체력을 잃으면 충전 1. 방어도만 깎이면(체력 피해 0) 발동하지 않는다(IsEffected 기준).
    /// 적의 다단 공격은 공격 행동 1회당 최대 1번 — 행동 ID로 중복을 막는다.
    /// 스코프 밖에서 도착한 지연 타격은 (같은 몬스터, 같은 프레임)을 한 행동으로 본다.
    /// </summary>
    [System.Serializable]
    public class PaybackModifier : CommonModifier
    {
        [System.NonSerialized] private int _lastChargedActionId;
        [System.NonSerialized] private Unit _lastOutOfScopeSource;
        [System.NonSerialized] private int _lastOutOfScopeFrame = -1;

        public override string ModifierName => "앙갚음";
        public override ModifierFamily Family => ModifierFamily.Dice;
        public override string Description
            => "적의 공격으로 체력을 잃으면 충전을 1 얻습니다. 적의 공격 행동 1회당 한 번만 발동합니다.";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null) return;
            if (trigger != CombatTrigger.OnPostAction) return;
            if (context.Target != owner || !(context.SourceUnit is Monster)) return;
            if (context.IsDirectHpLoss || context.IsSimulation || !context.IsEffected) return;
            if (!TryMarkAction(context.SourceUnit)) return;

            GrantCharge(1);
        }

        /// <summary>이 적 공격 행동에서 아직 충전하지 않았으면 기록하고 true.</summary>
        private bool TryMarkAction(Unit source)
        {
            if (AttackActionScope.IsActive)
            {
                int id = AttackActionScope.CurrentActionId;
                if (id == _lastChargedActionId) return false;
                _lastChargedActionId = id;
                return true;
            }

            int frame = Time.frameCount;
            if (ReferenceEquals(source, _lastOutOfScopeSource) && frame == _lastOutOfScopeFrame) return false;
            _lastOutOfScopeSource = source;
            _lastOutOfScopeFrame = frame;
            return true;
        }
    }
}
