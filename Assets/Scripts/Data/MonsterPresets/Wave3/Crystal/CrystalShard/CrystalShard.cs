using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using DiceOrbit.Data.Tile;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Systems.Effects;
using DiceOrbit.Data.MonsterPresets.Wave3.Crystal.Shared;

namespace DiceOrbit.Systems.Effects
{
    /// <summary>[받은 피해] 수정 파편이 이번 라운드 받은 누적 피해(가시화, 수정 화살 취소 판정).
    /// 자가 누적(OnHit, Target==Owner), 라운드 리셋(파편 턴 종료 시 0). 순수 카운터.</summary>
    public class CrystalDamageStatus : StatusEffect
    {
        public CrystalDamageStatus() : base(EffectType.CrystalDamageTaken, 0, -1, isStackable: true) { }

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (Owner == null) return;
            if (trigger == CombatTrigger.OnHit && !context.IsSimulation && context.IsEffected && context.Target == Owner)
                Value += Mathf.RoundToInt(context.OutputValue);
        }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (Owner != null && context.Phase == EventPhase.TurnEnd && trigger == CombatTrigger.OnPostAction && context.SourceUnit == Owner)
                Value = 0;
            base.OnTurnEvent(trigger, context);
        }
    }
}

namespace DiceOrbit.Data.MonsterPresets.Wave3.Crystal.CrystalShard
{
    /// <summary>[수정 비] 무작위 타일 count개에 있는 적에게 damage 피해. (RandomTiles + Tiles + count 6)</summary>
    [System.Serializable]
    public class CrystalRainSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 15;

        public CrystalRainSkill()
        {
            skillName = "수정 비";
            description = "{피해} 피해 · 무작위 8칸";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    /// <summary>[수정 화살] 무작위 대상 1명에게 damage 피해. 파편이 이번 라운드 받은 피해 ≥ cancelThreshold면 취소.</summary>
    [System.Serializable]
    public class CrystalArrowSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 15;
        [Tooltip("이 피해 이상 받으면 취소")]
        [SerializeField] private int cancelThreshold = 10;

        public CrystalArrowSkill()
        {
            skillName = "수정 화살";
            description = "{피해} 피해 · 1명\n피해 10 이상 주면 취소";
        }

        public override int GetPreviewDamage() => damage;

        public bool ShouldCancel(Monster shard)
            => shard != null && shard.StatusEffects != null
               && shard.StatusEffects.GetEffectValue(EffectType.CrystalDamageTaken) >= cancelThreshold;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            var shard = source as Monster;
            if (ShouldCancel(shard))
            {
                Debug.Log($"[수정 화살] {source.name} 받은 피해 ≥ {cancelThreshold} → 취소");
                return;
            }
            AttackUnits(source, targetUnits, damage);
        }
    }

    /// <summary>수정 파편 조건부 AI: 수정 핵 생존 → [0]수정 비 ONLY, 사망 → 50/50[수정 비, 수정 화살]. 자신에 받은-피해 추적 상태 시드.</summary>
    [System.Serializable]
    public class CrystalShardPattern : DiceOrbit.Data.MonsterAI.MonsterAI
    {
        public override MonsterSkill GetNextSkill()
        {
            if (availableSkills == null || availableSkills.Count == 0) return null;

            // 받은-피해 추적 상태 시드 (파편 자신) — 플레이어 턴 피해를 잡기 위해 의도 선정 시 보장.
            if (owner != null && owner.StatusEffects != null && !owner.StatusEffects.HasEffect(EffectType.CrystalDamageTaken))
                owner.StatusEffects.AddEffect(new CrystalDamageStatus());

            if (CrystalSet.GetCore() != null) return availableSkills[0];
            int idx = availableSkills.Count >= 2 ? Random.Range(0, 2) : 0;
            return availableSkills[idx];
        }
    }
}
