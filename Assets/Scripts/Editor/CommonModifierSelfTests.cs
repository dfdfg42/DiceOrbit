using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using DiceOrbit.Data.Modifiers;
using DiceOrbit.Data.Modifiers.Common;
using DiceOrbit.Systems.Effects;
using UnityEditor;
using UnityEngine;

namespace DiceOrbit.EditorTools
{
    /// <summary>
    /// 캐릭터 시트 동기화 B(공용 모디파이어 17종·충전·방어도 턴 초기화) 순수 로직 자가 테스트. 씬·플레이 모드 불필요.
    /// 메뉴 [DiceOrbit → Run Common Modifier Self-Tests] 또는 MCP RunCommand에서 RunAll() 호출.
    /// 유닛은 비활성 GameObject에 AddComponent로 만들고(Awake 미실행) 끝나면 전부 파괴한다.
    /// </summary>
    public static class CommonModifierSelfTests
    {
        private static int _failures;
        private static readonly List<GameObject> _spawned = new List<GameObject>();

        [MenuItem("DiceOrbit/Run Common Modifier Self-Tests")]
        public static void RunFromMenu() => RunAll();

        public static bool RunAll()
        {
            _failures = 0;
            // AddEffect → EffectApplied → VfxService.EnsureInstance가 만든 인스턴스를 정리하기 위해 사전 상태 기록
            bool vfxExisted = Object.FindFirstObjectByType<DiceOrbit.Visuals.VfxService>() != null;
            try
            {
                TestChargeGrantCapAndConsume();
                TestChargeOnlyEmpoweredAttacks();
                TestCharacterArmorResetsOnOwnTurnStart();
                TestDuplicateTypeCannotBeEquipped();
                TestAcquireCallsOnAcquiredButAddDoesNot();
                TestEffectiveAttackSumsBonuses();
                TestAttackActionResultDefaults();
                TestGrandFinaleOnlyStage3();
                TestRescuePicksLowestHurtAlly();
                TestFractureBonusOnlyForFirstUnarmoredTarget();
                TestBasicTrainingAddsAttack();
                TestMaxHpGainAndRestoreDoesNotReapply();
                TestRegistryHas17WithFamilies();
                // [tests-run]
            }
            finally
            {
                AttackActionScope.ResetAll();
                foreach (var go in _spawned)
                    if (go != null) Object.DestroyImmediate(go);
                _spawned.Clear();
                if (!vfxExisted && DiceOrbit.Visuals.VfxService.Instance != null)
                    Object.DestroyImmediate(DiceOrbit.Visuals.VfxService.Instance.gameObject);
            }

            if (_failures == 0) Debug.Log("[CommonModifierSelfTests] 전부 통과");
            else Debug.LogError($"[CommonModifierSelfTests] 실패 {_failures}건");
            return _failures == 0;
        }

        // ── 충전 ──────────────────────────────────────────────

        private static void TestChargeGrantCapAndConsume()
        {
            var holder = NewCharacter("ChargeHolder");
            var effects = NewEffects(holder);

            Check(ChargeStatus.Grant(effects, 2) == 2 && effects.GetEffectValue(EffectType.Charge) == 2, "충전 2 부여 → 2");
            Check(ChargeStatus.Grant(effects, 2) == 1 && effects.GetEffectValue(EffectType.Charge) == 3, "상한 3 — 2를 더 줘도 1만 들어간다");
            Check(ChargeStatus.Grant(effects, 1) == 0 && effects.GetEffectValue(EffectType.Charge) == 3, "가득 찬 상태에서 부여는 무시");

            ChargeStatus.ConsumeOne(effects);
            Check(effects.GetEffectValue(EffectType.Charge) == 2, "ConsumeOne 1회 → 2");
            ChargeStatus.ConsumeOne(effects);
            ChargeStatus.ConsumeOne(effects);
            Check(!effects.HasEffect(EffectType.Charge), "0이 되면 상태가 제거된다");
            ChargeStatus.ConsumeOne(effects);
            Check(!effects.HasEffect(EffectType.Charge), "충전이 없을 때 ConsumeOne은 무해");
        }

        private static void TestChargeOnlyEmpoweredAttacks()
        {
            var attacker = NewCharacter("ChargeAttacker");
            var target = NewMonster("ChargeTarget");
            var charge = new ChargeStatus(2);
            charge.SetOwner(attacker);

            AttackActionScope.ResetAll();
            AttackActionScope.Begin(attacker, new AttackActionInfo { ComboStage = 0, IsAutoAttack = true });
            var empowered = new AttackContext(attacker, target, "강화", 10f);
            charge.OnAttack(CombatTrigger.OnCalculateOutput, empowered);
            AttackActionScope.End();
            Check(Mathf.Approximately(empowered.OutputValue, 13f), $"강화 공격 +15%×2 = 13 (실제 {empowered.OutputValue})");

            AttackActionScope.Begin(attacker, new AttackActionInfo { ComboStage = -1, IsAutoAttack = true });
            var basic = new AttackContext(attacker, target, "기본", 10f);
            charge.OnAttack(CombatTrigger.OnCalculateOutput, basic);
            AttackActionScope.End();
            Check(Mathf.Approximately(basic.OutputValue, 10f), "기본공격에는 충전 보정 없음");

            var outside = new AttackContext(attacker, target, "스코프 밖", 10f);
            charge.OnAttack(CombatTrigger.OnCalculateOutput, outside);
            Check(Mathf.Approximately(outside.OutputValue, 10f), "행동 스코프 밖 피해에는 보정 없음");

            using (AttackActionScope.Simulate(attacker, new AttackActionInfo { ComboStage = 2, IsAutoAttack = true }))
            {
                var sim = new AttackContext(attacker, target, "예고", 10f) { IsSimulation = true };
                charge.OnAttack(CombatTrigger.OnCalculateOutput, sim);
                Check(Mathf.Approximately(sim.OutputValue, 13f), "행동 예고(시뮬레이션)에도 같은 보정");
            }

            AttackActionScope.Begin(attacker, new AttackActionInfo { ComboStage = 1, IsAutoAttack = true });
            var poison = new AttackContext(attacker, target, "중독", 10f) { IsDirectHpLoss = true };
            charge.OnAttack(CombatTrigger.OnCalculateOutput, poison);
            AttackActionScope.End();
            Check(Mathf.Approximately(poison.OutputValue, 10f), "직접 체력 손실엔 개입 안 함");
        }

        // ── 방어도 턴 초기화 (D2) ─────────────────────────────

        private static void TestCharacterArmorResetsOnOwnTurnStart()
        {
            var a = NewCharacter("ArmorA");
            var b = NewCharacter("ArmorB");
            Check(a.Stats.Owner == a, "InitializeStats가 Stats.Owner를 주입한다");

            a.Stats.TempArmor = 7;
            b.Stats.TempArmor = 5;
            var aTurnStart = new TurnEventContext(a, a, EventPhase.TurnStart);
            a.Stats.OnTurnEvent(CombatTrigger.OnPreAction, aTurnStart);
            b.Stats.OnTurnEvent(CombatTrigger.OnPreAction, aTurnStart);   // 방송은 파티 전원의 Stats에 간다
            Check(a.Stats.TempArmor == 0, "자기 TurnStart(OnPreAction)에 방어도 0");
            Check(b.Stats.TempArmor == 5, "다른 캐릭터의 TurnStart로는 초기화되지 않는다");

            a.Stats.TempArmor = 4;
            a.Stats.OnTurnEvent(CombatTrigger.OnPostAction, aTurnStart);
            Check(a.Stats.TempArmor == 4, "초기화는 OnPreAction 한 번만 (OnPostAction에선 안 함)");
            a.Stats.OnTurnEvent(CombatTrigger.OnPreAction, new TurnEventContext(a, a, EventPhase.TurnEnd));
            Check(a.Stats.TempArmor == 4, "TurnEnd에는 초기화하지 않는다");

            var monster = NewMonster("ArmorMonster");
            var monsterStats = new MonsterStats { TempArmor = 6 };
            monsterStats.OnTurnEvent(CombatTrigger.OnPreAction, new TurnEventContext(monster, monster, EventPhase.TurnStart));
            Check(monsterStats.TempArmor == 0, "몬스터 턴 시작 초기화는 기존대로");
            var c = NewCharacter("ArmorC");
            c.Stats.TempArmor = 3;
            c.Stats.OnTurnEvent(CombatTrigger.OnPreAction, new TurnEventContext(monster, monster, EventPhase.TurnStart));
            Check(c.Stats.TempArmor == 3, "몬스터 턴 시작은 캐릭터 방어도를 건드리지 않는다");
        }

        // ── 장착 규칙·획득·유효 공격력 ────────────────────────

        /// <summary>테스트 전용 — 공격력 보너스만 있는 모디파이어.</summary>
        private sealed class BonusProbeModifier : CharacterModifier
        {
            public override string ModifierName => "테스트 보너스";
            public override string Description => string.Empty;
            public override ModifierCategory Category => ModifierCategory.Generic;
            public override int AttackBonus => 2;
        }

        /// <summary>테스트 전용 — OnAcquired 호출 횟수를 센다.</summary>
        private sealed class AcquireProbeModifier : CharacterModifier
        {
            public int AcquiredCount;
            public override string ModifierName => "테스트 획득";
            public override string Description => string.Empty;
            public override ModifierCategory Category => ModifierCategory.Generic;
            public override void OnAcquired(Character owner) => AcquiredCount++;
        }

        private static void TestDuplicateTypeCannotBeEquipped()
        {
            var ch = NewCharacter("DupHolder");
            var first = new BonusProbeModifier();
            Check(first.CanApplyTo(ch), "처음엔 장착 가능");
            ch.Stats.Modifiers.Add(first);
            Check(!new BonusProbeModifier().CanApplyTo(ch), "같은 종류 두 번째는 CanApplyTo false");
            Check(new AcquireProbeModifier().CanApplyTo(ch), "다른 종류는 여전히 장착 가능");

            ch.Stats.Modifiers.Add(new BonusProbeModifier());
            Check(ch.Stats.Modifiers.Modifiers.Count == 1, "Add도 같은 종류 중복을 거부한다 (옛 세이브 3중첩 방어)");
        }

        private static void TestAcquireCallsOnAcquiredButAddDoesNot()
        {
            var acquired = NewCharacter("AcquireHolder");
            var viaAcquire = new AcquireProbeModifier();
            acquired.Stats.Modifiers.Acquire(viaAcquire);
            Check(viaAcquire.AcquiredCount == 1 && viaAcquire.owner == acquired, "Acquire = 장착 + OnAcquired 1회");
            acquired.Stats.Modifiers.Acquire(new AcquireProbeModifier());
            Check(acquired.Stats.Modifiers.Modifiers.Count == 1, "Acquire도 중복은 거부");

            var restored = NewCharacter("RestoreHolder");
            var viaAdd = new AcquireProbeModifier();
            restored.Stats.Modifiers.Add(viaAdd);
            Check(viaAdd.AcquiredCount == 0 && restored.Stats.Modifiers.Modifiers.Count == 1, "Add(세이브 복원 경로)는 OnAcquired를 부르지 않는다");
        }

        private static void TestEffectiveAttackSumsBonuses()
        {
            var ch = NewCharacter("AttackHolder", attack: 10);
            Check(ch.Stats.EffectiveAttack == 10, "보너스 없으면 유효 공격력 = 기본 공격력");
            ch.Stats.Modifiers.Add(new BonusProbeModifier());
            Check(ch.Stats.EffectiveAttack == 12 && ch.Stats.Attack == 10, "보너스 +2 → 12, 저장 필드(Attack)는 그대로");
        }

        // ── 공격 행동 결과 ────────────────────────────────────

        private static void TestAttackActionResultDefaults()
        {
            var empty = new AttackActionResult(-1, null, 0);
            Check(empty.Targets != null && empty.Targets.Count == 0, "대상 목록 null → 빈 목록");
            Check(!empty.IsEmpowered, "단계 -1 = 기본공격");

            var monster = NewMonster("ResultTarget");
            var finale = new AttackActionResult(2, new List<Unit> { monster }, 1);
            Check(finale.IsEmpowered && finale.ComboStage == 2 && finale.Targets.Count == 1 && finale.Kills == 1, "강화 3단계 결과 보존");
        }

        // ── 콤보 계열 ─────────────────────────────────────────

        private static void TestGrandFinaleOnlyStage3()
        {
            var ch = NewCharacter("FinaleHolder");
            var target = NewMonster("FinaleTarget");
            var finale = new GrandFinaleModifier();
            ch.Stats.Modifiers.Add(finale);
            Check(finale is CommonModifier, "대단원은 CommonModifier 골격");

            AttackActionScope.ResetAll();
            AttackActionScope.Begin(ch, new AttackActionInfo { ComboStage = 2, IsAutoAttack = true });
            var stage3 = new AttackContext(ch, target, "3단계", 10f);
            finale.OnAttack(CombatTrigger.OnCalculateOutput, stage3);
            AttackActionScope.End();
            Check(Mathf.Approximately(stage3.OutputValue, 12.5f), $"3단계 +25% (실제 {stage3.OutputValue})");

            AttackActionScope.Begin(ch, new AttackActionInfo { ComboStage = 1, IsAutoAttack = true });
            var stage2 = new AttackContext(ch, target, "2단계", 10f);
            finale.OnAttack(CombatTrigger.OnCalculateOutput, stage2);
            AttackActionScope.End();
            Check(Mathf.Approximately(stage2.OutputValue, 10f), "2단계엔 보정 없음");
        }

        // ── 위치 계열 ─────────────────────────────────────────

        private static void TestRescuePicksLowestHurtAlly()
        {
            var full = NewCharacter("RescueFull", hp: 50);
            var hurt = NewCharacter("RescueHurt", hp: 50);
            var worst = NewCharacter("RescueWorst", hp: 50);
            hurt.Stats.CurrentHP = 30;
            worst.Stats.CurrentHP = 20;

            Check(RescueTeamModifier.PickLowestHp(new List<Character> { full, hurt, worst }) == worst, "현재 체력이 가장 낮은 아군");
            Check(RescueTeamModifier.PickLowestHp(new List<Character> { full }) == null, "최대 체력인 아군은 대상에서 제외");

            var tie = NewCharacter("RescueTie", hp: 50);
            tie.Stats.CurrentHP = 20;
            for (int i = 0; i < 10; i++)
            {
                var pick = RescueTeamModifier.PickLowestHp(new List<Character> { worst, tie, hurt });
                Check(pick == worst || pick == tie, "동률이면 동률 중 하나 (무작위)");
            }
        }

        private static void TestFractureBonusOnlyForFirstUnarmoredTarget()
        {
            var attacker = NewCharacter("FractureAttacker");
            var first = NewCharacter("FractureFirst");     // 대상은 Stats가 확실한 캐릭터로 대신한다 (균열은 대상 종류를 가리지 않는다)
            var second = NewCharacter("FractureSecond");
            var fracture = new FractureModifier();
            attacker.Stats.Modifiers.Add(fracture);

            AttackActionScope.ResetAll();
            AttackActionScope.Begin(attacker, new AttackActionInfo { ComboStage = 0, IsAutoAttack = true });

            var pre1 = new AttackContext(attacker, first, "균열", 10f);
            fracture.OnAttack(CombatTrigger.OnPreAction, pre1);
            var calc1 = new AttackContext(attacker, first, "균열", 10f);
            fracture.OnAttack(CombatTrigger.OnCalculateOutput, calc1);
            Check(Mathf.Approximately(calc1.OutputValue, 11.5f), $"처음부터 방어도 없는 첫 대상 +15% (실제 {calc1.OutputValue})");

            var pre2 = new AttackContext(attacker, second, "균열", 10f);
            fracture.OnAttack(CombatTrigger.OnPreAction, pre2);
            var calc2 = new AttackContext(attacker, second, "균열", 10f);
            fracture.OnAttack(CombatTrigger.OnCalculateOutput, calc2);
            Check(Mathf.Approximately(calc2.OutputValue, 10f), "같은 행동의 두 번째 대상엔 보정 없음");

            var calc1Again = new AttackContext(attacker, first, "균열", 10f);
            fracture.OnAttack(CombatTrigger.OnCalculateOutput, calc1Again);
            Check(Mathf.Approximately(calc1Again.OutputValue, 11.5f), "같은 행동 안 첫 대상의 다음 타격도 +15%");
            AttackActionScope.End();

            using (AttackActionScope.Simulate(attacker, new AttackActionInfo { ComboStage = 1, IsAutoAttack = true }))
            {
                second.Stats.TempArmor = 5;
                var simArmored = new AttackContext(attacker, second, "예고", 10f) { IsSimulation = true };
                fracture.OnAttack(CombatTrigger.OnCalculateOutput, simArmored);
                Check(Mathf.Approximately(simArmored.OutputValue, 10f) && second.Stats.TempArmor == 5, "예고: 방어도 있는 대상은 보정 없음, 상태 변경 없음");

                var simBare = new AttackContext(attacker, first, "예고", 10f) { IsSimulation = true };
                fracture.OnAttack(CombatTrigger.OnCalculateOutput, simBare);
                Check(Mathf.Approximately(simBare.OutputValue, 11.5f), "예고: 현재 방어도 0이면 +15%");
            }
        }

        private static void TestBasicTrainingAddsAttack()
        {
            var ch = NewCharacter("TrainingHolder", attack: 10);
            ch.Stats.Modifiers.Add(new BasicTrainingModifier());
            Check(ch.Stats.EffectiveAttack == 12, "기본기 단련 장착 시 유효 공격력 +2");
            Check(ch.Stats.Attack == 10, "기본 공격력 필드는 그대로 (세이브 재유도 보호)");
        }

        // ── 생존 계열 ─────────────────────────────────────────

        private static void TestMaxHpGainAndRestoreDoesNotReapply()
        {
            var stats = new CharacterStats { MaxHP = 50, CurrentHP = 40 };
            Check(CommonModifier.ApplyMaxHpGain(stats, 10) == 10 && stats.MaxHP == 60 && stats.CurrentHP == 50, "최대·현재 체력 +10 (50/40 → 60/50)");
            CommonModifier.ApplyMaxHpGain(stats, 2);
            Check(stats.MaxHP == 62 && stats.CurrentHP == 52, "사냥 1처치 +2");
            Check(CommonModifier.ApplyMaxHpGain(stats, 0) == 0 && stats.MaxHP == 62, "0 이하는 무시");

            var restored = NewCharacter("SturdyRestore", hp: 70);
            restored.Stats.Modifiers.Add(new SturdyBodyModifier());   // 세이브 복원 경로
            Check(restored.Stats.MaxHP == 70 && restored.Stats.CurrentHP == 70, "복원(Add)은 완강한 신체를 다시 적용하지 않는다");
        }

        // ── 레지스트리 ────────────────────────────────────────

        private static void TestRegistryHas17WithFamilies()
        {
            // 직업 고유(2026-10-09 C)는 JobModifierSelfTests가 검증한다 — 여기선 공용 17종만 센다.
            var all = ModifierRegistry.CreateAll().FindAll(m => !(m is DiceOrbit.Data.Modifiers.Job.JobModifier));
            Check(all.Count == 17, $"공용 모디파이어 17종 (실제 {all.Count})");

            var perFamily = new Dictionary<ModifierFamily, int>();
            var names = new HashSet<string>();
            foreach (var m in all)
            {
                Check(m.Family != ModifierFamily.None, $"{m.GetType().Name}: 계열 지정됨");
                Check(m.Category == ModifierCategory.Generic, $"{m.GetType().Name}: Generic");
                Check(names.Add(m.ModifierName), $"{m.GetType().Name}: 이름 중복 없음");
                Check(!string.IsNullOrEmpty(m.Description), $"{m.GetType().Name}: 설명 있음");
                Check(ModifierRegistry.Exists(m.GetType().Name), $"{m.GetType().Name}: 세이브 ID로 조회됨");
                perFamily.TryGetValue(m.Family, out int n);
                perFamily[m.Family] = n + 1;
            }

            perFamily.TryGetValue(ModifierFamily.Position, out int position);
            perFamily.TryGetValue(ModifierFamily.Dice, out int dice);
            perFamily.TryGetValue(ModifierFamily.Combo, out int combo);
            perFamily.TryGetValue(ModifierFamily.Survival, out int survival);
            Check(position == 5 && dice == 4 && combo == 4 && survival == 4,
                  $"계열 분포 위치5/주사위4/콤보4/생존4 (실제 {position}/{dice}/{combo}/{survival})");

            foreach (var removed in new[] { "ZoneCrossModifier", "MomentumStrikeModifier", "ExtremeResonanceModifier",
                                            "SafetyNetModifier", "ChainReactionModifier", "OpeningBarrierModifier", "UnyieldingModifier" })
                Check(!ModifierRegistry.Exists(removed), $"삭제된 {removed}는 레지스트리에 없다 (옛 세이브 → 검증 실패, D9)");
        }

        // [tests-body]

        // ── 도우미 ────────────────────────────────────────────

        private static Character NewCharacter(string name, int attack = 10, int hp = 50)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            _spawned.Add(go);
            var character = go.AddComponent<Character>();
            character.InitializeStats(new CharacterStats { CharacterName = name, Attack = attack, MaxHP = hp, CurrentHP = hp });
            return character;
        }

        private static Monster NewMonster(string name)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            _spawned.Add(go);
            return go.AddComponent<Monster>();
        }

        /// <summary>비활성 유닛은 Awake가 돌지 않아 StatusEffects가 비어 있다 — 테스트용 매니저를 직접 붙인다.</summary>
        private static StatusEffectManager NewEffects(Unit unit)
        {
            var effects = unit.gameObject.AddComponent<StatusEffectManager>();
            effects.Initialize(unit);
            return effects;
        }

        private static void Check(bool condition, string what)
        {
            if (condition) return;
            _failures++;
            Debug.LogError("[CommonModifierSelfTests] FAIL: " + what);
        }
    }
}
