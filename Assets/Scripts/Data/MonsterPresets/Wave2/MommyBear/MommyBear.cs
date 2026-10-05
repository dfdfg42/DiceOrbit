using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;
using DiceOrbit.Data.Monsters;

namespace DiceOrbit.Data.MonsterPresets.Wave2.MommyBear
{
    /// <summary>
    /// [보호 본능] 가장 최근에 아기 곰을 공격한 적의 타일 + 좌우 각각 range칸에 광역 피해.
    /// 반응형: 사용 시 LastBabyAttacker를 소비(다음엔 [찢어]).
    /// </summary>
    [System.Serializable]
    public class ProtectiveInstinctSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 15;
        [Tooltip("공격자 타일 기준 좌우 각각 칸 수")]
        [SerializeField] private int range = 3;

        public ProtectiveInstinctSkill()
        {
            skillName = "보호 본능";
            description = "{피해} 피해 · 대상 좌우 3칸\n아기 곰을 때린 캐릭터를 노림";
        }

        public override int GetPreviewDamage() => damage;

        public override List<TileData> GetCustomTiles(MonsterSkill skill, Monster owner)
        {
            var attacker = BearPackTracker.LastBabyAttacker;
            if (attacker == null || !attacker.IsAlive || attacker.CurrentTile == null)
                return new List<TileData>();
            return BearHelper.ExpandAround(attacker.CurrentTile, range);
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            BearPackTracker.SetLastBabyAttacker(null);   // 반응형: 사용 시 소비 (다음엔 [찢어])
            AttackTiles(source, targetTiles, damage);
        }
    }

    /// <summary>[곰은 사람을 찢어] 무작위 대상 1명 기준 진행방향(Next) forwardTiles칸에 피해 (대상 타일 제외).
    /// FollowsTarget=true: 대상이 움직이면 공격 범위도 따라 이동, 실행 시점의 대상 위치로 착탄.</summary>
    [System.Serializable]
    public class MommyBearTear : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 15;
        [Tooltip("대상 타일 기준 진행방향으로 공격할 타일 수 (대상 타일 제외)")]
        [SerializeField] private int forwardTiles = 4;

        public MommyBearTear()
        {
            skillName = "곰은 사람을 찢어";
            description = "{피해} 피해 · 대상 앞 4칸\n대상을 따라 움직임";
        }

        public override int GetPreviewDamage() => damage;

        public override bool FollowsTarget => true;

        public override List<TileData> GetFollowTiles(Character target)
        {
            var tiles = new List<TileData>();
            if (target == null || target.CurrentTile == null) return tiles;
            var t = target.CurrentTile;
            for (int i = 0; i < forwardTiles && t?.NextTile != null; i++)
            {
                t = t.NextTile;
                tiles.Add(t);
            }
            return tiles;
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    /// <summary>엄마곰 조건부 AI: [곰은 사람을 찢어][1] ONLY, 단 아기곰이 최근 공격받았으면(LastBabyAttacker 존재) [보호 본능][0].</summary>
    [System.Serializable]
    public class MommyBearPattern : DiceOrbit.Data.MonsterAI.MonsterAI
    {
        public override MonsterSkill GetNextSkill()
        {
            if (availableSkills == null || availableSkills.Count == 0) return null;
            var attacker = BearPackTracker.LastBabyAttacker;
            int idx = (attacker != null && attacker.IsAlive) ? 0 : 1;
            if (idx >= availableSkills.Count) idx = 0;
            return availableSkills[idx];
        }
    }

    /// <summary>공용 헬퍼</summary>
    public static class BearHelper
    {
        public static Monster FindMonsterByName(string name)
        {
            var monsters = CombatManager.Instance?.ActiveMonsters;
            if (monsters == null) return null;
            foreach (var m in monsters)
                if (m != null && m.IsAlive && m.Stats != null && m.Stats.MonsterName == name)
                    return m;
            return null;
        }

        /// <summary>center 타일 + 진행방향(Next)·역방향(Prev) 각각 range칸을 포함한 타일 목록 (center 포함, 궤도 연결 따라 감).</summary>
        public static List<TileData> ExpandAround(TileData center, int range)
        {
            if (center == null) return new List<TileData>();
            var set = new HashSet<TileData> { center };
            var t = center;
            for (int i = 0; i < range && t?.NextTile != null; i++) { t = t.NextTile; set.Add(t); }
            t = center;
            for (int i = 0; i < range && t?.PreviousTile != null; i++) { t = t.PreviousTile; set.Add(t); }
            return new List<TileData>(set);
        }
    }
}
