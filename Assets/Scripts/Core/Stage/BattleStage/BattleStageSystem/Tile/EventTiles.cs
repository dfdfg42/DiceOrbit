using UnityEngine;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Tile
{
    /// <summary>
    /// 이벤트(조화의 정령/제련소)로 설치되는 타일 5종. 설치는 EventRunState가 전투 시작마다 수행.
    /// 공격 보정은 파이프라인 OnCalculateOutput에서 (IsSimulation 게이트 없음 — 예상 피해 프리뷰에도 반영),
    /// 회복/피해는 파이프라인 컨텍스트 경유 (직접 HP 대입 금지).
    /// 타일 훅(OnTraverse/OnEndTurn)은 Character 전용 — 몬스터에는 미적용 (현 구조).
    /// </summary>
    public class SharpTile : TileAttribute
    {
        public SharpTile(int percent = 10) : base(TileAttributeType.Sharp, percent, -1) { }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (!(context is AttackContext atk)) return;
            if (!(context.SourceUnit is Core.Character c) || c.CurrentTile != Owner) return;
            atk.OutputValue *= 1f + Value / 100f;
        }

        public override string GetDescription() => $"여기서 공격하면 주는 피해 +{Value}%";
    }

    public class DullTile : TileAttribute
    {
        public DullTile(int percent = 10) : base(TileAttributeType.Dull, percent, -1) { }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (!(context is AttackContext atk)) return;
            if (!(context.SourceUnit is Core.Character c) || c.CurrentTile != Owner) return;
            atk.OutputValue *= 1f - Value / 100f;
        }

        public override string GetDescription() => $"여기서 공격하면 주는 피해 -{Value}%";
    }

    /// <summary>통과(마지막 걸음 포함) 및 턴 종료 시 방어도. OnArrive는 쓰지 않는다 —
    /// 이동 루프가 도착 타일에도 OnTraverse를 호출하므로 (Character.cs) 겹치면 이중 지급.</summary>
    public class SturdyTile : TileAttribute
    {
        public SturdyTile(int armor = 10) : base(TileAttributeType.Sturdy, armor, -1) { }

        public override void OnTraverse(Core.Character character) => Grant(character);
        public override void OnEndTurn(Core.Character character) => Grant(character);

        public override void ForecastTraverse(Core.Character character, TileForecast forecast)
            => forecast.Note($"방어도 +{Value}", ForecastTone.Good);
        public override void ForecastEndTurn(Core.Character character, TileForecast forecast)
            => forecast.Note($"방어도 +{Value}", ForecastTone.Good);

        private void Grant(Core.Character character)
        {
            if (character == null || !character.IsAlive || character.Stats == null) return;
            character.Stats.TempArmor += Value;
            DiceOrbit.UI.CombatNotifier.Notify(character, $"방어도 +{Value}", new Color(0.6f, 0.75f, 1f));
        }

        public override string GetDescription() => $"밟으면 방어도 +{Value}";
    }

    public class HarmonyTile : TileAttribute
    {
        public HarmonyTile(int percent = 5) : base(TileAttributeType.Harmony, percent, -1) { }

        public override void OnEndTurn(Core.Character character)
        {
            if (character == null || !character.IsAlive || character.Stats == null) return;
            int amount = Mathf.Max(1, Mathf.RoundToInt(character.Stats.MaxHP * Value / 100f));
            var heal = new HealContext(null, character, "조화", amount);
            CombatPipeline.Instance?.Process(heal);
        }

        public override void ForecastEndTurn(Core.Character character, TileForecast forecast)
        {
            if (character == null || character.Stats == null) return;
            forecast.Note($"체력 +{Mathf.Max(1, Mathf.RoundToInt(character.Stats.MaxHP * Value / 100f))}", ForecastTone.Good);
        }

        public override string GetDescription() => $"최대 체력의 {Value}% 회복\n여기서 턴을 마칠 때";
    }

    public class DisharmonyTile : TileAttribute
    {
        public DisharmonyTile(int percent = 5) : base(TileAttributeType.Disharmony, percent, -1) { }

        public override void OnEndTurn(Core.Character character)
        {
            if (character == null || !character.IsAlive || character.Stats == null) return;
            int amount = Mathf.Max(1, Mathf.RoundToInt(character.Stats.MaxHP * Value / 100f));
            var hit = new AttackContext(null, character, "부조화", amount);
            CombatPipeline.Instance?.Process(hit);
        }

        public override void ForecastEndTurn(Core.Character character, TileForecast forecast)
        {
            if (character == null || character.Stats == null) return;
            int raw = Mathf.Max(1, Mathf.RoundToInt(character.Stats.MaxHP * Value / 100f));
            forecast.Note($"부조화 피해 {ForecastDamage(character, raw)}", ForecastTone.Bad);
        }

        public override string GetDescription() => $"최대 체력의 {Value}% 피해\n여기서 턴을 마칠 때";
    }
}
