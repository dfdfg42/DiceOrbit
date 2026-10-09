using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Core.Zones;
using DiceOrbit.Data.Modifiers.Common;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.Modifiers.Job
{
    /// <summary>직업 판별 ID(CharacterPreset.SaveId)와 표시 라벨 (2026-10-09 C, D1).</summary>
    public static class JobIds
    {
        public const string Warrior   = "Warrior";
        public const string Rogue     = "Rogue";
        public const string Mage      = "Mage";
        public const string Alchemist = "Alchemist";

        /// <summary>보상 카드 "○○ 전용"에 쓰는 한국어 직업 이름. 모르는 ID는 그대로 돌려준다.</summary>
        public static string Label(string jobId) => jobId switch
        {
            Warrior   => "전사",
            Rogue     => "도적",
            Mage      => "마법사",
            Alchemist => "연금술사",
            _         => jobId ?? string.Empty,
        };
    }

    /// <summary>
    /// 직업 고유 모디파이어 골격 (2026-10-09 C). 공용과 같은 장착 규칙(같은 종류 1개)에 "해당 직업 전용"이 더해진다.
    /// 직업 판별은 Stats.SourcePreset.SaveId 서수 비교(D1). 세이브 ID는 공용과 같이 구체 타입명(D6).
    /// </summary>
    [System.Serializable]
    public abstract class JobModifier : CommonModifier
    {
        /// <summary>이 모디파이어를 쓸 수 있는 직업의 SaveId (JobIds 상수).</summary>
        protected abstract string JobId { get; }

        /// <summary>표시용 직업 이름 (전사/도적/마법사/연금술사).</summary>
        public string JobLabel => JobIds.Label(JobId);

        public override bool CanApplyTo(Character character)
            => base.CanApplyTo(character) && JobModifierQueries.IsJob(character, JobId);

        /// <summary>owner의 강화 공격(행동 스코프 주체가 owner, ComboStage ≥ 0) 피해 계산인가.</summary>
        protected bool IsOwnEmpoweredCalculation(CombatTrigger trigger, AttackContext context)
            => IsOwnAttackCalculation(trigger, context)
               && AttackActionScope.IsActive
               && AttackActionScope.CurrentSource == owner
               && AttackActionScope.CurrentInfo.ComboStage >= 0;

        /// <summary>owner의 3단계 강화 공격 피해 계산인가 (스펙 §3.3 — B의 대단원과 같은 조건).</summary>
        protected bool IsOwnStage3Calculation(CombatTrigger trigger, AttackContext context)
            => IsOwnEmpoweredCalculation(trigger, context)
               && AttackActionScope.CurrentInfo.ComboStage == 2;

        /// <summary>보상 카드 상태 줄에 직업 고유면 " · ○○ 전용"을 붙인다 (D2, 순수 — 자가 테스트 대상).</summary>
        public static string AppendJobTag(string status, CharacterModifier modifier)
            => modifier is JobModifier job ? $"{status} · {job.JobLabel} 전용" : status;
    }

    /// <summary>직업 고유 모디파이어 조회 (D3 pull·D5 상태 강화가 쓴다). 저장 없이 질의 시점에 계산한다.</summary>
    public static class JobModifierQueries
    {
        /// <summary>캐릭터의 직업 SaveId가 jobId와 정확히 같은가 (서수 비교, 프리셋 없으면 false).</summary>
        public static bool IsJob(Character character, string jobId)
        {
            if (character == null || character.Stats == null || character.Stats.SourcePreset == null) return false;
            return string.Equals(character.Stats.SourcePreset.SaveId, jobId, System.StringComparison.Ordinal);
        }

        /// <summary>캐릭터가 장착한 T 인스턴스 (없으면 null).</summary>
        public static T Get<T>(Character character) where T : CharacterModifier
        {
            if (character == null || character.Stats == null || character.Stats.Modifiers == null) return null;
            var mods = character.Stats.Modifiers.Modifiers;
            if (mods == null) return null;

            for (int i = 0; i < mods.Count; i++)
                if (mods[i] is T found) return found;
            return null;
        }

        /// <summary>주인이 T를 장착했나.</summary>
        public static bool Has<T>(Character character) where T : CharacterModifier => Get<T>(character) != null;

        /// <summary>살아 있는 파티원 중 T 보유자가 있나 (PartyManager 기준, 없으면 false).</summary>
        public static bool AnyAliveHas<T>() where T : CharacterModifier
            => AnyAliveHas<T>(PartyManager.Instance != null ? PartyManager.Instance.Party : null);

        /// <summary>주어진 파티 중 살아 있는 T 보유자가 있나 (순수 — 자가 테스트 대상).</summary>
        public static bool AnyAliveHas<T>(IEnumerable<Character> party) where T : CharacterModifier
        {
            if (party == null) return false;
            foreach (var c in party)
                if (c != null && c.Stats != null && c.IsAlive && Has<T>(c)) return true;
            return false;
        }

        /// <summary>self와 같은 구역에 있는 '다른' 살아 있는 아군 수 (전우애). 구역 판정 불가면 0.</summary>
        public static int CountOtherAlliesInMyZone(Character self)
        {
            var zones = CombatZoneManager.Instance;
            var party = PartyManager.Instance;
            if (zones == null || party == null || self == null) return 0;

            int myZone = zones.GetZoneOf(self);
            if (myZone < 0) return 0;

            int count = 0;
            foreach (var ally in party.GetAliveCharacters())
            {
                if (ally == null || ally == self) continue;
                if (zones.GetZoneOf(ally) == myZone) count++;
            }
            return count;
        }
    }
}
