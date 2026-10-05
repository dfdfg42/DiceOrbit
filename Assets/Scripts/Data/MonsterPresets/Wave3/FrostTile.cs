using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using UnityEngine;

namespace DiceOrbit.Systems.Effects
{
    /// <summary>
    /// 빙결 중첩 — 스택형 DoT. 대상 캐릭터 턴 종료 시: 중첩만큼 피해(파이프라인) → 중첩 −1 → 0이면 제거.
    /// 눈사람 피격(+1)·빙결 타일(+5)로 누적. 칩에 숫자 표시. (Wave3 눈)
    /// </summary>
    public class FrostStackStatus : StatusEffect
    {
        public FrostStackStatus(int stacks) : base(EffectType.FrostStack, stacks, -1, isStackable: true) { }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            // 대상 캐릭터 턴 종료 시 틱.
            if (Owner != null
                && context.Phase == EventPhase.TurnEnd
                && trigger == CombatTrigger.OnPostAction
                && context.SourceUnit == Owner
                && !context.IsSimulation
                && Value > 0)
            {
                var tick = new AttackContext(null, Owner, "빙결", Value);
                CombatPipeline.Instance?.Process(tick);
                Value--;
                if (Value <= 0) Owner.StatusEffects?.RemoveEffect(EffectType.FrostStack);
            }

            base.OnTurnEvent(trigger, context); // 지속시간(-1 영구) — 제거는 Value 기준
        }
    }
}

namespace DiceOrbit.Data.Tile
{
    /// <summary>
    /// [빙결] 타일 — 그 위에서 턴을 종료한 캐릭터에게 빙결 중첩 stacks를 부여하고 자기 자신을 제거한다(SlimeTile 패턴).
    /// 중첩 가능, 몬스터 사망 후에도 유지, 웨이브 종료 시 SnowSet이 정리.
    /// </summary>
    public class FrostTile : TileAttribute
    {
        private readonly int stacks;

        public FrostTile(int stacks) : base(TileAttributeType.Frost, stacks, -1, false)
        {
            this.stacks = stacks;
        }

        public override void OnEndTurn(Character character) => Activate(character);

        public override void ForecastEndTurn(Character character, TileForecast forecast)
            => forecast.Note($"빙결 {stacks}중첩", ForecastTone.Bad);

        private void Activate(Character target)
        {
            if (target == null || !target.IsAlive || target.StatusEffects == null) return;
            target.StatusEffects.AddEffect(new DiceOrbit.Systems.Effects.FrostStackStatus(stacks));
            Owner?.RemoveAttribute(this);
        }

        public override string GetDescription()
            => $"여기서 턴을 마치면 빙결 {stacks}중첩\n발동 후 사라짐";
    }
}
