using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Core.Run;
using UnityEngine;

namespace DiceOrbit.Data.Potions
{
    /// <summary>폭발 포션 — 특정 적에게 즉시 피해 (파이프라인 경유 — 쇠약/방어 등 리액터 반응).</summary>
    [CreateAssetMenu(fileName = "ExplosionPotion", menuName = "DiceOrbit/Potions/Explosion Potion")]
    public class ExplosionPotion : Potion
    {
        [Header("효과")]
        [SerializeField] private int damage = 20;

        private void Reset()
        {
            PotionName = "폭발 포션";
            Description = "적 1명에게 피해 20";
            TargetType = PotionTargetType.Enemy;
            ShopPrice = 45;
            CombatOnly = true;
        }

        public override bool Use(Unit target = null)
        {
            if (!(target is Monster) || !target.IsAlive) return false;

            var ctx = new AttackContext(null, target, PotionName, damage);
            CombatPipeline.Instance?.Process(ctx);
            return true;
        }
    }
}
