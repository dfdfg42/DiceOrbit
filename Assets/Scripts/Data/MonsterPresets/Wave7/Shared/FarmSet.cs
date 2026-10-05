using UnityEngine;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using DiceOrbit.Data.Tile;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.MonsterPresets.Wave7.Shared
{
    /// <summary>
    /// 농장 세트 공유 로직(순수 함수 + 웨이브 훅).
    /// - 활력 스택: 식물 몬스터(식인 식물/화난 버섯)에 붙는 <see cref="VitalityStatus"/>로 저장 → UI 가시화.
    /// - 활력 타일 정리: 웨이브 종료(=다음 전투 시작) 시 모든 타일의 활력 속성 제거.
    /// - 아군 사망 감지: 살아있는 몬스터 수 최대치 대비 감소로 판정.
    /// </summary>
    public static class FarmSet
    {
        private static CombatManager hookedManager;
        private static int peakAlive;

        /// <summary>활력 스택을 가진 대상의 현재 활력. 없으면 0.</summary>
        public static int GetVitality(Unit u)
            => u != null && u.StatusEffects != null ? u.StatusEffects.GetEffectValue(EffectType.VitalityStack) : 0;

        /// <summary>현재 필드에 깔린 활력 타일 수 ([성장의 활력] 턴 시작 회복량).</summary>
        public static int CountVitalityTiles()
        {
            var orbit = GameManager.Instance != null ? GameManager.Instance.GetOrbitManager() : null;
            if (orbit == null || orbit.Tiles == null) return 0;
            return orbit.Tiles.Count(t => t != null && t.HasAttribute(TileAttributeType.Vitality));
        }

        /// <summary>대상에 활력 스택이 없으면 initial로 최초 부여.</summary>
        public static void EnsureVitality(Unit u, int initial)
        {
            if (u == null || u.StatusEffects == null) return;
            if (!u.StatusEffects.HasEffect(EffectType.VitalityStack))
                u.StatusEffects.AddEffect(new VitalityStatus(initial));
        }

        /// <summary>활력 스택을 가진 모든 살아있는 몬스터의 활력을 delta만큼 조정(음수=감소). 0 미만은 0으로 클램프.</summary>
        public static void ChangeVitalityAll(int delta)
        {
            var cm = CombatManager.Instance;
            if (cm == null || delta == 0) return;
            var monsters = cm.GetAliveMonsters();
            if (monsters == null) return;
            foreach (var m in monsters)
            {
                if (m == null || m.StatusEffects == null || !m.StatusEffects.HasEffect(EffectType.VitalityStack)) continue;
                m.StatusEffects.AddEffect(new VitalityStatus(delta)); // AddStack이 Value += delta (부호 합산)
                var eff = m.StatusEffects.GetActiveEffects().FirstOrDefault(e => e.Type == EffectType.VitalityStack);
                if (eff != null && eff.Value < 0) eff.Value = 0;
            }
        }

        /// <summary>현재 체력이 가장 낮은 살아있는 몬스터(아군). 없으면 null.</summary>
        public static Monster FindLowestHpAlly(Monster self)
        {
            var cm = CombatManager.Instance;
            if (cm == null) return null;
            var monsters = cm.GetAliveMonsters();
            if (monsters == null) return null;
            return monsters.Where(m => m != null && m.IsAlive && m.Stats != null)
                           .OrderBy(m => m.Stats.CurrentHP)
                           .FirstOrDefault();
        }

        /// <summary>이번 전투에서 최대 생존 수 대비 현재 생존 수가 줄면(=아군 하나라도 사망) true.</summary>
        public static bool HasAnyAllyDied()
        {
            var cm = CombatManager.Instance;
            if (cm == null) return false;
            var monsters = cm.GetAliveMonsters();
            int alive = monsters != null ? monsters.Count : 0;
            if (alive > peakAlive) peakAlive = alive;
            return alive < peakAlive;
        }

        public static void EnsureWaveHook()
        {
            var cm = CombatManager.Instance;
            if (cm == null || hookedManager == cm) return;
            if (hookedManager != null) hookedManager.OnCombatStart -= OnCombatStart;
            cm.OnCombatStart += OnCombatStart;
            hookedManager = cm;
        }

        private static void OnCombatStart()
        {
            peakAlive = 0;
            // 활력 스택은 몬스터 인스턴스의 상태라 새 전투에선 자동 소멸. 활력 타일만 정리한다.
            var orbit = GameManager.Instance != null ? GameManager.Instance.GetOrbitManager() : null;
            if (orbit == null || orbit.Tiles == null) return;
            foreach (var tile in orbit.Tiles)
                if (tile != null) tile.RemoveAttributeType(TileAttributeType.Vitality);
        }
    }
}

namespace DiceOrbit.Systems.Effects
{
    /// <summary>[활력] 식물 몬스터의 활력 스택(가시화). 중첩 누적(IsStackable), 영구(-1).
    /// 활력 ≤ FragileThreshold(7)이면 소유자가 받는 피해를 FragilePercent(20)% 증가.</summary>
    public class VitalityStatus : StatusEffect
    {
        private const int FragileThreshold = 7;
        private const int FragilePercent = 20;

        public VitalityStatus(int value) : base(DiceOrbit.Data.EffectType.VitalityStack, value, -1, isStackable: true) { }

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (Owner == null) return;
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.Target != Owner) return;
            if (Value > FragileThreshold) return;
            context.OutputValue *= 1f + FragilePercent / 100f;
        }
    }
}

namespace DiceOrbit.Data.Tile
{
    /// <summary>[활력] 타일. 캐릭터에겐 효과 없음(식물의 회복 자원). 그 위에서 턴을 종료하면 이 타일만 삭제된다
    /// (플레이어가 밟아 없앨 수 있음). 영구 유지(몬스터 사망해도 잔존), 웨이브 종료 시 FarmSet이 일괄 정리.</summary>
    public class VitalityTile : TileAttribute
    {
        public VitalityTile() : base(TileAttributeType.Vitality, 0, -1, false) { }

        // 효과 없음. 그 위에서 턴 종료 시 이 타일만 삭제.
        public override void OnEndTurn(Core.Character character) => Owner?.RemoveAttribute(this);

        public override void ForecastEndTurn(Core.Character character, TileForecast forecast)
            => forecast.Note("활력 타일 제거", ForecastTone.Good);

        public override string GetDescription()
            => "식물 몬스터의 회복 자원\n여기서 턴을 마치면 사라짐";
    }
}
