using System.Linq;
using System.Reflection;
using DiceOrbit.Core;
using DiceOrbit.Core.Combo;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Core.Tutorial;
using DiceOrbit.Data;
using DiceOrbit.Data.CharacterActives;
using DiceOrbit.Data.Passives;
using DiceOrbit.Systems.Effects;
using UnityEditor;
using UnityEngine;

namespace DiceOrbit.EditorTools
{
    /// <summary>
    /// 2026-08-28 콤보/상태 개편의 순수 로직 자가 테스트.
    /// 씬·플레이 모드 없이 도는 검증만 모았다 — 파이프라인 통합(방진 감쇄, 촉매 소모 등)은 플레이로 확인.
    /// 메뉴 [DiceOrbit → Run Combat Redesign Self-Tests] 또는 MCP RunCommand에서 RunAll() 호출.
    /// </summary>
    public static class CombatRedesignSelfTests
    {
        private static int _failures;

        [MenuItem("DiceOrbit/Run Combat Redesign Self-Tests")]
        public static void RunFromMenu() => RunAll();

        public static bool RunAll()
        {
            _failures = 0;

            TestComboTransitions();
            TestComboIndependence();
            TestPoisonSequence();
            TestCircularDistance();
            TestActionScopeBookkeeping();
            TestRoundStatuses();
            TestStageTables();
            TestSelectionActiveSummaries();
            TestTutorialCombatProgress();
            TestTutorialScenarioSetup();
            TestTutorialGuidedComboGates();

            if (_failures == 0) Debug.Log("[SelfTest] 전체 PASS — 콤보/중독/구역/스코프/단계표/튜토리얼");
            else Debug.LogError($"[SelfTest] 실패 {_failures}건 — 위 로그 확인");
            return _failures == 0;
        }

        private static void Check(bool condition, string label)
        {
            if (condition) return;
            _failures++;
            Debug.LogError("[SelfTest] FAIL: " + label);
        }

        private static void TestComboTransitions()
        {
            var t = new ComboTracker();
            Check(t.RegisterGateSuccess() == 0, "첫 게이트 성공은 1단계(0)");
            Check(t.ConfirmExecuted() == ComboOutcome.Advanced && t.Stage == 1, "1단계 후 전진");
            Check(t.RegisterGateSuccess() == 1, "둘째 게이트 성공은 2단계(1)");
            Check(t.ConfirmExecuted() == ComboOutcome.Advanced && t.Stage == 2, "2단계 후 전진");
            Check(t.RegisterGateSuccess() == 2, "셋째 게이트 성공은 3단계(2)");
            Check(t.ConfirmExecuted() == ComboOutcome.Finished && t.Stage == 0, "3단계 후 0으로 순환");

            t.ConfirmExecuted();
            t.Reset();
            Check(t.Stage == 0, "Reset은 0으로");
        }

        private static void TestComboIndependence()
        {
            var a = new ComboTracker();
            var b = new ComboTracker();
            a.ConfirmExecuted();
            a.ConfirmExecuted();
            Check(a.Stage == 2 && b.Stage == 0, "트래커는 서로 독립");
        }

        private static void TestPoisonSequence()
        {
            var p = new PoisonStatus(6);
            Check(p.Duration == -1, "중독은 지속시간 없음(-1)");

            string sequence = "";
            for (int i = 0; i < 6; i++) sequence += p.ConsumeTick() + (i < 5 ? "," : "");
            Check(sequence == "6,5,4,3,2,1", "중독 틱 수열 6,5,4,3,2,1 (실제: " + sequence + ")");
            Check(p.Value == 0, "6틱 후 잔여 0");
            Check(p.ConsumeTick() == 0, "0중첩 틱은 0");

            // 합산: 기존 4 + 추가 3 = 7 (StatusEffect.AddStack 경로)
            var q = new PoisonStatus(4);
            q.AddStack(3);
            Check(q.Value == 7, "중독 합산 4+3=7");

            // 2배 로직(죽음의 촉매): 현재치만큼 추가 = 정확히 2배
            var r = new PoisonStatus(5);
            r.AddStack(r.Value);
            Check(r.Value == 10, "중독 2배 5→10");
        }

        private static void TestCircularDistance()
        {
            Check(MageCircuit.CircularDistance(0, 3, 4) == 1, "n=4에서 0↔3 거리 1 (wrap)");
            Check(MageCircuit.CircularDistance(3, 0, 4) == 1, "wrap 대칭");
            Check(MageCircuit.CircularDistance(0, 2, 4) == 2, "맞은편 거리 2");
            Check(MageCircuit.CircularDistance(2, 2, 4) == 0, "자기 구역 거리 0");
        }

        private static void TestActionScopeBookkeeping()
        {
            AttackActionScope.ResetAll();
            Check(!AttackActionScope.IsActive, "초기 비활성");

            AttackActionScope.Begin(null);
            int first = AttackActionScope.CurrentActionId;
            Check(AttackActionScope.IsActive && first != 0, "Begin 후 활성");

            AttackActionScope.End();
            Check(!AttackActionScope.IsActive, "End 후 비활성");

            AttackActionScope.Begin(null);
            Check(AttackActionScope.CurrentActionId > first, "행동 ID 단조 증가");
            AttackActionScope.ResetAll();
            Check(!AttackActionScope.IsActive, "ResetAll 후 비활성");
        }

        private static void TestRoundStatuses()
        {
            // AddEffect → EffectApplied → VfxService.EnsureInstance 가 만든 인스턴스를 정리하기 위해 사전 상태 기록
            bool vfxExisted = UnityEngine.Object.FindFirstObjectByType<DiceOrbit.Visuals.VfxService>() != null;
            var attackerGo = new GameObject("RoundStatusAttacker"); attackerGo.SetActive(false);
            var otherGo    = new GameObject("RoundStatusOther");    otherGo.SetActive(false);
            var targetGo   = new GameObject("RoundStatusTarget");   targetGo.SetActive(false);
            var attacker = attackerGo.AddComponent<Character>();
            var other    = otherGo.AddComponent<Character>();
            var target   = targetGo.AddComponent<Monster>();

            // 감전: 같은 라운드에 서로 다른 공격자 두 번 모두 +25%, 소모되지 않는다
            var shock = new ShockStatus(25);
            shock.SetOwner(target);
            var hit1 = new AttackContext(attacker, target, "t1", 10f);
            shock.OnAttack(CombatTrigger.OnCalculateOutput, hit1);
            var hit2 = new AttackContext(other, target, "t2", 10f);
            shock.OnAttack(CombatTrigger.OnCalculateOutput, hit2);
            Check(Mathf.Approximately(hit1.OutputValue, 12.5f) && Mathf.Approximately(hit2.OutputValue, 12.5f),
                  "감전은 같은 라운드의 공격 두 번 모두 +25%");

            // 고양/약화: 주인이 주는 공격에만
            var inspire = new InspireStatus(25); inspire.SetOwner(attacker);
            var weaken  = new WeakenStatus(25);  weaken.SetOwner(attacker);
            var own = new AttackContext(attacker, target, "own", 10f);
            inspire.OnAttack(CombatTrigger.OnCalculateOutput, own);
            Check(Mathf.Approximately(own.OutputValue, 12.5f), "고양은 주인의 공격 +25%");
            var own2 = new AttackContext(attacker, target, "own2", 10f);
            weaken.OnAttack(CombatTrigger.OnCalculateOutput, own2);
            Check(Mathf.Approximately(own2.OutputValue, 7.5f), "약화는 주인의 공격 -25%");
            var notOwn = new AttackContext(other, target, "notOwn", 10f);
            inspire.OnAttack(CombatTrigger.OnCalculateOutput, notOwn);
            Check(Mathf.Approximately(notOwn.OutputValue, 10f), "고양은 남의 공격엔 적용 안 됨");

            // 직접 체력 손실(중독)엔 개입하지 않는다
            var poison = new AttackContext(null, target, "poison", 10f) { IsDirectHpLoss = true };
            shock.OnAttack(CombatTrigger.OnCalculateOutput, poison);
            Check(Mathf.Approximately(poison.OutputValue, 10f), "감전은 중독 피해에 개입 안 함");

            // 매니저: 재부여는 큰 값 유지, RemoveRoundStatuses는 라운드형만 제거
            var mgr = targetGo.AddComponent<StatusEffectManager>();
            mgr.Initialize(target);
            mgr.AddEffect(new ShockStatus(15));
            mgr.AddEffect(new ShockStatus(25));
            Check(mgr.GetEffectValue(EffectType.Shock) == 25, "라운드형 재부여는 큰 값(25) 유지, 합산 안 함");
            mgr.AddEffect(new ShockStatus(10));
            Check(mgr.GetEffectValue(EffectType.Shock) == 25, "작은 값 재부여는 무시");
            mgr.AddEffect(new CatalystStatus(25));
            mgr.RemoveRoundStatuses();
            Check(!mgr.HasEffect(EffectType.Shock), "라운드 종료 후 감전 제거");
            Check(mgr.HasEffect(EffectType.Catalyst), "촉매(1회성)는 라운드 종료로 제거되지 않음");

            UnityEngine.Object.DestroyImmediate(attackerGo);
            UnityEngine.Object.DestroyImmediate(otherGo);
            UnityEngine.Object.DestroyImmediate(targetGo);
            if (!vfxExisted && DiceOrbit.Visuals.VfxService.Instance != null)
                UnityEngine.Object.DestroyImmediate(DiceOrbit.Visuals.VfxService.Instance.gameObject);
        }

        private static void TestStageTables()
        {
            var warrior = new WarriorGreatswordActive();
            Check(Mathf.Approximately(warrior.GetStageMultiplier(0), 1.2f)
               && Mathf.Approximately(warrior.GetStageMultiplier(1), 1.2f)
               && Mathf.Approximately(warrior.GetStageMultiplier(2), 1.5f), "전사 배율 120/120/150%");
            Check(warrior.GetStageHits(0) == 1 && warrior.GetStageHits(2) == 1, "전사는 단타");
            Check(warrior.GetStageName(0) == "대검 베기" && warrior.GetStageName(1) == "균열 베기", "전사 1·2단계 이름 (시트)");

            var rogue = new RogueAmbushActive();
            Check(rogue.GetStageHits(0) == 2 && rogue.GetStageHits(1) == 3 && rogue.GetStageHits(2) == 4, "도적 타수 2/3/4");
            Check(Mathf.Approximately(rogue.GetStageMultiplier(0) * rogue.GetStageHits(0), 1.2f), "쌍비수 총 120%");
            Check(Mathf.Approximately(rogue.GetStageMultiplier(1) * rogue.GetStageHits(1), 2.25f), "맹독비수 총 225%");
            Check(Mathf.Approximately(rogue.GetStageMultiplier(2) * rogue.GetStageHits(2), 4.0f), "죽음의 촉매 총 400%");

            var mage = new MageEnergyBallActive();
            Check(Mathf.Approximately(mage.GetStageMultiplier(0), 1.4f)
               && Mathf.Approximately(mage.GetStageMultiplier(1), 1.1f)
               && Mathf.Approximately(mage.GetStageMultiplier(2), 1.8f), "마법사 배율 140/110/180%");

            var alch = new AlchemistThrowActive();
            Check(Mathf.Approximately(alch.GetStageMultiplier(0), 1.0f)
               && Mathf.Approximately(alch.GetStageMultiplier(1), 0.9f)
               && Mathf.Approximately(alch.GetStageMultiplier(2), 1.8f), "연금 배율 100/90/180%");
        }

        private static void TestSelectionActiveSummaries()
        {
            var go = new GameObject("CharacterSelectionSummarySelfTest");
            go.SetActive(false);
            var ui = go.AddComponent<UI.CharacterSelectionUI>();
            var buildSummary = typeof(UI.CharacterSelectionUI).GetMethod(
                "BuildActiveSummary",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Check(buildSummary != null, "선택창 액티브 요약 생성기를 찾을 수 있음");
            if (buildSummary == null)
            {
                UnityEngine.Object.DestroyImmediate(go);
                return;
            }

            var cases = new (string assetPath, string condition, string[] effects)[]
            {
                ("Assets/Scripts/Data/Character Preset/Warrior/Warrior.asset", "4 이상", new[] { "단일", "인접", "전장 전체", "방어도" }),
                ("Assets/Scripts/Data/Character Preset/Rogue/Rogue.asset", "3 이하", new[] { "2회", "3회", "4회", "중독", "2배" }),
                ("Assets/Scripts/Data/Character Preset/Mage/Mage.asset", "홀수", new[] { "단일", "2명", "마력 회로 전체", "감전" }),
                ("Assets/Scripts/Data/Character Preset/Alchemist/Alchemist.asset", "짝수", new[] { "단일", "인접", "전장 전체", "약화", "고양" }),
            };

            foreach (var testCase in cases)
            {
                var preset = AssetDatabase.LoadAssetAtPath<CharacterPreset>(testCase.assetPath);
                Check(preset != null, $"선택 설명 테스트 프리셋 로드: {testCase.assetPath}");
                if (preset == null) continue;

                var skill = preset.StartingActives.FirstOrDefault();
                Check(skill != null, $"{preset.CharacterName} 시작 액티브 존재");
                if (skill == null) continue;

                string text = buildSummary.Invoke(ui, new object[] { preset }) as string ?? string.Empty;
                int lineBreaks = text.Count(ch => ch == '\n');

                Check(lineBreaks == 0, $"{skill.GetType().Name} 선택 설명은 제목 없이 한 문장만 표시");
                Check(!text.Contains("<b>[") && !text.Contains("]</b>"), $"{skill.GetType().Name} 선택 설명에 액티브 제목 없음");
                Check(text.Contains(testCase.condition), $"{skill.GetType().Name} 선택 설명에 주사위 조건 포함");
                Check(testCase.effects.All(text.Contains), $"{skill.GetType().Name} 선택 설명에 단계별 핵심 효과 포함");
            }

            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void TestTutorialCombatProgress()
        {
            var warriorObject = new GameObject("TutorialProgressWarrior");
            var rogueObject = new GameObject("TutorialProgressRogue");
            var strangerObject = new GameObject("TutorialProgressStranger");
            var warrior = warriorObject.AddComponent<Character>();
            var rogue = rogueObject.AddComponent<Character>();
            var stranger = strangerObject.AddComponent<Character>();
            var progress = new TutorialCombatProgress(warrior, rogue);

            progress.Observe(stranger, 1, ComboOutcome.Advanced);
            progress.Observe(warrior, 0, ComboOutcome.Advanced);
            progress.Observe(rogue, 1, ComboOutcome.BrokenByDice);
            Check(!progress.WarriorStage1Done && !progress.RogueStage1Done,
                "튜토리얼 진행은 잘못된 캐릭터·단계·결과를 무시");

            progress.Observe(warrior, 1, ComboOutcome.Advanced);
            Check(progress.WarriorStage1Done, "전사 1단계 실제 발동을 인식");

            progress.Observe(rogue, 1, ComboOutcome.Advanced);
            Check(progress.RogueStage1Done, "도적 1단계 실제 발동을 인식");

            progress.Observe(warrior, 2, ComboOutcome.Advanced);
            Check(progress.WarriorStage2Done, "전사 2단계 실제 발동을 인식");

            progress.Observe(warrior, 0, ComboOutcome.Finished);
            Check(progress.WarriorFinished, "전사 3단계 완료를 인식");

            UnityEngine.Object.DestroyImmediate(warriorObject);
            UnityEngine.Object.DestroyImmediate(rogueObject);
            UnityEngine.Object.DestroyImmediate(strangerObject);
        }

        private static void TestTutorialScenarioSetup()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/TutorialScenario.prefab");
            var scenario = prefab != null ? prefab.GetComponent<TutorialScenario>() : null;
            Check(scenario != null, "튜토리얼 시나리오 프리팹 로드");
            if (scenario == null) return;

            Check(scenario.HasConfiguredPartyPresets, "튜토리얼 전사·도적 프리셋 연결");
            Check(scenario.HasConfiguredMonsterPresets, "튜토리얼 파란·초록 슬라임 프리셋 연결");

            var presets = scenario.BuildDemoMonsterPresets();
            Check(presets.Count == 4, "튜토리얼은 네 구역에 몬스터 한 마리씩 배치");
            if (presets.Count != 4) return;

            Check(presets[0] != null && presets[1] != null && presets[0] != presets[1],
                "0구역은 방진 체험용 파란 슬라임");
            Check(presets[1] == presets[2] && presets[2] == presets[3],
                "1~3구역은 같은 초록 슬라임 프리셋");
        }

        private static void TestTutorialGuidedComboGates()
        {
            var scenarioObject = new GameObject("TutorialGuidedComboScenario");
            var warriorObject = new GameObject("TutorialGuidedComboWarrior");
            var rogueObject = new GameObject("TutorialGuidedComboRogue");
            var scenario = scenarioObject.AddComponent<TutorialScenario>();
            var warrior = warriorObject.AddComponent<Character>();
            var rogue = rogueObject.AddComponent<Character>();

            typeof(TutorialScenario).GetProperty("Warrior")?.SetValue(scenario, warrior);
            typeof(TutorialScenario).GetProperty("Rogue")?.SetValue(scenario, rogue);

            bool createdCombo = ComboSystem.Instance == null;
            var combo = ComboSystem.EnsureInstance();
            combo.ResetAll();
            var steps = scenario.BuildCombatSteps();
            var guidedMoves = steps
                .Where(step => step.Advance == UI.Tutorial.TutorialAdvance.Custom && step.OnlyDieValue.HasValue)
                .ToList();

            Check(guidedMoves.Count == 4, "튜토리얼 직접 조작은 전사 3회·도적 1회");
            Check(guidedMoves.Count == 4
                  && guidedMoves.Select(step => step.OnlyDieValue.Value).SequenceEqual(new[] { 4, 2, 4, 4 }),
                "튜토리얼 고정 주사위 순서는 전사4·도적2·전사4·전사4");

            if (guidedMoves.Count == 4)
            {
                Check(!guidedMoves[0].Done() && !guidedMoves[1].Done() && !guidedMoves[2].Done(),
                    "실제 콤보 발동 전에는 이동 안내가 완료되지 않음");

                combo.ReportExecuted(warrior);
                Check(guidedMoves[0].Done() && !guidedMoves[1].Done(), "전사 1단계만 첫 안내를 완료");

                combo.ReportExecuted(rogue);
                Check(guidedMoves[1].Done(), "도적 1단계가 도적 안내를 완료");

                combo.ReportExecuted(warrior);
                Check(guidedMoves[2].Done(), "전사 2단계가 둘째 전사 안내를 완료");

                combo.ReportExecuted(warrior);
                Check(guidedMoves[3].Done(), "전사 3단계가 피날레 안내를 완료");
            }

            var handlerField = typeof(TutorialScenario).GetField("_onComboChanged", BindingFlags.Instance | BindingFlags.NonPublic);
            var handler = handlerField?.GetValue(scenario) as System.Action<Character, int, ComboOutcome>;
            if (handler != null)
                typeof(ComboSystem).GetEvent("OnComboChanged")?.RemoveEventHandler(combo, handler);

            UnityEngine.Object.DestroyImmediate(scenarioObject);
            UnityEngine.Object.DestroyImmediate(warriorObject);
            UnityEngine.Object.DestroyImmediate(rogueObject);
            if (createdCombo && combo != null) UnityEngine.Object.DestroyImmediate(combo.gameObject);
        }
    }
}
