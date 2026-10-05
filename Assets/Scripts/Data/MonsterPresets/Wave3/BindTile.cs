using DiceOrbit.Core;
using DiceOrbit.Data;
using UnityEngine;

namespace DiceOrbit.Data.Tile
{
    /// <summary>
    /// 속박 타일 — 캐릭터가 그 위에서 턴을 종료하면 이동 불가(속박) 상태이상을 부여한다. (구 SnowPrisonTileAttribute)
    /// </summary>
    public class BindTileAttribute : TileAttribute
    {
        public BindTileAttribute(TileAttributeType type, int value, int duration, bool isStackable = false)
            : base(type, value, duration, isStackable)
        {
        }

        public override void OnTraverse(Character character)
        {
            // 통과할 때는 효과 없음.
        }

        public override void OnEndTurn(Character character)
        {
            Activate(character);
        }

        public override void ForecastEndTurn(Character character, TileForecast forecast)
            => forecast.Note("속박: 다음 턴 이동 불가", ForecastTone.Bad);

        public void Activate(Character target)
        {
            if (target == null || !target.IsAlive) return;
            target.StatusEffects.AddEffect(new DiceOrbit.Systems.Effects.BindStatus(0, Value));
        }

        public override string GetDescription()
        {
            return "여기서 턴을 마치면 속박\n다음 턴 이동 불가";
        }
    }
}

namespace DiceOrbit.Systems.Effects
{
    /// <summary>
    /// 속박 디버프 — 이동 불가. 적용 시 CharacterStats.BindDebuff++, 만료 시 --. (구 FrozenDebuff)
    /// 이동만 막고 스킬은 허용한다(canMove() 게이트).
    /// </summary>
    public class BindStatus : StatusEffect
    {
        public BindStatus(int value, int duration) : base(EffectType.Bound, value, duration)
        {
            IsStackable = false;
        }

        public override void EffectApplied()
        {
            if (Owner.Stats is CharacterStats c) c.BindDebuff++;
        }

        public override void EffectExpired()
        {
            if (Owner.Stats is CharacterStats c) c.BindDebuff--;
        }
    }
}
