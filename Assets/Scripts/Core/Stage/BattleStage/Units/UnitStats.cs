using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using UnityEngine;
using static UnityEngine.UI.GridLayoutGroup;

namespace DiceOrbit.Data
{
    [System.Serializable]
    
    public class UnitStats : ICombatReactor
    {
        [Header("Combat Stats")]
        public Core.Unit Owner; // 스탯 소유자 (Unit)
        public int MaxHP = 50;
        public int CurrentHP = 50;
        public int Attack = 0;
        // Legacy field: fixed defense is not used in current combat rule.
        public int Defense = 0;
        public int TempArmor = 0; // 임시 방어도 (턴마다 초기화)
        public float DodgeChance = 0f; // 회피율 (0~100, %)
        public bool Invulnerable = false; // 튜토리얼 등: HP가 1 미만으로 안 내려가고 사망하지 않음

        // ICombatReactor implementation
        public virtual int Priority => 20;
        /// <summary>
        /// 턴 시작 시 일시 방어도를 0으로 (OnPreAction — 우선순위 20이라 모디파이어·상태(10)의 턴 시작 부여보다 먼저).
        /// - 몬스터: 몬스터 턴 시작 사건에서 초기화 (기존 동작 그대로 — MonsterStats에는 Owner를 주입하지 않는다).
        /// - 캐릭터: 자기 턴 시작에만 (D2, 2026-10-09 B). TurnEvent는 파티 전원의 Stats에 방송되므로
        ///   SourceUnit == Owner로 소유자를 확인한다. Owner는 Character.InitializeStats가 주입한다.
        /// </summary>
        public virtual void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (context.Phase != EventPhase.TurnStart || trigger != CombatTrigger.OnPreAction) return;

            if (this is MonsterStats)
            {
                if (context.SourceUnit is Monster) TempArmor = 0;
                return;
            }

            if (this is CharacterStats && Owner != null && context.SourceUnit == Owner)
                TempArmor = 0;
        }

        /// <summary>
        /// HP 증가
        /// </summary>
        public void Heal(int amount)
        {
            CurrentHP = Mathf.Min(CurrentHP + amount, MaxHP);
        }

        /// <summary>
        /// 피해 1회가 방어도·체력에 어떻게 들어가는지 (순수 — TakeDamage와 행동 예고가 공유).
        /// Slay-the-Spire 방식: 고정 방어력은 쓰지 않고, 방어도(TempArmor)가 먼저 흡수한 뒤 남은 값만 체력에 들어간다.
        /// 무적이면 체력이 1 밑으로 내려가지 않게 자른다. hpDamage는 초과분(오버킬)을 포함한 값이다.
        /// </summary>
        public static void ResolveDamage(int damage, int armor, int hp, bool invulnerable, out int armorAbsorbed, out int hpDamage)
        {
            int remaining = Mathf.Max(0, damage);

            armorAbsorbed = armor > 0 ? Mathf.Min(remaining, armor) : 0;
            remaining -= armorAbsorbed;

            if (invulnerable && remaining > 0)
                remaining = Mathf.Min(remaining, Mathf.Max(0, hp - 1));

            hpDamage = remaining;
        }

        /// <summary>
        /// 데미지 받기, 받은 데미지만큼 리턴
        /// </summary>
        public int TakeDamage(int damage)
        {
            ResolveDamage(damage, TempArmor, CurrentHP, Invulnerable, out int absorbed, out int actualDamage);

            if (absorbed > 0)
            {
                TempArmor -= absorbed;
                Debug.Log($"TempArmor absorbed {absorbed} dmg");
            }

            CurrentHP = Mathf.Max(0, CurrentHP - actualDamage);
            Debug.Log($" took {actualDamage} damage! (HP: {CurrentHP}/{MaxHP})");
            return actualDamage;
        }

        /// <summary>
        /// 직접 체력 손실 (중독 등). TempArmor를 소모하지 않고 HP만 깎는다. Invulnerable은 존중.
        /// </summary>
        public int TakeDirectDamage(int damage)
        {
            int remainingDamage = Mathf.Max(0, damage);

            if (Invulnerable && remainingDamage > 0)
                remainingDamage = Mathf.Min(remainingDamage, Mathf.Max(0, CurrentHP - 1));

            CurrentHP = Mathf.Max(0, CurrentHP - remainingDamage);
            Debug.Log($" took {remainingDamage} direct damage (armor bypassed)! (HP: {CurrentHP}/{MaxHP})");
            return remainingDamage;
        }

        /// <summary>
        /// 생존 확인
        /// </summary>
        public bool IsAlive => CurrentHP > 0;

        /// <summary>
        /// HP 비율
        /// </summary>
        public float HPRatio => MaxHP > 0 ? (float)CurrentHP / MaxHP : 0f;

        /// <summary>
        /// 기본 스탯 복사 (자식 클래스에서 사용)
        /// </summary>
        protected void CopyBaseTo(UnitStats target)
        {
            target.MaxHP = this.MaxHP;
            target.CurrentHP = this.CurrentHP;
            target.Attack = this.Attack;
            target.Defense = this.Defense;
        }
    }
}

