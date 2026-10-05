using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.Tile
{
    /// <summary>
    /// 불꽃 타일. 설치원 무관 동일 효과:
    /// - OnEndTurn: 그 위 캐릭터에 damage 피해(파이프라인 — 방어도/디버프 적용).
    /// - OnTraverse: 캐릭터가 이번 턴 아직 불을 안 껐으면(소화 마커 없음) 이 타일 삭제 + 마커 부여(1턴).
    /// 타일당 1개(AddAttribute가 Type 중복 무시), 영구(duration -1, 몬스터 사망해도 잔존).
    /// </summary>
    public class FireTile : TileAttribute
    {
        public FireTile(int damage = 20) : base(TileAttributeType.Flame, damage, -1) { }

        public override void OnEndTurn(Core.Character character)
        {
            if (character == null || !character.IsAlive) return;
            var ctx = new AttackContext(null, character, "불꽃", Value);
            CombatPipeline.Instance?.Process(ctx);
        }

        public override void OnTraverse(Core.Character character)
        {
            if (character == null || !character.IsAlive || character.StatusEffects == null) return;
            if (character.StatusEffects.HasEffect(EffectType.FireExtinguishMark)) return; // 이번 턴 이미 1개 소화함
            Owner?.RemoveAttribute(this);
            character.StatusEffects.AddEffect(new FireExtinguishMarkStatus());
        }

        /// <summary>지나가면 꺼진다 — 단, 한 턴에 하나만. 이미 껐거나 이 경로의 앞선 타일에서 끄기로 됐으면 그대로 남는다.</summary>
        public override void ForecastTraverse(Core.Character character, TileForecast forecast)
        {
            bool alreadyExtinguished = character != null && character.StatusEffects != null
                && character.StatusEffects.HasEffect(EffectType.FireExtinguishMark);
            if (alreadyExtinguished || forecast.HasPendingStatus(EffectType.FireExtinguishMark)) return;

            forecast.GrantStatus(EffectType.FireExtinguishMark, 0);
            forecast.Note("불꽃 끄기", ForecastTone.Good);
            forecast.MarkConsumed();
        }

        public override void ForecastEndTurn(Core.Character character, TileForecast forecast)
            => forecast.Note($"불꽃 피해 {ForecastDamage(character, Value)}", ForecastTone.Bad);

        public override string GetDescription() => $"여기서 턴을 마치면 피해 {Value}\n지나가면 꺼짐 · 한 턴에 하나만";
    }
}

namespace DiceOrbit.Systems.Effects
{
    /// <summary>불꽃 소화 마커: 존재 여부만 사용(전투 효과 없음). 지속 1턴 → 다음 턴 자동 소멸.</summary>
    public class FireExtinguishMarkStatus : StatusEffect
    {
        public FireExtinguishMarkStatus() : base(DiceOrbit.Data.EffectType.FireExtinguishMark, 0, 1) { }
    }

    /// <summary>
    /// 불꽃 요정용: 이번 턴 받은 누적 피해. 소유자가 피격될 때(OnCalculateOutput, Target==Owner) 누적,
    /// 소유자 턴 종료 시 0으로 리셋. 영구(-1)라 만료되지 않음.
    /// </summary>
    public class FireDamageTakenStatus : StatusEffect
    {
        public FireDamageTakenStatus() : base(DiceOrbit.Data.EffectType.FireDamageTaken, 0, -1) { }

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (Owner == null) return;
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.Target != Owner) return;
            Value += Mathf.Max(0, Mathf.RoundToInt(context.OutputValue));
        }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            // 소유자 턴 종료(실행 이후)에 이번 턴 누적을 0으로. Duration -1이라 base 감소 무영향.
            if (Owner != null && context.Phase == EventPhase.TurnEnd && context.SourceUnit == Owner)
                Value = 0;
        }
    }

    /// <summary>
    /// 불의 가호: 소유자(불꽃 소녀)가 방어도>0일 때 받는 피해 Value% 감소. 인형이 매턴 1턴짜리로 갱신.
    /// </summary>
    public class FlameGuardStatus : StatusEffect
    {
        public FlameGuardStatus(int percent, int duration) : base(DiceOrbit.Data.EffectType.FireGuard, percent, duration) { }

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (Owner == null || Owner.Stats == null) return;
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.Target != Owner) return;
            if (Owner.Stats.TempArmor <= 0) return; // 방어도 게이트
            context.OutputValue *= 1f - Value / 100f;
        }
    }
}
