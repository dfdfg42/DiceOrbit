using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Data.Monsters;

namespace DiceOrbit.Data.MonsterPresets.Wave4.LunaKnight
{
    /// <summary>
    /// 초승달 — 무작위 대상 1명이 속한 타일 + 좌우 2칸에 피해.
    /// 타깃/범위는 MonsterSkill 설정(RandomCharacter + Tiles + count 1 + range 2)이 담당.
    /// </summary>
    [System.Serializable]
    public class LunaKnightSkill1 : SkillData
    {
        [Header("Skill Settings")]
        [Tooltip("스킬 사용 시 입힐 피해량")]
        [SerializeField] private int damage = 30;

        public LunaKnightSkill1()
        {
            skillName = "초승달";
            description = "{피해} 피해 · 대상 좌우 2칸";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    /// <summary>
    /// 월광 — 모든 짝수 타일에 피해. (Custom 타깃팅: GetCustomTiles가 짝수 타일 전체 반환)
    /// </summary>
    [System.Serializable]
    public class LunaKnightSkill2 : SkillData
    {
        [Header("Skill Settings")]
        [Tooltip("스킬 사용 시 입힐 피해량")]
        [SerializeField] private int damage = 30;

        public LunaKnightSkill2()
        {
            skillName = "월광";
            description = "{피해} 피해 · 짝수 칸 전부";
        }

        public override int GetPreviewDamage() => damage;

        public override List<TileData> GetCustomTiles(MonsterSkill skill, Monster owner)
        {
            return GameManager.Instance.GetOrbitManager().Tiles
                .Where(tile => tile.TileIndex % 2 == 0)
                .ToList();
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }
}
