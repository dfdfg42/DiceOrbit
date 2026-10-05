using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Run;

namespace DiceOrbit.Data.Potions
{
    [CreateAssetMenu(fileName = "NewHealPotion", menuName = "DiceOrbit/Potions/Heal Potion")]
    public class HealPotion : Potion
    {
        [Header("회복 설정")]
        public int healAmount = 10;

        private void Reset()
        {
            PotionName = "회복 포션";
            Description = "아군 1명 체력 10 회복";
            TargetType = PotionTargetType.Ally; // 아군만 타겟팅
            ShopPrice = 50;
        }

        public override bool Use(Unit target = null)
        {
            // 타겟이 아군(Character)인지 검증 후 파이프라인 경유 회복
            // (직접 HP 대입 금지 — 힐 알림/VFX/유물·패시브 반응이 전부 파이프라인에 달려 있음)
            if (target is Character character && character.IsAlive && character.Stats != null)
            {
                var heal = new DiceOrbit.Core.Pipeline.HealContext(null, character, PotionName, healAmount);
                DiceOrbit.Core.Pipeline.CombatPipeline.Instance?.Process(heal);
                return true;
            }

            return false; // 조건 불충족으로 사용 취소
        }
    }
}
