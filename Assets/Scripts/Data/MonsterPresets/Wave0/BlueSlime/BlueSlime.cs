using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Data.MonsterPresets.Wave0.BlueSlime
{
    /// <summary>[점액 분사] 설치된 점액 타일 전부 + 무작위 타일 randomTileCount개에 damage 피해.
    /// (Custom 타깃팅: GetCustomTiles가 점액 타일 ∪ 무작위 타일을 인텐트로 반환) 파란·초록 슬라임 공용.</summary>
    [System.Serializable]
    public class SlimeSpraySkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 15;
        [Tooltip("설치된 점액 타일 외에 추가로 무작위 선택할 타일 수")]
        [SerializeField] private int randomTileCount = 6;

        public SlimeSpraySkill()
        {
            skillName = "점액 분사";
            description = "{피해} 피해 · 점액과 무작위 6칸";
        }

        public override int GetPreviewDamage() => damage;

        public override List<TileData> GetCustomTiles(MonsterSkill skill, Core.Monster owner)
        {
            var orbit = GameManager.Instance?.GetOrbitManager();
            if (orbit?.Tiles == null) return new List<TileData>();

            // 설치된 점액 타일 전부
            var result = new HashSet<TileData>(
                orbit.Tiles.Where(t => t != null && t.HasAttribute(TileAttributeType.Slime)));

            // 점액이 없는 타일 중에서 무작위 randomTileCount개 추가
            var pool = orbit.Tiles.Where(t => t != null && !result.Contains(t)).ToList();
            for (int i = 0; i < randomTileCount && pool.Count > 0; i++)
            {
                int idx = Random.Range(0, pool.Count);
                result.Add(pool[idx]);
                pool.RemoveAt(idx);
            }

            return result.ToList();
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }
}
