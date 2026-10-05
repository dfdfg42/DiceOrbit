using DiceOrbit.Data;
using DiceOrbit.Data.Monsters;
using DiceOrbit.UI;
using UnityEditor;
using UnityEngine;

namespace DiceOrbit.EditorTools
{
    /// <summary>
    /// 몬스터 기술 설명 자가 테스트 (2026-10-05). 씬·플레이 모드 불필요.
    /// 기술 설명은 말풍선 옆 칩에 그대로 뜨므로 짧아야 한다 — 모든 몬스터 프리셋의 모든 기술에 설명이 있는지, 한두 줄·줄당 20자 안쪽인지,
    /// {피해} 자리와 실제 예상 피해가 맞는지 본다. 새 몬스터를 넣고 설명을 길게 쓰거나 빼먹으면 여기서 걸린다.
    /// 메뉴 [DiceOrbit → Run Monster Intent Self-Tests] 또는 MCP RunCommand에서 RunAll() 호출.
    /// </summary>
    public static class MonsterIntentSelfTests
    {
        private const string PresetDir = "Assets/Scripts/Data/MonsterPresets";
        private const int MaxLines = 2;
        private const int MaxLineLength = 20;   // 말풍선 옆 칩에 한 줄로 들어가는 글자 수의 어림

        private static int _failures;

        [MenuItem("DiceOrbit/Run Monster Intent Self-Tests")]
        public static void RunFromMenu() => RunAll();

        public static bool RunAll()
        {
            _failures = 0;
            int skills = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:MonsterPreset", new[] { PresetDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var preset = AssetDatabase.LoadAssetAtPath<MonsterPreset>(path);
                if (preset.AIPattern == null || preset.AIPattern.availableSkills == null) continue;

                foreach (var skill in preset.AIPattern.availableSkills)
                {
                    var data = skill?.skillData;
                    if (data == null) continue;
                    skills++;
                    string where = $"{preset.name} / {data.SkillName}";
                    string summary = data.Description;

                    if (string.IsNullOrWhiteSpace(summary)) { Check(false, $"{where}: 설명이 있다"); continue; }

                    // 길이는 화면에 보이는 대로 잰다 — {피해}는 두 자리 숫자로 바뀐다
                    string[] lines = summary.Trim().Replace(MonsterIntentText.DamageToken, "00").Split('\n');
                    Check(lines.Length <= MaxLines, $"{where}: 설명은 {MaxLines}줄 이하 (지금 {lines.Length}줄)");
                    foreach (string line in lines)
                        Check(line.Trim().Length <= MaxLineLength, $"{where}: 한 줄은 {MaxLineLength}자 이하 — \"{line.Trim()}\"");

                    bool usesDamage = summary.Contains(MonsterIntentText.DamageToken);
                    bool dealsDamage = data.GetPreviewDamage() > 0;
                    if (usesDamage) Check(dealsDamage, $"{where}: {MonsterIntentText.DamageToken}를 쓰는 기술은 예상 피해가 0보다 크다");
                    if (dealsDamage) Check(usesDamage, $"{where}: 피해를 주는 기술은 설명에 {MonsterIntentText.DamageToken}가 있다 (피해 숫자를 따로 보여 주는 곳이 없다)");
                    Check(!summary.Replace(MonsterIntentText.DamageToken, "").Contains("{"), $"{where}: 모르는 치환 자리가 없다");
                }
            }

            Check(skills > 0, "몬스터 기술을 하나 이상 찾았다");
            if (_failures == 0) Debug.Log($"[MonsterIntentSelfTests] 전부 통과 (기술 {skills}개)");
            else Debug.LogError($"[MonsterIntentSelfTests] 실패 {_failures}건 (기술 {skills}개)");
            return _failures == 0;
        }

        private static void Check(bool condition, string what)
        {
            if (condition) return;
            _failures++;
            Debug.LogError("[MonsterIntentSelfTests] FAIL: " + what);
        }
    }
}
