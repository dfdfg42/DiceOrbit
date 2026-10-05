using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Combo;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Core.Zones;
using DiceOrbit.Visuals;
using UnityEngine;

namespace DiceOrbit.Data.Skills
{
    /// <summary>
    /// 3단계 콤보 강화공격의 공통 골격 (2026-08-28 개편).
    /// 게이트(requirement)에 맞는 주사위를 연속으로 쓸 때마다 1→2→3단계 공격이 나가고,
    /// 3단계 후 또는 조건 실패·미이동·표적 없음 시 콤보가 0으로 돌아간다 — 단계 관리는 ComboSystem,
    /// 실행 루프는 AutoAttackSystem, 이 클래스는 '각 단계가 무엇을 하는가'만 정의한다.
    /// </summary>
    [System.Serializable]
    public abstract class ComboActiveSkill : CharacterActiveSkill
    {
        public const int StageCount = ComboTracker.StageCount;

        // ── 단계 정의 (자식이 구현) ───────────────────────────
        public abstract string GetStageName(int stage);
        public abstract float  GetStageMultiplier(int stage);

        /// <summary>대상 '하나당' 타격 횟수 (도적 다단 공격용). 기본 1.</summary>
        public virtual int GetStageHits(int stage) => 1;

        /// <summary>이 단계가 때릴 대상들.</summary>
        public abstract List<Unit> ResolveStageTargets(Character source, int stage, IReadOnlyList<int> passedZones);

        /// <summary>타격 1회가 '적중한 순간' (발사체면 도착 시). 중독·감전·약화 부여는 여기서.</summary>
        public virtual void OnStageHitLanded(Character source, Unit target, int stage, int hitIndex) { }

        /// <summary>단계의 모든 타격이 끝난 뒤 1회 (행동 스코프 종료 후). 방어도·고양·중독 2배는 여기서.</summary>
        public virtual void OnStageCompleted(Character source, List<Unit> targets, int stage) { }

        /// <summary>
        /// 단계 1개의 설명 — 첫 줄 "[이름] 피해 얼마", 둘째 줄 "누구에게 + 덤" (덤이 없고 짧으면 한 줄).
        /// 정보 패널 폭에 한 줄씩 들어가는 길이로 쓴다 — 목록·현재 단계 표시가 공유한다.
        /// </summary>
        public abstract string GetStageDescription(int stage);

        // 설명은 단계별 효과 줄만 — 콤보 규칙 서두와 주사위 조건은 패널이 별도 필드로 이미 보여주므로
        // 여기 다시 쓰면 중복이다 (2026-08-28 결정).

        /// <summary>문맥 없는 설명 (전투 밖) — 단계마다 한 줄씩 3줄.</summary>
        public override string GetDynamicDescription()
            => $"1단계 {StageLine(0)}\n"
             + $"2단계 {StageLine(1)}\n"
             + $"3단계 {StageLine(2)}";

        private string StageLine(int stage) => GetStageDescription(stage).Replace("\n", " · ");

        /// <summary>전투 중 정보 패널 — 지금 단계의 효과 설명만 (2026-08-28 결정).</summary>
        public override string GetDynamicDescription(Character source)
            => GetStageDescription(PeekStage(source));

        /// <summary>유효 대상 줄은 쓰지 않는다 — 단계 설명이 대상까지 말하므로 중복 (2026-08-28 결정).</summary>
        public override string GetTargetLabel() => string.Empty;

        /// <summary>단계 피해 = 공격력 × 단계 배율 (타격 1회분).</summary>
        public int CalculateStageDamage(Character source, int stage)
        {
            int attack = source != null && source.Stats != null ? source.Stats.Attack : 0;
            return Mathf.Max(1, Mathf.RoundToInt(attack * Mathf.Max(0.05f, GetStageMultiplier(stage))));
        }

        /// <summary>
        /// 스테이지 타격 1회 적용. 발사체가 있으면 도착 시 피해·부가효과가 들어간다.
        /// AutoAttackSystem이 타격 사이 간격을 관리하며 반복 호출한다.
        /// </summary>
        public void ApplyStageHit(Character source, Unit target, int stage, int hitIndex)
        {
            if (source == null || target == null || !target.IsAlive) return;

            int raw = CalculateStageDamage(source, stage);
            var context = new AttackContext(source, target, GetStageName(stage), raw);
            context.VfxCue = impactCue;
            HitDirector.ReportAttackLaunched(source, target.transform.position);   // 발사 반동 + 휘두르는 소리

            if (projectilePrefab != null)
            {
                Vector3 from = source.transform.position + Vector3.up * 0.5f;
                Vector3 to   = target.transform.position + Vector3.up * 0.5f;
                var ctx = context;
                int capturedHit = hitIndex;
                ProjectileService.Launch(projectilePrefab, from, to, projectileDuration, projectileArcHeight,
                    () =>
                    {
                        CombatPipeline.Instance?.Process(ctx);
                        OnStageHitLanded(source, target, stage, capturedHit);
                    });
            }
            else
            {
                CombatPipeline.Instance?.Process(context);
                OnStageHitLanded(source, target, stage, hitIndex);
            }
        }

        // ── 현재 단계 (UI/미리보기용) ─────────────────────────
        protected int PeekStage(Character source)
            => ComboSystem.Instance != null ? ComboSystem.Instance.PeekStage(source) : 0;

        // ── 구식 API를 현재 단계로 매핑 (미리보기·정보 패널·조준선이 그대로 동작) ──
        public override int CalculateRawDamage(Character source, ActiveSkillSlot ability, int diceValue)
            => CalculateStageDamage(source, PeekStage(source));

        public override string BuildPreview(Character source, ActiveSkillSlot ability, int diceValue)
        {
            int stage = PeekStage(source);
            return $"{stage + 1}단계 [{GetStageName(stage)}] 예상 피해: {CalculateStageDamage(source, stage)}"
                   + (GetStageHits(stage) > 1 ? $" x{GetStageHits(stage)}타" : string.Empty);
        }

        public override List<Unit> ResolveTargets(Character source, IReadOnlyList<int> passedZones)
            => ResolveStageTargets(source, PeekStage(source), passedZones);

        // ── 대상 수집 헬퍼 (원형 구역 wrap 포함) ──────────────
        protected static List<Unit> OwnersOfCurrentZone(Character source)
        {
            var result = new List<Unit>();
            var zones = CombatZoneManager.Instance;
            if (zones == null || source == null) return result;

            var owner = zones.GetOwner(zones.GetZoneOf(source));
            if (owner != null) result.Add(owner);
            return result;
        }

        /// <summary>현재 구역 + 양옆 spread개 구역의 주인 전원 (원형 wrap).</summary>
        protected static List<Unit> OwnersOfCurrentAndAdjacent(Character source, int spread)
        {
            var result = new List<Unit>();
            var zones = CombatZoneManager.Instance;
            if (zones == null || source == null) return result;

            int myZone = zones.GetZoneOf(source);
            if (myZone < 0) return result;

            int n = zones.ZoneCount;
            int limit = Mathf.Clamp(spread, 0, n / 2);
            for (int d = 0; d <= limit; d++)
            {
                var forward = zones.GetOwner(((myZone + d) % n + n) % n);
                if (forward != null && !result.Contains(forward)) result.Add(forward);

                var backward = zones.GetOwner(((myZone - d) % n + n) % n);
                if (backward != null && !result.Contains(backward)) result.Add(backward);
            }
            return result;
        }

        /// <summary>전장의 살아 있는 몬스터 전원.</summary>
        protected static List<Unit> AllLivingMonsters()
        {
            var result = new List<Unit>();
            var cm = CombatManager.Instance;
            if (cm == null || cm.ActiveMonsters == null) return result;

            foreach (var m in cm.ActiveMonsters)
                if (m != null && m.IsAlive) result.Add(m);
            return result;
        }

        /// <summary>살아 있는 파티원 전원 (자신 포함).</summary>
        protected static List<Character> AllLivingAllies()
        {
            var result = new List<Character>();
            var party = PartyManager.Instance;
            if (party == null) return result;

            foreach (var ch in party.GetAliveCharacters())
                if (ch != null) result.Add(ch);
            return result;
        }

        /// <summary>대상에게 상태를 부여하고 상태 버블을 띄운다 (죽은 대상 무시).</summary>
        protected static void GrantStatus(Unit target, EffectType type, int value)
        {
            if (target == null || !target.IsAlive || target.StatusEffects == null) return;

            target.StatusEffects.AddEffect(Systems.Effects.StatusEffectManager.CreateEffect(type, value, -1));
            var data = UI.TooltipKeywordFormatter.BuildStatusDisplayData(type.ToString(), value, -1);
            UI.CombatNotifier.NotifyStatus(target, data.Name, data.Color);
        }
    }
}
