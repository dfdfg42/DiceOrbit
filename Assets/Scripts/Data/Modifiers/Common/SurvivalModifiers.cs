using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.Modifiers.Common
{
    /// <summary>[공방 일체] 공격 행동(기본/강화)을 마친 뒤 방어도 3.</summary>
    [System.Serializable]
    public class OffenseDefenseModifier : CommonModifier, IAttackActionFinishedListener
    {
        private const int Armor = 3;

        public override string ModifierName => "공방 일체";
        public override ModifierFamily Family => ModifierFamily.Survival;
        public override string Description
            => $"공격을 마친 뒤 방어도를 {Armor}만큼 얻습니다.";

        public void OnAttackActionFinished(Character character, AttackActionResult result)
        {
            if (character != owner || result.Targets.Count == 0) return;
            GrantArmor(Armor);
        }
    }
}
