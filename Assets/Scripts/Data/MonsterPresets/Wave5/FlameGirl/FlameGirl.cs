using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Tile;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Data.MonsterPresets.Wave5.Shared;

namespace DiceOrbit.Data.MonsterPresets.Wave5.FlameGirl
{
    /// <summary>[대화재] 모든 불꽃 타일 삭제 + 모든 타일에 damage 피해.</summary>
    [System.Serializable]
    public class ConflagrationSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 25;

        public ConflagrationSkill() { skillName = "대화재"; description = "{피해} 피해 · 모든 칸\n불꽃 타일을 전부 태움"; }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            var orbit = GameManager.Instance?.GetOrbitManager();
            if (orbit == null) return;
            foreach (var t in orbit.Tiles.ToList())
                t?.RemoveAttributeType(TileAttributeType.Flame);
            AttackTiles(source, orbit.Tiles, damage);
        }
    }

    /// <summary>[타오르는 무대] 턴 시작 시 불꽃 타일 개수만큼 일시 방어도 획득.</summary>
    [System.Serializable]
    public class FlameStagePassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [Tooltip("불꽃 타일 1개당 방어도")]
        [SerializeField] private int armorPerTile = 1;

        public FlameStagePassive()
        {
            passiveName = "타오르는 무대";
            description = "턴 시작 시 방어도 획득\n남은 불꽃 타일 수만큼";
            priority = 10; isStackable = false;
        }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null || owner.Stats == null) return;
            if (trigger != CombatTrigger.OnPreAction || context.Phase != EventPhase.TurnStart || context.SourceUnit != owner) return;

            var orbit = GameManager.Instance?.GetOrbitManager();
            if (orbit == null) return;
            int n = orbit.Tiles.Count(t => t != null && t.HasAttribute(TileAttributeType.Flame));
            if (n <= 0) return;
            owner.Stats.TempArmor += n * armorPerTile;
            Debug.Log($"[타오르는 무대] 불꽃 {n}개 → 방어도 +{n * armorPerTile}");
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }

    /// <summary>[소각] HP 50%↓ 최초 1회, 무작위 8타일에 불꽃 설치.</summary>
    [System.Serializable]
    public class IncinerationPassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [SerializeField] private int tileCount = 8;
        [SerializeField] private int fireDamage = 20;

        [System.NonSerialized] private bool fired = false;

        public IncinerationPassive()
        {
            passiveName = "소각";
            description = "무작위 8칸에 불꽃 설치\n체력이 처음 50% 이하가 될 때";
            priority = 10; isStackable = false;
        }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null || owner.Stats == null) return;
            if (trigger != CombatTrigger.OnPreAction || context.Phase != EventPhase.TurnStart || context.SourceUnit != owner) return;
            if (fired || owner.Stats.HPRatio > 0.5f) return;

            var orbit = GameManager.Instance?.GetOrbitManager();
            if (orbit == null) return;
            var candidates = orbit.Tiles.Where(t => t != null && !t.HasAttribute(TileAttributeType.Flame)).ToList();
            int place = Mathf.Min(tileCount, candidates.Count);
            for (int i = 0; i < place; i++)
            {
                int r = Random.Range(0, candidates.Count);
                candidates[r].AddAttribute(new FireTile(fireDamage));
                candidates.RemoveAt(r);
            }
            fired = true;
            Debug.Log($"[소각] HP50%↓ → 불꽃 {place}개 설치");
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }
}
