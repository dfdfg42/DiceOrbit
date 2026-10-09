using System.Collections.Generic;
using DiceOrbit.Core;
using UnityEngine;

namespace DiceOrbit.Systems.Effects
{
    /// <summary>
    /// '공격 행동' 경계 (2026-08-28). 도적 5연타·마법사 광역처럼 타격(AttackContext)이 여러 번 발생해도
    /// Begin~End 사이는 하나의 행동이다. 촉매 같은 1회성 상태는 행동 안의 모든 타격에
    /// 적용된 뒤 End()에서 일괄 제거된다 — 첫 타격에서 지워져 나머지 타격이 혜택을 못 받는 문제를 막는다.
    ///
    /// 스코프 밖에서 처리되는 지연 컨텍스트(발사체 등)는 OneShotActionStatus가 자체 폴백으로
    /// 해당 컨텍스트 종료 시 스스로를 제거한다.
    /// </summary>
    /// <summary>
    /// 현재 공격 행동의 실행 정보 — 공용 모디파이어(대단원·충전 소비 등)가 조건 판정에 읽는다.
    /// 캐릭터 자동공격만 채우고, 몬스터 행동은 기본값(IsAutoAttack=false)으로 남는다.
    /// </summary>
    public struct AttackActionInfo
    {
        /// <summary>이 행동에 사용한 주사위 눈 (해당 없음 = 0).</summary>
        public int DiceValue;
        /// <summary>콤보 단계 (0~2). 기본공격/해당 없음 = -1.</summary>
        public int ComboStage;
        /// <summary>이동 시작 구역과 도착 구역이 달랐는가.</summary>
        public bool CrossedZone;
        /// <summary>캐릭터 자동공격 행동인가 (기본/강화 불문).</summary>
        public bool IsAutoAttack;
    }

    public static class AttackActionScope
    {
        /// <summary>현재 행동 ID. 0이면 스코프 없음.</summary>
        public static int CurrentActionId { get; private set; }

        /// <summary>현재 행동의 주체 (자동공격 캐릭터 또는 행동 중인 몬스터).</summary>
        public static Unit CurrentSource { get; private set; }

        /// <summary>현재 행동의 실행 정보 (주사위/콤보 단계/구역 이동 여부).</summary>
        public static AttackActionInfo CurrentInfo { get; private set; }

        public static bool IsActive => CurrentActionId != 0;

        private static int _nextId = 1;
        private static readonly List<(StatusEffect effect, Unit owner)> _consumed
            = new List<(StatusEffect, Unit)>();

        public static void Begin(Unit source)
            => Begin(source, new AttackActionInfo { ComboStage = -1 });

        public static void Begin(Unit source, AttackActionInfo info)
        {
            if (IsActive)
            {
                Debug.LogError($"[AttackActionScope] Begin 중첩 — 이전 행동(id {CurrentActionId})을 강제 종료한다. 호출 순서를 확인할 것.");
                EndInternal();
            }
            CurrentActionId = _nextId++;
            CurrentSource = source;
            CurrentInfo = info;
        }

        /// <summary>이 행동에서 소비된 1회성 상태 등록. End()에서 소유자에게서 제거된다. 중복 등록 무해.</summary>
        public static void MarkConsumed(StatusEffect effect, Unit owner)
        {
            if (effect == null || owner == null) return;
            foreach (var entry in _consumed)
                if (entry.effect == effect) return;
            _consumed.Add((effect, owner));
        }

        /// <summary>
        /// 예고 전용 스코프 (행동 예고 2026-10-04) — using 블록 동안 Current*를 시뮬레이션 값으로 바꾸고,
        /// 끝나면 이전 값(진행 중인 실제 행동 포함)을 그대로 되돌린다. 소비 목록은 건드리지 않는다 —
        /// 시뮬레이션 컨텍스트(IsSimulation)에서는 1회성 상태가 MarkConsumed를 부르지 않는다.
        /// </summary>
        public static SimulationScope Simulate(Unit source, AttackActionInfo info) => new SimulationScope(source, info);

        public readonly struct SimulationScope : System.IDisposable
        {
            private const int SimulationActionId = int.MinValue;   // 실제 행동 ID(1부터 증가)와 겹치지 않는다

            private readonly int _previousId;
            private readonly Unit _previousSource;
            private readonly AttackActionInfo _previousInfo;

            public SimulationScope(Unit source, AttackActionInfo info)
            {
                _previousId = CurrentActionId;
                _previousSource = CurrentSource;
                _previousInfo = CurrentInfo;

                CurrentActionId = SimulationActionId;
                CurrentSource = source;
                CurrentInfo = info;
            }

            public void Dispose()
            {
                CurrentActionId = _previousId;
                CurrentSource = _previousSource;
                CurrentInfo = _previousInfo;
            }
        }

        public static void End()
        {
            if (!IsActive) return;
            EndInternal();
        }

        private static void EndInternal()
        {
            // 제거가 또 다른 리액션을 부를 수 있으므로 스코프 상태를 먼저 닫고 복사본으로 제거한다.
            var pending = new List<(StatusEffect effect, Unit owner)>(_consumed);
            _consumed.Clear();
            CurrentActionId = 0;
            CurrentSource = null;
            CurrentInfo = default;

            foreach (var (effect, owner) in pending)
                if (owner != null) owner.StatusEffects?.RemoveEffect(effect.Type);
        }

        /// <summary>전투 시작/종료 시 잔여 상태 정리 (스코프가 열린 채 전투가 끝난 경우 대비).</summary>
        public static void ResetAll()
        {
            _consumed.Clear();
            CurrentActionId = 0;
            CurrentSource = null;
            CurrentInfo = default;
        }
    }
}
