using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Tile;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Systems.Effects
{
    /// <summary>[받은 피해] 흡혈박쥐가 이번 턴 받은 누적 피해(깨물기 취소 판정, 가시화).
    /// 자가 누적(OnCalculateOutput, Target==Owner), 소유자 턴 종료 시 0 리셋. 영구(-1). FireDamageTakenStatus 미러.</summary>
    public class BiteDamageTakenStatus : StatusEffect
    {
        public BiteDamageTakenStatus() : base(DiceOrbit.Data.EffectType.BiteDamageTaken, 0, -1) { }

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (Owner == null) return;
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.Target != Owner) return;
            Value += Mathf.Max(0, Mathf.RoundToInt(context.OutputValue));
        }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (Owner != null && context.Phase == EventPhase.TurnEnd && context.SourceUnit == Owner)
                Value = 0;
        }
    }
}

namespace DiceOrbit.Data.MonsterPresets.Wave6.VampireBat
{
    /// <summary>[흡혈] 자신의 공격이 적중할 때마다 체력 healAmount 회복 + 피해량 attackGain 영구 증가(중첩).
    /// 깨물기 취소 판정용 받은-피해 상태(BiteDamageTakenStatus)를 최초 1회 시드한다.</summary>
    [System.Serializable]
    public class BloodsuckPassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [Tooltip("공격 적중 시 회복량")]
        [SerializeField] private int healAmount = 5;
        [Tooltip("공격 적중 시 영구 증가하는 피해량")]
        [SerializeField] private int attackGain = 2;

        public BloodsuckPassive()
        {
            passiveName = "흡혈";
            description = "체력 5 회복 · 주는 피해 +2 (누적)\n공격이 적중할 때마다";
            priority = 10; isStackable = false;
        }

        public override void Initialize(Unit Owner)
        {
            base.Initialize(Owner);
            // 깨물기 취소 판정용 이번-턴 받은-피해 상태 시드(영구, 자기-리셋). 이미 있으면 무해.
            if (Owner?.StatusEffects != null && !Owner.StatusEffects.HasEffect(DiceOrbit.Data.EffectType.BiteDamageTaken))
                Owner.StatusEffects.AddEffect(new BiteDamageTakenStatus());
        }

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null) return;
            if (trigger != CombatTrigger.OnHit) return;
            if (context.IsSimulation || !context.IsEffected) return;
            if (context.SourceUnit != owner) return;

            owner.Heal(healAmount);
            if (owner.StatusEffects != null)
                owner.StatusEffects.AddEffect(new BuffAttackStatus(attackGain, -1) { IsStackable = true });
            Debug.Log($"[흡혈] {owner.name} 체력 +{healAmount}, 피해량 +{attackGain} (영구)");
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }

    /// <summary>[깨물기] 무작위 대상 1명에게 damage. 단, 이번 턴 받은 누적 피해 ≥ cancelThreshold면 취소.</summary>
    [System.Serializable]
    public class BiteSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 20;
        [Tooltip("이번 턴 받은 누적 피해가 이 값 이상이면 취소")]
        [SerializeField] private int cancelThreshold = 15;

        public BiteSkill() { skillName = "깨물기"; description = "{피해} 피해 · 1명\n피해 15 이상 주면 취소"; }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            int taken = source?.StatusEffects != null
                ? source.StatusEffects.GetEffectValue(DiceOrbit.Data.EffectType.BiteDamageTaken) : 0;
            if (taken >= cancelThreshold)
            {
                Debug.Log($"[깨물기] 취소 — 이번 턴 누적 피해 {taken}");
                return;
            }
            AttackUnits(source, targetUnits, damage);
        }
    }

    /// <summary>[선혈 강습] 지정 인덱스 구간([aStart..aEnd] ∪ [bStart..bEnd]) 타일의 적에게 damage. (Custom 타깃팅)</summary>
    [System.Serializable]
    public class BloodRaidSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 20;
        [Tooltip("첫 번째 타일 구간(포함)")]
        [SerializeField] private int aStart = 1;
        [SerializeField] private int aEnd = 4;
        [Tooltip("두 번째 타일 구간(포함)")]
        [SerializeField] private int bStart = 10;
        [SerializeField] private int bEnd = 13;

        public BloodRaidSkill() { skillName = "선혈 강습"; description = "{피해} 피해 · 1~4·10~13번 칸"; }

        public override int GetPreviewDamage() => damage;

        public override List<TileData> GetCustomTiles(MonsterSkill skill, Monster owner)
        {
            var orbit = GameManager.Instance?.GetOrbitManager();
            if (orbit == null) return new List<TileData>();
            return orbit.Tiles.Where(t => t != null &&
                    ((t.TileIndex >= aStart && t.TileIndex <= aEnd) ||
                     (t.TileIndex >= bStart && t.TileIndex <= bEnd)))
                .ToList();
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }
}
