using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Data.MonsterPresets.Wave3.SnowMan;

namespace DiceOrbit.Data.MonsterPresets.Wave3.SnowGolem
{
    /// <summary>[눈 방패] 이전 턴에 피격 받은(=가장 많이 다친) 아군 몬스터에게 일시 방어도 armor 부여.
    /// 다친 아군이 없으면 무작위 아군. (자신 제외)</summary>
    [System.Serializable]
    public class SnowShieldSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int armor = 10;

        public SnowShieldSkill()
        {
            skillName = "눈 방패";
            description = "아군에게 방어도\n지난 턴에 맞은 몬스터";
        }

        public override int GetPreviewDamage() => 0;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            var others = SnowSet.OtherAliveMonsters(source as Monster).ToList();
            if (others.Count == 0) return;

            // 이전 턴에 피격 받은 아군 ≈ 현재 HP가 최대치보다 낮은(피해 입은) 아군 중 가장 많이 다친 쪽. 없으면 무작위.
            var hurt = others.Where(m => m.Stats != null && m.Stats.CurrentHP < m.Stats.MaxHP)
                             .OrderByDescending(m => m.Stats.MaxHP - m.Stats.CurrentHP)
                             .ToList();
            var target = hurt.Count > 0 ? hurt[0] : others[Random.Range(0, others.Count)];
            if (target != null && target.Stats != null) target.Stats.TempArmor += armor;
        }
    }

    /// <summary>[눈 주먹] 무작위 대상 1명 기준 진행방향(Next) forwardTiles칸(대상 타일 제외)에 피해.
    /// FollowsTarget=true: 대상이 움직이면 공격 범위도 따라 이동.</summary>
    [System.Serializable]
    public class SnowFistSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 15;
        [Tooltip("대상 타일 기준 진행방향으로 공격할 타일 수 (대상 타일 제외)")]
        [SerializeField] private int forwardTiles = 5;

        public SnowFistSkill()
        {
            skillName = "눈 주먹";
            description = "{피해} 피해 · 대상 앞 5칸\n대상을 따라 움직임";
        }

        public override int GetPreviewDamage() => damage;

        public override bool FollowsTarget => true;

        public override List<TileData> GetFollowTiles(Character target)
        {
            var tiles = new List<TileData>();
            if (target == null || target.CurrentTile == null) return tiles;
            var t = target.CurrentTile;
            for (int i = 0; i < forwardTiles && t?.NextTile != null; i++) { t = t.NextTile; tiles.Add(t); }
            return tiles;
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    /// <summary>눈골렘 조건부 AI: 눈방패[0] ONLY, 최초 아군 사망 시 눈주먹[1] ONLY.</summary>
    [System.Serializable]
    public class SnowGolemPattern : DiceOrbit.Data.MonsterAI.MonsterAI
    {
        public override MonsterSkill GetNextSkill()
        {
            if (availableSkills == null || availableSkills.Count == 0) return null;
            int idx = SnowSet.HasAnyAllyDied() ? 1 : 0;
            if (idx >= availableSkills.Count) idx = 0;
            return availableSkills[idx];
        }
    }
}
