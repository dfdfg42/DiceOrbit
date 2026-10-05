using DiceOrbit.Core;
using DiceOrbit.Core.Run;
using DiceOrbit.Data;
using DiceOrbit.Systems.Effects;
using DiceOrbit.UI;
using UnityEngine;

namespace DiceOrbit.Data.Potions
{
    /// <summary>파워 포션 — 특정 아군에게 2턴 파워 부여 (가하는 피해 25% 증가).</summary>
    [CreateAssetMenu(fileName = "PowerPotion", menuName = "DiceOrbit/Potions/Power Potion")]
    public class PowerPotion : Potion
    {
        [Header("효과")]
        [SerializeField] private int percent = 25;
        [SerializeField] private int turns = 2;

        private void Reset()
        {
            PotionName = "파워 포션";
            Description = "아군 1명에게 파워 2턴\n주는 피해 +25%";
            TargetType = PotionTargetType.Ally;
            ShopPrice = 50;
            CombatOnly = true;
        }

        public override bool Use(Unit target = null)
        {
            if (!(target is Character) || !target.IsAlive || target.StatusEffects == null) return false;

            target.StatusEffects.AddEffect(StatusEffectManager.CreateEffect(EffectType.Power, percent, turns));
            var data = TooltipKeywordFormatter.BuildStatusDisplayData(EffectType.Power.ToString(), percent, turns);
            CombatNotifier.NotifyStatus(target, data.Name, data.Color);
            return true;
        }
    }
}
