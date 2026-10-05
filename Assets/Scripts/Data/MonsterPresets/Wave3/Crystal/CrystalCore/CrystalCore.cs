using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Systems.Effects;
using DiceOrbit.Data.MonsterPresets.Wave3.Crystal.Shared;

namespace DiceOrbit.Data.MonsterPresets.Wave3.Crystal.CrystalCore
{
    /// <summary>[자수정] 웨이브 시작(수정 핵 첫 턴) 시 무작위 tileCount 타일에 자수정 설치. 1회만.</summary>
    [System.Serializable]
    public class SummonAmethystPassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [SerializeField] private int tileCount = 4;
        private bool placed;

        public SummonAmethystPassive()
        {
            passiveName = "자수정";
            description = "전투 시작 시 자수정 4칸 설치\n밟으면 수정 핵의 수정 중첩 +1";
            priority = 10; isStackable = false;
        }

        public override void Initialize(Unit Owner)
        {
            base.Initialize(Owner);
            CrystalSet.EnsureWaveHook();
        }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null || placed) return;
            if (trigger != CombatTrigger.OnPreAction || context.Phase != EventPhase.TurnStart || context.SourceUnit != owner) return;

            PlantAmethyst();
            placed = true;
        }

        private void PlantAmethyst()
        {
            var orbit = GameManager.Instance?.GetOrbitManager();
            if (orbit?.Tiles == null) return;
            var candidates = orbit.Tiles.Where(t => t != null && !t.HasAttribute(TileAttributeType.Amethyst)).ToList();
            int place = Mathf.Min(tileCount, candidates.Count);
            for (int i = 0; i < place; i++)
            {
                int r = Random.Range(0, candidates.Count);
                candidates[r].AddAttribute(new AmethystTile());
                candidates.RemoveAt(r);
            }
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }

    /// <summary>[수정 폭발] 무작위 자수정 타일 2개 + 좌우 각각 ±1칸에 damage 피해.
    /// (TilesWithAttribute=Amethyst + range 1 + count 2로 배선)</summary>
    [System.Serializable]
    public class CrystalBurstSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 20;

        public CrystalBurstSkill()
        {
            skillName = "수정 폭발";
            description = "{피해} 피해 · 자수정 2곳 좌우 1칸";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    /// <summary>[수정 폭풍] 자수정 타일을 제외한 모든 타일의 적에게 damage 피해.
    /// 발동 시 수정 중첩을 0으로 초기화하고, 시전자(수정 핵) 자신이 다음 몬스터 턴에 기절(1회 스킵).
    /// (AllTargets + Characters로 배선; Execute에서 자수정 위 캐릭터 제외)</summary>
    [System.Serializable]
    public class CrystalStormSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 20;
        [Tooltip("적 기절 지속 턴 (2 = 다음 플레이어 턴 스킵)")]
        [SerializeField] private int stunTurns = 2;

        public CrystalStormSkill()
        {
            skillName = "수정 폭풍";
            description = "{피해} 피해 + 기절\n자수정 없는 모든 칸";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            if (targetUnits != null)
            {
                var victims = targetUnits
                    .OfType<Character>()
                    .Where(c => c.IsAlive && !(c.CurrentTile != null && c.CurrentTile.HasAttribute(TileAttributeType.Amethyst)))
                    .Cast<Unit>()
                    .ToList();
                AttackUnits(source, victims, damage);

                // 적에게 다음 턴 기절
                foreach (var u in victims)
                    if (u is Character c && c.IsAlive && c.StatusEffects != null)
                        c.StatusEffects.AddEffect(new StunDebuff(stunTurns));
            }

            // 발동 시 수정 중첩 0으로 초기화
            var core = source as Monster;
            if (core != null) CrystalSet.ResetStacks(core);
        }
    }

    /// <summary>수정 핵 조건부 AI: 수정 중첩 ≥ stormThreshold → [1]수정 폭풍, 아니면 [0]수정 폭발.</summary>
    [System.Serializable]
    public class CrystalCorePattern : DiceOrbit.Data.MonsterAI.MonsterAI
    {
        [Tooltip("이 값 이상이면 수정 폭풍(index 1) 발동")]
        [SerializeField] private int stormThreshold = 8;

        public override MonsterSkill GetNextSkill()
        {
            if (availableSkills == null || availableSkills.Count == 0) return null;
            int stacks = CrystalSet.GetStacks(owner);
            int idx = (stacks >= stormThreshold && availableSkills.Count > 1) ? 1 : 0;
            return availableSkills[idx];
        }
    }
}
