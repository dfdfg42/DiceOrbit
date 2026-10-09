using System.Collections.Generic;
using DiceOrbit.Core;
using UnityEngine;

namespace DiceOrbit.Data.Modifiers.Job
{
    /// <summary>
    /// 상태 강화 공통 규칙 (2026-10-09 C, D5 — 시트 「버프·디버프」 "효과 강화").
    /// 감전·촉매의 실효 %를 계산할 때마다 "살아 있는 파티원 중 강화 모디파이어 보유자가 있나"를 조회해
    /// 있으면 max(Value, 40)으로 쓴다. 부여 출처와 무관하고, 보유자가 쓰러지면 조회에서 빠져 자동으로 해제된다.
    /// 상태 생성자도 같은 값으로 부여해 아이콘 수치를 맞춘다 — 재부여는 큰 값 유지(A의 D6)라 안전하다.
    /// </summary>
    public static class StatusEnhancement
    {
        /// <summary>강화된 감전·촉매 수치(%).</summary>
        public const int EnhancedPercent = 40;

        /// <summary>지금 파티(PartyManager) 기준으로 이 상태가 강화 중인가.</summary>
        public static bool IsEnhanced(EffectType type)
            => IsEnhanced(type, PartyManager.Instance != null ? PartyManager.Instance.Party : null);

        /// <summary>주어진 파티 기준으로 이 상태가 강화 중인가 (순수 — 자가 테스트 대상).</summary>
        public static bool IsEnhanced(EffectType type, IEnumerable<Character> party) => type switch
        {
            EffectType.Shock    => JobModifierQueries.AnyAliveHas<HighVoltageModifier>(party),
            EffectType.Catalyst => JobModifierQueries.AnyAliveHas<HighPurityCatalystModifier>(party),
            _ => false,
        };

        /// <summary>지금 파티 기준 실효 %.</summary>
        public static int EffectivePercent(EffectType type, int value) => EffectivePercent(value, IsEnhanced(type));

        /// <summary>주어진 파티 기준 실효 % (순수).</summary>
        public static int EffectivePercent(EffectType type, int value, IEnumerable<Character> party)
            => EffectivePercent(value, IsEnhanced(type, party));

        /// <summary>강화 중이면 max(value, 40), 아니면 value (순수).</summary>
        public static int EffectivePercent(int value, bool enhanced) => enhanced ? Mathf.Max(value, EnhancedPercent) : value;
    }
}
