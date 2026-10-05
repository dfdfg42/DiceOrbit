using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Tile;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.MonsterPresets.Wave7.Shared
{
    /// <summary>[성장의 활력] 식물 몬스터 패시브. 턴 시작 시 활력 타일 개수만큼 체력 회복.</summary>
    [System.Serializable]
    public class GrowthVitalityPassive : PassiveAbility
    {
        public GrowthVitalityPassive()
        {
            passiveName = "성장의 활력";
            description = "턴 시작 시 체력 회복\n남은 활력 타일 수만큼";
            priority = 10; isStackable = false;
        }

        public override void Initialize(Unit Owner)
        {
            base.Initialize(Owner);
            FarmSet.EnsureWaveHook();
        }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null) return;
            if (trigger != CombatTrigger.OnPreAction || context.Phase != EventPhase.TurnStart || context.SourceUnit != owner) return;
            int n = FarmSet.CountVitalityTiles();
            if (n > 0) owner.Heal(n);
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }

    /// <summary>[농장의 활력] 무작위 타일 tileCount개에 활력 타일 설치 (효과 없음, 그 위 턴 종료 시 삭제).
    /// 대상 없음 — Execute가 직접 배치. 난쟁이 농부/허수아비 P1.</summary>
    [System.Serializable]
    public class FarmVitalitySkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int tileCount = 2;

        public FarmVitalitySkill() { skillName = "농장의 활력"; description = "활력 2칸 설치"; }

        public override int GetPreviewDamage() => 0;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            FarmSet.EnsureWaveHook();
            var orbit = GameManager.Instance?.GetOrbitManager();
            if (orbit == null || orbit.Tiles == null) return;
            var candidates = orbit.Tiles.Where(t => t != null && !t.HasAttribute(TileAttributeType.Vitality)).ToList();
            int place = Mathf.Min(tileCount, candidates.Count);
            for (int i = 0; i < place; i++)
            {
                int r = Random.Range(0, candidates.Count);
                candidates[r].AddAttribute(new VitalityTile());
                candidates.RemoveAt(r);
            }
        }
    }

    /// <summary>(레거시·미사용) 옛 [농장의 활력] 패시브. 현재는 FarmVitalitySkill(스킬) + AllyDeathGuardPattern의 받은-피해 시드로 대체.</summary>
    [System.Serializable]
    public class FarmVitalityPassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [SerializeField] private int tilesPerTurn = 1;

        public FarmVitalityPassive()
        {
            passiveName = "농장의 활력";
            description = "턴 시작 시 무작위 칸에 활력 설치\n밟으면 모든 식물 몬스터 활력 -1";
            priority = 10; isStackable = false;
        }

        public override void Initialize(Unit Owner)
        {
            base.Initialize(Owner);
            FarmSet.EnsureWaveHook();
            if (Owner != null && Owner.StatusEffects != null && !Owner.StatusEffects.HasEffect(EffectType.BiteDamageTaken))
                Owner.StatusEffects.AddEffect(new BiteDamageTakenStatus());
        }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null) return;
            if (trigger != CombatTrigger.OnPreAction || context.Phase != EventPhase.TurnStart || context.SourceUnit != owner) return;

            var orbit = GameManager.Instance?.GetOrbitManager();
            if (orbit == null || orbit.Tiles == null) return;
            var candidates = orbit.Tiles.Where(t => t != null && !t.HasAttribute(TileAttributeType.Vitality)).ToList();
            int place = Mathf.Min(tilesPerTurn, candidates.Count);
            for (int i = 0; i < place; i++)
            {
                int r = Random.Range(0, candidates.Count);
                candidates[r].AddAttribute(new VitalityTile());
                candidates.RemoveAt(r);
            }
            if (place > 0) Debug.Log($"[농장의 활력] 활력 타일 {place}개 설치");
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }

    /// <summary>공용 타일 피해 스킬 — 대상 타일(MonsterSkill 타깃팅)에 damage. 포식/몸통박치기/허수아비 때리기/포자 살포 등.</summary>
    [System.Serializable]
    public class FarmDamageSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private string skillLabel = "공격";
        [SerializeField] private int damage = 20;

        public override string SkillName => string.IsNullOrEmpty(skillLabel) ? "공격" : skillLabel;
        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
            => AttackTiles(source, targetTiles, damage);
    }

    /// <summary>[허수아비 때리기] 무작위 대상 1명 기준 진행방향(Next) forwardTiles칸(대상 타일 제외)에 damage.
    /// FollowsTarget=true: 대상이 움직이면 공격 범위도 따라 이동.</summary>
    [System.Serializable]
    public class FarmStrikeSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private string skillLabel = "허수아비 때리기";
        [SerializeField] private int damage = 20;
        [Tooltip("대상 타일 기준 진행방향으로 공격할 타일 수 (대상 타일 제외)")]
        [SerializeField] private int forwardTiles = 5;

        public override string SkillName => string.IsNullOrEmpty(skillLabel) ? "공격" : skillLabel;
        public override int GetPreviewDamage() => damage;
        public override bool FollowsTarget => true;

        public override List<TileData> GetFollowTiles(Character target)
        {
            var tiles = new List<TileData>();
            if (target == null || target.CurrentTile == null) return tiles;
            var t = target.CurrentTile;
            for (int i = 0; i < forwardTiles && t?.NextTile != null; i++) { t = t.NextTile; tiles.Add(t); }
            return tiles;
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
            => AttackTiles(source, targetTiles, damage);
    }

    /// <summary>[비료] 모든 아군(활력 보유 몬스터)의 활력 스택 +amount.</summary>
    [System.Serializable]
    public class FertilizerSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int amount = 1;

        public FertilizerSkill() { skillName = "비료"; description = "모든 아군 활력 중첩 +1"; }

        public override int GetPreviewDamage() => 0;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
            => FarmSet.ChangeVitalityAll(amount);
    }

    /// <summary>[농장 지키기] 체력이 가장 낮은 아군에게 일시 방어도 armor.</summary>
    [System.Serializable]
    public class FarmGuardSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int armor = 5;

        public FarmGuardSkill() { skillName = "농장 지키기"; description = "방어도 +5\n체력이 가장 낮은 아군"; }

        public override int GetPreviewDamage() => 0;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            var ally = FarmSet.FindLowestHpAlly(source as Monster);
            if (ally != null && ally.Stats != null)
            {
                ally.Stats.TempArmor += armor;
                Debug.Log($"[농장 지키기] {ally.Stats.MonsterName} 방어도 +{armor}");
            }
        }
    }

    /// <summary>[덩굴 묶기] 무작위 활력 타일 1개 기준 좌우 range칸(활력 타일 제외)의 적에게 damage. Custom 타깃팅.</summary>
    [System.Serializable]
    public class VineBindSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 20;
        [SerializeField] private int range = 3;

        public VineBindSkill() { skillName = "덩굴 묶기"; description = "{피해} 피해 · 활력 타일 좌우 3칸"; }

        public override int GetPreviewDamage() => damage;

        public override List<TileData> GetCustomTiles(MonsterSkill skill, Monster owner)
        {
            var orbit = GameManager.Instance?.GetOrbitManager();
            if (orbit == null || orbit.Tiles == null) return new List<TileData>();
            var vitality = orbit.Tiles.Where(t => t != null && t.HasAttribute(TileAttributeType.Vitality)).ToList();
            if (vitality.Count == 0) return new List<TileData>();
            var center = vitality[Random.Range(0, vitality.Count)];
            int total = orbit.Tiles.Count;
            var result = new HashSet<TileData>();
            for (int i = -range; i <= range; i++)
            {
                if (i == 0) continue; // 활력 타일(중심) 제외
                int idx = ((center.TileIndex + i) % total + total) % total;
                var tile = orbit.GetTile(idx);
                if (tile != null) result.Add(tile);
            }
            return result.ToList();
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
            => AttackTiles(source, targetTiles, damage);
    }

    /// <summary>[쇠스랑] 무작위 대상 1명에게 damage. 단, 이번 턴 받은 누적 피해 ≥ cancelThreshold면 취소. (깨물기와 동일 메커니즘)</summary>
    [System.Serializable]
    public class PitchforkSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 20;
        [Tooltip("이번 턴 받은 누적 피해가 이 값 이상이면 취소")]
        [SerializeField] private int cancelThreshold = 15;

        public PitchforkSkill() { skillName = "쇠스랑"; description = "{피해} 피해 · 1명\n피해 15 이상 주면 취소"; }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            int taken = source != null && source.StatusEffects != null
                ? source.StatusEffects.GetEffectValue(EffectType.BiteDamageTaken) : 0;
            if (taken >= cancelThreshold)
            {
                Debug.Log($"[쇠스랑] 취소 — 이번 턴 누적 피해 {taken}");
                return;
            }
            AttackUnits(source, targetUnits, damage);
        }
    }

    /// <summary>아군 사망 분기 패턴(허수아비/농부). 아군 사망 전 → index 0 ONLY, 사망 후 → [0]/[1] 랜덤.</summary>
    [System.Serializable]
    public class AllyDeathGuardPattern : DiceOrbit.Data.MonsterAI.MonsterAI
    {
        public override MonsterSkill GetNextSkill()
        {
            if (availableSkills == null || availableSkills.Count == 0) return null;
            // [쇠스랑] 취소 판정용 이번-턴 받은-피해 추적 시드 (난쟁이 농부; 허수아비엔 무해)
            if (owner != null && owner.StatusEffects != null && !owner.StatusEffects.HasEffect(EffectType.BiteDamageTaken))
                owner.StatusEffects.AddEffect(new BiteDamageTakenStatus());
            if (!FarmSet.HasAnyAllyDied()) return availableSkills[0];
            int idx = availableSkills.Count >= 2 ? Random.Range(0, 2) : 0;
            return availableSkills[idx];
        }
    }
}
