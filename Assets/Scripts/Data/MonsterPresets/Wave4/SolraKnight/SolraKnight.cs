using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Data.Monsters;

namespace DiceOrbit.Data.MonsterPresets.Wave4.SolraKnight
{
    /// <summary>
    /// 천공검 — 무작위 대상 2명이 속한 타일 + 좌우 1칸에 피해.
    /// 타깃/범위는 MonsterSkill 설정(RandomCharacter + Tiles + count 2 + range 1)이 담당.
    /// </summary>
    [System.Serializable]
    public class SolraKnightSkill1 : SkillData
    {
        [Header("Skill Settings")]
        [Tooltip("스킬 사용 시 입힐 피해량")]
        [SerializeField] private int damage = 30;

        public SolraKnightSkill1()
        {
            skillName = "천공검";
            description = "{피해} 피해 · 2명의 좌우 1칸";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    /// <summary>
    /// 플레어 — 모든 홀수 타일에 피해. (Custom 타깃팅: GetCustomTiles가 홀수 타일 전체 반환)
    /// </summary>
    [System.Serializable]
    public class SolraKnightSkill2 : SkillData
    {
        [Header("Skill Settings")]
        [Tooltip("스킬 사용 시 입힐 피해량")]
        [SerializeField] private int damage = 30;

        public SolraKnightSkill2()
        {
            skillName = "플레어";
            description = "{피해} 피해 · 홀수 칸 전부";
        }

        public override int GetPreviewDamage() => damage;

        public override List<TileData> GetCustomTiles(MonsterSkill skill, Monster owner)
        {
            return GameManager.Instance.GetOrbitManager().Tiles
                .Where(tile => tile.TileIndex % 2 == 1)
                .ToList();
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }
}
