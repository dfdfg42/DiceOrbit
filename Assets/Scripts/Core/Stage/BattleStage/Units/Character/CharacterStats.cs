using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Data.Skills;
using DiceOrbit.Data.Passives;

namespace DiceOrbit.Data
{
    [System.Serializable]
    public class CharacterStats : UnitStats
    {
        [Header("Basic Info")]
        public string CharacterName = "Hero";

        [Header("점감 부활 (스펙 §4)")]
        [Tooltip("남은 부활 횟수. 부활 HP = 스톡 x 25% (3→75%, 2→50%, 1→25%). 0에서 사망 = 영구사망 = 게임오버")]
        public int RevivalStock = 3;

        [Header("Skills")]
        public List<ActiveSkillSlot>   ActiveAbilities  = new List<ActiveSkillSlot>();
        public List<CharacterPassiveSkill> PassiveInstances = new List<CharacterPassiveSkill>();

        [Header("Reference")]
        public Core.CharacterPreset SourcePreset;

        [Header("Combat Stats")]
        public int MoveBuff    = 0;
        public int MoveDebuff  = 0;
        public int MoveOnThisTurn = 0;
        public int BindDebuff  = 0;
        public int StunDebuff  = 0;

        [HideInInspector]
        public Data.Modifiers.IModifierManager Modifiers;

        public int ActiveAbilityCount => ActiveAbilities.Count;

        /// <summary>
        /// 유효 공격력 = 기본 공격력(Attack) + 장착 모디파이어의 AttackBonus 합 (D6, 2026-10-09 B).
        /// 기본공격·콤보 단계 피해·행동 예고·정보 패널은 이 값을 읽는다. Attack 필드 자체는 바꾸지 않는다.
        /// </summary>
        public int EffectiveAttack
        {
            get
            {
                int bonus = 0;
                var mods = Modifiers != null ? Modifiers.Modifiers : null;
                if (mods != null)
                    for (int i = 0; i < mods.Count; i++)
                        if (mods[i] != null) bonus += mods[i].AttackBonus;
                return Attack + bonus;
            }
        }

        public ActiveSkillSlot GetActiveAbilityByIndex(int index)
        {
            if (index < 0 || index >= ActiveAbilities.Count) return null;
            return ActiveAbilities[index];
        }

        // (캐릭터 레벨/HP 성장 커브는 철거됨 — 성장은 전부 모디파이어로, 기획 REV05)

        public bool canMove() => BindDebuff == 0;
        public bool canAct()  => StunDebuff == 0;
    }
}
