using DiceOrbit.Core;
using DiceOrbit.Core.Run;
using UnityEngine;

namespace DiceOrbit.Data.Potions
{
    /// <summary>랜덤 포션 — 직접 사용 불가. 매 전투 시작 시 무작위 일반 포션으로 변신
    /// (변신 로직은 PotionManager.RerollRandomPotions — OnCombatStart 구독).</summary>
    [CreateAssetMenu(fileName = "RandomPotion", menuName = "DiceOrbit/Potions/Random Potion")]
    public class RandomPotion : Potion
    {
        private void Reset()
        {
            PotionName = "랜덤 포션";
            Description = "전투 시작마다 무작위 일반 포션으로 변함";
            TargetType = PotionTargetType.None;
            ShopPrice = 30;
            CombatOnly = false;
        }

        public override bool Use(Unit target = null)
        {
            Debug.Log("[Potion] 랜덤 포션은 직접 사용할 수 없습니다 — 전투 시작 시 변신합니다.");
            return false;
        }
    }
}
