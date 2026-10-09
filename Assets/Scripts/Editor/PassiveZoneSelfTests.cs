using System;
using System.Collections.Generic;
using DiceOrbit.Data.Passives;
using DiceOrbit.Visuals;
using UnityEditor;
using UnityEngine;

namespace DiceOrbit.EditorTools
{
    /// <summary>
    /// 패시브 구역 표시 순수 로직 자가 테스트 (패시브 구역 표시 리워크 2026-10-04). 씬·플레이 모드 불필요.
    /// 메뉴 [DiceOrbit → Run PassiveZone Self-Tests] 또는 MCP RunCommand에서 RunAll() 호출.
    /// </summary>
    public static class PassiveZoneSelfTests
    {
        private static int _failures;

        [MenuItem("DiceOrbit/Run PassiveZone Self-Tests")]
        public static void RunFromMenu() => RunAll();

        public static bool RunAll()
        {
            _failures = 0;

            TestSectorStaysInsideZone();
            TestSectorIsClosedLoopWithoutJumps();
            TestSectorSharpCornersWhenRadiusZero();
            TestSectorRejectsBadArguments();
            TestSectorWorksForAnyZoneOffset();
            TestGuardStatus();
            TestFlankStatus();
            TestCircuitStatus();

            if (_failures == 0) Debug.Log("[PassiveZoneSelfTests] 전부 통과");
            else Debug.LogError($"[PassiveZoneSelfTests] 실패 {_failures}건");
            return _failures == 0;
        }

        // ── 부채꼴 테두리 기하 ─────────────────────────────────

        private const float Inner = 2.95f, Outer = 15.2f, Inset = 0.3f, Round = 0.9f;

        private static void TestSectorStaysInsideZone()
        {
            var points = new List<Vector3>();
            ZoneSectorShape.Build(-9f, 81f, Inner, Outer, Inset, Round, 3f, 0.1f, points);
            Check(points.Count > 20, "사분면 테두리는 점이 충분하다");

            const float eps = 1e-3f;
            foreach (var p in points)
            {
                float r = new Vector2(p.x, p.z).magnitude;
                // 안쪽 모서리의 베지어는 호의 현을 타고 들어와 호보다 살짝 안쪽을 지난다 (시위 높이 ≈ 0.03)
                Check(r >= Inner - 0.05f && r <= Outer + eps, $"모든 점이 안·밖 반지름 사이 (r={r:0.###})");
                Check(DistanceFromRay(p, -9f) >= Inset - eps, "시작 경계에서 들임 거리 이상 떨어져 있다");
                Check(DistanceFromRay(p, 81f) <= -(Inset - eps), "끝 경계에서 들임 거리 이상 떨어져 있다");
                Check(Mathf.Approximately(p.y, 0.1f), "모든 점이 같은 높이");
            }
        }

        private static void TestSectorIsClosedLoopWithoutJumps()
        {
            var points = new List<Vector3>();
            ZoneSectorShape.Build(0f, 90f, Inner, Outer, Inset, Round, 3f, 0f, points);

            float longest = 0f, shortest = float.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                float d = Vector3.Distance(points[i], points[(i + 1) % points.Count]);   // 마지막 → 첫 점 포함
                longest = Mathf.Max(longest, d);
                shortest = Mathf.Min(shortest, d);
            }
            float straightEdge = Outer - Inner;   // 가장 긴 선분은 경계 직선 변 — 그보다 길면 점이 건너뛴 것
            Check(longest <= straightEdge, $"폐곡선에 건너뛰는 구간이 없다 (최장 {longest:0.##})");
            Check(shortest > 1e-4f, "겹치는 점(길이 0 선분)이 없다");
        }

        private static void TestSectorSharpCornersWhenRadiusZero()
        {
            var points = new List<Vector3>();
            ZoneSectorShape.Build(0f, 90f, Inner, Outer, Inset, 0f, 3f, 0f, points);

            // 각진 모서리 넷이 정확히 들임 선과 호의 교점에 있다
            float outerSlack = Mathf.Asin(Inset / Outer) * Mathf.Rad2Deg;
            float innerSlack = Mathf.Asin(Inset / Inner) * Mathf.Rad2Deg;
            Check(Contains(points, ZoneSectorShape.Polar(outerSlack, Outer, 0f)), "바깥-시작 모서리");
            Check(Contains(points, ZoneSectorShape.Polar(90f - outerSlack, Outer, 0f)), "바깥-끝 모서리");
            Check(Contains(points, ZoneSectorShape.Polar(90f - innerSlack, Inner, 0f)), "안쪽-끝 모서리");
            Check(Contains(points, ZoneSectorShape.Polar(innerSlack, Inner, 0f)), "안쪽-시작 모서리");
        }

        private static void TestSectorRejectsBadArguments()
        {
            var points = new List<Vector3>();
            ExpectThrows<ArgumentException>(() => ZoneSectorShape.Build(0f, 90f, 5f, 4f, 0.3f, 0.5f, 3f, 0f, points), "밖 반지름 ≤ 안 반지름은 예외");
            ExpectThrows<ArgumentException>(() => ZoneSectorShape.Build(0f, 90f, 0.2f, 4f, 0.3f, 0.5f, 3f, 0f, points), "들임 ≥ 안 반지름은 예외");
            ExpectThrows<ArgumentException>(() => ZoneSectorShape.Build(0f, 8f, 2f, 10f, 0.3f, 0.5f, 3f, 0f, points), "들임보다 좁은 구역은 예외");
            ExpectThrows<ArgumentException>(() => ZoneSectorShape.Build(0f, 90f, 2f, 10f, 0.3f, 0.5f, 0f, 0f, points), "호 간격 0은 예외");
            ExpectThrows<ArgumentNullException>(() => ZoneSectorShape.Build(0f, 90f, 2f, 10f, 0.3f, 0.5f, 3f, 0f, null), "점 목록 null은 예외");
        }

        private static void TestSectorWorksForAnyZoneOffset()
        {
            // 구역 넷(반 칸 밀림 포함)을 다 그려도 서로 겹치지 않는다 — 이웃 테두리 사이 간격 = 들임 × 2
            var a = new List<Vector3>();
            var b = new List<Vector3>();
            ZoneSectorShape.Build(261f, 351f, Inner, Outer, Inset, Round, 3f, 0f, a);
            ZoneSectorShape.Build(351f, 441f, Inner, Outer, Inset, Round, 3f, 0f, b);

            float closest = float.MaxValue;
            foreach (var p in a)
                foreach (var q in b)
                    closest = Mathf.Min(closest, Vector3.Distance(p, q));
            Check(closest >= Inset * 2f - 1e-3f, $"이웃 구역 테두리는 들임 두 배 이상 떨어진다 (최소 {closest:0.###})");
        }

        // ── 지금 효과 한 줄 ───────────────────────────────────

        private static void TestGuardStatus()
        {
            var off = WarriorGuardPassive.FormatZoneStatus(0, 10f);
            Check(!off.Active && off.Effect == "같은 구역 아군 없음", "방진: 아군 없으면 꺼짐");

            var two = WarriorGuardPassive.FormatZoneStatus(2, 10f);
            Check(two.Active && two.Effect == "받는 피해 -20%", "방진: 아군 2명 = -20%");

            Check(Mathf.Approximately(WarriorGuardPassive.ReductionPercent(20, 10f), 90f), "방진: 감쇄율 상한 90%");
            Check(WarriorGuardPassive.FormatZoneStatus(20, 10f).Effect == "받는 피해 -90%", "방진: 표시도 상한을 따른다");
            Check(WarriorGuardPassive.ReductionPercent(-1, 10f) == 0f, "방진: 음수 인원은 0%");
        }

        private static void TestFlankStatus()
        {
            var off = RogueFlankPassive.FormatZoneStatus(0, 20f);
            Check(!off.Active && off.Effect == "같은 구역 아군 없음", "협공: 아군 없으면 꺼짐");

            var three = RogueFlankPassive.FormatZoneStatus(3, 20f);
            Check(three.Active && three.Effect == "주는 피해 +60%", "협공: 아군 3명 = +60%");

            Check(RogueFlankPassive.FormatZoneStatus(1, 12.5f).Effect == "주는 피해 +12.5%", "협공: 소수 한 자리까지");
        }

        private static void TestCircuitStatus()
        {
            var alone = MageRangedPassive.FormatZoneStatus(1);
            Check(!alone.Active && alone.Effect == "자기 구역만", "마력 회로: 자기 구역뿐이면 꺼짐");
            Check(!MageRangedPassive.FormatZoneStatus(0).Active, "마력 회로: 구역 밖(0)도 꺼짐");

            var three = MageRangedPassive.FormatZoneStatus(3);
            Check(three.Active && three.Effect == "3구역 연결", "마력 회로: 3구역 연결");
        }

        // ── 도우미 ────────────────────────────────────────────

        /// <summary>원점에서 deg 방향으로 뻗는 반직선으로부터의 부호 있는 거리 (반시계 쪽이 +).</summary>
        private static float DistanceFromRay(Vector3 p, float deg)
        {
            float rad = deg * Mathf.Deg2Rad;
            return -Mathf.Sin(rad) * p.x + Mathf.Cos(rad) * p.z;
        }

        private static bool Contains(List<Vector3> points, Vector3 target)
        {
            foreach (var p in points)
                if (Vector3.Distance(p, target) < 1e-3f) return true;
            return false;
        }

        private static void Check(bool condition, string what)
        {
            if (condition) return;
            _failures++;
            Debug.LogError("[PassiveZoneSelfTests] FAIL: " + what);
        }

        private static void ExpectThrows<T>(Action action, string what) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            catch (Exception e) { _failures++; Debug.LogError($"[PassiveZoneSelfTests] FAIL: {what} — 다른 예외 {e.GetType().Name}"); return; }
            _failures++;
            Debug.LogError("[PassiveZoneSelfTests] FAIL: " + what + " — 예외가 나지 않음");
        }
    }
}
