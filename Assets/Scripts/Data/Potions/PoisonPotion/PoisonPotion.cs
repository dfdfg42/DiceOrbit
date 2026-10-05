using DiceOrbit.Core;
using DiceOrbit.Core.Run;
using DiceOrbit.Data;
using DiceOrbit.Systems.Effects;
using DiceOrbit.UI;
using UnityEngine;

namespace DiceOrbit.Data.Potions
{
    /// <summary>독 포션 — 특정 적에게 중독 중첩 부여 (턴마다 중첩만큼 피해 후 중첩 -1, 방어도 무시).</summary>
    [CreateAssetMenu(fileName = "PoisonPotion", menuName = "DiceOrbit/Potions/Poison Potion")]
    public class PoisonPotion : Potion
    {
        [Header("효과")]
        [Tooltip("부여할 중독 중첩. 턴마다 남은 중첩만큼 피해를 주고 1씩 줄어든다.")]
        [SerializeField] private int stacks = 5;

        private void Reset()
        {
            PotionName = "독 포션";
            Description = "적 1명에게 중독 5\n턴 시작마다 수치만큼 피해 · 방어도 무시";
            TargetType = PotionTargetType.Enemy;
            ShopPrice = 50;
            CombatOnly = true;
        }

        public override bool Use(Unit target = null)
        {
            if (!(target is Monster) || !target.IsAlive || target.StatusEffects == null) return false;

            target.StatusEffects.AddEffect(StatusEffectManager.CreateEffect(EffectType.Poison, stacks, -1));
            var data = TooltipKeywordFormatter.BuildStatusDisplayData(EffectType.Poison.ToString(), stacks, -1);
            CombatNotifier.NotifyStatus(target, data.Name, data.Color);
            return true;
        }
    }
}
