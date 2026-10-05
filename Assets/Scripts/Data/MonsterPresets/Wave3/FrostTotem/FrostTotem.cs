using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using DiceOrbit.Data.Tile;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Data.MonsterPresets.Wave3.SnowMan;

namespace DiceOrbit.Systems.Effects
{
    /// <summary>취약 — 부착된 유닛이 받는 피해량을 Value% 증가시킨다. 중첩 불가. (구 FrostbiteDebuff, Wave4 공유)</summary>
    public class VulnerableStatus : StatusEffect
    {
        public VulnerableStatus(int percent, int duration) : base(EffectType.Vulnerable, percent, duration)
        {
            IsStackable = false;
        }

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (Owner == null) return;
            if (trigger == CombatTrigger.OnCalculateOutput && context.Target == Owner)
                context.OutputValue *= 1f + (Value / 100f);
        }
    }
}

namespace DiceOrbit.Data.MonsterPresets.Wave3.FrostTotem
{
    /// <summary>[서리 꽃] 무작위 대상 1명이 속한 타일 + 좌우 각각 2칸에 피해. (RandomCharacter + Tiles + range 2)</summary>
    [System.Serializable]
    public class FrostFlower : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 20;

        public FrostFlower()
        {
            skillName = "서리 꽃";
            description = "{피해} 피해 · 대상 좌우 2칸";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    /// <summary>[빙결] 무작위 tileCount 타일에 빙결 타일 설치. 대상 없음 — Execute가 직접 배치.</summary>
    [System.Serializable]
    public class FrostPlantSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int tileCount = 4;
        [SerializeField] private int frostStacks = 5;

        public FrostPlantSkill()
        {
            skillName = "빙결";
            description = "4칸 얼림\n그 위에서 턴을 마치면 빙결";
        }

        public override int GetPreviewDamage() => 0;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            SnowSet.EnsureWaveHook();
            var orbit = GameManager.Instance?.GetOrbitManager();
            if (orbit?.Tiles == null) return;
            var candidates = orbit.Tiles.Where(t => t != null && !t.HasAttribute(TileAttributeType.Frost)).ToList();
            int place = Mathf.Min(tileCount, candidates.Count);
            for (int i = 0; i < place; i++)
            {
                int r = Random.Range(0, candidates.Count);
                candidates[r].AddAttribute(new FrostTile(frostStacks));
                candidates.RemoveAt(r);
            }
        }
    }

    /// <summary>서리토템 조건부 AI: 빙결[0] ONLY, 최초 아군 사망 시 50/50[빙결, 서리꽃].</summary>
    [System.Serializable]
    public class FrostTotemPattern : DiceOrbit.Data.MonsterAI.MonsterAI
    {
        public override MonsterSkill GetNextSkill()
        {
            if (availableSkills == null || availableSkills.Count == 0) return null;
            if (!SnowSet.HasAnyAllyDied()) return availableSkills[0];
            int idx = availableSkills.Count >= 2 ? Random.Range(0, 2) : 0;
            return availableSkills[idx];
        }
    }
}
