using DiceOrbit.Core;
using DiceOrbit.Core.Run;
using DiceOrbit.Data.Tile;
using UnityEngine;

namespace DiceOrbit.Data.Potions
{
    /// <summary>중화 포션 — 지정 타일의 디버프 속성(지뢰·꿀·눈감옥·뼈)을 제거.</summary>
    [CreateAssetMenu(fileName = "NeutralizePotion", menuName = "DiceOrbit/Potions/Neutralize Potion")]
    public class NeutralizePotion : Potion
    {
        // 파티에게 해로운 타일 속성 (이로운 속성 — Cloud/ScoutHeal/Reagent — 은 남긴다)
        private static readonly TileAttributeType[] DebuffTypes =
        {
            TileAttributeType.RandMine,
            TileAttributeType.Honey,
            TileAttributeType.Bind,
            TileAttributeType.Bone,
            TileAttributeType.Dull,
            TileAttributeType.Disharmony,
        };

        private void Reset()
        {
            PotionName = "중화 포션";
            Description = "타일 1칸의 해로운 효과 모두 제거";
            TargetType = PotionTargetType.Tile;
            ShopPrice = 40;
            CombatOnly = true;
        }

        public override bool Use(Unit target = null) => false;   // 타일 전용

        public override bool UseOnTile(TileData tile)
        {
            if (tile == null) return false;

            bool removed = false;
            foreach (var type in DebuffTypes)
            {
                if (!tile.HasAttribute(type)) continue;
                tile.RemoveAttributeType(type);
                removed = true;
            }

            if (!removed) Debug.Log("[Potion] 중화 — 제거할 디버프가 없는 타일 (소모 안 됨)");
            return removed;
        }
    }
}
