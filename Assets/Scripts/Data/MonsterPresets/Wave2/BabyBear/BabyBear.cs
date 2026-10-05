using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Data.MonsterPresets.Wave2.BabyBear
{
    /// <summary>[꿀 묻히기] 무작위 타일(preset RandomTiles + count)에 꿀 타일 설치. 통과 시 회복 + 아기곰 회복, 한 턴 2개 이상 밟으면 혈당 스파이크(이동 불가).</summary>
    [System.Serializable]
    public class HoneyPawSkill : SkillData
    {
        [Header("Skill Settings")]
        [Tooltip("꿀 타일 통과 시 회복량")]
        [SerializeField] private int healOnStep = 2;
        [Tooltip("이 개수 이상 밟으면 혈당 스파이크(이동 불가)")]
        [SerializeField] private int bindThreshold = 2;
        [Tooltip("혈당 스파이크(이동 불가) 지속 턴")]
        [SerializeField] private int bindDuration = 2;
        [Tooltip("꿀 타일 발동 시 아기 곰·엄마 곰에게 줄 일시 방어도")]
        [SerializeField] private int bearArmor = 2;

        public HoneyPawSkill()
        {
            skillName = "꿀 묻히기";
            description = "꿀 5칸 설치\n밟으면 회복하지만 이동 감소";
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            if (targetTiles == null) return;
            foreach (var tile in targetTiles)
            {
                if (tile == null || tile.HasAttribute(TileAttributeType.Honey)) continue;
                tile.AddAttribute(new HoneyPawTile(healOnStep, bindThreshold, bindDuration, bearArmor));
            }
        }
    }

    /// <summary>[돌진] 무작위 대상 1명이 속한 타일 + 좌우 각각 2칸에 피해. (RandomCharacter + Tiles + range 2)</summary>
    [System.Serializable]
    public class BabyBearCharge : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 15;

        public BabyBearCharge()
        {
            skillName = "돌진";
            description = "{피해} 피해 · 대상 좌우 2칸";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    /// <summary>
    /// [아기 곰은 꿀을 좋아해] 꿀 타일 발동 시마다 아기 곰·엄마 곰에게 일시 방어도 부여
    /// (HoneyPawTile이 BearPackTracker.GrantTempArmorToBears로 처리).
    /// 이 패시브는 아기곰 피격 시 공격자를 기록(엄마곰 보호 본능용) + 웨이브 훅 보장.
    /// </summary>
    [System.Serializable]
    public class HoneyLoverPassive : PassiveAbility
    {
        public HoneyLoverPassive()
        {
            passiveName = "아기 곰은 꿀을 좋아해";
            description = "아기 곰·엄마 곰 방어도 +2\n꿀 타일이 발동할 때마다";
            priority = 10;
            isStackable = false;
        }

        public override void Initialize(Unit Owner)
        {
            base.Initialize(Owner);
            BearPackTracker.EnsureWaveHook();
        }

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null) return;
            // 아기곰 피격 → 최근 공격자 기록 (엄마곰 보호 본능/조건부 AI용)
            if (trigger == CombatTrigger.OnHit && context.Target == owner
                && !context.IsSimulation && context.SourceUnit is Character attacker)
                BearPackTracker.SetLastBabyAttacker(attacker);
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }
}
