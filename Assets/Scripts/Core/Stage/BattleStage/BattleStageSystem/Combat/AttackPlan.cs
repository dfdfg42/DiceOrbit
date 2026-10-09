using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.Core
{
    public enum AttackPlanKind
    {
        /// <summary>기본공격 (주사위가 강화 공격 조건을 못 맞췄다). 표적이 없으면 Targets가 비어 있다.</summary>
        Basic,
        /// <summary>현재 콤보 단계의 강화 공격.</summary>
        Combo,
        /// <summary>조건은 맞았지만 때릴 표적이 없다 — 공격이 나가지 않고 콤보가 초기화된다.</summary>
        ComboNoTarget,
    }

    /// <summary>
    /// 자동공격 1회의 계획 — 무엇이 나가고 누구를 때리는가 (행동 예고 2026-10-04).
    /// AutoAttackSystem.PlanAttack이 만들고, 실행 루틴과 행동 예고가 같은 계획을 읽는다.
    /// 계획을 세우는 것만으로는 아무 상태도 바뀌지 않는다.
    /// </summary>
    public sealed class AttackPlan
    {
        public AttackPlanKind Kind;

        /// <summary>강화 공격 스킬 (Combo·ComboNoTarget일 때).</summary>
        public Data.Skills.ComboActiveSkill ComboSkill;

        /// <summary>콤보 단계 (0~2). 기본공격이면 -1.</summary>
        public int Stage = -1;

        public readonly List<Unit> Targets = new List<Unit>();

        /// <summary>이동 시작 구역과 도착 구역이 달랐는가.</summary>
        public bool CrossedZone;

        /// <summary>기본공격으로 끊기는 콤보(1단계 이상)가 있었는가.</summary>
        public bool HadCombo;

        /// <summary>강화 공격 슬롯이 ComboActiveSkill이 아니다 (설계 위반 — 실행 루틴이 에러로 알린다).</summary>
        public bool EmpoweredIsNotCombo;

        /// <summary>계획의 주체(슬롯 소유자) — 주인 기준 타격 수(여분의 비수·일제 타격, 2026-10-09 C)에 쓴다. PlanAttack이 채운다.</summary>
        public Character Source;

        /// <summary>대상 하나당 타격 횟수 — 실행 루프와 행동 예고가 같이 읽는다.</summary>
        public int Hits => Kind == AttackPlanKind.Combo && ComboSkill != null ? Mathf.Max(1, ComboSkill.GetStageHits(Source, Stage)) : 1;

        /// <summary>이 계획으로 여는 공격 행동 스코프의 정보 — 모디파이어·1회성 상태가 조건 판정에 읽는다.</summary>
        public Systems.Effects.AttackActionInfo BuildActionInfo(int diceValue) => new Systems.Effects.AttackActionInfo
        {
            DiceValue = diceValue,
            ComboStage = Kind == AttackPlanKind.Combo ? Stage : -1,
            CrossedZone = CrossedZone,
            IsAutoAttack = true,
        };
    }
}
