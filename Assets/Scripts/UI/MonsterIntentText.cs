using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 몬스터가 다음에 쓸 기술의 설명 글 (2026-10-05).
    /// 글은 기술의 설명(<see cref="SkillData.Description"/>) 그대로다 — 간결한 한두 줄. 그 안의 <c>{피해}</c>만 지금 상황의 예상 피해
    /// (취약·쇠약·방어도 같은 보정 반영)로 바뀐다.
    ///
    /// 몬스터 기술을 보여 주는 곳은 전부 여기를 거친다 — 머리 위 의도 말풍선 옆 설명 칩(MonsterUI), 몬스터 턴의 행동 말풍선(MonsterActionLabel),
    /// 정보 패널(UnitInfoBuilder). 그래서 어디서나 같은 글, 같은 숫자가 나온다.
    /// </summary>
    public static class MonsterIntentText
    {
        /// <summary>설명 글에서 예상 피해로 바뀌는 자리.</summary>
        public const string DamageToken = "{피해}";

        /// <summary>둘째 줄(조건·부연)은 작고 흐리게.</summary>
        private const string NoteOpen = "<size=86%><color=#6B665C>";
        private const string NoteClose = "</color></size>";

        /// <summary>
        /// 다음 기술의 예상 피해 — 파이프라인 시뮬레이션(패시브·상태 보정 포함). 피해 기술이 아니면 0.
        /// 대표 대상: 의도가 노리는 유닛 → 노리는 타일 위의 캐릭터 → 살아 있는 파티원.
        /// </summary>
        public static int PreviewDamage(Monster monster)
        {
            var data = monster != null ? monster.NextSkill?.skillData : null;
            if (data == null) return 0;

            int baseDamage = data.GetPreviewDamage();
            if (baseDamage <= 0) return 0;

            var target = ResolvePreviewTarget(monster);
            if (target == null || CombatPipeline.Instance == null) return baseDamage;
            return CombatPipeline.Instance.SimulateCalculation(new AttackContext(monster, target, data.SkillName, baseDamage));
        }

        /// <summary>설명 글 (줄바꿈 포함, 서식 없음, {피해} 치환됨). 기술에 설명이 없으면 빈 문자열.</summary>
        public static string Summary(Monster monster)
        {
            var data = monster != null ? monster.NextSkill?.skillData : null;
            string template = data != null ? data.Description : null;
            if (string.IsNullOrWhiteSpace(template)) return string.Empty;

            template = template.Trim();
            return template.Contains(DamageToken)
                ? template.Replace(DamageToken, PreviewDamage(monster).ToString())
                : template;
        }

        /// <summary>첫 줄만 — 한 줄로 보여 줄 자리(몬스터 턴의 행동 말풍선)용.</summary>
        public static string FirstLine(Monster monster)
        {
            string summary = Summary(monster);
            int breakAt = summary.IndexOf('\n');
            return breakAt < 0 ? summary : summary.Substring(0, breakAt).TrimEnd();
        }

        /// <summary>말풍선 옆 칩용 — 첫 줄은 그대로, 둘째 줄부터는 작고 흐리게 (TMP 리치 텍스트).</summary>
        public static string RichSummary(Monster monster)
        {
            string summary = Summary(monster);
            int breakAt = summary.IndexOf('\n');
            if (breakAt < 0) return summary;
            return summary.Substring(0, breakAt).TrimEnd() + "\n" + NoteOpen + summary.Substring(breakAt + 1).Trim() + NoteClose;
        }

        private static Unit ResolvePreviewTarget(Monster monster)
        {
            var intent = monster.CurrentIntent;
            if (intent != null)
            {
                if (intent.Targets != null)
                {
                    foreach (var target in intent.Targets)
                        if (target != null && target.IsAlive) return target;
                }

                if (intent.TargetTiles != null)
                {
                    foreach (var tile in intent.TargetTiles)
                    {
                        var characters = tile != null ? tile.GetCharactersOnTile() : null;
                        if (characters == null) continue;
                        foreach (var character in characters)
                            if (character != null && character.IsAlive) return character;
                    }
                }
            }

            var alive = PartyManager.Instance?.GetAliveCharacters();
            return (alive != null && alive.Count > 0) ? alive[0] : null;
        }
    }
}
