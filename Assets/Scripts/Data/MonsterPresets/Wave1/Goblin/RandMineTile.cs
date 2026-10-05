using DiceOrbit.Core.Pipeline;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace DiceOrbit.Data.Tile
{
    public class RandMineTile : TileAttribute
    {
        public RandMineTile(TileAttributeType type, int value, int duration, bool isStackable = false) : base(type, value, duration, isStackable)
        {

        }

        public override void OnTraverse(Core.Character character)
        {
            Explosion(character);
        }

        public override void OnEndTurn(Core.Character character)
        {
            Explosion(character);
        }

        public override void ForecastTraverse(Core.Character character, TileForecast forecast)
        {
            forecast.Note($"지뢰 피해 {ForecastDamage(character, Value)}", ForecastTone.Bad);
            forecast.MarkConsumed();   // 터지면 사라진다
        }

        public override void ForecastEndTurn(Core.Character character, TileForecast forecast)
            => forecast.Note($"지뢰 피해 {ForecastDamage(character, Value)}", ForecastTone.Bad);

        public void Explosion(Core.Character target)
        {
            if (target == null || !target.IsAlive) return;

            var context = new Core.Pipeline.AttackContext(
                null,
                target,
                "Mine Explosion", Value
            );
            Core.Pipeline.CombatPipeline.Instance?.Process(context);
            Owner.RemoveAttribute(this);
        }

        // 지속 시간은 타일 패널의 제목 줄이 따로 보여 준다
        public override string GetDescription() => $"밟으면 피해 {Value} · 터지면 사라짐";
    }
}
