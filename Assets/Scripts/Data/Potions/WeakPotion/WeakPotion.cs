using DiceOrbit.Core;
using DiceOrbit.Core.Run;
using DiceOrbit.Data;
using DiceOrbit.Systems.Effects;
using DiceOrbit.UI;
using UnityEngine;

namespace DiceOrbit.Data.Potions
{
    /// <summary>쇠약 포션 — 특정 적에게 2턴 쇠약 부여 (가하는 피해 25% 감소).</summary>
    [CreateAssetMenu(fileName = "WeakPotion", menuName = "DiceOrbit/Potions/Weak Potion")]
    public class WeakPotion : Potion
    {
        [Header("효과")]
        [SerializeField] private int percent = 25;
        [SerializeField] private int turns = 2;

        private void Reset()
        {
            PotionName = "쇠약 포션";
            Description = "적 1명에게 쇠약 2턴\n주는 피해 -25%";
            TargetType = PotionTargetType.Enemy;
            ShopPrice = 45;
            CombatOnly = true;
        }

        public override bool Use(Unit target = null)
        {
            if (!(target is Monster) || !target.IsAlive || target.StatusEffects == null) return false;

            target.StatusEffects.AddEffect(StatusEffectManager.CreateEffect(EffectType.Weak, percent, turns));
            var data = TooltipKeywordFormatter.BuildStatusDisplayData(EffectType.Weak.ToString(), percent, turns);
            CombatNotifier.NotifyStatus(target, data.Name, data.Color);
            return true;
        }
    }
}
