using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Tile;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Systems.Effects;
using DiceOrbit.Data.MonsterPresets.Wave5.Shared;

namespace DiceOrbit.Data.MonsterPresets.Wave5.FlameDoll
{
    /// <summary>[불의 가호] 턴시작: 보스(불꽃 소녀)에 FlameGuardStatus(1턴) 부여. 인형 생존 시 매턴 갱신.</summary>
    [System.Serializable]
    public class FlameGraceGuardPassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [Tooltip("보스 방어도 보유 시 받는 피해 감소 %")]
        [SerializeField] private int reductionPercent = 20;

        public FlameGraceGuardPassive()
        {
            passiveName = "불의 가호";
            description = "불꽃 소녀가 받는 피해 -20%\n불꽃 소녀에게 방어도가 있는 동안";
            priority = 10; isStackable = false;
        }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null) return;
            if (trigger != CombatTrigger.OnPreAction || context.Phase != EventPhase.TurnStart || context.SourceUnit != owner) return;

            var boss = FlameSet.FindBoss();
            if (boss == null || !boss.IsAlive || boss.StatusEffects == null) return;
            boss.StatusEffects.AddEffect(new FlameGuardStatus(reductionPercent, 1));
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }

    /// <summary>[불꽃 방패] 불꽃 소녀 + 자신(인형)에 일시 방어도 amount 부여.</summary>
    [System.Serializable]
    public class FlameShieldSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int amount = 10;

        public FlameShieldSkill() { skillName = "불꽃 방패"; description = "방어도 +10\n불꽃 소녀와 자신"; }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            if (source?.Stats != null) source.Stats.TempArmor += amount; // 자신(인형)
            var boss = FlameSet.FindBoss();
            if (boss != null && boss.IsAlive && boss.Stats != null) boss.Stats.TempArmor += amount;
            Debug.Log($"[불꽃 방패] 보스+자신 방어도 +{amount}");
        }
    }
}
