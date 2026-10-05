using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Data.MonsterPresets.Wave5.FlameFairy
{
    /// <summary>[불짚이기] 이번 턴 요정이 받은 누적 피해가 threshold↑면 취소, 아니면 무작위 1명에게 damage.
    /// 받은-피해 추적 상태(FireDamageTakenStatus)는 BossGuardPattern(seedFireDamage=true)이 시드한다.</summary>
    [System.Serializable]
    public class KindlingSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 20;
        [Tooltip("이번 턴 받은 누적 피해가 이 값 이상이면 취소")]
        [SerializeField] private int cancelThreshold = 15;

        public KindlingSkill() { skillName = "불짚이기"; description = "{피해} 피해 · 1명\n피해 15 이상 주면 취소"; }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            int taken = source?.StatusEffects != null ? source.StatusEffects.GetEffectValue(EffectType.FireDamageTaken) : 0;
            if (taken >= cancelThreshold)
            {
                Debug.Log($"[불짚이기] 취소 — 이번 턴 누적 피해 {taken}");
                return;
            }
            AttackUnits(source, targetUnits, damage);
        }
    }
}
