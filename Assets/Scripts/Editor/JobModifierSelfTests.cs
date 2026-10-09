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
                TestStatusEnhancementPercent();
                TestEnhancerAliveOrDead();
                TestShockWithoutEnhancerUnchanged();
                TestRogueStage3Resolution();
                TestRogueStage3UsesOwner();
                TestVeteranFlankPercent();
                TestVenomStingPerHit();
                TestSteadfastFormationPercent();
                TestComradeshipCondition();
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

        // ── 상태 강화 (D5) ────────────────────────────────────

        private static void TestStatusEnhancementPercent()
        {
            Check(StatusEnhancement.EffectivePercent(25, false) == 25, "강화 없음 → 원래 값");
            Check(StatusEnhancement.EffectivePercent(25, true) == 40, "강화 → 40");
            Check(StatusEnhancement.EffectivePercent(50, true) == 50, "이미 40보다 크면 큰 값 유지");
            Check(!StatusEnhancement.IsEnhanced(EffectType.Inspire, null), "감전·촉매 외 상태는 강화 대상 아님");
        }

        private static void TestEnhancerAliveOrDead()
        {
            var mage = NewCharacter("VoltMage", JobIds.Mage);
            var alch = NewCharacter("PurityAlch", JobIds.Alchemist);
            var volt = new HighVoltageModifier();
            var purity = new HighPurityCatalystModifier();
            Check(volt.CanApplyTo(mage) && !volt.CanApplyTo(alch), "고전압 = 마법사 전용");
            Check(purity.CanApplyTo(alch) && !purity.CanApplyTo(mage), "고순도 촉매 = 연금술사 전용");
            Check(volt.Family == ModifierFamily.Combo && purity.Family == ModifierFamily.Combo, "둘 다 콤보 계열");

            mage.Stats.Modifiers.Add(volt);
            alch.Stats.Modifiers.Add(purity);
            var party = new List<Character> { mage, alch };

            Check(StatusEnhancement.IsEnhanced(EffectType.Shock, party) && StatusEnhancement.IsEnhanced(EffectType.Catalyst, party),
                  "보유자 생존 → 감전·촉매 강화");
            Check(StatusEnhancement.EffectivePercent(EffectType.Shock, 25, party) == 40, "고전압 보유 마법사 생존: 감전 25 → 40");

            mage.Stats.CurrentHP = 0;
            Check(!StatusEnhancement.IsEnhanced(EffectType.Shock, party), "마법사 사망 → 감전 강화 해제");
            Check(StatusEnhancement.EffectivePercent(EffectType.Shock, 25, party) == 25, "고전압 보유 마법사 사망: 감전 25");
            Check(StatusEnhancement.EffectivePercent(EffectType.Catalyst, 25, party) == 40, "연금술사는 살아 있어 촉매는 계속 40");
        }

        private static void TestShockWithoutEnhancerUnchanged()
        {
            // 매니저 없음 = 강화자 없음 → 기존 동작 그대로 (CombatRedesignSelfTests 회귀 방지)
            var victim = NewMonster("ShockVictim");
            var hitter = NewCharacter("ShockHitter");
            var shock = new ShockStatus(25);
            shock.SetOwner(victim);
            Check(shock.Value == 25, "강화자 없으면 부여값 그대로");

            var ctx = new AttackContext(hitter, victim, "타격", 10f);
            shock.OnAttack(CombatTrigger.OnCalculateOutput, ctx);
            Check(Mathf.Approximately(ctx.OutputValue, 12.5f), $"감전 25% → 12.5 (실제 {ctx.OutputValue})");
        }

        // ── 도적 3단계 (스펙 §3.4) ────────────────────────────

        private static void TestRogueStage3Resolution()
        {
            RogueAmbushActive.ResolveStage3(4, false, false, out int h0, out float s0);
            Check(h0 == 4 && Mathf.Approximately(s0, 1f), "기본 4회 ×1");
            RogueAmbushActive.ResolveStage3(4, true, false, out int h1, out float s1);
            Check(h1 == 5 && Mathf.Approximately(s1, 1f), "여분의 비수 → 5회");
            RogueAmbushActive.ResolveStage3(4, false, true, out int h2, out float s2);
            Check(h2 == 1 && Mathf.Approximately(s2, 4.3f), $"일제 타격 4→1 = ×4.3 (실제 {s2})");
            RogueAmbushActive.ResolveStage3(4, true, true, out int h3, out float s3);
            Check(h3 == 1 && Mathf.Approximately(s3, 5.4f), $"여분의 비수 + 일제 타격 5→1 = ×5.4 (실제 {s3})");
        }

        private static void TestRogueStage3UsesOwner()
        {
            var skill = new RogueAmbushActive();
            var rogue = NewCharacter("AmbushRogue", JobIds.Rogue, attack: 10);
            Check(new SpareDaggerModifier().CanApplyTo(rogue) && !new SpareDaggerModifier().CanApplyTo(NewCharacter("AmbushMage", JobIds.Mage)),
                  "여분의 비수 = 도적 전용");
            Check(skill.GetStageHits(rogue, 2) == 4 && skill.CalculateStageDamage(rogue, 2) == 10, "모디파이어 없음: 3단계 4회 × 10");
            Check(skill.GetStageHits(rogue, 0) == 2 && skill.GetStageHits(rogue, 1) == 3, "1·2단계 타격 수는 그대로");

            rogue.Stats.Modifiers.Add(new SpareDaggerModifier());
            Check(skill.GetStageHits(rogue, 2) == 5, "여분의 비수: 3단계 5회");
            Check(skill.GetStageDescription(rogue, 2).Contains("5회"), "설명도 5회");

            rogue.Stats.Modifiers.Add(new VolleyStrikeModifier());
            Check(skill.GetStageHits(rogue, 2) == 1, "일제 타격: 3단계 1회");
            int damage = skill.CalculateStageDamage(rogue, 2);
            Check(damage == 54, $"5→1: 공격력 10 × 100% × 5.4 = 54 (실제 {damage})");
            string desc = skill.GetStageDescription(rogue, 2);
            Check(desc.Contains("540%") && desc.Contains("1회"), $"설명 540% 1회 (실제 {desc})");

            var plan = new AttackPlan { Source = rogue, Kind = AttackPlanKind.Combo, ComboSkill = skill, Stage = 2 };
            Check(plan.Hits == 1, "AttackPlan.Hits(실행 루프·행동 예고)도 주인 기준");
            Check(skill.GetStageHits(null, 2) == 4 && skill.GetStageHits(2) == 4, "주인 없음·기존 시그니처 → 기본 4회");
        }

        // ── 도적 위치·콤보 ────────────────────────────────────

        private static void TestVeteranFlankPercent()
        {
            Check(Mathf.Approximately(RogueFlankPassive.EffectivePerAllyPercent(20f, false), 20f), "협공 기본 아군 1명당 20%");
            Check(Mathf.Approximately(RogueFlankPassive.EffectivePerAllyPercent(20f, true), 25f), "노련한 협공 아군 1명당 25%");
            var rogue = NewCharacter("VetRogue", JobIds.Rogue);
            var vet = new VeteranFlankModifier();
            Check(vet.CanApplyTo(rogue) && !vet.CanApplyTo(NewCharacter("VetWarrior", JobIds.Warrior)), "노련한 협공 = 도적 전용");
            Check(vet.Family == ModifierFamily.Position, "노련한 협공 = 위치 계열");
        }

        private static void TestVenomStingPerHit()
        {
            var rogue = NewCharacter("StingRogue", JobIds.Rogue);
            var sting = new VenomStingModifier();
            rogue.Stats.Modifiers.Add(sting);
            var heavy = NewMonster("StingHeavy");
            var light = NewMonster("StingLight");
            var heavyFx = NewEffects(heavy);
            var lightFx = NewEffects(light);
            heavyFx.AddEffect(StatusEffectManager.CreateEffect(EffectType.Poison, 10, -1));
            lightFx.AddEffect(StatusEffectManager.CreateEffect(EffectType.Poison, 9, -1));

            AttackActionScope.ResetAll();
            AttackActionScope.Begin(rogue, new AttackActionInfo { ComboStage = 1, IsAutoAttack = true });
            for (int hit = 0; hit < 3; hit++)
            {
                Hit(sting, rogue, heavy);
                Hit(sting, rogue, light);
                if (hit == 0) lightFx.AddEffect(StatusEffectManager.CreateEffect(EffectType.Poison, 1, -1));   // 행동 중 10 도달
            }
            AttackActionScope.End();
            Check(heavyFx.GetEffectValue(EffectType.Poison) == 13,
                  $"공격 직전 중독 10 → 실제 타격 3회마다 +1 = 13 (실제 {heavyFx.GetEffectValue(EffectType.Poison)})");
            Check(lightFx.GetEffectValue(EffectType.Poison) == 10,
                  $"행동 중 10 도달은 소급하지 않음 (실제 {lightFx.GetEffectValue(EffectType.Poison)})");

            AttackActionScope.Begin(rogue, new AttackActionInfo { ComboStage = -1, IsAutoAttack = true });
            Hit(sting, rogue, heavy);
            AttackActionScope.End();
            Check(heavyFx.GetEffectValue(EffectType.Poison) == 13, "기본공격엔 발동 안 함");

            using (AttackActionScope.Simulate(rogue, new AttackActionInfo { ComboStage = 2, IsAutoAttack = true }))
            {
                var sim = new AttackContext(rogue, heavy, "예고", 10f) { IsSimulation = true };
                sting.OnAttack(CombatTrigger.OnPreAction, sim);
                sting.OnAttack(CombatTrigger.OnPostAction, sim);
            }
            Check(heavyFx.GetEffectValue(EffectType.Poison) == 13, "행동 예고(시뮬레이션)는 상태를 바꾸지 않음");

            AttackActionScope.Begin(rogue, new AttackActionInfo { ComboStage = 2, IsAutoAttack = true });
            var killing = new AttackContext(rogue, heavy, "마지막 타격", 10f);
            sting.OnAttack(CombatTrigger.OnPreAction, killing);
            heavy.Stats.CurrentHP = 0;   // 이 타격으로 쓰러졌다
            sting.OnAttack(CombatTrigger.OnPostAction, killing);
            AttackActionScope.End();
            Check(heavyFx.GetEffectValue(EffectType.Poison) == 13, "타격으로 쓰러진 대상엔 부여 안 함");
        }

        /// <summary>파이프라인 한 타격의 OnPreAction → OnPostAction 통지만 흉내 낸다 (피해 적용 없음).</summary>
        private static void Hit(CharacterModifier modifier, Unit source, Unit target)
        {
            var ctx = new AttackContext(source, target, "타격", 10f);
            modifier.OnAttack(CombatTrigger.OnPreAction, ctx);
            modifier.OnAttack(CombatTrigger.OnPostAction, ctx);
        }

        // ── 전사 위치 ─────────────────────────────────────────

        private static void TestSteadfastFormationPercent()
        {
            Check(Mathf.Approximately(WarriorGuardPassive.EffectivePerAllyPercent(10f, false), 10f), "방진 기본 아군 1명당 10%");
            Check(Mathf.Approximately(WarriorGuardPassive.EffectivePerAllyPercent(10f, true), 15f), "견고한 방진 아군 1명당 15%");
            Check(Mathf.Approximately(WarriorGuardPassive.ReductionPercent(3, 15f), 45f), "아군 3명 × 15% = 45% 감소");
            var warrior = NewCharacter("SteadWarrior", JobIds.Warrior);
            var steadfast = new SteadfastFormationModifier();
            Check(steadfast.CanApplyTo(warrior) && !steadfast.CanApplyTo(NewCharacter("SteadRogue", JobIds.Rogue)), "견고한 방진 = 전사 전용");
            Check(steadfast.Family == ModifierFamily.Position, "견고한 방진 = 위치 계열");
        }

        private static void TestComradeshipCondition()
        {
            Check(ComradeshipModifier.BonusFor(0) == 0 && ComradeshipModifier.BonusFor(1) == 0, "같은 구역 다른 아군 0~1명: 보너스 없음");
            Check(ComradeshipModifier.BonusFor(2) == 1 && ComradeshipModifier.BonusFor(3) == 1, "2명 이상: 기본 공격력 +1 (미중첩)");

            var warrior = NewCharacter("ComradeWarrior", JobIds.Warrior, attack: 10);
            warrior.Stats.Modifiers.Add(new ComradeshipModifier());
            Check(warrior.Stats.EffectiveAttack == 10 && warrior.Stats.Attack == 10,
                  "구역 판정 불가(매니저 없음) → 보너스 없음, 저장 필드는 그대로");
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
