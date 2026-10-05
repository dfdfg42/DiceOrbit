using DiceOrbit.Core;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.Tile
{
    /// <summary>
    /// [뼈 무덤]으로 설치되는 뼈 타일. 캐릭터가 지나가거나 그 위에서 턴을 종료하면
    /// 지정된 해골 병사(beneficiary)에게 일시 방어도를 부여한다. 타일은 영구 유지되며
    /// 해골 병사 사망 시 SkeletonDeath가 제거한다.
    /// </summary>
    public class BoneTile : TileAttribute
    {
        private Monster beneficiary;

        public BoneTile(TileAttributeType type, int value, int duration, bool isStackable = false)
            : base(type, value, duration, isStackable)
        {
        }

        public BoneTile(TileAttributeType type, int value, int duration, Monster beneficiary, bool isStackable = false)
            : base(type, value, duration, isStackable)
        {
            this.beneficiary = beneficiary;
        }

        public void SetBeneficiary(Monster monster) => beneficiary = monster;

        public override void OnTraverse(Core.Character character) => Activate();

        public override void OnEndTurn(Core.Character character) => Activate();

        public override void ForecastTraverse(Core.Character character, TileForecast forecast) => ForecastArmor(forecast);
        public override void ForecastEndTurn(Core.Character character, TileForecast forecast) => ForecastArmor(forecast);

        private void ForecastArmor(TileForecast forecast)
        {
            if (beneficiary != null && beneficiary.IsAlive)
                forecast.Note($"해골 병사 방어도 +{Value}", ForecastTone.Bad);
        }

        private void Activate()
        {
            // 지정된 해골 병사에게만 방어도 부여 (살아있을 때) + 이번 라운드 발동 마커
            if (beneficiary != null && beneficiary.IsAlive)
            {
                beneficiary.Stats.TempArmor += Value;
                beneficiary.StatusEffects?.AddEffect(new BoneMarkStatus());
            }
        }

        // 지속 시간은 타일 패널의 제목 줄이 따로 보여 준다
        public override string GetDescription() => $"밟으면 해골 병사 방어도 +{Value}";
    }
}
