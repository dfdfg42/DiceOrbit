using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using DiceOrbit.Systems.Effects;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DiceOrbit.Data.MonsterPresets.Wave3.SnowMan
{
    /// <summary>눈 세트 공유 헬퍼.</summary>
    public static class SnowSet
    {
        private static CombatManager hookedManager;
        private static int peakCount;

        public static IEnumerable<Monster> OtherAliveMonsters(Monster owner)
        {
            var monsters = CombatManager.Instance?.ActiveMonsters;
            if (monsters == null) yield break;
            foreach (var m in monsters)
                if (m != null && m != owner && m.IsAlive) yield return m;
        }

        public static List<TileData> ExpandLR(TileData center, int range)
        {
            var result = new List<TileData>();
            if (center == null) return result;
            var set = new HashSet<TileData> { center };
            var t = center;
            for (int i = 0; i < range && t?.NextTile != null; i++) { t = t.NextTile; set.Add(t); }
            t = center;
            for (int i = 0; i < range && t?.PreviousTile != null; i++) { t = t.PreviousTile; set.Add(t); }
            result.AddRange(set);
            return result;
        }

        // ── 진창눈 취소용 받은-피해 추적 (가시화, SnowDamageStatus) ──
        public static void AddDamageTaken(Monster snowman, int amount)
        {
            if (snowman == null || snowman.StatusEffects == null || amount <= 0) return;
            snowman.StatusEffects.AddEffect(new SnowDamageStatus(amount));
        }

        public static int GetDamageTaken(Monster snowman)
        {
            if (snowman == null || snowman.StatusEffects == null) return 0;
            return snowman.StatusEffects.GetEffectValue(EffectType.SnowDamageTaken);
        }

        public static void ResetDamageTaken(Monster snowman)
        {
            if (snowman == null || snowman.StatusEffects == null) return;
            snowman.StatusEffects.RemoveEffect(EffectType.SnowDamageTaken);
        }

        /// <summary>다른 아군이 하나라도 죽었는가 (조건부 AI용). 최대 생존 수(=초기) 대비 감소로 판정.</summary>
        public static bool HasAnyAllyDied()
        {
            var cm = CombatManager.Instance;
            if (cm == null) return false;
            int alive = cm.GetAliveMonsters().Count;
            if (alive > peakCount) peakCount = alive;
            return alive < peakCount;
        }

        public static void EnsureWaveHook()
        {
            var cm = CombatManager.Instance;
            if (cm == null || hookedManager == cm) return;
            if (hookedManager != null) hookedManager.OnCombatStart -= OnCombatStart;
            cm.OnCombatStart += OnCombatStart;
            hookedManager = cm;
        }

        private static void OnCombatStart()
        {
            peakCount = 0;
            var orbit = GameManager.Instance != null ? GameManager.Instance.GetOrbitManager() : null;
            if (orbit?.Tiles == null) return;
            foreach (var tile in orbit.Tiles)
                if (tile != null) tile.RemoveAttributeType(TileAttributeType.Frost);
        }
    }

    /// <summary>[받은 피해] 눈사람이 이번 라운드 받은 누적 피해(가시화). 진창눈 취소 판정용. 순수 카운터, 영구, 누적.</summary>
    public class SnowDamageStatus : StatusEffect
    {
        public SnowDamageStatus(int amount) : base(EffectType.SnowDamageTaken, amount, -1, isStackable: true) { }
    }

    /// <summary>[진창눈] 무작위 대상 1명에게 damage 피해. 눈사람이 이번 라운드 cancelDamageThreshold 이상 받았으면 취소.</summary>
    [System.Serializable]
    public class ThrowSnow : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 20;
        [Tooltip("이 피해 이상 받으면 공격 취소")]
        [SerializeField] private int cancelDamageThreshold = 10;

        public ThrowSnow()
        {
            skillName = "진창눈";
            description = "{피해} 피해 + 이동 불가 · 1명\n피해 10 이상 주면 취소";
        }

        public override int GetPreviewDamage() => damage;

        public bool ShouldCancel(Monster snowman)
            => snowman != null && SnowSet.GetDamageTaken(snowman) >= cancelDamageThreshold;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            var snowman = source as Monster;
            if (ShouldCancel(snowman))
            {
                Debug.Log($"[진창눈] {source.name} 피해 {SnowSet.GetDamageTaken(snowman)} ≥ {cancelDamageThreshold} → 취소");
                return;
            }
            AttackUnits(source, targetUnits, damage);
        }
    }

    /// <summary>[눈보라] 무작위 대상 2명 기준 좌우 각각 sideRange칸(대상 타일 제외)에 피해.
    /// FollowsTarget=true: 대상이 움직이면 공격 범위도 따라 이동 (대상 수는 asset targetCount=2).</summary>
    [System.Serializable]
    public class SnowStorm : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 15;
        [Tooltip("대상 타일 기준 좌우 각각 칸 수 (대상 타일 제외)")]
        [SerializeField] private int sideRange = 1;

        public SnowStorm()
        {
            skillName = "눈보라";
            description = "{피해} 피해 · 2명의 좌우 1칸\n대상을 따라 움직임";
        }

        public override int GetPreviewDamage() => damage;

        public override bool FollowsTarget => true;

        public override List<TileData> GetFollowTiles(Character target)
        {
            var tiles = new List<TileData>();
            if (target == null || target.CurrentTile == null) return tiles;
            var t = target.CurrentTile;
            for (int i = 0; i < sideRange && t?.NextTile != null; i++) { t = t.NextTile; tiles.Add(t); }
            t = target.CurrentTile;
            for (int i = 0; i < sideRange && t?.PreviousTile != null; i++) { t = t.PreviousTile; tiles.Add(t); }
            return tiles;
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    /// <summary>
    /// [행복한 눈사람] 눈사람의 공격이 적중한 적에게 빙결 중첩 +frostStacks. 빙결: 매 턴 종료 시 중첩만큼 피해(중첩 −1).
    /// 또한 눈사람이 받은 피해를 누적해 [진창눈]의 취소 판정에 쓰고, 턴 종료 시 초기화한다.
    /// </summary>
    [System.Serializable]
    public class HappySnowmanPassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [Tooltip("피격 적에게 부여할 빙결 중첩")]
        [SerializeField] private int frostStacks = 3;

        public HappySnowmanPassive()
        {
            passiveName = "행복한 눈사람";
            description = "공격에 맞으면 빙결 3중첩\n턴 종료 시 중첩만큼 피해";
            priority = 10;
            isStackable = false;
        }

        public override string GetDynamicDescription()
            => $"공격에 맞으면 빙결 {frostStacks}중첩\n턴 종료 시 중첩만큼 피해";

        public override void Initialize(Unit Owner)
        {
            base.Initialize(Owner);
            SnowSet.EnsureWaveHook();
        }

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null) return;
            if (trigger != CombatTrigger.OnHit || context.IsSimulation || !context.IsEffected) return;

            // 눈사람이 받은 피해 누적 — 진창눈(취소 가능 패턴)이 예약됐을 때만.
            if (context.Target == owner)
            {
                var m = owner as Monster;
                var throwSnow = m?.NextSkill?.skillData as ThrowSnow;
                if (throwSnow != null)
                {
                    SnowSet.AddDamageTaken(m, Mathf.RoundToInt(context.OutputValue));
                    if (throwSnow.ShouldCancel(m)) m.CancelIntent();
                }
            }

            // 눈사람 공격이 적중한 적 → 빙결 중첩 부여
            if (context.SourceUnit == owner && context.Target is Character victim && victim.IsAlive && victim.StatusEffects != null)
                victim.StatusEffects.AddEffect(new FrostStackStatus(frostStacks));
        }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null) return;
            if (trigger == CombatTrigger.OnPostAction && context.Phase == EventPhase.TurnEnd && context.SourceUnit == owner)
                SnowSet.ResetDamageTaken(owner as Monster);
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }
}
