using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using DiceOrbit.Data.MonsterPresets.Wave2;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Systems.Effects
{
    /// <summary>혈당 스파이크 — 다음 턴 이동 불가. 적용 시 CharacterStats.BindDebuff++, 만료 시 --. (Wave2 곰, BindStatus 패턴)</summary>
    public class BloodSugarSpikeStatus : StatusEffect
    {
        public BloodSugarSpikeStatus(int duration) : base(EffectType.BloodSugarSpike, 0, duration)
        {
            IsStackable = false;
        }

        public override void EffectApplied()
        {
            if (Owner != null && Owner.Stats is CharacterStats c) c.BindDebuff++;
        }

        public override void EffectExpired()
        {
            if (Owner != null && Owner.Stats is CharacterStats c) c.BindDebuff--;
        }
    }
}

namespace DiceOrbit.Data.Tile
{
    /// <summary>
    /// 아기곰 [꿀 묻히기]로 설치되는 꿀 타일. 통과 시:
    ///  1) 그 캐릭터 회복(미끼), 2) 아기 곰·엄마 곰 일시 방어도 +bearArmor([아기 곰은 꿀을 좋아해]),
    ///  3) '먹은 꿀' 카운트, 4) 한 턴에 bindThreshold개 이상 밟으면 혈당 스파이크(다음 턴 이동 불가),
    ///  5) 자기 삭제. 몬스터 사망 후 유지, 웨이브 종료 시 BearPackTracker가 정리.
    /// </summary>
    public class HoneyPawTile : TileAttribute
    {
        private readonly int healAmount;
        private readonly int bindThreshold;
        private readonly int bindDuration;
        private readonly int bearArmor;

        public HoneyPawTile(int healAmount, int bindThreshold, int bindDuration, int bearArmor)
            : base(TileAttributeType.Honey, healAmount, -1, false)
        {
            this.healAmount = healAmount;
            this.bindThreshold = bindThreshold;
            this.bindDuration = bindDuration;
            this.bearArmor = bearArmor;
        }

        public override void OnTraverse(Character character) => Activate(character);

        public override void ForecastTraverse(Character character, TileForecast forecast)
        {
            if (healAmount > 0) forecast.Note($"꿀: 체력 +{healAmount}", ForecastTone.Good);
            if (bearArmor > 0) forecast.Note($"꿀: 곰 방어도 +{bearArmor}", ForecastTone.Bad);
            forecast.MarkConsumed();   // 발동 후 사라진다
        }

        private void Activate(Character target)
        {
            if (target == null || !target.IsAlive) return;

            // 1) 통과 캐릭터 회복 (미끼)
            if (healAmount > 0)
            {
                var heal = new HealContext(null, target, "꿀", healAmount);
                CombatPipeline.Instance?.Process(heal);
            }

            // 2) [아기 곰은 꿀을 좋아해] 아기 곰·엄마 곰에게 일시 방어도 부여
            BearPackTracker.GrantTempArmorToBears(bearArmor);

            // 3) '먹은 꿀' 카운트
            BearPackTracker.RegisterHoneyEaten();

            // 4) 한 턴에 bindThreshold개 이상 밟으면 혈당 스파이크(다음 턴 이동 불가)
            int turn = CombatManager.Instance != null ? CombatManager.Instance.TurnCount : 0;
            int stepped = BearPackTracker.RegisterHoneyStep(target, turn);
            if (stepped >= bindThreshold)
                target.StatusEffects?.AddEffect(new BloodSugarSpikeStatus(bindDuration));

            // 5) 발동 후 삭제
            Owner?.RemoveAttribute(this);
        }

        public override string GetDescription()
            => $"밟으면 체력 {healAmount} 회복 · 발동 후 사라짐\n한 턴에 {bindThreshold}개 밟으면 다음 턴 이동 불가";
    }
}
