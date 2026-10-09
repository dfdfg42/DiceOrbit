using DiceOrbit.Core;
using UnityEngine;

namespace DiceOrbit.Data.Modifiers.Common
{
    /// <summary>
    /// 주사위 사용 사건을 받는 모디파이어 (저속 방호 등).
    /// AutoAttackSystem이 이동+공격 처리 시작 시(공격 유무 무관) 캐릭터의 모디파이어에 통지한다.
    /// </summary>
    public interface IDiceUseListener
    {
        void OnDiceUsed(Character character, int diceValue);
    }

    /// <summary>
    /// 콤보 끊김(조건 불일치 → 기본공격 발생) 사건을 받는 모디파이어 (안전장치).
    /// 콤보가 1단계 이상 쌓인 상태에서 끊기고 기본공격이 실제로 나갈 때만 통지된다.
    /// </summary>
    public interface IComboBreakListener
    {
        void OnComboBrokenBasicAttack(Character character);
    }

    /// <summary>
    /// 공용 모디파이어 공통 골격 (2026-08-28 전면 교체).
    /// 모든 캐릭터가 장착 가능하고, 같은 종류를 최대 3개까지 중첩할 수 있다 — 효과는 중첩 수에 선형 비례.
    ///
    /// 중첩 집계: 인스턴스가 각자 효과를 내면 곱연산으로 어긋나므로, 목록의 '첫 인스턴스(primary)'만
    /// 전체 중첩 수만큼 한 번에 적용한다. 자식은 훅 진입부에서 IsPrimary()를 확인할 것.
    /// </summary>
    [System.Serializable]
    public abstract class StackableCommonModifier : CharacterModifier
    {
        public const int MaxStacks = 3;

        public override ModifierCategory Category => ModifierCategory.Generic;

        /// <summary>이 캐릭터에 장착된 같은 종류 모디파이어 수 (자신 포함).</summary>
        protected int StackCount()
        {
            var mods = owner?.Stats?.Modifiers?.Modifiers;
            if (mods == null) return 1;

            int count = 0;
            var type = GetType();
            for (int i = 0; i < mods.Count; i++)
                if (mods[i] != null && mods[i].GetType() == type) count++;
            return Mathf.Max(1, count);
        }

        /// <summary>같은 종류 중 목록의 첫 인스턴스인가 — 효과 적용은 primary만 한다 (이중 적용 방지).</summary>
        protected bool IsPrimary()
        {
            var mods = owner?.Stats?.Modifiers?.Modifiers;
            if (mods == null) return true;

            var type = GetType();
            for (int i = 0; i < mods.Count; i++)
            {
                var m = mods[i];
                if (m == null || m.GetType() != type) continue;
                return ReferenceEquals(m, this);
            }
            return true;
        }

        /// <summary>임시 방어도 부여 + 버블 (부여형 모디파이어 공용).</summary>
        protected void GrantArmor(int amount)
        {
            if (owner == null || owner.Stats == null || amount <= 0) return;
            owner.Stats.TempArmor += amount;
            UI.CombatNotifier.Notify(owner, $"{ModifierName} 방어도 +{amount}", new Color(0.65f, 0.8f, 1f));
        }
    }
}
