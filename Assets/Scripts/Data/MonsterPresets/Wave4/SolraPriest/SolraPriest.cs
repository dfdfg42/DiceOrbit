using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;
using System.Collections.Generic;

namespace DiceOrbit.Data.MonsterPresets.Wave4.SolraPriest
{
    /// <summary>
    /// 일식 — 무작위 대상 1명이 속한 타일 + 좌우 2칸에 피해.
    /// 타깃/범위는 MonsterSkill 설정(RandomCharacter + Tiles + count 1 + range 2)이 담당.
    /// (흑점 = 공용 FactionDebuffSkill{Vulnerable}, 양력 = 공용 TurnParityWeaknessPassive — 프리셋에서 배선.)
    /// </summary>
    [System.Serializable]
    public class SolraPriestSkill1 : SkillData
    {
        [Header("Skill Settings")]
        [Tooltip("스킬 사용 시 입힐 피해량")]
        [SerializeField] private int damage = 30;

        public SolraPriestSkill1()
        {
            skillName = "일식";
            description = "{피해} 피해 · 대상 좌우 2칸";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }
}
