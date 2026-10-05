using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Visuals;

namespace DiceOrbit.Data.MonsterPresets.Wave1.Goblin
{
    // ==========================================
    // 웨이브 단위 지뢰밭 정리 훅
    // ==========================================
    /// <summary>
    /// 지뢰는 고블린 사망 후에도 유지되고, 전투 종료(=다음 전투 시작) 시에만 정리된다.
    /// CombatManager.OnCombatStart 에 한 번만 구독해 잔여 지뢰 타일을 제거한다.
    /// </summary>
    public static class MineFieldCleaner
    {
        private static CombatManager hookedManager;

        public static void EnsureWaveHook()
        {
            var cm = CombatManager.Instance;
            if (cm == null) return;
            if (hookedManager == cm) return;

            if (hookedManager != null) hookedManager.OnCombatStart -= OnCombatStart;
            cm.OnCombatStart += OnCombatStart;
            hookedManager = cm;
        }

        private static void OnCombatStart()
        {
            var orbit = GameManager.Instance != null ? GameManager.Instance.GetOrbitManager() : null;
            if (orbit?.Tiles == null) return;

            foreach (var tile in orbit.Tiles)
                if (tile != null) tile.RemoveAttributeType(TileAttributeType.RandMine);
        }
    }

    // ==========================================
    // 패턴 1 [몽둥이 질]
    // ==========================================
    /// <summary>
    /// 턴 시작 기준 무작위 대상 1명이 속한 타일 + 좌우 각각 2칸에 피해.
    /// 대상/범위는 MonsterSkill 설정으로 결정 (RandomCharacter + Tiles + range 2).
    /// </summary>
    [System.Serializable]
    public class GoblinClubSwing : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 15;

        public GoblinClubSwing()
        {
            skillName = "몽둥이 질";
            description = "{피해} 피해 · 대상 좌우 2칸";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    // ==========================================
    // 패턴 2 [지뢰 폭발]
    // ==========================================
    /// <summary>
    /// 설치된 지뢰 타일 + 좌우 각각 한 칸에 피해를 주고, 폭발한 지뢰 타일을 제거한다.
    /// 대상 타일은 MonsterSkill 설정으로 결정 (TilesWithAttribute=RandMine + range 1).
    /// </summary>
    [System.Serializable]
    public class MineBombSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 15;
        // vfxProfile은 SkillData 베이스로 승격됨 (필드명 동일 → 기존 직렬화 데이터 보존)

        public MineBombSkill()
        {
            skillName = "지뢰 폭발";
            description = "{피해} 피해 · 지뢰 좌우 1칸";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            var partyManager = PartyManager.Instance;
            if (partyManager == null) return;

            var affectedTiles = targetTiles != null
                ? targetTiles.Where(tile => tile != null).Distinct().ToList()
                : new List<TileData>();

            var mineTiles = affectedTiles
                .Where(tile => tile.HasAttribute(TileAttributeType.RandMine))
                .ToList();

            if (affectedTiles.Count == 0) return;

            // 시전 VFX는 SkillData.ExecuteSkillWithIntent가 공통 처리. 타일 폭발만 여기서.
            foreach (var tile in affectedTiles)
                DiceOrbit.Visuals.VfxService.PlayOn(
                    string.IsNullOrEmpty(impactCue) ? DiceOrbit.Visuals.VfxTags.TileImpact : impactCue, tile);

            foreach (var character in partyManager.GetAliveCharacters())
            {
                if (character == null || !character.IsAlive) continue;
                if (character.CurrentTile == null || !affectedTiles.Contains(character.CurrentTile)) continue;

                var context = new AttackContext(source, character, SkillName, damage);
                context.VfxCue = impactCue;   // 비면 파이프라인이 루트 impact 사용
                CombatPipeline.Instance?.Process(context);
            }

            // 폭발 후 지뢰 속성 제거
            foreach (var mineTile in mineTiles)
            {
                var mines = mineTile.GetAttributes()
                    .Where(attr => attr != null && attr.Type == TileAttributeType.RandMine)
                    .ToList();
                foreach (var mine in mines)
                    mineTile.RemoveAttribute(mine);
            }
        }
    }

    // ==========================================
    // 패시브 [지뢰 설치]
    // ==========================================
    /// <summary>
    /// 매 턴 시작 시 무작위 타일 2개에 지뢰를 설치한다.
    /// 같은 타일에 겹쳐 설치되면 피해가 누적된다(중첩).
    /// 지뢰는 고블린 사망 후에도 유지되고 웨이브 종료 시 정리된다(MineFieldCleaner).
    /// </summary>
    [System.Serializable]
    public class PlantMinePassive : PassiveAbility
    {
        [Header("Mine Settings")]
        [Tooltip("설치할 지뢰의 피해량")]
        [SerializeField] private int mineDamage = 20;

        [Tooltip("매 턴 설치할 지뢰 개수")]
        [SerializeField] private int minesPerTurn = 2;

        public PlantMinePassive()
        {
            passiveName = "지뢰 설치";
            description = "턴 종료마다 무작위 칸에 지뢰 설치\n밟으면 피해 · 터지면 사라짐";
            priority = 10;
            isStackable = false;
        }

        public override void Initialize(Unit Owner)
        {
            base.Initialize(Owner);
            MineFieldCleaner.EnsureWaveHook();
        }

        public override string GetDynamicDescription()
            => $"턴 종료마다 지뢰 {minesPerTurn}개 설치\n밟으면 피해 {mineDamage} · 같은 칸에 겹쳐짐";

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (context.Phase == EventPhase.TurnEnd && context.SourceUnit == owner && trigger == CombatTrigger.OnPostAction)
            {
                PlantMines();
            }
        }

        private void PlantMines()
        {
            var orbitManager = GameManager.Instance?.GetOrbitManager();
            if (orbitManager == null) return;

            int tileCount = orbitManager.TileCount;
            if (tileCount <= 0) return;

            var chosen = new HashSet<int>();
            int target = Mathf.Min(minesPerTurn, tileCount);
            int safety = 0;
            while (chosen.Count < target && safety++ < tileCount * 4)
                chosen.Add(Random.Range(0, tileCount));

            foreach (var index in chosen)
            {
                var tile = orbitManager.GetTile(index);
                if (tile == null) continue;

                // 이미 지뢰가 있으면 피해를 누적(중첩), 없으면 새로 설치
                var existing = tile.GetAttributes().FirstOrDefault(a => a != null && a.Type == TileAttributeType.RandMine);
                if (existing != null)
                {
                    existing.AddStack(mineDamage);
                }
                else
                {
                    tile.AddAttribute(new RandMineTile(TileAttributeType.RandMine, mineDamage, -1, isStackable: true));
                }
            }
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }

    // ==========================================
    // 사망 효과
    // ==========================================
    /// <summary>
    /// 고블린 사망 효과. 지뢰는 사망 후에도 유지되므로 여기서 제거하지 않는다.
    /// (지뢰 정리는 웨이브 종료 시 MineFieldCleaner가 담당)
    /// </summary>
    [System.Serializable]
    public class GoblinDeath : DeathEffect
    {
        public GoblinDeath()
        {
            effectName = "Goblin Death";
            description = "쓰러져도 지뢰는 전투 끝까지 남음";
        }

        public override void Execute(Monster deadMonster)
        {
            Debug.Log($"[GoblinDeath] {deadMonster.name} died. (지뢰는 웨이브 종료 시 정리)");
        }
    }
}
