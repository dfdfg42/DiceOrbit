using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Tile
{
    /// <summary>
    /// 시작(0번) 타일의 치유 속성 — 지나가면 소량 회복.
    /// 구 레벨업 타일(treavse_LevelUP)을 대체한다 (성장은 웨이브 클리어 보상 모디파이어로 일원화, 기획 REV05).
    /// TileAttributeType.ScoutHeal을 재사용 (삭제된 정찰병 캐릭터의 고아 멤버 — enum 순서 보존).
    /// </summary>
    public class StartHealTile : TileAttribute
    {
        public StartHealTile(TileAttributeType type, int value, int duration, bool isStackable = false)
            : base(type, value, duration, isStackable)
        {
        }

        public override void OnTraverse(Core.Character character)
        {
            if (character == null || !character.IsAlive) return;

            // 파이프라인 경유 힐 (패시브/모디파이어 반응 + 기본 힐 VFX 일관성)
            var context = new HealContext(null, character, "치유 타일", Value);
            CombatPipeline.Instance?.Process(context);
        }

        public override void ForecastTraverse(Core.Character character, TileForecast forecast)
            => forecast.Note($"체력 +{Value}", ForecastTone.Good);

        public override string GetDisplayName() => "치유 타일";

        public override string GetDescription() => $"밟으면 체력 {Value} 회복";
    }
}
