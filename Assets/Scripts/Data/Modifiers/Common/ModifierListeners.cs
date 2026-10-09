using System.Collections.Generic;
using DiceOrbit.Core;

namespace DiceOrbit.Data.Modifiers.Common
{
    // 모디파이어가 받는 자동공격 사건들 (2026-10-09 B, 스펙 §3.4).
    // AutoAttackSystem이 캐릭터의 장착 모디파이어 중 해당 인터페이스를 구현한 것에 직접 통지한다 —
    // 인터페이스 기본 메서드(DIM)에 기대지 않는다 (2026-07-21 죽은 훅 사고 재발 방지).

    /// <summary>이동을 마친 직후(공격 전) — 출발 구역·도착 구역 (기동 장갑). 구역 판정 불가면 -1.</summary>
    public interface IMoveFinishedListener
    {
        void OnMoveFinished(Character character, int fromZone, int toZone);
    }

    /// <summary>사용한 주사위가 강화 공격 조건을 못 맞춘 판정 직후 — 기본공격 실행 여부와 무관 (예비 충전).</summary>
    public interface IComboGateFailedListener
    {
        void OnComboGateFailed(Character character);
    }

    /// <summary>공격 행동(기본/강화)이 끝난 뒤, 스코프 종료 후 (대단원·독 바른 무기·공방 일체·사냥).</summary>
    public interface IAttackActionFinishedListener
    {
        void OnAttackActionFinished(Character character, AttackActionResult result);
    }

    /// <summary>이동+공격 처리 전체가 끝난 뒤 — 공격이 없었어도 (구조대).</summary>
    public interface IActionFinishedListener
    {
        void OnActionFinished(Character character);
    }

    /// <summary>공격 행동 1회의 결과.</summary>
    public readonly struct AttackActionResult
    {
        /// <summary>강화 공격 단계 (0~2). 기본공격 = -1.</summary>
        public readonly int ComboStage;

        /// <summary>이 행동에서 실제로 타격을 시도한 대상들 (시도 시점에 살아 있던 대상, 중복 없음).</summary>
        public readonly IReadOnlyList<Unit> Targets;

        /// <summary>이 행동으로 처치한 수 — 스코프 종료 직후 기준 (마무리 효과·중독 틱 처치 제외).</summary>
        public readonly int Kills;

        public bool IsEmpowered => ComboStage >= 0;

        public AttackActionResult(int comboStage, IReadOnlyList<Unit> targets, int kills)
        {
            ComboStage = comboStage;
            Targets = targets ?? System.Array.Empty<Unit>();
            Kills = kills;
        }
    }
}
