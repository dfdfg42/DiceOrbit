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

    /// <summary>
    /// [회로 공명] 아군이 있는 구역(마법사 구역 포함, 같은 구역 여러 명은 1개)이 3개 이상이면 자신의 강화 공격 피해 +15%.
    /// 구역 수 = MageCircuit.GetCircuitZones(마력 회로와 같은 계산) — 질의 시점 계산이라 이동·사망이 즉시 반영된다.
    /// </summary>
    [System.Serializable]
    public class CircuitResonanceModifier : JobModifier
    {
        public const float Percent = 15f;
        public const int RequiredZones = 3;

        protected override string JobId => JobIds.Mage;
        public override string ModifierName => "회로 공명";
        public override ModifierFamily Family => ModifierFamily.Position;
        public override string Description
            => $"아군이 있는 구역(마법사 구역 포함)이 {RequiredZones}개 이상이면 강화 공격의 피해가 {Percent:0.#}% 증가합니다.";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (!IsOwnEmpoweredCalculation(trigger, context)) return;
            if (!Qualifies(MageCircuit.GetCircuitZones(owner).Count)) return;

            context.OutputValue *= 1f + Percent / 100f;
        }

        /// <summary>회로 구역 수가 조건을 만족하나 (순수 — 자가 테스트 대상).</summary>
        public static bool Qualifies(int circuitZoneCount) => circuitZoneCount >= RequiredZones;
    }

    /// <summary>
    /// [마나흡수] 3단계 강화 공격 피해 +15%. 그 공격의 충전 1 소비 후, 마법사가 그 공격으로 직접 처치한 몬스터 수만큼 충전 획득(최대 3).
    /// - "소비 후 획득": AutoAttackSystem이 ChargeStatus.ConsumeOne을 IAttackActionFinishedListener 통지보다 먼저 부른다(B).
    /// - 처치 수 = AttackActionResult.Kills(스코프 종료 직후 기준) — 디버프·유물 처치는 들어가지 않고, 소급도 없다.
    /// </summary>
    [System.Serializable]
    public class ManaAbsorbModifier : JobModifier, IAttackActionFinishedListener
    {
        public const float Percent = 15f;
        public const int MaxChargeGain = 3;

        protected override string JobId => JobIds.Mage;
        public override string ModifierName => "마나흡수";
        public override ModifierFamily Family => ModifierFamily.Combo;
        public override string Description
            => $"3단계 강화 공격의 피해가 {Percent:0.#}% 증가합니다. 그 공격으로 처치한 몬스터 1명마다 충전을 1 얻습니다(최대 {MaxChargeGain}).";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (!IsOwnStage3Calculation(trigger, context)) return;
            context.OutputValue *= 1f + Percent / 100f;
        }

        public void OnAttackActionFinished(Character character, AttackActionResult result)
        {
            if (owner == null || character != owner || !owner.IsAlive) return;
            if (result.ComboStage != 2) return;

            int gain = ChargeGainFor(result.Kills);
            if (gain > 0) GrantCharge(gain);
        }

        /// <summary>처치 수 → 충전 획득량 (최대 3, 순수 — 자가 테스트 대상). 충전 상한 3은 ChargeStatus.Grant가 따로 보장한다.</summary>
        public static int ChargeGainFor(int kills) => Mathf.Clamp(kills, 0, MaxChargeGain);
    }
}
