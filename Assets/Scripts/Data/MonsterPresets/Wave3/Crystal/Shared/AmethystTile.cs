using DiceOrbit.Core;
using DiceOrbit.Data.MonsterPresets.Wave3.Crystal.Shared;

namespace DiceOrbit.Data.Tile
{
    /// <summary>
    /// [자수정] 타일. 캐릭터가 통과하거나 그 위에서 턴을 종료하면 살아있는 수정 핵에게 수정 중첩 1을 공급한다.
    /// SlimeTile과 달리 발동해도 사라지지 않고 영구 유지된다. 수정 핵 사망 시 지연 제거(다음 발동 때),
    /// 웨이브 종료 시 CrystalSet이 일괄 정리.
    /// </summary>
    public class AmethystTile : TileAttribute
    {
        public AmethystTile() : base(TileAttributeType.Amethyst, 0, -1, false) { }

        public override void OnTraverse(Character character) => Activate();
        public override void OnEndTurn(Character character) => Activate();

        public override void ForecastTraverse(Character character, TileForecast forecast) => ForecastStack(forecast);
        public override void ForecastEndTurn(Character character, TileForecast forecast) => ForecastStack(forecast);

        private static void ForecastStack(TileForecast forecast)
        {
            if (CrystalSet.GetCore() != null) forecast.Note("수정 핵 수정 중첩 +1", ForecastTone.Bad);
        }

        private void Activate()
        {
            var core = CrystalSet.GetCore();
            if (core != null)
                CrystalSet.AddStack(core, 1);
            else
                Owner?.RemoveAttribute(this); // 수정 핵 사망 → 지연 제거
        }

        public override string GetDescription()
            => "밟으면 수정 핵의 수정 중첩 +1";
    }
}
