using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using DiceOrbit.Data.CharacterActives;
using DiceOrbit.Data.Modifiers;
using DiceOrbit.Data.Modifiers.Common;
using DiceOrbit.Data.Modifiers.Job;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using DiceOrbit.Systems.Effects;
using UnityEditor;
using UnityEngine;

namespace DiceOrbit.EditorTools
{
    /// <summary>
    /// 캐릭터 시트 동기화 C(직업 고유 모디파이어 16종) 순수 로직 자가 테스트. 씬·플레이 모드 불필요.
    /// 메뉴 [DiceOrbit → Run Job Modifier Self-Tests] 또는 MCP RunCommand에서 RunAll() 호출.
    /// 유닛은 비활성 GameObject에 AddComponent로 만들고(Awake 미실행), 직업은 임시 CharacterPreset의 saveId로 준다.
    /// 끝나면 만든 GameObject·프리셋을 전부 파괴한다. 매니저(PartyManager·CombatManager·CombatZoneManager)는 없다고 가정한다.
    /// </summary>
    public static class JobModifierSelfTests
    {
        private static int _failures;
        private static readonly List<Object> _spawned = new List<Object>();

        [MenuItem("DiceOrbit/Run Job Modifier Self-Tests")]
        public static void RunFromMenu() => RunAll();

        public static bool RunAll()
        {
            _failures = 0;
            // AddEffect → EffectApplied → VfxService.EnsureInstance가 만든 인스턴스를 정리하기 위해 사전 상태 기록
            bool vfxExisted = Object.FindFirstObjectByType<DiceOrbit.Visuals.VfxService>() != null;
            // 상태·방어도 버블(CombatNotifier → FloatingPopupQueue.EnsureInstance)이 에디트 모드에서 만든 대기열 오브젝트도 정리한다
            // (에디트 모드에선 Awake가 돌지 않아 Instance가 안 잡히고 호출마다 새로 생길 수 있다).
            var popupsBefore = new HashSet<DiceOrbit.UI.FloatingPopupQueue>(
                Object.FindObjectsByType<DiceOrbit.UI.FloatingPopupQueue>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            try
            {
                TestJobIdsAndLabels();
                TestJobRestriction();
                TestJobQueries();
                TestJobTagOnRewardStatus();
                // [tests-run]
            }
            finally
            {
                AttackActionScope.ResetAll();
                foreach (var obj in _spawned)
                    if (obj != null) Object.DestroyImmediate(obj);
                _spawned.Clear();
                if (!vfxExisted && DiceOrbit.Visuals.VfxService.Instance != null)
                    Object.DestroyImmediate(DiceOrbit.Visuals.VfxService.Instance.gameObject);
                foreach (var queue in Object.FindObjectsByType<DiceOrbit.UI.FloatingPopupQueue>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (queue != null && !popupsBefore.Contains(queue)) Object.DestroyImmediate(queue.gameObject);
            }

            if (_failures == 0) Debug.Log("[JobModifierSelfTests] 전부 통과");
            else Debug.LogError($"[JobModifierSelfTests] 실패 {_failures}건");
            return _failures == 0;
        }

        // ── 공통 기반 ─────────────────────────────────────────

        /// <summary>테스트 전용 전사 모디파이어 — 실제 16종에 기대지 않고 베이스 규칙만 본다.</summary>
        [System.Serializable]
        private class WarriorProbeModifier : JobModifier
        {
            protected override string JobId => JobIds.Warrior;
            public override string ModifierName => "전사 탐침";
            public override string Description => "테스트 전용";
        }

        private static void TestJobIdsAndLabels()
        {
            Check(JobIds.Label(JobIds.Warrior) == "전사" && JobIds.Label(JobIds.Rogue) == "도적"
                  && JobIds.Label(JobIds.Mage) == "마법사" && JobIds.Label(JobIds.Alchemist) == "연금술사",
                  "직업 라벨 전사/도적/마법사/연금술사");
            Check(new WarriorProbeModifier().JobLabel == "전사", "JobLabel = 직업 라벨");
            Check(new WarriorProbeModifier().Category == ModifierCategory.Generic, "직업 고유도 Generic(파이프라인 훅)");
        }

        private static void TestJobRestriction()
        {
            var warrior = NewCharacter("JobWarrior", JobIds.Warrior);
            var rogue = NewCharacter("JobRogue", JobIds.Rogue);
            var noPreset = NewCharacter("JobNone");
            var probe = new WarriorProbeModifier();

            Check(probe.CanApplyTo(warrior), "전사 전용 → 전사에게 장착 가능");
            Check(!probe.CanApplyTo(rogue), "전사 전용 → 도적에게 장착 불가");
            Check(!probe.CanApplyTo(noPreset), "프리셋 없는 캐릭터에게 장착 불가");
            Check(!probe.CanApplyTo(null), "null 캐릭터에게 장착 불가");
            Check(JobModifierQueries.IsJob(warrior, JobIds.Warrior) && !JobModifierQueries.IsJob(warrior, "warrior"),
                  "SaveId 서수 비교 (대소문자 구분)");

            warrior.Stats.Modifiers.Add(probe);
            Check(!new WarriorProbeModifier().CanApplyTo(warrior), "같은 종류 중복 금지(base 규칙)도 유지");
        }

        private static void TestJobQueries()
        {
            var warrior = NewCharacter("QueryWarrior", JobIds.Warrior);
            var rogue = NewCharacter("QueryRogue", JobIds.Rogue);
            Check(!JobModifierQueries.Has<WarriorProbeModifier>(warrior), "장착 전 Has = false");
            warrior.Stats.Modifiers.Add(new WarriorProbeModifier());
            Check(JobModifierQueries.Has<WarriorProbeModifier>(warrior), "장착 후 Has = true");
            Check(JobModifierQueries.Get<WarriorProbeModifier>(warrior) != null, "Get으로 인스턴스 조회");
            Check(!JobModifierQueries.Has<WarriorProbeModifier>(rogue), "다른 캐릭터는 Has = false");
            Check(!JobModifierQueries.Has<WarriorProbeModifier>(null), "null 캐릭터 Has = false");

            var party = new List<Character> { rogue, warrior };
            Check(JobModifierQueries.AnyAliveHas<WarriorProbeModifier>(party), "살아 있는 보유자가 있으면 true");
            warrior.Stats.CurrentHP = 0;
            Check(!JobModifierQueries.AnyAliveHas<WarriorProbeModifier>(party), "보유자가 쓰러지면 false (D5 자동 해제)");
            Check(!JobModifierQueries.AnyAliveHas<WarriorProbeModifier>(null), "파티 null = false");
            Check(!JobModifierQueries.AnyAliveHas<WarriorProbeModifier>(), "PartyManager 없음 = false");
        }

        private static void TestJobTagOnRewardStatus()
        {
            Check(JobModifier.AppendJobTag("전사 · 신규", new WarriorProbeModifier()) == "전사 · 신규 · 전사 전용",
                  "직업 고유 카드 상태 줄 끝에 '○○ 전용'");
            Check(JobModifier.AppendJobTag("도적 · 장착 불가", new WarriorProbeModifier()) == "도적 · 장착 불가 · 전사 전용",
                  "다른 직업 선택 시 장착 불가 + 전용 표시");
            Check(JobModifier.AppendJobTag("전사 · 신규", new GrandFinaleModifier()) == "전사 · 신규", "공용은 그대로");
        }

        // [tests-body]

        // ── 도우미 ────────────────────────────────────────────

        /// <summary>비활성 캐릭터. jobId가 있으면 그 saveId를 가진 임시 프리셋을 SourcePreset으로 단다.</summary>
        private static Character NewCharacter(string name, string jobId = null, int attack = 10, int hp = 50)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            _spawned.Add(go);
            var character = go.AddComponent<Character>();
            var stats = new CharacterStats { CharacterName = name, Attack = attack, MaxHP = hp, CurrentHP = hp };
            if (jobId != null) stats.SourcePreset = NewPreset(jobId);
            character.InitializeStats(stats);
            return character;
        }

        /// <summary>saveId는 private 직렬화 필드라 SerializedObject로 굽는다 (SaveIdValidator와 같은 방식).</summary>
        private static CharacterPreset NewPreset(string saveId)
        {
            var preset = ScriptableObject.CreateInstance<CharacterPreset>();
            preset.name = saveId;
            _spawned.Add(preset);
            var so = new SerializedObject(preset);
            so.FindProperty("saveId").stringValue = saveId;
            so.ApplyModifiedPropertiesWithoutUndo();
            return preset;
        }

        private static Monster NewMonster(string name, int hp = 50)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            _spawned.Add(go);
            var monster = go.AddComponent<Monster>();
            if (monster.Stats != null)
            {
                monster.Stats.MaxHP = hp;
                monster.Stats.CurrentHP = hp;
            }
            return monster;
        }

        /// <summary>
        /// 비활성 유닛은 Awake가 돌지 않아 StatusEffects가 비어 있다 — 매니저를 붙이고 Unit.statusEffects 필드에도 꽂는다
        /// (모디파이어 도우미가 unit.StatusEffects로 부여하므로).
        /// </summary>
        private static StatusEffectManager NewEffects(Unit unit)
        {
            var effects = unit.gameObject.AddComponent<StatusEffectManager>();
            effects.Initialize(unit);
            var so = new SerializedObject(unit);
            so.FindProperty("statusEffects").objectReferenceValue = effects;
            so.ApplyModifiedPropertiesWithoutUndo();
            return effects;
        }

        private static void Check(bool condition, string what)
        {
            if (condition) return;
            _failures++;
            Debug.LogError("[JobModifierSelfTests] FAIL: " + what);
        }
    }
}
