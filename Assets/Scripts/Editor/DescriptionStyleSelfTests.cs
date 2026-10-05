using System;
using DiceOrbit.Core;
using DiceOrbit.Core.Run;
using DiceOrbit.Data;
using DiceOrbit.Data.Modifiers;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Data.Skills;
using DiceOrbit.UI;
using UnityEditor;
using UnityEngine;

namespace DiceOrbit.EditorTools
{
    /// <summary>
    /// 설명 글 형식 자가 테스트 (2026-10-05). 씬·플레이 모드 불필요.
    /// 게임의 설명 글은 전부 같은 간결한 형식이다 — 한두 줄(첫 줄 = 무슨 일, 둘째 줄 = 조건·부연), 명사형으로 끝낸다("~합니다" 없음), 수치는 "+N%".
    /// 캐릭터 스킬·패시브, 몬스터 패시브·사망 효과, 포션, 유물, 키워드 사전, 모디파이어, 주사위 효과를 훑어
    /// 장문·문장형으로 쓴 글을 잡는다. 몬스터 기술(말풍선 옆 칩)은 더 좁은 기준이라 <see cref="MonsterIntentSelfTests"/>가 따로 본다.
    /// 메뉴 [DiceOrbit → Run Description Style Self-Tests] 또는 MCP RunCommand에서 RunAll() 호출.
    /// </summary>
    public static class DescriptionStyleSelfTests
    {
        private const string CharacterDir = "Assets/Scripts/Data/Character Preset";
        private const string MonsterDir = "Assets/Scripts/Data/MonsterPresets";
        private const string KeywordDatabasePath = "UI/TooltipKeywordDatabase";

        private const int MaxLines = 2;
        private const int MaxLineLength = 28;          // 전투 정보 패널에 한 줄로 들어가는 글자 수의 어림 — 넘으면 낱말 중간에서 꺾인다
        private const int MaxSummaryLineLength = 36;   // 모집 화면의 스킬 요약 (패널이 넓다)
        private static readonly string[] SentenceEndings = { "니다", "세요", "어요" };

        private static int _failures;
        private static int _checked;

        [MenuItem("DiceOrbit/Run Description Style Self-Tests")]
        public static void RunFromMenu() => RunAll();

        public static bool RunAll()
        {
            _failures = 0;
            _checked = 0;

            Characters();
            Monsters();
            Potions();
            Artifacts();
            Keywords();
            Modifiers();
            DieEffects();

            Check(_checked > 0, "설명을 하나 이상 찾았다");
            if (_failures == 0) Debug.Log($"[DescriptionStyleSelfTests] 전부 통과 (설명 {_checked}개)");
            else Debug.LogError($"[DescriptionStyleSelfTests] 실패 {_failures}건 (설명 {_checked}개)");
            return _failures == 0;
        }

        private static void Characters()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:CharacterPreset", new[] { CharacterDir }))
            {
                var preset = AssetDatabase.LoadAssetAtPath<CharacterPreset>(AssetDatabase.GUIDToAssetPath(guid));

                foreach (var passive in preset.StartingPassives)
                {
                    if (passive == null) continue;
                    string where = $"{preset.name} / 패시브 {passive.PassiveName}";
                    Required(passive.GetDynamicDescription(), passive.Description, where);
                    Style(passive.GetDynamicDescription(), where + " (지금 수치)");
                    Style(passive.Description, where + " (에셋)");
                }

                foreach (var active in preset.StartingActives)
                {
                    if (active == null) continue;
                    string where = $"{preset.name} / 스킬 {active.SkillName}";
                    Style(active.GetSelectionSummary(), where + " (모집 화면)", MaxSummaryLineLength);
                    Style(active.Description, where + " (에셋)", MaxSummaryLineLength);

                    if (!(active is ComboActiveSkill combo)) continue;
                    for (int stage = 0; stage < ComboActiveSkill.StageCount; stage++)
                    {
                        string text = combo.GetStageDescription(stage);
                        Check(!string.IsNullOrWhiteSpace(text), $"{where} {stage + 1}단계: 설명이 있다");
                        Style(text, $"{where} {stage + 1}단계");   // 전투 정보 패널에 지금 단계의 글이 그대로 뜬다
                    }
                }
            }
        }

        private static void Monsters()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:MonsterPreset", new[] { MonsterDir }))
            {
                var preset = AssetDatabase.LoadAssetAtPath<MonsterPreset>(AssetDatabase.GUIDToAssetPath(guid));

                foreach (var passive in preset.StartingPassives)
                {
                    if (passive == null) continue;
                    string where = $"{preset.name} / 패시브 {passive.PassiveName}";
                    Required(passive.GetDynamicDescription(), passive.Description, where);
                    Style(passive.GetDynamicDescription(), where + " (지금 수치)");
                    Style(passive.Description, where + " (에셋)");
                }

                foreach (var death in preset.OnDeathEffects)
                    if (death != null) Style(death.Description, $"{preset.name} / 사망 효과 {death.EffectName}");
            }
        }

        private static void Potions()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Potion"))
            {
                var potion = AssetDatabase.LoadAssetAtPath<Potion>(AssetDatabase.GUIDToAssetPath(guid));
                string where = $"포션 {potion.PotionName}";
                Required(potion.Description, null, where);
                Style(potion.Description, where);
            }
        }

        private static void Artifacts()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:ArtifactData"))
            {
                var artifact = AssetDatabase.LoadAssetAtPath<ArtifactData>(AssetDatabase.GUIDToAssetPath(guid));
                string where = $"유물 {artifact.artifactName}";
                Required(artifact.artifactTooltip, null, where);
                Style(artifact.artifactTooltip, where);
            }
        }

        private static void Keywords()
        {
            var database = Resources.Load<TooltipKeywordDatabase>(KeywordDatabasePath);
            Check(database != null, $"Resources/{KeywordDatabasePath} 키워드 사전이 있다");
            if (database == null) return;

            foreach (var entry in database.entries)
            {
                if (entry == null) continue;
                string where = $"키워드 {entry.key}";
                Required(entry.description, null, where);
                Style(entry.description, where);
            }
        }

        private static void Modifiers()
        {
            foreach (Type type in TypeCache.GetTypesDerivedFrom<CharacterModifier>())
            {
                if (type.IsAbstract || type.GetConstructor(Type.EmptyTypes) == null) continue;
                var modifier = (CharacterModifier)Activator.CreateInstance(type);
                string where = $"모디파이어 {modifier.ModifierName}";
                Required(modifier.Description, null, where);
                Style(modifier.Description, where);
            }
        }

        private static void DieEffects()
        {
            foreach (Type type in TypeCache.GetTypesDerivedFrom<DieEffect>())
            {
                if (type.IsAbstract || type.GetConstructor(Type.EmptyTypes) == null) continue;
                var effect = (DieEffect)Activator.CreateInstance(type);
                string where = $"주사위 효과 {type.Name}";
                Required(effect.Preview(), null, where);
                Style(effect.Preview(), where, MaxLineLength, maxLines: 1);   // 주사위 툴팁의 한 칸
            }
        }

        private static void Required(string text, string alternative, string where)
            => Check(!string.IsNullOrWhiteSpace(text) || !string.IsNullOrWhiteSpace(alternative), $"{where}: 설명이 있다");

        /// <summary>글 하나가 형식에 맞는가 — 빈 글은 보지 않는다(있어야 하는지는 Required가 본다).</summary>
        private static void Style(string text, string where, int maxLineLength = MaxLineLength, int maxLines = MaxLines)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _checked++;

            string[] lines = text.Trim().Split('\n');
            Check(lines.Length <= maxLines, $"{where}: {maxLines}줄 이하 (지금 {lines.Length}줄)");
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                Check(line.Length <= maxLineLength, $"{where}: 한 줄은 {maxLineLength}자 이하 (지금 {line.Length}자) — \"{line}\"");
                Check(!line.EndsWith("."), $"{where}: 마침표로 끝나지 않는다 — \"{line}\"");
                foreach (string ending in SentenceEndings)
                    Check(!line.Contains(ending), $"{where}: 문장형(\"~{ending}\")이 아니라 명사형으로 끝낸다 — \"{line}\"");
            }
        }

        private static void Check(bool condition, string what)
        {
            if (condition) return;
            _failures++;
            Debug.LogError("[DescriptionStyleSelfTests] FAIL: " + what);
        }
    }
}
