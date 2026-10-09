using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Core.Zones;
using DiceOrbit.Data.Modifiers.Common;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using UnityEngine;

namespace DiceOrbit.Data.Modifiers.Job
{
    /// <summary>
    /// [고전압] 감전 25 → 40% (출처 무관). 효과는 StatusEnhancement가 감전 계산·부여 때 이 모디파이어를 조회해 낸다(D5) —
    /// 이 클래스 자체엔 훅이 없다. 보유 마법사가 쓰러지면 조회에서 빠져 원래대로 돌아간다.
    /// </summary>
    [System.Serializable]
    public class HighVoltageModifier : JobModifier
    {
        protected override string JobId => JobIds.Mage;
        public override string ModifierName => "고전압";
        public override ModifierFamily Family => ModifierFamily.Combo;
        public override string Description
            => $"감전으로 늘어나는 받는 피해가 25%에서 {StatusEnhancement.EnhancedPercent}%로 강화됩니다. 누가 부여한 감전이든 적용되며, 이 마법사가 쓰러지면 원래대로 돌아갑니다.";
    }

    /// <summary>
    /// [마법의 땅] 전투 시작 시 무작위 구역 1개의 모든 타일에 마법의 땅 설치. 마법사가 그 타일에서 공격하면 피해 +15%(다단·광역 전체).
    /// 설치: 주인의 CombatStart 방송(OnPreAction 1회, 살아 있는 파티원에게만 방송됨). 삭제: 설치 직전 자기 것 + CombatManager.EndCombat.
    /// 보정: 주인의 공격 계산(IsOwnAttackCalculation)에서 주인 CurrentTile이 마법의 땅이면 ×1.15 — 행동 예고는 PretendAt으로 도착 타일을 본다.
    /// </summary>
    [System.Serializable]
    public class MagicLandModifier : JobModifier
    {
        public const float Percent = 15f;

        protected override string JobId => JobIds.Mage;
        public override string ModifierName => "마법의 땅";
        public override ModifierFamily Family => ModifierFamily.Position;
        public override string Description
            => $"전투 시작 시 무작위 구역 하나의 모든 타일에 마법의 땅을 설치합니다. 마법의 땅 위에서 공격하면 피해가 {Percent:0.#}% 증가하며, 마법의 땅은 전투가 끝나면 사라집니다.";

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null) return;
            if (context.Phase != EventPhase.CombatStart) return;
            if (trigger != CombatTrigger.OnPreAction) return;   // 방송 1회당 1번만
            if (context.SourceUnit != owner || context.IsSimulation) return;

            Install();
        }

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (!IsOwnAttackCalculation(trigger, context)) return;
            if (!IsOnMagicLand(owner)) return;

            context.OutputValue *= 1f + Percent / 100f;
        }

        /// <summary>캐릭터가 지금(예고 중이면 가정한 타일에) 마법의 땅 위에 서 있나.</summary>
        public static bool IsOnMagicLand(Character character)
        {
            if (character == null) return false;
            var tile = character.CurrentTile;
            return tile != null && tile.HasAttribute(TileAttributeType.MagicLand);
        }

        private void Install()
        {
            MagicLandTile.ClearFrom(this);   // 이전 전투 잔여 정리 (EndCombat을 거치지 않은 시작 경로 대비)

            var zones = CombatZoneManager.Instance;
            if (zones == null) return;

            int zone = Random.Range(0, zones.ZoneCount);
            var tiles = zones.GetTilesInZone(zone);
            if (tiles == null) return;

            foreach (var tile in tiles)
                if (tile != null) tile.AddAttribute(new MagicLandTile(this));
        }
    }
}
