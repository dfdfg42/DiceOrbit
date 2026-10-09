using System;
using System.Collections.Generic;
using DiceOrbit.Core.Run;
using DiceOrbit.Data.Modifiers;
using UnityEditor;
using UnityEngine;

namespace DiceOrbit.EditorTools
{
    /// <summary>
    /// 보상 흐름 순수 로직 자가 테스트 (보상 리워크 2026-10-03). 씬·플레이 모드 불필요.
    /// 메뉴 [DiceOrbit → Run Reward Flow Self-Tests] 또는 MCP RunCommand에서 RunAll() 호출.
    /// </summary>
    public static class RewardFlowSelfTests
    {
        private static int _failures;

        [MenuItem("DiceOrbit/Run Reward Flow Self-Tests")]
        public static void RunFromMenu() => RunAll();

        public static bool RunAll()
        {
            _failures = 0;

            TestBothBeatsRunInOrderThenFinish();
            TestUpgradeOnly();
            TestDiceOnly();
            TestNoChoiceBeatsNeedsWrap();
            TestBlockedLootForcesWrapAfterChoices();
            TestBlockedLootClearedBeforeEndSkipsWrap();
            TestResolveAfterFinishedThrows();
            TestChoicesWhereAreDistinctAndClamped();
            TestChoicesWhereExcludesIneligible();
            TestChoicesForEmptyPartyIsEmpty();
            TestEveryRegistryModifierHasFamilyAndLabel();

            if (_failures == 0) Debug.Log("[RewardFlowSelfTests] 전부 통과");
            else Debug.LogError($"[RewardFlowSelfTests] 실패 {_failures}건");
            return _failures == 0;
        }

        // ── 박자 순서 ─────────────────────────────────────────

        private static void TestBothBeatsRunInOrderThenFinish()
        {
            var flow = new RewardFlow(hasUpgrade: true, hasDice: true);
            Check(flow.ChoiceBeats.Count == 2, "둘 다 있으면 결정 박자 2개");
            Check(flow.Current == RewardBeat.Upgrade, "첫 박자 = 강화");
            flow.Resolve();
            Check(flow.Current == RewardBeat.Dice, "강화 다음 = 주사위");
            flow.Resolve();
            Check(flow.Current == RewardBeat.Finished && flow.IsFinished, "마지막 결정 뒤 자동 종료 (Wrap 없음)");
        }

        private static void TestUpgradeOnly()
        {
            var flow = new RewardFlow(hasUpgrade: true, hasDice: false);
            Check(flow.Current == RewardBeat.Upgrade, "강화만: 첫 박자 = 강화");
            flow.Resolve();
            Check(flow.IsFinished, "강화만: 강화 뒤 종료");
        }

        private static void TestDiceOnly()
        {
            var flow = new RewardFlow(hasUpgrade: false, hasDice: true);
            Check(flow.Current == RewardBeat.Dice, "주사위만: 첫 박자 = 주사위");
            flow.Resolve();
            Check(flow.IsFinished, "주사위만: 주사위 뒤 종료");
        }

        private static void TestNoChoiceBeatsNeedsWrap()
        {
            var flow = new RewardFlow(hasUpgrade: false, hasDice: false);
            Check(flow.Current == RewardBeat.Wrap, "결정 박자가 없으면 [계속] 박자 — 전리품을 볼 틈");
            flow.Resolve();
            Check(flow.IsFinished, "[계속] 뒤 종료");
        }

        private static void TestBlockedLootForcesWrapAfterChoices()
        {
            var flow = new RewardFlow(hasUpgrade: true, hasDice: false) { HasBlockedLoot = true };
            flow.Resolve();
            Check(flow.Current == RewardBeat.Wrap, "못 받은 전리품이 남으면 자동 종료 대신 [계속]");
            flow.Resolve();
            Check(flow.IsFinished, "막힌 전리품 [계속] 뒤 종료");
        }

        private static void TestBlockedLootClearedBeforeEndSkipsWrap()
        {
            var flow = new RewardFlow(hasUpgrade: true, hasDice: false) { HasBlockedLoot = true };
            flow.HasBlockedLoot = false;   // 결정 중에 포션을 받아 막힘이 풀렸다
            flow.Resolve();
            Check(flow.IsFinished, "막힘이 풀리면 Wrap 없이 종료");
        }

        private static void TestResolveAfterFinishedThrows()
        {
            var flow = new RewardFlow(hasUpgrade: true, hasDice: false);
            flow.Resolve();
            ExpectThrows<InvalidOperationException>(() => flow.Resolve(), "끝난 흐름 Resolve는 예외");
        }

        // ── 제시 풀 ───────────────────────────────────────────

        private static void TestChoicesWhereAreDistinctAndClamped()
        {
            var three = ModifierRegistry.GetRandomChoicesWhere(_ => true, 3);
            Check(three.Count == 3, "전체 풀에서 3장");
            var types = new List<Type>();
            foreach (var m in three) { Check(!types.Contains(m.GetType()), "제시는 종류 중복 없음"); types.Add(m.GetType()); }

            int all = ModifierRegistry.CreateAll().Count;
            Check(ModifierRegistry.GetRandomChoicesWhere(_ => true, all + 5).Count == all, "풀보다 많이 요청하면 풀 크기로 잘림");
            Check(ModifierRegistry.GetRandomChoicesWhere(_ => true, 0).Count == 0, "0장 요청 = 빈 목록");
        }

        private static void TestChoicesWhereExcludesIneligible()
        {
            var banned = ModifierRegistry.CreateAll()[0].GetType();
            int all = ModifierRegistry.CreateAll().Count;
            for (int round = 0; round < 20; round++)
            {
                var picks = ModifierRegistry.GetRandomChoicesWhere(m => m.GetType() != banned, all);
                foreach (var m in picks) Check(m.GetType() != banned, "조건에서 탈락한 종류는 제시되지 않는다");
                Check(picks.Count == all - 1, "탈락 1종을 뺀 나머지 전부");
            }
            Check(ModifierRegistry.GetRandomChoicesWhere(_ => false, 3).Count == 0, "받을 수 있는 종류가 없으면 빈 목록");
        }

        private static void TestChoicesForEmptyPartyIsEmpty()
        {
            Check(ModifierRegistry.GetRandomChoicesForParty(null, 3).Count == 0, "파티 null = 빈 목록 (전체 풀로 대신 뽑지 않는다)");
            Check(ModifierRegistry.GetRandomChoicesForParty(new List<DiceOrbit.Core.Character>(), 3).Count == 0, "빈 파티 = 빈 목록");
        }

        private static void TestEveryRegistryModifierHasFamilyAndLabel()
        {
            var perFamily = new Dictionary<ModifierFamily, int>();
            foreach (var m in ModifierRegistry.CreateAll())
            {
                Check(m.Family != ModifierFamily.None, $"{m.GetType().Name}: 계열 지정됨");
                if (m.Family == ModifierFamily.None) continue;
                Check(!string.IsNullOrEmpty(m.Family.Label()), $"{m.GetType().Name}: 계열 라벨 있음");
                perFamily.TryGetValue(m.Family, out int n);
                perFamily[m.Family] = n + 1;
            }
            foreach (var kv in perFamily) Check(kv.Value >= 1, $"{kv.Key}: 계열당 1종 이상 (실제 {kv.Value})");
            Check(perFamily.Count == 4, "4계열 모두 1종 이상");
            ExpectThrows<ArgumentOutOfRangeException>(() => ModifierFamily.None.Label(), "None 계열 라벨은 예외");
        }

        // ── 도우미 ────────────────────────────────────────────

        private static void Check(bool condition, string what)
        {
            if (condition) return;
            _failures++;
            Debug.LogError("[RewardFlowSelfTests] FAIL: " + what);
        }

        private static void ExpectThrows<T>(Action action, string what) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            catch (Exception e) { _failures++; Debug.LogError($"[RewardFlowSelfTests] FAIL: {what} — 다른 예외 {e.GetType().Name}"); return; }
            _failures++;
            Debug.LogError("[RewardFlowSelfTests] FAIL: " + what + " — 예외가 나지 않음");
        }
    }
}
