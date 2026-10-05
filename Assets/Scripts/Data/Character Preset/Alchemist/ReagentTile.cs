using DiceOrbit.Data.Passives;

namespace DiceOrbit.Data.Tile
{
    /// <summary>
    /// 연금술사 [시약 준비]로 설치되는 영구 시약 타일 (2026-08-28 개편).
    /// 살아 있는 아군 누구든(연금술사 포함) 지나가거나 도착하면 촉매(다음 자동공격 행동 +%)를 얻는다.
    /// - 타일은 소모되지 않고 전투가 끝날 때까지 유지된다 (발동 후에도 재사용 가능).
    /// - 가만히 타일 위에서 턴을 끝내는 것으로는 발동하지 않는다 (OnEndTurn 훅 없음).
    /// - 한 번의 이동에서 같은 타일은 한 번만 밟히므로(경로가 타일을 재방문하지 않음) 중복 발동이 없다.
    /// - 촉매는 비중첩 — 이미 가진 채 다시 밟으면 갱신만 된다 (StatusEffectManager.AddEffect).
    /// </summary>
    public class ReagentTile : TileAttribute
    {
        private readonly ReagentPrepPassive ownerPassive;

        public ReagentTile(ReagentPrepPassive passive)
            : base(TileAttributeType.Reagent, 0, -1, false)
        {
            ownerPassive = passive;
        }

        public override void OnTraverse(Core.Character character) => TryCatalyze(character);

        /// <summary>촉매는 이번 자동공격에 바로 적용된다 — 예고의 피해 수치에도 들어가도록 상태 획득을 알린다.</summary>
        public override void ForecastTraverse(Core.Character character, TileForecast forecast)
        {
            int percent = ownerPassive != null ? ownerPassive.CatalystPercent : 25;
            forecast.GrantStatus(EffectType.Catalyst, percent);
            forecast.Note($"촉매 획득 (이번 공격 피해 +{percent}%)", ForecastTone.Good);
        }

        private void TryCatalyze(Core.Character character)
        {
            if (character == null || !character.IsAlive || character.StatusEffects == null) return;

            int percent = ownerPassive != null ? ownerPassive.CatalystPercent : 25;
            character.StatusEffects.AddEffect(
                Systems.Effects.StatusEffectManager.CreateEffect(EffectType.Catalyst, percent, -1));

            var data = UI.TooltipKeywordFormatter.BuildStatusDisplayData(EffectType.Catalyst.ToString(), percent, -1);
            UI.CombatNotifier.NotifyStatus(character, data.Name, data.Color);
            // 타일은 제거하지 않는다 — 영구 유지가 이 타일의 정체성이다.
        }

        public override string GetDescription()
        {
            int percent = ownerPassive != null ? ownerPassive.CatalystPercent : 25;
            return $"아군이 밟으면 촉매 · 소모 안 됨\n다음 자동공격 피해 +{percent}%";
        }
    }
}
