using DiceOrbit.Core;
using DiceOrbit.Core.Run;
using DiceOrbit.UI;
using UnityEngine;

namespace DiceOrbit.Data.Potions
{
    /// <summary>방어 포션 — 특정 아군에게 일시 방어도 부여 (턴마다 초기화되는 TempArmor, 피해 선흡수).</summary>
    [CreateAssetMenu(fileName = "DefensePotion", menuName = "DiceOrbit/Potions/Defense Potion")]
    public class DefensePotion : Potion
    {
        [Header("효과")]
        [SerializeField] private int armorAmount = 30;

        private void Reset()
        {
            PotionName = "방어 포션";
            Description = "아군 1명에게 방어도 +30";
            TargetType = PotionTargetType.Ally;
            ShopPrice = 45;
            CombatOnly = true;
        }

        public override bool Use(Unit target = null)
        {
            if (!(target is Character) || !target.IsAlive || target.Stats == null) return false;

            target.Stats.TempArmor += armorAmount;
            CombatNotifier.Notify(target, $"방어도 +{armorAmount}", new Color(0.6f, 0.75f, 1f));
            return true;
        }
    }
}
