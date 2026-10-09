using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Modifiers.Common;
using DiceOrbit.Systems.Effects;
using UnityEngine;

namespace DiceOrbit.Data.Modifiers.Job
{
    /// <summary>
    /// [고순도 촉매] 촉매 25 → 40% (출처 무관). StatusEnhancement가 촉매 계산·부여 때 조회한다(D5).
    /// ReagentPrepPassive.CatalystPercent도 같은 값을 돌려줘 시약 타일 부여·행동 예고·설명이 일치한다.
    /// </summary>
    [System.Serializable]
    public class HighPurityCatalystModifier : JobModifier
    {
        protected override string JobId => JobIds.Alchemist;
        public override string ModifierName => "고순도 촉매";
        public override ModifierFamily Family => ModifierFamily.Combo;
        public override string Description
            => $"촉매로 늘어나는 다음 자동공격 피해가 25%에서 {StatusEnhancement.EnhancedPercent}%로 강화됩니다. 누가 얻은 촉매든 적용되며, 이 연금술사가 쓰러지면 원래대로 돌아갑니다.";
    }

    /// <summary>[추가 시약] 전투 시작 시 설치하는 시약 타일 3 → 4개. ReagentPrepPassive의 배치 개수가 조회한다(D3).</summary>
    [System.Serializable]
    public class ExtraReagentModifier : JobModifier
    {
        public const int ExtraTiles = 1;

        protected override string JobId => JobIds.Alchemist;
        public override string ModifierName => "추가 시약";
        public override ModifierFamily Family => ModifierFamily.Position;
        public override string Description
            => $"전투 시작 시 설치하는 시약 타일이 {ExtraTiles}개 늘어납니다.";
    }

    /// <summary>
    /// [시약 분배] 아군이 시약 타일에서 촉매를 얻으면 그 타일 구역의 무작위 다른 아군 1명에게도 촉매.
    /// ReagentTile.TryCatalyze가 타일 주인 연금술사(살아 있음)의 보유 여부를 조회해 PickRecipient로 대상을 고른다.
    /// 이미 촉매가 있는 아군은 제외, 대상 없으면 전달 없음, 분배로 받은 촉매는 다시 분배하지 않음(스펙 §2).
    /// </summary>
    [System.Serializable]
    public class ReagentShareModifier : JobModifier
    {
        protected override string JobId => JobIds.Alchemist;
        public override string ModifierName => "시약 분배";
        public override ModifierFamily Family => ModifierFamily.Position;
        public override string Description
            => "아군이 시약 타일에서 촉매를 얻으면 그 구역의 다른 아군 1명(무작위, 이미 촉매가 있는 아군 제외)도 촉매를 얻습니다.";

        /// <summary>
        /// 분배 대상 1명 — exclude(촉매를 받은 본인)·쓰러진 아군·이미 촉매가 있는 아군·상태 관리자가 없는 유닛 제외, 무작위.
        /// 없으면 null (순수 — 자가 테스트 대상).
        /// </summary>
        public static Character PickRecipient(IReadOnlyList<Character> candidates, Character exclude)
        {
            if (candidates == null) return null;

            var pool = new List<Character>();
            foreach (var c in candidates)
            {
                if (c == null || c == exclude || c.Stats == null || !c.IsAlive) continue;
                if (c.StatusEffects == null || c.StatusEffects.HasEffect(EffectType.Catalyst)) continue;
                pool.Add(c);
            }
            return pool.Count == 0 ? null : pool[Random.Range(0, pool.Count)];
        }
    }

    /// <summary>
    /// [약화 가스] 3단계 강화 공격 피해 +15%, 그 공격을 마친 뒤 살아 있는 모든 몬스터에게 약화 25%.
    /// 3단계 기존 효과(아군 고양 — AlchemistThrowActive.OnStageCompleted)는 그대로 유지된다.
    /// </summary>
    [System.Serializable]
    public class WeakeningGasModifier : JobModifier, IAttackActionFinishedListener
    {
        public const float Percent = 15f;
        public const int WeakenPercent = 25;

        protected override string JobId => JobIds.Alchemist;
        public override string ModifierName => "약화 가스";
        public override ModifierFamily Family => ModifierFamily.Combo;
        public override string Description
            => $"3단계 강화 공격의 피해가 {Percent:0.#}% 증가하고, 그 공격을 마친 뒤 모든 몬스터에게 약화 {WeakenPercent}%를 부여합니다.";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (!IsOwnStage3Calculation(trigger, context)) return;
            context.OutputValue *= 1f + Percent / 100f;
        }

        public void OnAttackActionFinished(Character character, AttackActionResult result)
        {
            if (owner == null || character != owner || !owner.IsAlive) return;
            if (result.ComboStage != 2) return;

            var cm = CombatManager.Instance;
            if (cm == null || cm.ActiveMonsters == null) return;

            foreach (var monster in cm.ActiveMonsters)
                if (monster != null && monster.IsAlive)
                    GrantStatusTo(monster, EffectType.Weaken, WeakenPercent);
        }
    }
}
