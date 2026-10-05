using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;
using DiceOrbit.Data.Monsters;

namespace DiceOrbit.Data.MonsterPresets.Wave0.Shared
{
    /// <summary>[박치기] 무작위 대상 1명이 속한 타일 + 좌우 각각 2칸에 damage 피해.
    /// (RandomCharacter + Tiles + range 2로 배선) 파란·초록 슬라임 공용.</summary>
    [System.Serializable]
    public class SlimeBodySlamSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 15;

        public SlimeBodySlamSkill()
        {
            skillName = "박치기";
            description = "{피해} 피해 · 대상 좌우 2칸";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    /// <summary>[점액] 무작위 타일 1개(점액 없는 곳)에 점액 타일 설치. 대상 타게팅 없음 — Execute가 직접 배치.</summary>
    [System.Serializable]
    public class SlimePlantSkill : SkillData
    {
        [Header("Skill Settings")]
        [Tooltip("점액 타일: 밟으면 다음 턴 이동 감소량")]
        [SerializeField] private int slowAmount = 1;
        [SerializeField] private int slowDuration = 2;

        public SlimePlantSkill()
        {
            skillName = "점액";
            description = "점액 1칸 설치\n밟으면 이동 감소";
        }

        public override int GetPreviewDamage() => 0;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            SlimeSet.EnsureWaveHook();
            var orbit = GameManager.Instance?.GetOrbitManager();
            if (orbit?.Tiles == null) return;
            var candidates = orbit.Tiles.Where(t => t != null && !t.HasAttribute(TileAttributeType.Slime)).ToList();
            if (candidates.Count == 0) return;
            candidates[Random.Range(0, candidates.Count)].AddAttribute(new SlimeTile(slowAmount, slowDuration));
        }
    }

    /// <summary>초록 슬라임 조건부 AI: 파란 슬라임 생존 시 availableSkills[0]([점액])만,
    /// 파란 슬라임 사망 시 [0]([점액])·[1]([점액 분사]) 중 50%씩 무작위.</summary>
    [System.Serializable]
    public class SlimeGreenPattern : DiceOrbit.Data.MonsterAI.MonsterAI
    {
        public override MonsterSkill GetNextSkill()
        {
            if (availableSkills == null || availableSkills.Count == 0) return null;
            int idx = SlimeSet.IsBlueSlimeAlive()
                ? 0
                : (Random.value < 0.5f ? 0 : 1);
            if (idx >= availableSkills.Count) idx = 0;
            return availableSkills[idx];
        }
    }
}
