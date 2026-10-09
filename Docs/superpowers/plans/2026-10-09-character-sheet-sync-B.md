# 캐릭터 시트 동기화 B Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 공용 모디파이어를 12종에서 시트의 17종(위치 5 / 주사위 4 / 콤보 4 / 생존 4)으로 개편하고, 같은 종류 중복 장착 금지(D1)·충전 자원(`ChargeStatus`)·캐릭터 일시 방어도의 플레이어 턴 시작 초기화(D2)를 도입한다.

**Architecture:** 충전은 스택형 상태이상 `ChargeStatus`(새 `EffectType.Charge`, enum 끝에 추가)로 얹어 아이콘 줄·파이프라인 계산을 재사용한다. 모디파이어 베이스(`CharacterModifier`)에 `AttackBonus`·`OnAcquired`·중복 금지 `CanApplyTo`를 virtual로 선언하고, `ModifierManager.Acquire`(획득 경로 전용)와 `Add`(세이브 복원 전용)를 분리한다. 새 사건(이동 완료·강화 조건 실패·공격 행동 종료·행동 종료)은 `AutoAttackSystem`이 리스너 인터페이스로 통지한다(DIM 미사용, 기존 `IDiceUseListener` 패턴). 3중첩 골격 `StackableCommonModifier`는 `CommonModifier`로 단순화한다.

**Tech Stack:** Unity 6000.3.8f1 · C# · Git LFS
**Spec:** Docs/superpowers/specs/2026-10-09-character-sheet-sync-B-design.md
**규칙:** 심사 잠금 — 로컬 커밋만, push 금지. 커밋 메시지 끝에 `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
---

## 파일 구조

| 파일 | 변경 | 책임 |
|---|---|---|
| `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/EffectData.cs` | 수정 | `EffectType.Charge`를 enum **맨 끝**에 추가 |
| `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Effects/ChargeStatus.cs` (+`.meta`) | 생성 | 충전 상태: 상한 3, 강화 공격 +15%/스택, `Grant`/`ConsumeOne`/`Clear` 진입점 |
| `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Effects/StatusEffectManager.cs` | 수정 | `CreateEffect`에 Charge 케이스, 스택 감소 `ReduceStack` |
| `Assets/Scripts/UI/TooltipKeywordFormatter.cs` | 수정 | `Charge → 충전` 이름 매핑 |
| `Assets/Resources/UI/TooltipKeywordDatabase.asset` (LFS) | 수정 | "충전"·"Charge" 키워드 항목 |
| `Assets/Scripts/Core/Stage/BattleStage/Units/UnitStats.cs` | 수정 | 캐릭터 일시 방어도 턴 시작 초기화(소유자 확인), TODO 해소 |
| `Assets/Scripts/Core/Stage/BattleStage/Units/Character/Character.cs` | 수정 | `InitializeStats`에서 `stat.Owner = this` 주입 |
| `Assets/Scripts/Core/Stage/BattleStage/Units/Character/CharacterStats.cs` | 수정 | `EffectiveAttack` |
| `Assets/Scripts/Core/Stage/BattleStage/Units/Character/Modifiers/CharacterModifier.cs` | 수정 | `AttackBonus`·`OnAcquired`·중복 금지 `CanApplyTo` |
| `Assets/Scripts/Core/Stage/BattleStage/Units/Character/Modifiers/ModifierManager.cs` | 수정 | `Acquire`, 같은 타입 중복 추가 거부 |
| `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/AutoAttackSystem.cs` | 수정 | 유효 공격력, 새 통지 4종, 처치 수, 충전 소비, 콤보 끊김 통지 제거 |
| `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Skills/ComboActiveSkill.cs` | 수정 | 단계 피해에 유효 공격력 |
| `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Forecast/ActionForecaster.cs` | 수정 | 예고 기본공격에 유효 공격력 |
| `Assets/Scripts/UI/InfoPanel/UnitInfoBuilder.cs` | 수정 | 정보 패널 공격력 = 유효 공격력 |
| `Assets/Scripts/Data/Modifiers/Common/ModifierListeners.cs` (+`.meta`) | 생성 | `IDiceUseListener`(이동) + `IMoveFinishedListener`·`IComboGateFailedListener`·`IAttackActionFinishedListener`·`IActionFinishedListener`, `AttackActionResult` |
| `Assets/Scripts/Data/Modifiers/Common/StackableCommonModifier.cs` → `CommonModifier.cs` | 이름 변경+수정 | 중첩 제거, 공용 도우미(방어도·충전·회복·최대 체력·상태 부여) |
| `Assets/Scripts/Data/Modifiers/Common/PositionModifiers.cs` | 수정 | 합류 전술·독립 행동(15%), 듀얼·기동 장갑·구조대, 경계 돌파 삭제 |
| `Assets/Scripts/Data/Modifiers/Common/DiceModifiers.cs` | 수정 | 저속 방호(4)·앙갚음(재작성)·예비 충전·사전 준비, 관성 타격·극점 공명 삭제 |
| `Assets/Scripts/Data/Modifiers/Common/ComboModifiers.cs` | 수정 | 대단원(25%+방어도 4)·균열·독 바른 무기·기본기 단련, 안전장치·연쇄 반응 삭제 |
| `Assets/Scripts/Data/Modifiers/Common/SurvivalModifiers.cs` | 수정 | 공방 일체·완강한 신체·자연 치유·사냥, 선제 방벽·불굴 삭제 |
| `Assets/Scripts/Data/Modifiers/ModifierRegistry.cs` | 수정 | 17종 팩토리 |
| `Assets/Scripts/Data/Modifiers/ModifierFamily.cs` | 수정 | 주석(17종 5/4/4/4) |
| `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Zone/CombatZoneManager.cs` | 수정 | `GetZoneOfMonster(Monster)` |
| `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/CombatManager.cs` | 수정 | 전투 시작 방송 전·전투 종료 시 파티 충전 제거 |
| `Assets/Scripts/UI/Reward/RewardUI.cs` | 수정 | `Acquire`, 카드 상태 문구(보유 중/신규), 핍 0/1 |
| `Assets/Scripts/UI/ShopUI.cs` | 수정 | `Acquire` |
| `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/BattleStageDebugManager.cs` | 수정 | `Acquire` |
| `Assets/Scripts/Editor/CommonModifierSelfTests.cs` (+`.meta`) | 생성 | 충전·방어도·장착·유효 공격력·레지스트리 자가 테스트 |
| `Assets/Scripts/Editor/RewardFlowSelfTests.cs` | 수정 | 계열 단언을 "4계열 모두 1종 이상"으로 |

## 검증 도구

**헤드리스 컴파일** (csproj가 낡아도 스크립트가 패치·복원한다. Git Bash):

```bash
PYTHONIOENCODING=utf-8 python "C:/Users/dfdfg/AppData/Local/Temp/claude/C--Users-dfdfg-UnityProjects-Dice-Orbit/0bedfca3-5b1e-4056-9b75-2a944d05bce1/scratchpad/compile_check.py"
```

마지막 줄이 `RESULT PASS`면 통과, `RESULT FAIL`이면 위에 찍힌 `: error ` 줄을 고친다. (계획 작성 시점 기준선: PASS.)

**에디터 자가 테스트:** 에이전트는 Unity 에디터에 접근할 수 없다. 테스트는 TDD로 작성하되, 실행은 사용자 몫으로 남긴다 — 메뉴 `DiceOrbit → Run Common Modifier Self-Tests`(이 계획에서 새로 만듦), `DiceOrbit → Run Reward Flow Self-Tests`, `DiceOrbit → Run Combat Redesign Self-Tests`. 각 태스크의 "테스트 실행" 단계는 **"pending user run"** 으로 기록하고 진행한다. 에이전트 쪽 "실패 확인"은 헤드리스 컴파일 FAIL(아직 없는 타입·멤버 참조)로 대신한다.

**새 `.cs` 파일의 `.meta`:** Unity를 열지 않으므로 직접 만든다 (기존 프로젝트의 2줄 형식):

```bash
python -c "import uuid,sys; open(sys.argv[1],'w',newline='\n').write('fileFormatVersion: 2\nguid: %s\n' % uuid.uuid4().hex)" "<파일>.cs.meta"
```

**인코딩:** 모든 `.cs`/`.md`는 UTF-8(BOM 없음). Write/Edit 도구는 그대로 UTF-8로 쓴다.

**공통 규칙 (모든 태스크):**
- `EffectType`은 끝에만 추가한다.
- 모디파이어/상태 훅은 베이스에 선언된 virtual의 `public override`로만 구현한다(인터페이스 기본 메서드에 기대지 않는다). 새 사건은 `Data/Modifiers/Common/ModifierListeners.cs`의 리스너 인터페이스로 받는다.
- UnityEngine.Object는 `?.`/`??` 대신 명시적 `!= null`로 검사한다.

---

### Task 1: 충전 상태 `ChargeStatus` + 키워드 + 자가 테스트 골격

**Files:**
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/EffectData.cs` (enum 끝, 현재 `Weaken,` 줄 다음 — 43행)
- Create: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Effects/ChargeStatus.cs` (+`.meta`)
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Effects/StatusEffectManager.cs` (`RemoveRoundStatuses` 다음, `CreateEffect` switch)
- Modify: `Assets/Scripts/UI/TooltipKeywordFormatter.cs` (`StatusNameAliases`, 115~119행 부근)
- Modify: `Assets/Resources/UI/TooltipKeywordDatabase.asset` (파일 끝에 항목 2개)
- Create: `Assets/Scripts/Editor/CommonModifierSelfTests.cs` (+`.meta`)

- [ ] **Step 1: 실패하는 테스트 작성 — 자가 테스트 파일 생성**

`Assets/Scripts/Editor/CommonModifierSelfTests.cs` (이후 태스크는 `// [tests-run]` 마커 위에 호출 줄을, `// [tests-body]` 마커 위에 메서드를 추가한다):

```csharp
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
```

`.meta` 생성:

```bash
python -c "import uuid,sys; open(sys.argv[1],'w',newline='\n').write('fileFormatVersion: 2\nguid: %s\n' % uuid.uuid4().hex)" "Assets/Scripts/Editor/CommonModifierSelfTests.cs.meta"
```

- [ ] **Step 2: 컴파일로 실패 확인**

Run: 헤드리스 컴파일. Expected: `RESULT FAIL` — `ChargeStatus`, `EffectType.Charge` 미정의(CS0246/CS0117).

- [ ] **Step 3: `EffectType.Charge` 추가 (끝에만)**

`EffectData.cs`에서

```csharp
        Weaken,             // 약화: 이번 라운드 동안 주는 공격 피해 -V% (RoundStatus, 2026-10-09)
    }
```

를

```csharp
        Weaken,             // 약화: 이번 라운드 동안 주는 공격 피해 -V% (RoundStatus, 2026-10-09)
        Charge,             // 충전: 스택형(최대 3), 강화 공격 피해 +15%/스택, 강화 공격 후 1 소비 (ChargeStatus, 2026-10-09 B) — 반드시 enum 끝에 유지
    }
```

로 바꾼다.

- [ ] **Step 4: `StatusEffectManager` — `ReduceStack` + `CreateEffect` 케이스**

`RemoveRoundStatuses()` 메서드 바로 아래(`public IReadOnlyCollection<StatusEffect> GetActiveEffects()` 위)에 추가:

```csharp
        /// <summary>
        /// 스택형 상태의 스택을 amount만큼 줄인다. 0 이하가 되면 제거한다. 남은 스택 수를 반환한다(없으면 0).
        /// 제거 후 재부여(아이콘·VFX 깜빡임) 없이 값만 바꾸고 UI에 변경을 알린다 — 충전 소비 등.
        /// </summary>
        public int ReduceStack(EffectType type, int amount)
        {
            if (!activeEffects.TryGetValue(type, out var effect) || effect == null) return 0;

            effect.Value -= Mathf.Max(0, amount);
            if (effect.Value <= 0)
            {
                RemoveEffect(type);
                return 0;
            }

            OnChanged?.Invoke();
            return effect.Value;
        }
```

`CreateEffect`의 switch에서

```csharp
                case EffectType.Weaken:
                    return new WeakenStatus(value);
```

다음 줄에 추가:

```csharp

                // 충전 — 스택형, duration 무시(-1 고정). 상한(3)은 ChargeStatus.Grant 한 곳에서만 보장한다.
                case EffectType.Charge:
                    return new ChargeStatus(value);
```

- [ ] **Step 5: `ChargeStatus.cs` 생성**

`Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Effects/ChargeStatus.cs`:

```csharp
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using UnityEngine;

namespace DiceOrbit.Systems.Effects
{
    /// <summary>
    /// 충전 (시트 「버프·디버프」, 2026-10-09 B). 스택형 — Value가 곧 스택 수, 지속시간 없음(-1).
    /// - 강화 공격(행동 스코프의 ComboStage ≥ 0, 주체가 자신)의 피해 +15%/스택. 행동 예고(시뮬레이션)에도 같은 보정(부수효과 없음).
    /// - 강화 공격 행동을 마친 뒤 AutoAttackSystem이 ConsumeOne으로 1스택 소비한다.
    /// - 최대 3스택(획득 경로 합계) — 상한은 Grant 한 곳에서만 보장한다.
    /// - 전투 시작 방송 전·전투 종료 시 CombatManager가 Clear로 제거한다(전투를 넘어 이월되지 않음).
    /// </summary>
    public class ChargeStatus : StatusEffect
    {
        public const int MaxStacks = 3;
        public const float PercentPerStack = 15f;

        public ChargeStatus(int stacks)
            : base(EffectType.Charge, Mathf.Clamp(stacks, 0, MaxStacks), -1, isStackable: true) { }

        /// <summary>스택 수에 따른 피해 배율 (1 + 0.15 × 스택).</summary>
        public static float Multiplier(int stacks) => 1f + PercentPerStack / 100f * Mathf.Clamp(stacks, 0, MaxStacks);

        /// <summary>지금 열린 행동 스코프가 owner의 강화 공격인가 (실제 행동·예고 시뮬레이션 공통).</summary>
        public static bool IsEmpoweredAttackOf(Unit owner)
            => owner != null
               && AttackActionScope.IsActive
               && AttackActionScope.CurrentSource == owner
               && AttackActionScope.CurrentInfo.ComboStage >= 0;

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (Owner == null || Value <= 0) return;
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.SourceUnit != Owner || context.IsDirectHpLoss) return;
            if (!IsEmpoweredAttackOf(Owner)) return;

            context.OutputValue *= Multiplier(Value);
        }

        /// <summary>
        /// 상태 매니저에 충전 stacks를 더한다 — 합계가 3을 넘지 않게 자른다. 실제로 더한 스택 수를 반환한다(버블 없음, 테스트용 진입점).
        /// </summary>
        public static int Grant(StatusEffectManager effects, int stacks)
        {
            if (effects == null || stacks <= 0) return 0;

            int current = effects.GetEffectValue(EffectType.Charge);
            int add = Mathf.Min(stacks, MaxStacks - current);
            if (add <= 0) return 0;

            effects.AddEffect(new ChargeStatus(add));
            return add;
        }

        /// <summary>유닛에 충전 부여 + 획득 버블. 실제로 더한 스택 수를 반환한다.</summary>
        public static int Grant(Unit unit, int stacks)
        {
            if (unit == null || !unit.IsAlive) return 0;

            int added = Grant(unit.StatusEffects, stacks);
            if (added > 0)
            {
                var data = UI.TooltipKeywordFormatter.BuildStatusDisplayData(EffectType.Charge.ToString(), added, -1);
                UI.CombatNotifier.NotifyStatus(unit, $"{data.Name} +{added}", data.Color);
            }
            return added;
        }

        /// <summary>충전 1스택 소비. 0이 되면 제거. 충전이 없으면 아무 일도 없다.</summary>
        public static void ConsumeOne(StatusEffectManager effects)
        {
            if (effects == null) return;
            effects.ReduceStack(EffectType.Charge, 1);
        }

        public static void ConsumeOne(Unit unit)
        {
            if (unit == null) return;
            ConsumeOne(unit.StatusEffects);
        }

        /// <summary>충전을 전부 제거한다 (전투 경계).</summary>
        public static void Clear(Unit unit)
        {
            if (unit == null || unit.StatusEffects == null) return;
            unit.StatusEffects.RemoveEffect(EffectType.Charge);
        }
    }
}
```

`.meta` 생성:

```bash
python -c "import uuid,sys; open(sys.argv[1],'w',newline='\n').write('fileFormatVersion: 2\nguid: %s\n' % uuid.uuid4().hex)" "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Effects/ChargeStatus.cs.meta"
```

- [ ] **Step 6: 이름 매핑 추가**

`TooltipKeywordFormatter.cs`의 `StatusNameAliases`에서

```csharp
                { "Weaken",        "약화"    },
```

다음 줄에 추가:

```csharp
                { "Charge",        "충전"    },
```

- [ ] **Step 7: 키워드 사전 항목 추가 (LFS .asset)**

헬퍼(`Tools/asset_yaml_set.py`)는 새 항목 추가를 지원하지 않으므로 아래 스크립트를 scratchpad에 쓰고 실행한다. 한글은 스크립트 안에서 `\uXXXX`로 이스케이프되어 기록된다 (CLI 인자로 한글을 넘기지 않는다). 기존 항목 형식(`key`/`description`/`color`/`icon` 4필드)과 같다.

```bash
cat > "C:/Users/dfdfg/AppData/Local/Temp/claude/C--Users-dfdfg-UnityProjects-Dice-Orbit/0bedfca3-5b1e-4056-9b75-2a944d05bce1/scratchpad/add_charge_keyword.py" <<'PY'
import sys

PATH = r"C:\Users\dfdfg\UnityProjects\Dice-Orbit\Assets\Resources\UI\TooltipKeywordDatabase.asset"
DESC = "\uAC15\uD654 \uACF5\uACA9\uC758 \uD53C\uD574\uAC00 \uC2A4\uD0DD\uB2F9 15% \uC99D\uAC00\uD569\uB2C8\uB2E4. \uAC15\uD654 \uACF5\uACA9\uC744 \uB9C8\uCE5C \uB4A4 1\uC2A4\uD0DD\uC744 \uC18C\uBE44\uD569\uB2C8\uB2E4. \uCD5C\uB300 3\uC2A4\uD0DD."
KEY_KO = "\uCDA9\uC804"  # 충전


def esc(s):
    out = []
    for ch in s:
        if ch == '"':
            out.append('\\"')
        elif ch == "\\":
            out.append("\\\\")
        elif ord(ch) < 128:
            out.append(ch)
        else:
            out.append("\\u%04X" % ord(ch))
    return '"' + "".join(out) + '"'


with open(PATH, "rb") as f:
    raw = f.read()
if raw.startswith(b"version https://git-lfs"):
    sys.exit("LFS pointer only — run 'git lfs pull' first")
text = raw.decode("utf-8")
if '- key: Charge\n' in text or ('- key: ' + esc(KEY_KO) + '\n') in text:
    sys.exit("charge entries already exist")
if not text.endswith("\n"):
    text += "\n"

entry = (
    "  - key: {key}\n"
    "    description: {desc}\n"
    "    color: {{r: 0.6, g: 0.85, b: 1, a: 1}}\n"
    "    icon: {{fileID: 0}}\n"
)
text += entry.format(key=esc(KEY_KO), desc=esc(DESC))
text += entry.format(key="Charge", desc=esc(DESC))

with open(PATH, "wb") as f:
    f.write(text.encode("utf-8"))
print("added 2 entries")
PY
python "C:/Users/dfdfg/AppData/Local/Temp/claude/C--Users-dfdfg-UnityProjects-Dice-Orbit/0bedfca3-5b1e-4056-9b75-2a944d05bce1/scratchpad/add_charge_keyword.py"
tail -n 8 Assets/Resources/UI/TooltipKeywordDatabase.asset
```

Expected: `added 2 entries`, 마지막 8줄이 `- key: "\uCDA9\uC804"`·`- key: Charge` 두 항목(들여쓰기 2칸 `- key`, 4칸 나머지 필드).

- [ ] **Step 8: 컴파일 PASS 확인**

Run: 헤드리스 컴파일. Expected: `RESULT PASS`.
자가 테스트 `DiceOrbit → Run Common Modifier Self-Tests`: pending user run (기대: "전부 통과").

- [ ] **Step 9: 커밋**

```bash
git add Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/EffectData.cs \
  Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Effects/ChargeStatus.cs \
  Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Effects/ChargeStatus.cs.meta \
  Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Effects/StatusEffectManager.cs \
  Assets/Scripts/UI/TooltipKeywordFormatter.cs \
  Assets/Resources/UI/TooltipKeywordDatabase.asset \
  Assets/Scripts/Editor/CommonModifierSelfTests.cs \
  Assets/Scripts/Editor/CommonModifierSelfTests.cs.meta
git commit -m "$(cat <<'EOF'
feat(status): 충전(ChargeStatus) — 최대 3스택, 강화 공격 +15%/스택, EffectType.Charge(끝에 추가) + 키워드 사전

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: 캐릭터 일시 방어도 — 플레이어 턴 시작 시 0 (D2)

**Files:**
- Modify: `Assets/Scripts/Core/Stage/BattleStage/Units/UnitStats.cs:25-35` (`OnTurnEvent`)
- Modify: `Assets/Scripts/Core/Stage/BattleStage/Units/Character/Character.cs:86-89` (`InitializeStats`)
- Modify: `Assets/Scripts/Editor/CommonModifierSelfTests.cs`

**순서 근거:** TurnStart 방송은 `Process` 한 번에 OnPreAction→OnCalculateOutput→OnHit→OnPostAction 순으로 모든 리액터를 돈다. 초기화는 **OnPreAction**(UnitStats 우선순위 20 — 모디파이어·상태 10보다 먼저)에서, 턴 시작 부여·회복(자연 치유 등)은 **OnPostAction**에서 한다 → 초기화가 항상 먼저다. 캐릭터 턴 시작 시 OnPreAction에서 캐릭터 방어도를 주는 기존 코드는 없다(`TempArmor +=` 전수 확인: 타일·주사위·포션·전사 3단계는 모두 행동 중 부여). 몬스터 조건은 **그대로** 둔다 (MonsterStats에는 Owner를 주입하지 않으므로 동작 불변).

- [ ] **Step 1: 실패하는 테스트 작성**

`CommonModifierSelfTests.cs`에서 `                // [tests-run]` 줄 **위**에 추가:

```csharp
                TestCharacterArmorResetsOnOwnTurnStart();
```

`        // [tests-body]` 줄 **위**에 추가:

```csharp
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

```

- [ ] **Step 2: 실패 확인**

Run: 헤드리스 컴파일 → `RESULT PASS`(테스트는 컴파일되지만 현재 코드에선 `Owner` 미주입·캐릭터 초기화 없음으로 **실행 시 실패**). 실행 실패 확인은 pending user run.

- [ ] **Step 3: `Character.InitializeStats`에서 Owner 주입**

`Character.cs`에서

```csharp
        public void InitializeStats(CharacterStats newStats)
        {
            stat = newStats;
            var preset = stat.SourcePreset;
```

를

```csharp
        public void InitializeStats(CharacterStats newStats)
        {
            stat = newStats;
            stat.Owner = this;   // 턴 사건의 소유자 확인(UnitStats.OnTurnEvent — 일시 방어도 초기화)에 쓴다
            var preset = stat.SourcePreset;
```

로 바꾼다.

- [ ] **Step 4: `UnitStats.OnTurnEvent` 교체**

`UnitStats.cs`의

```csharp
        public virtual void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            //턴 시작 시 임시 방어도를 깎는다
            if (context.Phase == EventPhase.TurnStart && // TODO : owner를 할당시킨 후, context.SourceUnit == Owner로 바꿔야 함(매우 중요)
                trigger == CombatTrigger.OnPreAction &&
                context.SourceUnit is Monster &&
                this is MonsterStats)
            {
                TempArmor = 0;
            }
        }
```

를

```csharp
        /// <summary>
        /// 턴 시작 시 일시 방어도를 0으로 (OnPreAction — 우선순위 20이라 모디파이어·상태(10)의 턴 시작 부여보다 먼저).
        /// - 몬스터: 몬스터 턴 시작 사건에서 초기화 (기존 동작 그대로 — MonsterStats에는 Owner를 주입하지 않는다).
        /// - 캐릭터: 자기 턴 시작에만 (D2, 2026-10-09 B). TurnEvent는 파티 전원의 Stats에 방송되므로
        ///   SourceUnit == Owner로 소유자를 확인한다. Owner는 Character.InitializeStats가 주입한다.
        /// </summary>
        public virtual void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (context.Phase != EventPhase.TurnStart || trigger != CombatTrigger.OnPreAction) return;

            if (this is MonsterStats)
            {
                if (context.SourceUnit is Monster) TempArmor = 0;
                return;
            }

            if (this is CharacterStats && Owner != null && context.SourceUnit == Owner)
                TempArmor = 0;
        }
```

로 바꾼다. (`using static UnityEngine.UI.GridLayoutGroup;` 등 기존 using은 건드리지 않는다.)

- [ ] **Step 5: 컴파일 PASS**

Run: 헤드리스 컴파일 → `RESULT PASS`. 자가 테스트: pending user run.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/Core/Stage/BattleStage/Units/UnitStats.cs \
  Assets/Scripts/Core/Stage/BattleStage/Units/Character/Character.cs \
  Assets/Scripts/Editor/CommonModifierSelfTests.cs
git commit -m "$(cat <<'EOF'
feat(combat): 캐릭터 일시 방어도를 플레이어 턴 시작에 0으로 — Stats.Owner 주입, UnitStats TODO 해소

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: 모디파이어 베이스 — `AttackBonus`·`OnAcquired`·중복 금지·`Acquire`·`EffectiveAttack`

**Files:**
- Modify: `Assets/Scripts/Core/Stage/BattleStage/Units/Character/Modifiers/CharacterModifier.cs:29-30, 56-67`
- Modify: `Assets/Scripts/Core/Stage/BattleStage/Units/Character/Modifiers/ModifierManager.cs` (전체 교체)
- Modify: `Assets/Scripts/Core/Stage/BattleStage/Units/Character/CharacterStats.cs:35`
- Modify: `Assets/Scripts/Data/Modifiers/Common/StackableCommonModifier.cs:67-78` (3중첩 `CanApplyTo` override 삭제 — 베이스 규칙을 따르게)
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/AutoAttackSystem.cs:257`
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Skills/ComboActiveSkill.cs:60`
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Forecast/ActionForecaster.cs:175`
- Modify: `Assets/Scripts/UI/InfoPanel/UnitInfoBuilder.cs:24`
- Modify: `Assets/Scripts/Editor/CommonModifierSelfTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

`// [tests-run]` 위에:

```csharp
                TestDuplicateTypeCannotBeEquipped();
                TestAcquireCallsOnAcquiredButAddDoesNot();
                TestEffectiveAttackSumsBonuses();
```

`// [tests-body]` 위에:

```csharp
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

```

- [ ] **Step 2: 실패 확인**

Run: 헤드리스 컴파일 → `RESULT FAIL` (`AttackBonus`/`OnAcquired`/`Acquire`/`EffectiveAttack` 미정의).

- [ ] **Step 3: `CharacterModifier` 확장**

`CharacterModifier.cs`에서

```csharp
        /// <summary>계열 — 보상 카드의 아이콘·라벨 (2026-10-03). 공용 12종은 전부 지정, 기본은 None(보상 카드로 제시 불가).</summary>
        public virtual  ModifierFamily   Family       => ModifierFamily.None;
```

를

```csharp
        /// <summary>계열 — 보상 카드의 아이콘·라벨 (2026-10-03). 공용 17종은 전부 지정, 기본은 None(보상 카드로 제시 불가).</summary>
        public virtual  ModifierFamily   Family       => ModifierFamily.None;

        /// <summary>
        /// 기본 공격력 보너스 (D6, 2026-10-09 B — 기본기 단련). CharacterStats.EffectiveAttack이 합산한다.
        /// 공격력은 세이브에 저장되지 않고 프리셋에서 재유도되므로 저장 필드(Attack)는 절대 바꾸지 않는다.
        /// </summary>
        public virtual  int              AttackBonus  => 0;
```

로 바꾸고,

```csharp
        /// <summary>
        /// 스킬 툴팁에 표시할 설명 줄.
        /// 이 모디파이어가 해당 스킬과 관련 없으면 string.Empty 반환.
        /// 같은 타입이 여러 개 장착된 경우 UI에서 (×N)으로 그룹핑됨.
        /// </summary>
        public virtual string GetSkillLine(CharacterActiveSkill skill) => string.Empty;

        /// <summary>
        /// 이 모디파이어를 해당 캐릭터에게 줄 수 있는지(보상 제시/장착 가능 여부).
        /// 기본은 모든 캐릭터(Generic). 특정 스킬에만 작동하는 시그니처는 override해서 제한한다.
        /// </summary>
        public virtual bool CanApplyTo(Character character) => true;
```

를

```csharp
        /// <summary>
        /// 스킬 툴팁에 표시할 설명 줄.
        /// 이 모디파이어가 해당 스킬과 관련 없으면 string.Empty 반환.
        /// </summary>
        public virtual string GetSkillLine(CharacterActiveSkill skill) => string.Empty;

        /// <summary>
        /// 획득 시 1회 (D7, 2026-10-09 B) — 보상·상점·디버그의 ModifierManager.Acquire에서만 불린다.
        /// 세이브 복원(PartyManager.Apply → Add)에서는 불리지 않는다: 최대 체력처럼 세이브에 이미 반영된 효과를 두 번 주지 않기 위해.
        /// </summary>
        public virtual void OnAcquired(Character owner) { }

        /// <summary>
        /// 이 모디파이어를 해당 캐릭터에게 줄 수 있는지(보상 제시/장착 가능 여부).
        /// 기본 규칙(D1): 같은 종류(타입)는 캐릭터당 1개 — 이미 갖고 있으면 false. 장착 수 제한은 없다.
        /// 직업 전용 등 추가 조건은 자식이 base.CanApplyTo(c) &amp;&amp; … 로 확장한다.
        /// </summary>
        public virtual bool CanApplyTo(Character character)
        {
            var mods = character != null && character.Stats != null && character.Stats.Modifiers != null
                ? character.Stats.Modifiers.Modifiers
                : null;
            if (mods == null) return true;

            var type = GetType();
            for (int i = 0; i < mods.Count; i++)
                if (mods[i] != null && mods[i].GetType() == type) return false;
            return true;
        }
```

로 바꾼다.

- [ ] **Step 4: `ModifierManager` 전체 교체**

`ModifierManager.cs` 전체를 다음으로:

```csharp
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using UnityEngine;

namespace DiceOrbit.Data.Modifiers
{
    public interface IModifierManager
    {
        void Initialize(Character character);
        /// <summary>장착만 한다 — 세이브 복원 경로 전용. 같은 종류가 이미 있으면 거부한다.</summary>
        void Add(CharacterModifier mod);
        /// <summary>획득 = 장착 + OnAcquired — 보상·상점·디버그 경로 전용. 같은 종류가 이미 있으면 거부한다.</summary>
        void Acquire(CharacterModifier mod);
        void Remove(CharacterModifier mod);
        void CollectReactors(List<ICombatReactor> list);
        void ApplyTo(CharacterModfierContext context);
        IReadOnlyList<CharacterModifier> Modifiers { get; }
    }

    /// <summary>
    /// 캐릭터에 붙는 모디파이어 컨테이너.
    /// 컨텍스트는 스킬이 필요할 때마다 새로 생성(GenerateContext)되고,
    /// 여기서 장착된 모디파이어들의 OnRefreshSkill을 그 위에 덧칠한다("도화지" 패턴).
    /// 같은 종류는 캐릭터당 1개(D1, 2026-10-09 B) — Add/Acquire 둘 다 중복을 거부한다(옛 세이브의 중첩도 하나만 남는다).
    /// </summary>
    public class ModifierManager : IModifierManager
    {
        private Character _owner;
        private readonly List<CharacterModifier> _modifiers = new();

        public IReadOnlyList<CharacterModifier> Modifiers => _modifiers;
        public int Count => _modifiers.Count;

        public void Initialize(Character character)
        {
            _owner = character;
        }

        public void Add(CharacterModifier mod)
        {
            TryAdd(mod);
        }

        public void Acquire(CharacterModifier mod)
        {
            if (!TryAdd(mod)) return;
            mod.OnAcquired(_owner);
        }

        public void Remove(CharacterModifier mod)
        {
            if (mod == null) return;
            _modifiers.Remove(mod);
        }

        private bool TryAdd(CharacterModifier mod)
        {
            if (mod == null) return false;
            if (ContainsType(mod.GetType()))
            {
                Debug.LogWarning($"[ModifierManager] '{mod.ModifierName}'는 이미 장착돼 있어 추가하지 않는다 (같은 종류 중복 장착 불가).");
                return false;
            }
            mod.owner = _owner;
            _modifiers.Add(mod);
            return true;
        }

        private bool ContainsType(System.Type type)
        {
            for (int i = 0; i < _modifiers.Count; i++)
                if (_modifiers[i] != null && _modifiers[i].GetType() == type) return true;
            return false;
        }

        /// <summary>주어진 컨텍스트(빈 도화지)에 장착된 모디파이어 효과를 일괄 적용한다.</summary>
        public void ApplyTo(CharacterModfierContext context)
        {
            if (context == null) return;
            foreach (var mod in _modifiers)
            {
                if (mod == null) continue;
                mod.OnRefreshSkill(context);
                if (context.IsCancelled) break;
            }
        }

        /// <summary>Character.CollectReactors에서 호출됨 (Generic 모디파이어의 파이프라인 훅).</summary>
        public void CollectReactors(List<ICombatReactor> list)
        {
            foreach (var m in _modifiers)
                if (m != null) list.Add(m);
        }
    }
}
```

- [ ] **Step 5: `CharacterStats.EffectiveAttack`**

`CharacterStats.cs`에서

```csharp
        public int ActiveAbilityCount => ActiveAbilities.Count;
```

다음에 추가:

```csharp

        /// <summary>
        /// 유효 공격력 = 기본 공격력(Attack) + 장착 모디파이어의 AttackBonus 합 (D6, 2026-10-09 B).
        /// 기본공격·콤보 단계 피해·행동 예고·정보 패널은 이 값을 읽는다. Attack 필드 자체는 바꾸지 않는다.
        /// </summary>
        public int EffectiveAttack
        {
            get
            {
                int bonus = 0;
                var mods = Modifiers != null ? Modifiers.Modifiers : null;
                if (mods != null)
                    for (int i = 0; i < mods.Count; i++)
                        if (mods[i] != null) bonus += mods[i].AttackBonus;
                return Attack + bonus;
            }
        }
```

- [ ] **Step 6: 읽는 곳 4곳 교체**

1. `AutoAttackSystem.cs:257` — `var context = new AttackContext(character, target, attackName, character.Stats.Attack);` → `var context = new AttackContext(character, target, attackName, character.Stats.EffectiveAttack);`
2. `ComboActiveSkill.cs:60` — `int attack = source != null && source.Stats != null ? source.Stats.Attack : 0;` → `int attack = source != null && source.Stats != null ? source.Stats.EffectiveAttack : 0;`
3. `ActionForecaster.cs:175` — `: character.Stats.Attack;` → `: character.Stats.EffectiveAttack;`
4. `UnitInfoBuilder.cs:24` (캐릭터 `Build(Character ch)` 안의 것만, 42행 몬스터는 그대로) — `attack: s?.Attack ?? 0,` → `attack: s != null ? s.EffectiveAttack : 0,`

- [ ] **Step 7: `StackableCommonModifier`의 3중첩 `CanApplyTo` 삭제**

`StackableCommonModifier.cs`에서 다음 블록을 통째로 삭제한다 (베이스의 중복 금지 규칙을 따르게):

```csharp
        /// <summary>3중첩 제한 — 이미 3개면 보상에 더 제시되지 않는다.</summary>
        public override bool CanApplyTo(Character character)
        {
            var mods = character?.Stats?.Modifiers?.Modifiers;
            if (mods == null) return true;

            int count = 0;
            var type = GetType();
            for (int i = 0; i < mods.Count; i++)
                if (mods[i] != null && mods[i].GetType() == type) count++;
            return count < MaxStacks;
        }

```

(`MaxStacks` 상수는 RewardUI가 아직 참조하므로 Task 5까지 남긴다.)

- [ ] **Step 8: 컴파일 PASS**

Run: 헤드리스 컴파일 → `RESULT PASS`. 자가 테스트: pending user run.

- [ ] **Step 9: 커밋**

```bash
git add Assets/Scripts/Core/Stage/BattleStage/Units/Character/Modifiers/CharacterModifier.cs \
  Assets/Scripts/Core/Stage/BattleStage/Units/Character/Modifiers/ModifierManager.cs \
  Assets/Scripts/Core/Stage/BattleStage/Units/Character/CharacterStats.cs \
  Assets/Scripts/Data/Modifiers/Common/StackableCommonModifier.cs \
  Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/AutoAttackSystem.cs \
  Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Skills/ComboActiveSkill.cs \
  Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Forecast/ActionForecaster.cs \
  Assets/Scripts/UI/InfoPanel/UnitInfoBuilder.cs \
  Assets/Scripts/Editor/CommonModifierSelfTests.cs
git commit -m "$(cat <<'EOF'
feat(modifier): 베이스 확장 — AttackBonus·OnAcquired·같은 종류 중복 금지, Acquire/Add 분리, 유효 공격력 4곳 반영

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: 새 통지 지점 — 리스너 4종 + `AutoAttackSystem` 배선(처치 수·충전 소비)

**Files:**
- Create: `Assets/Scripts/Data/Modifiers/Common/ModifierListeners.cs` (+`.meta`)
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/AutoAttackSystem.cs:154-252` (`ResolveRoutine`~`BasicAttackRoutine`), `:368-388` (통지 도우미)
- Modify: `Assets/Scripts/Editor/CommonModifierSelfTests.cs`

**설계:**
- `ResolveRoutine` = 가드 → `IDiceUseListener` → `IMoveFinishedListener`(출발 구역 = 이동 전 기록 `passedZones[0]`, 도착 구역 = 현재) → 공격 처리(새 private `AttackRoutine`) → `IActionFinishedListener`. 이동 완료 통지를 `ResolveRoutine` 안(턴당 1회 가드 뒤)에 두어 중복 통지를 막는다 — `ResolveRoutine`의 유일한 호출자는 `MoveThenAttackRoutine`(이동 직후)이다.
- 강화 조건 실패 = `plan.Kind == Basic && !plan.EmpoweredIsNotCombo` (강화 액티브가 없거나 눈이 조건 불일치). 판정 직후, 기본공격 실행 여부와 무관하게 `IComboGateFailedListener`.
- 처치 수(파이프라인 수정 없음): 행동 중 실제로 타격을 시도한 대상(살아 있던 대상)을 `attacked`에 모으고, **스코프 종료 직후·마무리 효과 전**에 그중 죽은(또는 파괴된) 대상을 센다 → 마무리 효과(중독 2배 등)·다음 턴 중독 틱·유물의 턴 사건 피해는 집계되지 않는다.
- 강화 공격 순서(스펙 §3.4): `End` → `OnStageCompleted` → `ChargeStatus.ConsumeOne` → `IAttackActionFinishedListener` → `ReportExecuted`. 기본공격: `End` → `IAttackActionFinishedListener`. 대상이 없어 공격 행동이 열리지 않으면 공격 행동 종료 통지도 없다.
- 통지는 장착 목록의 복사본을 돈다(통지 중 목록 변경 대비).

- [ ] **Step 1: 실패하는 테스트 작성**

`// [tests-run]` 위에:

```csharp
                TestAttackActionResultDefaults();
```

`// [tests-body]` 위에:

```csharp
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

```

- [ ] **Step 2: 실패 확인**

Run: 헤드리스 컴파일 → `RESULT FAIL` (`AttackActionResult` 미정의).

- [ ] **Step 3: `ModifierListeners.cs` 생성**

`Assets/Scripts/Data/Modifiers/Common/ModifierListeners.cs`:

```csharp
using System.Collections.Generic;
using DiceOrbit.Core;

namespace DiceOrbit.Data.Modifiers.Common
{
    // 모디파이어가 받는 자동공격 사건들 (2026-10-09 B, 스펙 §3.4).
    // AutoAttackSystem이 캐릭터의 장착 모디파이어 중 해당 인터페이스를 구현한 것에 직접 통지한다 —
    // 인터페이스 기본 메서드(DIM)에 기대지 않는다 (2026-07-21 죽은 훅 사고 재발 방지).

    /// <summary>이동을 마친 직후(공격 전) — 출발 구역·도착 구역 (기동 장갑). 구역 판정 불가면 -1.</summary>
    public interface IMoveFinishedListener
    {
        void OnMoveFinished(Character character, int fromZone, int toZone);
    }

    /// <summary>사용한 주사위가 강화 공격 조건을 못 맞춘 판정 직후 — 기본공격 실행 여부와 무관 (예비 충전).</summary>
    public interface IComboGateFailedListener
    {
        void OnComboGateFailed(Character character);
    }

    /// <summary>공격 행동(기본/강화)이 끝난 뒤, 스코프 종료 후 (대단원·독 바른 무기·공방 일체·사냥).</summary>
    public interface IAttackActionFinishedListener
    {
        void OnAttackActionFinished(Character character, AttackActionResult result);
    }

    /// <summary>이동+공격 처리 전체가 끝난 뒤 — 공격이 없었어도 (구조대).</summary>
    public interface IActionFinishedListener
    {
        void OnActionFinished(Character character);
    }

    /// <summary>공격 행동 1회의 결과.</summary>
    public readonly struct AttackActionResult
    {
        /// <summary>강화 공격 단계 (0~2). 기본공격 = -1.</summary>
        public readonly int ComboStage;

        /// <summary>이 행동에서 실제로 타격을 시도한 대상들 (시도 시점에 살아 있던 대상, 중복 없음).</summary>
        public readonly IReadOnlyList<Unit> Targets;

        /// <summary>이 행동으로 처치한 수 — 스코프 종료 직후 기준 (마무리 효과·중독 틱 처치 제외).</summary>
        public readonly int Kills;

        public bool IsEmpowered => ComboStage >= 0;

        public AttackActionResult(int comboStage, IReadOnlyList<Unit> targets, int kills)
        {
            ComboStage = comboStage;
            Targets = targets ?? System.Array.Empty<Unit>();
            Kills = kills;
        }
    }
}
```

`.meta`:

```bash
python -c "import uuid,sys; open(sys.argv[1],'w',newline='\n').write('fileFormatVersion: 2\nguid: %s\n' % uuid.uuid4().hex)" "Assets/Scripts/Data/Modifiers/Common/ModifierListeners.cs.meta"
```

- [ ] **Step 4: `AutoAttackSystem` — `ResolveRoutine`~`BasicAttackRoutine` 교체**

154행 `        /// <summary>` (“한 캐릭터의 공격 1회.” 요약의 시작)부터 252행 `BasicAttackRoutine`의 닫는 `        }`까지를 다음으로 교체한다 (Task 3에서 바꾼 `LaunchBasicHit`은 그 아래라 건드리지 않는다):

```csharp
        /// <summary>
        /// 한 캐릭터의 이동 후 처리 1회. 이번 턴에 이미 처리됐으면 아무 일도 하지 않는다.
        /// 순서: 주사위 사용 통지 → 이동 완료 통지 → 공격(강화/기본) → 행동 종료 통지(공격이 없어도).
        /// diceValue가 게이트를 만족하면 현재 콤보 단계의 강화 공격이, 아니면 콤보가 끊기며 기본공격이 나간다.
        /// (2026-08-28 콤보 개편 — 단계 관리: ComboSystem / 단계 정의: ComboActiveSkill / 판정: PlanAttack)
        /// </summary>
        public IEnumerator ResolveRoutine(Character character, int diceValue, IReadOnlyList<int> passedZones)
        {
            if (character == null || !character.IsAlive) yield break;
            if (_resolvedThisTurn.Contains(character)) yield break;
            _resolvedThisTurn.Add(character);

            // 주사위 '사용' 사건 — 공격 성사 여부와 무관하게 통지 (저속 방호 등 공용 모디파이어).
            NotifyDiceUsed(character, diceValue);

            // 이동 완료 사건 — 출발 구역은 이동 전에 기록한 경로의 첫 구역, 도착 구역은 지금 선 구역 (기동 장갑).
            var zoneMgr = CombatZoneManager.Instance;
            int toZone = zoneMgr != null ? zoneMgr.GetZoneOf(character) : -1;
            int fromZone = passedZones != null && passedZones.Count > 0 ? passedZones[0] : toZone;
            NotifyMoveFinished(character, fromZone, toZone);

            yield return AttackRoutine(character, diceValue, passedZones);

            // 이동+공격 처리 전체 종료 — 공격이 없었어도 통지 (구조대).
            NotifyActionFinished(character);
        }

        /// <summary>공격 처리 — 계획(PlanAttack)대로 강화 공격 또는 기본공격을 실행한다.</summary>
        private IEnumerator AttackRoutine(Character character, int diceValue, IReadOnlyList<int> passedZones)
        {
            var combo = Combo.ComboSystem.EnsureInstance();
            var plan = PlanAttack(character, diceValue, passedZones);

            if (plan.Kind == AttackPlanKind.Basic)
            {
                if (plan.EmpoweredIsNotCombo)
                {
                    Debug.LogError($"[AutoAttack] '{character.Stats?.CharacterName}'의 강화 공격이 ComboActiveSkill이 아니다 — 기본공격으로 대체. 프리셋을 확인할 것.");
                }
                else
                {
                    // 강화 조건 실패 판정 직후 — 기본공격이 실제로 나가는지와 무관 (예비 충전).
                    NotifyComboGateFailed(character);
                    combo.ResetCombo(character, Combo.ComboOutcome.BrokenByDice);   // 게이트 실패 — 콤보 즉시 초기화
                }

                yield return BasicAttackRoutine(character, diceValue, plan);
                yield break;
            }

            if (plan.Kind == AttackPlanKind.ComboNoTarget)
            {
                combo.ResetCombo(character, Combo.ComboOutcome.BrokenByNoTarget);
                yield break;
            }

            var comboSkill = plan.ComboSkill;
            int stage = plan.Stage;
            var targets = plan.Targets;
            var attacked = new List<Unit>();

            character.OnSkillExecutionStarted();
            comboSkill.PlayCast(character);

            // 한 단계의 모든 타격(다단×다중 대상)은 하나의 '공격 행동' — 촉매/고양/감전/충전이 전체에 적용된다.
            Systems.Effects.AttackActionScope.Begin(character, plan.BuildActionInfo(diceValue));

            float perHit = ResolveHitDelay(comboSkill);
            int hits = plan.Hits;
            for (int h = 0; h < hits; h++)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    var target = targets[i];
                    if (target == null || !target.IsAlive) continue;

                    if (!attacked.Contains(target)) attacked.Add(target);
                    comboSkill.ApplyStageHit(character, target, stage, h);
                    if (perHit > 0f) yield return new WaitForSeconds(perHit);
                }
            }

            Systems.Effects.AttackActionScope.End();

            // 처치 수는 마무리 효과(중독 2배 등) 전에 센다 — 디버프 피해 처치는 사냥에 들어가지 않는다.
            int kills = CountKills(attacked);

            // 마무리 효과(방어도 부여·중독 2배·고양 살포)는 행동 스코프 밖 — 다음 행동을 위한 상태다.
            comboSkill.OnStageCompleted(character, targets, stage);

            // 강화 공격을 마친 뒤 충전 1 소비 — 직업 고유(마나흡수, C)의 "소비 후 획득"이 이 뒤 통지에서 이어진다.
            Systems.Effects.ChargeStatus.ConsumeOne(character);

            NotifyAttackActionFinished(character, new Data.Modifiers.Common.AttackActionResult(stage, attacked, kills));
            combo.ReportExecuted(character);

            if (attackInterval > 0f) yield return new WaitForSeconds(attackInterval);
        }

        /// <summary>
        /// 기본공격 1회 (단일 대상). 기본공격도 하나의 공격 행동이라 촉매가 여기에도 적용·소모된다.
        /// plan.HadCombo면(1단계 이상 쌓인 콤보가 조건 불일치로 끊긴 경우) 공격 발생 시점에
        /// 안전장치류 모디파이어에 통지한다.
        /// </summary>
        private IEnumerator BasicAttackRoutine(Character character, int diceValue, AttackPlan plan)
        {
            var targets = plan.Targets;
            if (targets.Count == 0) yield break;

            character.OnSkillExecutionStarted();

            // "콤보가 끊기고 일반 자동공격 발생" — 공격이 실제로 나가는 이 시점에만 통지.
            if (plan.HadCombo) NotifyComboBreak(character);

            var weapon = FindProjectileSource(character);
            var attacked = new List<Unit>();
            Systems.Effects.AttackActionScope.Begin(character, plan.BuildActionInfo(diceValue));

            float perHit = ResolveHitDelay(weapon);
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null || !target.IsAlive) continue;

                if (!attacked.Contains(target)) attacked.Add(target);
                LaunchBasicHit(character, target, weapon);
                if (perHit > 0f) yield return new WaitForSeconds(perHit);
            }

            Systems.Effects.AttackActionScope.End();

            int kills = CountKills(attacked);
            NotifyAttackActionFinished(character, new Data.Modifiers.Common.AttackActionResult(-1, attacked, kills));

            if (attackInterval > 0f) yield return new WaitForSeconds(attackInterval);
        }

        /// <summary>
        /// 행동 중 타격을 시도한 대상 중 지금 죽어 있는(또는 이미 파괴된) 수 — 시도 시점엔 모두 살아 있었다.
        /// 스코프 종료 직후에 불러야 마무리 효과·턴 사건 피해 처치가 섞이지 않는다.
        /// </summary>
        private static int CountKills(List<Unit> attacked)
        {
            int kills = 0;
            foreach (var target in attacked)
                if (target == null || !target.IsAlive) kills++;
            return kills;
        }
```

(타격 간 대기 `perHit` = 발사체 비행 시간 + 간격이므로 마지막 타격의 피해 콜백은 `End()` 전에 끝난다 — 기존 촉매 소모와 같은 전제.)

- [ ] **Step 5: 통지 도우미 교체**

368~388행의

```csharp
        /// <summary>주사위 사용 사건을 캐릭터의 공용 모디파이어에 통지 (저속 방호 등).</summary>
        private static void NotifyDiceUsed(Character character, int diceValue)
        {
```

부터 `NotifyComboBreak`의 닫는 `        }`까지를 다음으로 교체:

```csharp
        /// <summary>
        /// 캐릭터의 장착 모디파이어 중 리스너 T를 구현한 것에 통지한다.
        /// 통지 중 장착 목록이 바뀌어도 안전하도록 복사본을 돈다.
        /// </summary>
        private static void NotifyModifiers<T>(Character character, System.Action<T> invoke) where T : class
        {
            var mods = character != null && character.Stats != null && character.Stats.Modifiers != null
                ? character.Stats.Modifiers.Modifiers
                : null;
            if (mods == null || mods.Count == 0) return;

            var snapshot = new List<Data.Modifiers.CharacterModifier>(mods);
            foreach (var mod in snapshot)
                if (mod is T listener) invoke(listener);
        }

        /// <summary>주사위 사용 사건 (저속 방호 등).</summary>
        private static void NotifyDiceUsed(Character character, int diceValue)
            => NotifyModifiers<Data.Modifiers.Common.IDiceUseListener>(character, l => l.OnDiceUsed(character, diceValue));

        /// <summary>이동 완료 사건 (기동 장갑).</summary>
        private static void NotifyMoveFinished(Character character, int fromZone, int toZone)
            => NotifyModifiers<Data.Modifiers.Common.IMoveFinishedListener>(character, l => l.OnMoveFinished(character, fromZone, toZone));

        /// <summary>강화 조건 실패 사건 (예비 충전).</summary>
        private static void NotifyComboGateFailed(Character character)
            => NotifyModifiers<Data.Modifiers.Common.IComboGateFailedListener>(character, l => l.OnComboGateFailed(character));

        /// <summary>공격 행동 종료 사건 (대단원·독 바른 무기·공방 일체·사냥).</summary>
        private static void NotifyAttackActionFinished(Character character, Data.Modifiers.Common.AttackActionResult result)
            => NotifyModifiers<Data.Modifiers.Common.IAttackActionFinishedListener>(character, l => l.OnAttackActionFinished(character, result));

        /// <summary>행동(이동+공격) 종료 사건 (구조대).</summary>
        private static void NotifyActionFinished(Character character)
            => NotifyModifiers<Data.Modifiers.Common.IActionFinishedListener>(character, l => l.OnActionFinished(character));

        /// <summary>콤보 끊김(조건 불일치→기본공격 발생) 사건 통지 (안전장치 — Task 5에서 제거).</summary>
        private static void NotifyComboBreak(Character character)
            => NotifyModifiers<Data.Modifiers.Common.IComboBreakListener>(character, l => l.OnComboBrokenBasicAttack(character));
```

- [ ] **Step 6: 컴파일 PASS**

Run: 헤드리스 컴파일 → `RESULT PASS`. 자가 테스트: pending user run.

- [ ] **Step 7: 커밋**

```bash
git add Assets/Scripts/Data/Modifiers/Common/ModifierListeners.cs \
  Assets/Scripts/Data/Modifiers/Common/ModifierListeners.cs.meta \
  Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/AutoAttackSystem.cs \
  Assets/Scripts/Editor/CommonModifierSelfTests.cs
git commit -m "$(cat <<'EOF'
feat(combat): 자동공격 새 통지 — 이동 완료·강화 조건 실패·공격 행동 종료(처치 수)·행동 종료, 강화 공격 후 충전 1 소비

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: 중첩 제거·7종 삭제 — `CommonModifier`, 유지 4종 수치, 앙갚음 재작성, 공방 일체

**Files:**
- Rename+Modify: `Assets/Scripts/Data/Modifiers/Common/StackableCommonModifier.cs` → `CommonModifier.cs` (`.meta`도 함께 `git mv` — GUID 유지)
- Modify: `Assets/Scripts/Data/Modifiers/Common/ModifierListeners.cs` (`IDiceUseListener` 이동)
- Modify (전체 교체): `PositionModifiers.cs`, `DiceModifiers.cs`, `ComboModifiers.cs`, `SurvivalModifiers.cs` (모두 `Assets/Scripts/Data/Modifiers/Common/`)
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/AutoAttackSystem.cs` (콤보 끊김 통지 제거)
- Modify: `Assets/Scripts/Data/Modifiers/ModifierRegistry.cs:13-33, 136`
- Modify: `Assets/Scripts/Data/Modifiers/ModifierFamily.cs:3-6`
- Modify: `Assets/Scripts/UI/Reward/RewardUI.cs:580-584, 619-624`
- Modify: `Assets/Scripts/Editor/RewardFlowSelfTests.cs:146-147`
- Modify: `Assets/Scripts/Editor/CommonModifierSelfTests.cs`

삭제 대상(이 태스크 후 Assets/Scripts 어디에도 남지 않아야 함): `ZoneCrossModifier`, `MomentumStrikeModifier`, `ExtremeResonanceModifier`, `SafetyNetModifier`, `ChainReactionModifier`, `OpeningBarrierModifier`, `UnyieldingModifier`, `IComboBreakListener`, `StackableCommonModifier`, `StackCount`, `IsPrimary`, `MaxStacks`(모디파이어 쪽; `ChargeStatus.MaxStacks`는 별개).

- [ ] **Step 1: 실패하는 테스트 작성**

`// [tests-run]` 위에:

```csharp
                TestGrandFinaleOnlyStage3();
```

`// [tests-body]` 위에:

```csharp
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

```

- [ ] **Step 2: 실패 확인**

Run: 헤드리스 컴파일 → `RESULT FAIL` (`CommonModifier` 미정의).

- [ ] **Step 3: 베이스 이름 변경 + 교체**

```bash
git mv Assets/Scripts/Data/Modifiers/Common/StackableCommonModifier.cs Assets/Scripts/Data/Modifiers/Common/CommonModifier.cs
git mv Assets/Scripts/Data/Modifiers/Common/StackableCommonModifier.cs.meta Assets/Scripts/Data/Modifiers/Common/CommonModifier.cs.meta
```

`CommonModifier.cs` 전체:

```csharp
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Systems.Effects;
using UnityEngine;

namespace DiceOrbit.Data.Modifiers.Common
{
    /// <summary>
    /// 공용 모디파이어 공통 골격 (2026-08-28 도입, 2026-10-09 B 단순화).
    /// 모든 캐릭터가 장착할 수 있고, 같은 종류는 캐릭터당 1개다(CharacterModifier.CanApplyTo 기본 규칙) — 중첩 없음.
    /// 부여형 효과(방어도·충전·회복·최대 체력·상태)의 공용 도우미를 둔다.
    /// 이 클래스 이름은 세이브에 저장되지 않는다(세이브는 구체 타입명만 저장).
    /// </summary>
    [System.Serializable]
    public abstract class CommonModifier : CharacterModifier
    {
        public override ModifierCategory Category => ModifierCategory.Generic;

        /// <summary>owner가 주는 공격의 피해 계산 단계인가 (직접 체력 손실 제외).</summary>
        protected bool IsOwnAttackCalculation(CombatTrigger trigger, AttackContext context)
            => owner != null
               && trigger == CombatTrigger.OnCalculateOutput
               && context.SourceUnit == owner
               && !context.IsDirectHpLoss;

        /// <summary>임시 방어도 부여 + 버블.</summary>
        protected void GrantArmor(int amount)
        {
            if (owner == null || owner.Stats == null || !owner.IsAlive || amount <= 0) return;
            owner.Stats.TempArmor += amount;
            UI.CombatNotifier.Notify(owner, $"{ModifierName} 방어도 +{amount}", new Color(0.65f, 0.8f, 1f));
        }

        /// <summary>owner에게 충전 부여 (상한 3은 ChargeStatus.Grant가 보장, 버블 포함).</summary>
        protected void GrantCharge(int stacks)
        {
            if (owner == null || !owner.IsAlive) return;
            ChargeStatus.Grant(owner, stacks);
        }

        /// <summary>
        /// 대상 회복 — 파이프라인(HealContext) 경유, Unit.Heal이 최대 체력에서 자른다. 최대 체력이면 아무 일도 없다.
        /// 파이프라인이 없으면(테스트·특수 씬) 스탯에 직접 회복한다 (PartyManager.HealAllParty와 같은 폴백).
        /// </summary>
        protected void HealTarget(Character target, int amount)
        {
            if (target == null || target.Stats == null || !target.IsAlive || amount <= 0) return;
            if (target.Stats.CurrentHP >= target.Stats.MaxHP) return;

            if (CombatPipeline.Instance != null)
                CombatPipeline.Instance.Process(new HealContext(owner, target, ModifierName, amount));
            else
                target.Stats.Heal(amount);

            UI.CombatNotifier.Notify(target, $"{ModifierName} 체력 +{amount}", new Color(0.55f, 1f, 0.6f));
        }

        /// <summary>owner의 최대 체력·현재 체력을 amount만큼 영구히 올린다 + 버블 (완강한 신체·사냥).</summary>
        protected void RaiseMaxHp(int amount)
        {
            if (owner == null || owner.Stats == null) return;
            if (ApplyMaxHpGain(owner.Stats, amount) <= 0) return;
            UI.CombatNotifier.Notify(owner, $"{ModifierName} 최대 체력 +{amount}", new Color(0.55f, 1f, 0.6f));
        }

        /// <summary>최대 체력 +amount, 현재 체력 +amount(최대 체력 이하로). 실제로 올린 양을 반환 (순수 — 자가 테스트 대상).</summary>
        public static int ApplyMaxHpGain(UnitStats stats, int amount)
        {
            if (stats == null || amount <= 0) return 0;
            stats.MaxHP += amount;
            stats.CurrentHP = Mathf.Min(stats.CurrentHP + amount, stats.MaxHP);
            return amount;
        }

        /// <summary>대상에게 상태를 부여하고 상태 버블을 띄운다 (죽은 대상 무시). ComboActiveSkill.GrantStatus와 같은 방식.</summary>
        protected static void GrantStatusTo(Unit target, EffectType type, int value)
        {
            if (target == null || !target.IsAlive || target.StatusEffects == null || value <= 0) return;

            target.StatusEffects.AddEffect(StatusEffectManager.CreateEffect(type, value, -1));
            var data = UI.TooltipKeywordFormatter.BuildStatusDisplayData(type.ToString(), value, -1);
            UI.CombatNotifier.NotifyStatus(target, data.Name, data.Color);
        }
    }
}
```

- [ ] **Step 4: `IDiceUseListener`를 `ModifierListeners.cs`로**

`ModifierListeners.cs`에서

```csharp
    /// <summary>이동을 마친 직후(공격 전) — 출발 구역·도착 구역 (기동 장갑). 구역 판정 불가면 -1.</summary>
```

바로 위에 추가:

```csharp
    /// <summary>
    /// 주사위 사용 사건 (저속 방호 등). 이동+공격 처리 시작 시(공격 유무 무관) 통지된다.
    /// </summary>
    public interface IDiceUseListener
    {
        void OnDiceUsed(Character character, int diceValue);
    }

```

- [ ] **Step 5: `PositionModifiers.cs` 전체 교체**

```csharp
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Core.Zones;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.Modifiers.Common
{
    /// <summary>[합류 전술] 공격할 때 같은 구역에 다른 살아 있는 아군이 있으면 피해 +15%.</summary>
    [System.Serializable]
    public class JointTacticsModifier : CommonModifier
    {
        private const float Percent = 15f;

        public override string ModifierName => "합류 전술";
        public override ModifierFamily Family => ModifierFamily.Position;
        public override string Description
            => $"공격할 때 같은 구역에 다른 아군이 있으면 피해가 {Percent:0.#}% 증가합니다.";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (!IsOwnAttackCalculation(trigger, context)) return;
            if (!ZoneAllyCounter.HasOtherAllyInMyZone(owner)) return;

            context.OutputValue *= 1f + Percent / 100f;
        }
    }

    /// <summary>[독립 행동] 공격할 때 같은 구역에 다른 아군이 없으면 피해 +15%.</summary>
    [System.Serializable]
    public class LoneWolfModifier : CommonModifier
    {
        private const float Percent = 15f;

        public override string ModifierName => "독립 행동";
        public override ModifierFamily Family => ModifierFamily.Position;
        public override string Description
            => $"공격할 때 같은 구역에 다른 아군이 없으면 피해가 {Percent:0.#}% 증가합니다.";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (!IsOwnAttackCalculation(trigger, context)) return;

            var zones = CombatZoneManager.Instance;
            if (zones == null || zones.GetZoneOf(owner) < 0) return;   // 구역 밖이면 판정 불가
            if (ZoneAllyCounter.HasOtherAllyInMyZone(owner)) return;

            context.OutputValue *= 1f + Percent / 100f;
        }
    }

    /// <summary>같은 구역의 '다른 아군' 존재 판정 (합류 전술/독립 행동 공용).</summary>
    internal static class ZoneAllyCounter
    {
        public static bool HasOtherAllyInMyZone(Character self)
        {
            var zones = CombatZoneManager.Instance;
            var party = PartyManager.Instance;
            if (zones == null || party == null || self == null) return false;

            int myZone = zones.GetZoneOf(self);
            if (myZone < 0) return false;

            foreach (var ally in party.GetAliveCharacters())
            {
                if (ally == null || ally == self) continue;
                if (zones.GetZoneOf(ally) == myZone) return true;
            }
            return false;
        }
    }
}
```

- [ ] **Step 6: `DiceModifiers.cs` 전체 교체**

```csharp
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Systems.Effects;
using UnityEngine;

namespace DiceOrbit.Data.Modifiers.Common
{
    /// <summary>[저속 방호] 눈 3 이하 주사위를 사용하면 방어도 4. 공격 성사 여부와 무관.</summary>
    [System.Serializable]
    public class LowRollGuardModifier : CommonModifier, IDiceUseListener
    {
        private const int Armor = 4;

        public override string ModifierName => "저속 방호";
        public override ModifierFamily Family => ModifierFamily.Dice;
        public override string Description
            => $"주사위 눈이 3 이하면 방어도를 {Armor}만큼 얻습니다.";

        public void OnDiceUsed(Character character, int diceValue)
        {
            if (character != owner || diceValue > 3) return;
            GrantArmor(Armor);
        }
    }

    /// <summary>
    /// [앙갚음] 적의 공격으로 체력을 잃으면 충전 1. 방어도만 깎이면(체력 피해 0) 발동하지 않는다(IsEffected 기준).
    /// 적의 다단 공격은 공격 행동 1회당 최대 1번 — 행동 ID로 중복을 막는다.
    /// 스코프 밖에서 도착한 지연 타격은 (같은 몬스터, 같은 프레임)을 한 행동으로 본다.
    /// </summary>
    [System.Serializable]
    public class PaybackModifier : CommonModifier
    {
        [System.NonSerialized] private int _lastChargedActionId;
        [System.NonSerialized] private Unit _lastOutOfScopeSource;
        [System.NonSerialized] private int _lastOutOfScopeFrame = -1;

        public override string ModifierName => "앙갚음";
        public override ModifierFamily Family => ModifierFamily.Dice;
        public override string Description
            => "적의 공격으로 체력을 잃으면 충전을 1 얻습니다. 적의 공격 행동 1회당 한 번만 발동합니다.";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null) return;
            if (trigger != CombatTrigger.OnPostAction) return;
            if (context.Target != owner || !(context.SourceUnit is Monster)) return;
            if (context.IsDirectHpLoss || context.IsSimulation || !context.IsEffected) return;
            if (!TryMarkAction(context.SourceUnit)) return;

            GrantCharge(1);
        }

        /// <summary>이 적 공격 행동에서 아직 충전하지 않았으면 기록하고 true.</summary>
        private bool TryMarkAction(Unit source)
        {
            if (AttackActionScope.IsActive)
            {
                int id = AttackActionScope.CurrentActionId;
                if (id == _lastChargedActionId) return false;
                _lastChargedActionId = id;
                return true;
            }

            int frame = Time.frameCount;
            if (ReferenceEquals(source, _lastOutOfScopeSource) && frame == _lastOutOfScopeFrame) return false;
            _lastOutOfScopeSource = source;
            _lastOutOfScopeFrame = frame;
            return true;
        }
    }
}
```

- [ ] **Step 7: `ComboModifiers.cs` 전체 교체**

```csharp
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.Modifiers.Common
{
    /// <summary>[대단원] 콤보 3단계 강화 공격 피해 +25%.</summary>
    [System.Serializable]
    public class GrandFinaleModifier : CommonModifier
    {
        private const float Percent = 25f;

        public override string ModifierName => "대단원";
        public override ModifierFamily Family => ModifierFamily.Combo;
        public override string Description
            => $"3단계 강화 공격의 피해가 {Percent:0.#}% 증가합니다.";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (!IsOwnAttackCalculation(trigger, context)) return;
            if (!AttackActionScope.IsActive || AttackActionScope.CurrentSource != owner) return;
            if (AttackActionScope.CurrentInfo.ComboStage != 2) return;

            context.OutputValue *= 1f + Percent / 100f;
        }
    }
}
```

- [ ] **Step 8: `SurvivalModifiers.cs` 전체 교체 (공방 일체)**

```csharp
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.Modifiers.Common
{
    /// <summary>[공방 일체] 공격 행동(기본/강화)을 마친 뒤 방어도 3.</summary>
    [System.Serializable]
    public class OffenseDefenseModifier : CommonModifier, IAttackActionFinishedListener
    {
        private const int Armor = 3;

        public override string ModifierName => "공방 일체";
        public override ModifierFamily Family => ModifierFamily.Survival;
        public override string Description
            => $"공격을 마친 뒤 방어도를 {Armor}만큼 얻습니다.";

        public void OnAttackActionFinished(Character character, AttackActionResult result)
        {
            if (character != owner || result.Targets.Count == 0) return;
            GrantArmor(Armor);
        }
    }
}
```

- [ ] **Step 9: `AutoAttackSystem` — 콤보 끊김 통지 제거**

`BasicAttackRoutine` 요약 주석

```csharp
        /// <summary>
        /// 기본공격 1회 (단일 대상). 기본공격도 하나의 공격 행동이라 촉매가 여기에도 적용·소모된다.
        /// plan.HadCombo면(1단계 이상 쌓인 콤보가 조건 불일치로 끊긴 경우) 공격 발생 시점에
        /// 안전장치류 모디파이어에 통지한다.
        /// </summary>
```

를

```csharp
        /// <summary>
        /// 기본공격 1회 (단일 대상). 기본공격도 하나의 공격 행동이라 촉매가 여기에도 적용·소모된다.
        /// </summary>
```

로 바꾸고, 같은 메서드 안의

```csharp
            // "콤보가 끊기고 일반 자동공격 발생" — 공격이 실제로 나가는 이 시점에만 통지.
            if (plan.HadCombo) NotifyComboBreak(character);

```

을 삭제한다. 통지 도우미 끝의

```csharp

        /// <summary>콤보 끊김(조건 불일치→기본공격 발생) 사건 통지 (안전장치 — Task 5에서 제거).</summary>
        private static void NotifyComboBreak(Character character)
            => NotifyModifiers<Data.Modifiers.Common.IComboBreakListener>(character, l => l.OnComboBrokenBasicAttack(character));
```

도 삭제한다. (`plan.HadCombo`는 행동 예고가 계속 쓴다 — 필드는 남긴다.)

- [ ] **Step 10: 레지스트리 — 현재 구현된 6종**

`ModifierRegistry.cs`에서

```csharp
        // 2026-08-28 전면 교체 — 공용 12종 (위치/주사위/콤보/생존 4계열, 종류당 최대 3중첩).
        // 캐릭터 전용 시그니처는 전부 폐기됐다. CanApplyTo가 3중첩 상한을 강제한다.
        private static readonly System.Func<CharacterModifier>[] Factories =
        {
            // 위치
            () => new Common.JointTacticsModifier(),
            () => new Common.LoneWolfModifier(),
            () => new Common.ZoneCrossModifier(),
            // 주사위
            () => new Common.LowRollGuardModifier(),
            () => new Common.MomentumStrikeModifier(),
            () => new Common.ExtremeResonanceModifier(),
            // 콤보
            () => new Common.SafetyNetModifier(),
            () => new Common.ChainReactionModifier(),
            () => new Common.GrandFinaleModifier(),
            // 생존
            () => new Common.OpeningBarrierModifier(),
            () => new Common.UnyieldingModifier(),
            () => new Common.PaybackModifier(),
        };
```

를

```csharp
        // 2026-10-09 B — 시트 공용 17종 (위치 5 / 주사위 4 / 콤보 4 / 생존 4). 같은 종류는 캐릭터당 1개(CanApplyTo).
        // 캐릭터 전용 시그니처는 전부 폐기됐다. 삭제된 타입명을 담은 옛 세이브는 Validate에서 실패 → 새 게임 폴백(D9).
        private static readonly System.Func<CharacterModifier>[] Factories =
        {
            // 위치
            () => new Common.JointTacticsModifier(),
            () => new Common.LoneWolfModifier(),
            // 주사위
            () => new Common.LowRollGuardModifier(),
            () => new Common.PaybackModifier(),
            // 콤보
            () => new Common.GrandFinaleModifier(),
            // 생존
            () => new Common.OffenseDefenseModifier(),
        };
```

로 바꾸고, `CountOn` 요약

```csharp
        /// <summary>캐릭터에 장착된 같은 종류(타입) 모디파이어 수 — 보상 카드의 "중첩 n → n+1" 표시용.</summary>
```

를

```csharp
        /// <summary>캐릭터에 장착된 같은 종류(타입) 모디파이어 수 — 중복 장착 불가라 0 또는 1 (보상 카드의 보유 여부 표시용).</summary>
```

로 바꾼다.

- [ ] **Step 11: `ModifierFamily.cs` 주석**

```csharp
    /// 공용 12종은 계열당 3종. 계열이 없는 모디파이어(None)는 보상 카드로 제시할 수 없다.
```

를

```csharp
    /// 공용 17종 = 위치 5 / 주사위 4 / 콤보 4 / 생존 4 (2026-10-09 B). 계열이 없는 모디파이어(None)는 보상 카드로 제시할 수 없다.
```

로 바꾼다.

- [ ] **Step 12: `RewardUI` — 상태 문구와 핍(0/1)**

`RefreshUpgrade()`의

```csharp
                int stacks = ModifierRegistry.CountOn(_selectedCharacter, offer);
                bool can = offer.CanApplyTo(_selectedCharacter);
                string status = !can ? $"{who} · 최대 중첩"
                              : stacks == 0 ? $"{who} · 신규"
                              : $"{who} · 중첩 {stacks} → {stacks + 1}";
```

를

```csharp
                bool owned = ModifierRegistry.CountOn(_selectedCharacter, offer) > 0;
                bool can = offer.CanApplyTo(_selectedCharacter);
                string status = owned ? $"{who} · 보유 중"
                              : !can ? $"{who} · 장착 불가"
                              : $"{who} · 신규";
```

로, `StackPips`의

```csharp
            int max = StackableCommonModifier.MaxStacks;
```

를

```csharp
            const int max = 1;   // 같은 종류 중복 장착 불가 (2026-10-09 B) — 보유 ●, 미보유 ○
```

로 바꾼다. (`using DiceOrbit.Data.Modifiers.Common;`은 네임스페이스가 남아 있으므로 그대로 둬도 컴파일된다.)

- [ ] **Step 13: `RewardFlowSelfTests` — 계열 단언 완화**

```csharp
            foreach (var kv in perFamily) Check(kv.Value == 3, $"{kv.Key}: 계열당 3종 (실제 {kv.Value})");
            Check(perFamily.Count == 4, "계열 4개");
```

를

```csharp
            foreach (var kv in perFamily) Check(kv.Value >= 1, $"{kv.Key}: 계열당 1종 이상 (실제 {kv.Value})");
            Check(perFamily.Count == 4, "4계열 모두 1종 이상");
```

로 바꾼다.

- [ ] **Step 14: 잔존 참조 감사 + 컴파일 PASS**

```bash
grep -rnE "ZoneCrossModifier|MomentumStrikeModifier|ExtremeResonanceModifier|SafetyNetModifier|ChainReactionModifier|OpeningBarrierModifier|UnyieldingModifier|IComboBreakListener|StackableCommonModifier|StackCount\(|IsPrimary\(" Assets/Scripts
```

Expected: 출력 없음. 이어서 헤드리스 컴파일 → `RESULT PASS`. 자가 테스트(Common Modifier·Reward Flow): pending user run.

- [ ] **Step 15: 커밋**

```bash
git add -A Assets/Scripts/Data/Modifiers/Common/ \
  Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/AutoAttackSystem.cs \
  Assets/Scripts/Data/Modifiers/ModifierRegistry.cs \
  Assets/Scripts/Data/Modifiers/ModifierFamily.cs \
  Assets/Scripts/UI/Reward/RewardUI.cs \
  Assets/Scripts/Editor/RewardFlowSelfTests.cs \
  Assets/Scripts/Editor/CommonModifierSelfTests.cs
git commit -m "$(cat <<'EOF'
refactor(modifier): 3중첩 제거 — CommonModifier, 7종·콤보 끊김 통지 삭제, 합류/독립 15%·저속 방호 4·대단원 25%, 앙갚음=충전, 공방 일체

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: 획득 경로 — 보상·상점·디버그는 `Acquire`

**Files:**
- Modify: `Assets/Scripts/UI/Reward/RewardUI.cs:607`
- Modify: `Assets/Scripts/UI/ShopUI.cs:396`
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/BattleStageDebugManager.cs:53`
- (변경 없음 확인) `Assets/Scripts/Core/Stage/PartyManager.cs:228` — 세이브 복원은 `Add` 유지

테스트: Acquire/Add 분리는 Task 3의 `TestAcquireCallsOnAcquiredButAddDoesNot`가 덮는다. UI 배선은 자가 테스트 대상이 아니다(플레이 확인).

- [ ] **Step 1: 교체 3곳**

1. `RewardUI.cs` `ConfirmUpgrade()` — `_selectedCharacter.Stats.Modifiers.Add(offer);` → `_selectedCharacter.Stats.Modifiers.Acquire(offer);`
2. `ShopUI.cs` `ShowNextRepick` 람다 — `_incoming.Stats?.Modifiers?.Add(captured);` →

```csharp
                    if (_incoming.Stats != null && _incoming.Stats.Modifiers != null)
                        _incoming.Stats.Modifiers.Acquire(captured);
```

3. `BattleStageDebugManager.cs` — `modifiers.Add(new DiceOrbit.Data.Modifiers.Common.JointTacticsModifier());` → `modifiers.Acquire(new DiceOrbit.Data.Modifiers.Common.JointTacticsModifier());`

- [ ] **Step 2: 복원 경로 확인**

```bash
grep -rn "Modifiers.Add(\|Modifiers?.Add(\|modifiers.Add(new" Assets/Scripts
```

Expected: `PartyManager.cs:165`(세이브 데이터 목록 — 모디파이어 아님)과 `PartyManager.cs:228`(복원 — `Add` 유지)만 남는다.

- [ ] **Step 3: 컴파일 PASS**

Run: 헤드리스 컴파일 → `RESULT PASS`.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/UI/Reward/RewardUI.cs Assets/Scripts/UI/ShopUI.cs \
  Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/BattleStageDebugManager.cs
git commit -m "$(cat <<'EOF'
feat(modifier): 보상·상점·디버그 획득은 Acquire(OnAcquired 1회), 세이브 복원은 Add 유지

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 7: 위치 계열 신규 3종 — 듀얼·기동 장갑·구조대 (+ `GetZoneOfMonster`)

**Files:**
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Zone/CombatZoneManager.cs` (`GetZoneOf(Character)` 다음, 124행 뒤)
- Modify: `Assets/Scripts/Data/Modifiers/Common/PositionModifiers.cs`
- Modify: `Assets/Scripts/Data/Modifiers/ModifierRegistry.cs`
- Modify: `Assets/Scripts/Editor/CommonModifierSelfTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

`// [tests-run]` 위에:

```csharp
                TestRescuePicksLowestHurtAlly();
```

`// [tests-body]` 위에:

```csharp
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

```

- [ ] **Step 2: 실패 확인**

Run: 헤드리스 컴파일 → `RESULT FAIL` (`RescueTeamModifier` 미정의).

- [ ] **Step 3: `CombatZoneManager.GetZoneOfMonster`**

`GetZoneOf(Character)` 메서드 닫는 `        }` 다음(“이 구역의 주인 몬스터.” 요약 위)에 추가:

```csharp

        /// <summary>
        /// 몬스터가 스폰 시 배정된 구역 번호. 배정되지 않았으면 -1 (듀얼의 '대상 양옆 구역' 판정용).
        /// 원 배정은 사망 후에도 유지되므로 죽은 몬스터도 원래 구역을 돌려준다.
        /// </summary>
        public int GetZoneOfMonster(Monster monster)
        {
            if (monster == null) return -1;
            foreach (var pair in _assignedOwners)
                if (pair.Value == monster) return pair.Key;
            return -1;
        }
```

- [ ] **Step 4: `PositionModifiers.cs`에 3종 추가**

파일 첫 줄 `using DiceOrbit.Core;` 위에 `using System.Collections.Generic;`을 추가하고,

```csharp
    /// <summary>같은 구역의 '다른 아군' 존재 판정 (합류 전술/독립 행동 공용).</summary>
```

바로 위에 다음을 삽입:

```csharp
    /// <summary>
    /// [듀얼] 공격 대상의 양옆 구역(원형 wrap)에 다른 살아 있는 몬스터가 없으면 피해 +15%.
    /// 같은 구역은 보지 않는다(구역당 몬스터 1마리 규칙). 대상 구역을 모르면 보정 없음.
    /// </summary>
    [System.Serializable]
    public class DuelModifier : CommonModifier
    {
        private const float Percent = 15f;

        public override string ModifierName => "듀얼";
        public override ModifierFamily Family => ModifierFamily.Position;
        public override string Description
            => $"공격 대상의 양옆 구역에 다른 몬스터가 없으면 피해가 {Percent:0.#}% 증가합니다.";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (!IsOwnAttackCalculation(trigger, context)) return;
            if (!(context.Target is Monster target)) return;
            if (!IsIsolated(target)) return;

            context.OutputValue *= 1f + Percent / 100f;
        }

        /// <summary>대상의 양옆 구역에 대상이 아닌 살아 있는 주인이 하나도 없는가.</summary>
        public static bool IsIsolated(Monster target)
        {
            var zones = CombatZoneManager.Instance;
            if (zones == null || target == null) return false;

            int zone = zones.GetZoneOfMonster(target);
            if (zone < 0) return false;

            int n = zones.ZoneCount;
            int left = ((zone - 1) % n + n) % n;
            int right = (zone + 1) % n;
            return !HasOtherLivingOwner(zones, left, target) && !HasOtherLivingOwner(zones, right, target);
        }

        private static bool HasOtherLivingOwner(CombatZoneManager zones, int zone, Monster self)
        {
            var owner = zones.GetOwner(zone);   // 죽었거나 없으면 null
            return owner != null && owner != self;
        }
    }

    /// <summary>[기동 장갑] 다른 구역으로 이동을 마치면 방어도 4. 공격할 몬스터가 없어도 발동.</summary>
    [System.Serializable]
    public class MobileArmorModifier : CommonModifier, IMoveFinishedListener
    {
        private const int Armor = 4;

        public override string ModifierName => "기동 장갑";
        public override ModifierFamily Family => ModifierFamily.Position;
        public override string Description
            => $"다른 구역으로 이동을 마치면 방어도를 {Armor}만큼 얻습니다.";

        public void OnMoveFinished(Character character, int fromZone, int toZone)
        {
            if (character != owner) return;
            if (fromZone < 0 || toZone < 0 || fromZone == toZone) return;
            GrantArmor(Armor);
        }
    }

    /// <summary>
    /// [구조대] 행동(이동만 해도 포함)을 마친 뒤 같은 구역에서 현재 체력이 가장 낮은 아군 1명(자신 포함, 동률 무작위) 체력 +2.
    /// 최대 체력인 아군은 대상에서 제외(초과 회복 없음). 회복은 HealContext로 파이프라인을 거친다.
    /// </summary>
    [System.Serializable]
    public class RescueTeamModifier : CommonModifier, IActionFinishedListener
    {
        private const int HealAmount = 2;

        public override string ModifierName => "구조대";
        public override ModifierFamily Family => ModifierFamily.Position;
        public override string Description
            => $"행동을 마친 뒤 같은 구역에서 체력이 가장 낮은 아군(자신 포함)의 체력을 {HealAmount} 회복합니다.";

        public void OnActionFinished(Character character)
        {
            if (character != owner || owner == null || !owner.IsAlive) return;

            var target = PickTarget(owner);
            if (target != null) HealTarget(target, HealAmount);
        }

        /// <summary>self와 같은 구역의 살아 있는 아군(자신 포함) 중 회복 대상.</summary>
        public static Character PickTarget(Character self)
        {
            var zones = CombatZoneManager.Instance;
            var party = PartyManager.Instance;
            if (zones == null || party == null || self == null) return null;

            int myZone = zones.GetZoneOf(self);
            if (myZone < 0) return null;

            var candidates = new List<Character>();
            foreach (var ally in party.GetAliveCharacters())
                if (ally != null && zones.GetZoneOf(ally) == myZone) candidates.Add(ally);

            return PickLowestHp(candidates);
        }

        /// <summary>최대 체력 미만인 생존자 중 현재 체력 최저 1명, 동률이면 무작위 (순수 — 자가 테스트 대상). 없으면 null.</summary>
        public static Character PickLowestHp(IReadOnlyList<Character> candidates)
        {
            if (candidates == null) return null;

            List<Character> lowest = null;
            int best = int.MaxValue;
            foreach (var c in candidates)
            {
                if (c == null || c.Stats == null || !c.IsAlive) continue;
                if (c.Stats.CurrentHP >= c.Stats.MaxHP) continue;

                if (c.Stats.CurrentHP < best)
                {
                    best = c.Stats.CurrentHP;
                    lowest = new List<Character> { c };
                }
                else if (c.Stats.CurrentHP == best)
                {
                    lowest.Add(c);
                }
            }

            if (lowest == null) return null;
            return lowest[UnityEngine.Random.Range(0, lowest.Count)];
        }
    }

```

- [ ] **Step 5: 레지스트리 등록**

`ModifierRegistry.cs`에서

```csharp
            () => new Common.LoneWolfModifier(),
```

다음에 추가:

```csharp
            () => new Common.DuelModifier(),
            () => new Common.MobileArmorModifier(),
            () => new Common.RescueTeamModifier(),
```

- [ ] **Step 6: 컴파일 PASS**

Run: 헤드리스 컴파일 → `RESULT PASS`. 자가 테스트: pending user run.

- [ ] **Step 7: 커밋**

```bash
git add Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Zone/CombatZoneManager.cs \
  Assets/Scripts/Data/Modifiers/Common/PositionModifiers.cs \
  Assets/Scripts/Data/Modifiers/ModifierRegistry.cs \
  Assets/Scripts/Editor/CommonModifierSelfTests.cs
git commit -m "$(cat <<'EOF'
feat(modifier): 위치 계열 — 듀얼(대상 양옆 구역 비면 +15%)·기동 장갑(구역 이동 시 방어도 4)·구조대(같은 구역 최저 체력 +2), GetZoneOfMonster

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 8: 주사위 계열 신규 2종 — 예비 충전·사전 준비 (+ 전투 경계 충전 제거)

**Files:**
- Modify: `Assets/Scripts/Data/Modifiers/Common/DiceModifiers.cs` (파일 끝)
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/CombatManager.cs` (`BroadcastCombatStart` 206행, `EndCombat` 297행 `ExpireRoundStatuses();` 줄, `ExpireRoundStatuses` 메서드 718~729행 뒤)
- Modify: `Assets/Scripts/Data/Modifiers/ModifierRegistry.cs`

**충전 제거 위치(순서 확인 결과):** `IntroThenStart`는 `BroadcastCombatStart()` → `StartCombat()` 순이다. 스펙 문구대로 `StartCombat`에서 지우면 사전 준비가 CombatStart 방송에서 준 충전까지 지워진다. 그래서 제거를 **`BroadcastCombatStart` 맨 앞**(방송 루프·가드보다 먼저)에 둔다. 디버그 폴백(`GameFlowManager`가 방송 없이 `StartCombat`만 부름)도 덮도록 **`EndCombat`에서도** 제거한다 — 충전은 전투를 넘어 이월되지 않는다.

테스트: 두 모디파이어는 `ChargeStatus.Grant(Unit)`(상태 매니저·버블)과 파이프라인 방송에 기대므로 자가 테스트(비활성 유닛, StatusEffects 없음)로는 의미 있는 검증이 안 된다 → 플레이 확인(스펙 §5-3). 충전 자체의 상한·소비는 Task 1 테스트가 덮는다.

- [ ] **Step 1: `DiceModifiers.cs`에 2종 추가**

파일 끝의

```csharp
            _lastOutOfScopeSource = source;
            _lastOutOfScopeFrame = frame;
            return true;
        }
    }
}
```

를

```csharp
            _lastOutOfScopeSource = source;
            _lastOutOfScopeFrame = frame;
            return true;
        }
    }

    /// <summary>[예비 충전] 사용한 주사위가 강화 공격 조건을 못 맞추면 충전 1 (기본공격 실행 여부 무관).</summary>
    [System.Serializable]
    public class ReserveChargeModifier : CommonModifier, IComboGateFailedListener
    {
        public override string ModifierName => "예비 충전";
        public override ModifierFamily Family => ModifierFamily.Dice;
        public override string Description
            => "사용한 주사위가 강화 공격 조건을 맞추지 못하면 충전을 1 얻습니다.";

        public void OnComboGateFailed(Character character)
        {
            if (character != owner) return;
            GrantCharge(1);
        }
    }

    /// <summary>[사전 준비] 전투 시작 시 충전 1 (CombatStart 방송, 방송 1회당 1번).</summary>
    [System.Serializable]
    public class PreparationModifier : CommonModifier
    {
        public override string ModifierName => "사전 준비";
        public override ModifierFamily Family => ModifierFamily.Dice;
        public override string Description
            => "전투 시작 시 충전을 1 얻습니다.";

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null) return;
            if (context.Phase != EventPhase.CombatStart) return;
            if (trigger != CombatTrigger.OnPreAction) return;   // 방송 1회당 1번만 (트리거 4회 방지)
            if (context.SourceUnit != owner || context.IsSimulation) return;

            GrantCharge(1);
        }
    }
}
```

로 바꾼다.

- [ ] **Step 2: `CombatManager` — 충전 제거**

(a) `BroadcastCombatStart`의

```csharp
        private void BroadcastCombatStart()
        {
            if (Pipeline.CombatPipeline.Instance == null || PartyManager.Instance == null) return;
```

를

```csharp
        private void BroadcastCombatStart()
        {
            // 이전 전투의 충전은 남기지 않는다 — 방송(사전 준비의 전투 시작 충전)보다 반드시 먼저 (2026-10-09 B).
            ClearPartyCharge();

            if (Pipeline.CombatPipeline.Instance == null || PartyManager.Instance == null) return;
```

로 바꾼다.

(b) `EndCombat`의

```csharp
            ExpireRoundStatuses(); // 턴 도중 승리·패배여도 라운드형 상태가 남지 않게
```

다음 줄에 추가:

```csharp
            ClearPartyCharge();    // 충전은 전투를 넘어 이월되지 않는다 (방송 없는 디버그 시작 경로 포함)
```

(c) `ExpireRoundStatuses()` 메서드 닫는 `        }` 다음에 추가:

```csharp

        /// <summary>파티 전원의 충전 제거 — 전투 경계에서 호출 (2026-10-09 B).</summary>
        private void ClearPartyCharge()
        {
            var party = PartyManager.Instance;
            if (party == null || party.Party == null) return;

            foreach (var character in party.Party)
                if (character != null) Systems.Effects.ChargeStatus.Clear(character);
        }
```

- [ ] **Step 3: 레지스트리 등록**

`ModifierRegistry.cs`에서

```csharp
            () => new Common.PaybackModifier(),
```

를

```csharp
            () => new Common.ReserveChargeModifier(),
            () => new Common.PreparationModifier(),
            () => new Common.PaybackModifier(),
```

로 바꾼다.

- [ ] **Step 4: 컴파일 PASS**

Run: 헤드리스 컴파일 → `RESULT PASS`.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Data/Modifiers/Common/DiceModifiers.cs \
  Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/CombatManager.cs \
  Assets/Scripts/Data/Modifiers/ModifierRegistry.cs
git commit -m "$(cat <<'EOF'
feat(modifier): 주사위 계열 — 예비 충전(조건 실패 시 충전 1)·사전 준비(전투 시작 충전 1), 충전은 방송 전·전투 종료 시 제거

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 9: 콤보 계열 — 대단원 방어도, 균열·독 바른 무기·기본기 단련

**Files:**
- Modify (전체 교체): `Assets/Scripts/Data/Modifiers/Common/ComboModifiers.cs`
- Modify: `Assets/Scripts/Data/Modifiers/ModifierRegistry.cs`
- Modify: `Assets/Scripts/Editor/CommonModifierSelfTests.cs`

**균열 규칙:** OnPreAction(실제 행동만)에서 행동 ID가 바뀌었으면 그 컨텍스트의 대상을 "첫 대상"으로 기록하고, 원래 방어도가 0인지 기록한 뒤 방어도를 최대 4 제거한다(행동당 1회). OnCalculateOutput에서는 같은 행동·같은 대상이고 "처음부터 방어도 없음"이 기록됐을 때만 +15% — 이 효과로 0이 된 경우는 보정 없음. 시뮬레이션(행동 예고)은 상태를 바꾸지 않고 대상의 현재 방어도가 0이면 +15%로 근사한다(예고는 어떤 대상이 첫 대상인지 모름 — 결정 사항 참조).

- [ ] **Step 1: 실패하는 테스트 작성**

`// [tests-run]` 위에:

```csharp
                TestFractureBonusOnlyForFirstUnarmoredTarget();
                TestBasicTrainingAddsAttack();
```

`// [tests-body]` 위에:

```csharp
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

```

- [ ] **Step 2: 실패 확인**

Run: 헤드리스 컴파일 → `RESULT FAIL` (`FractureModifier`, `BasicTrainingModifier` 미정의).

- [ ] **Step 3: `ComboModifiers.cs` 전체 교체**

```csharp
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Systems.Effects;
using UnityEngine;

namespace DiceOrbit.Data.Modifiers.Common
{
    /// <summary>[대단원] 콤보 3단계 강화 공격 피해 +25%, 그 공격을 마친 뒤 방어도 4 (3단계가 실제로 실행돼야 함).</summary>
    [System.Serializable]
    public class GrandFinaleModifier : CommonModifier, IAttackActionFinishedListener
    {
        private const float Percent = 25f;
        private const int Armor = 4;

        public override string ModifierName => "대단원";
        public override ModifierFamily Family => ModifierFamily.Combo;
        public override string Description
            => $"3단계 강화 공격의 피해가 {Percent:0.#}% 증가하고, 그 공격을 마친 뒤 방어도를 {Armor}만큼 얻습니다.";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (!IsOwnAttackCalculation(trigger, context)) return;
            if (!AttackActionScope.IsActive || AttackActionScope.CurrentSource != owner) return;
            if (AttackActionScope.CurrentInfo.ComboStage != 2) return;

            context.OutputValue *= 1f + Percent / 100f;
        }

        public void OnAttackActionFinished(Character character, AttackActionResult result)
        {
            if (character != owner || result.ComboStage != 2 || result.Targets.Count == 0) return;
            GrantArmor(Armor);
        }
    }

    /// <summary>
    /// [균열] 공격 전 첫 대상의 일시 방어도 4 제거(행동당 1회). 그 대상에게 처음부터 방어도가 없었으면 그 대상에 대한 피해 +15%.
    /// 이 효과로 0이 된 경우는 +15% 없음. "첫 대상" = 행동에서 처음 OnPreAction에 들어온 대상.
    /// 행동 예고(시뮬레이션)는 상태를 바꾸지 않고, 대상의 현재 방어도가 0이면 +15%로 근사한다.
    /// </summary>
    [System.Serializable]
    public class FractureModifier : CommonModifier
    {
        private const int ArmorBreak = 4;
        private const float Percent = 15f;

        [System.NonSerialized] private int _actionId;
        [System.NonSerialized] private Unit _firstTarget;
        [System.NonSerialized] private bool _firstTargetHadNoArmor;

        public override string ModifierName => "균열";
        public override ModifierFamily Family => ModifierFamily.Combo;
        public override string Description
            => $"공격 전 첫 대상의 방어도를 {ArmorBreak} 제거합니다. 그 대상에게 처음부터 방어도가 없었다면 피해가 {Percent:0.#}% 증가합니다.";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null || context.SourceUnit != owner || context.IsDirectHpLoss) return;
            if (!AttackActionScope.IsActive || AttackActionScope.CurrentSource != owner) return;

            if (trigger == CombatTrigger.OnPreAction)
            {
                if (!context.IsSimulation) BreakFirstTarget(context.Target);
                return;
            }

            if (trigger != CombatTrigger.OnCalculateOutput) return;

            if (context.IsSimulation)
            {
                if (context.Target != null && context.Target.Stats != null && context.Target.Stats.TempArmor <= 0)
                    context.OutputValue *= 1f + Percent / 100f;
                return;
            }

            if (AttackActionScope.CurrentActionId == _actionId && context.Target == _firstTarget && _firstTargetHadNoArmor)
                context.OutputValue *= 1f + Percent / 100f;
        }

        /// <summary>이 행동의 첫 대상을 기록하고 방어도를 깎는다 — 행동 ID가 같으면 이미 처리됐다.</summary>
        private void BreakFirstTarget(Unit target)
        {
            int id = AttackActionScope.CurrentActionId;
            if (id == _actionId) return;

            _actionId = id;
            _firstTarget = target;
            _firstTargetHadNoArmor = false;
            if (target == null || target.Stats == null) return;

            int armor = target.Stats.TempArmor;
            _firstTargetHadNoArmor = armor <= 0;
            if (armor <= 0) return;

            int removed = Mathf.Min(ArmorBreak, armor);
            target.Stats.TempArmor -= removed;
            UI.CombatNotifier.Notify(target, $"{ModifierName} 방어도 -{removed}", new Color(1f, 0.7f, 0.45f));
        }
    }

    /// <summary>
    /// [독 바른 무기] 공격을 마친 뒤 공격한 몬스터마다 중독 1 (광역이면 대상마다).
    /// 공격 종료 시점에 이미 죽은 대상은 건너뛴다.
    /// </summary>
    [System.Serializable]
    public class PoisonedWeaponModifier : CommonModifier, IAttackActionFinishedListener
    {
        private const int PoisonStacks = 1;

        public override string ModifierName => "독 바른 무기";
        public override ModifierFamily Family => ModifierFamily.Combo;
        public override string Description
            => $"공격을 마친 뒤 공격한 몬스터마다 중독을 {PoisonStacks} 부여합니다.";

        public void OnAttackActionFinished(Character character, AttackActionResult result)
        {
            if (character != owner) return;

            foreach (var target in result.Targets)
                if (target is Monster && target != null && target.IsAlive)
                    GrantStatusTo(target, EffectType.Poison, PoisonStacks);
        }
    }

    /// <summary>[기본기 단련] 기본 공격력 +2 — 유효 공격력(CharacterStats.EffectiveAttack)으로 반영, 저장 필드는 그대로(D6).</summary>
    [System.Serializable]
    public class BasicTrainingModifier : CommonModifier
    {
        private const int Bonus = 2;

        public override string ModifierName => "기본기 단련";
        public override ModifierFamily Family => ModifierFamily.Combo;
        public override string Description
            => $"기본 공격력이 {Bonus} 증가합니다.";

        public override int AttackBonus => Bonus;
    }
}
```

- [ ] **Step 4: 레지스트리 등록**

`ModifierRegistry.cs`에서

```csharp
            () => new Common.GrandFinaleModifier(),
```

다음에 추가:

```csharp
            () => new Common.FractureModifier(),
            () => new Common.PoisonedWeaponModifier(),
            () => new Common.BasicTrainingModifier(),
```

- [ ] **Step 5: 컴파일 PASS**

Run: 헤드리스 컴파일 → `RESULT PASS`. 자가 테스트: pending user run.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/Data/Modifiers/Common/ComboModifiers.cs \
  Assets/Scripts/Data/Modifiers/ModifierRegistry.cs \
  Assets/Scripts/Editor/CommonModifierSelfTests.cs
git commit -m "$(cat <<'EOF'
feat(modifier): 콤보 계열 — 대단원 방어도 4, 균열(첫 대상 방어도 -4/무방어 +15%)·독 바른 무기(대상마다 중독 1)·기본기 단련(공격력 +2)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 10: 생존 계열 — 완강한 신체·자연 치유·사냥

**Files:**
- Modify (전체 교체): `Assets/Scripts/Data/Modifiers/Common/SurvivalModifiers.cs`
- Modify: `Assets/Scripts/Data/Modifiers/ModifierRegistry.cs`
- Modify: `Assets/Scripts/Editor/CommonModifierSelfTests.cs`

**D7:** 완강한 신체는 `OnAcquired`(획득 1회)에서, 사냥은 처치 시 실제 `MaxHP`를 올린다. `MaxHP`는 세이브에 저장되고 복원은 `Add`(OnAcquired 미호출)라 이어하기에서 두 번 적용되지 않는다.

- [ ] **Step 1: 실패하는 테스트 작성**

`// [tests-run]` 위에:

```csharp
                TestMaxHpGainAndRestoreDoesNotReapply();
```

`// [tests-body]` 위에:

```csharp
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

```

- [ ] **Step 2: 실패 확인**

Run: 헤드리스 컴파일 → `RESULT FAIL` (`SturdyBodyModifier` 미정의).

- [ ] **Step 3: `SurvivalModifiers.cs` 전체 교체**

```csharp
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.Modifiers.Common
{
    /// <summary>[공방 일체] 공격 행동(기본/강화)을 마친 뒤 방어도 3.</summary>
    [System.Serializable]
    public class OffenseDefenseModifier : CommonModifier, IAttackActionFinishedListener
    {
        private const int Armor = 3;

        public override string ModifierName => "공방 일체";
        public override ModifierFamily Family => ModifierFamily.Survival;
        public override string Description
            => $"공격을 마친 뒤 방어도를 {Armor}만큼 얻습니다.";

        public void OnAttackActionFinished(Character character, AttackActionResult result)
        {
            if (character != owner || result.Targets.Count == 0) return;
            GrantArmor(Armor);
        }
    }

    /// <summary>[완강한 신체] 획득 시 1회 최대 체력·현재 체력 +10 (D7 — 세이브 복원에선 다시 적용하지 않음).</summary>
    [System.Serializable]
    public class SturdyBodyModifier : CommonModifier
    {
        private const int Amount = 10;

        public override string ModifierName => "완강한 신체";
        public override ModifierFamily Family => ModifierFamily.Survival;
        public override string Description
            => $"최대 체력과 현재 체력이 {Amount} 증가합니다.";

        public override void OnAcquired(Character acquiredBy)
        {
            if (acquiredBy == null || acquiredBy != owner) return;
            RaiseMaxHp(Amount);
        }
    }

    /// <summary>[자연 치유] 플레이어 턴 시작 시 체력 +2 (최대 체력 초과 없음). TurnStart의 OnPostAction 한 번만 — 방어도 초기화(OnPreAction) 뒤.</summary>
    [System.Serializable]
    public class NaturalHealingModifier : CommonModifier
    {
        private const int HealAmount = 2;

        public override string ModifierName => "자연 치유";
        public override ModifierFamily Family => ModifierFamily.Survival;
        public override string Description
            => $"플레이어 턴 시작 시 체력을 {HealAmount} 회복합니다.";

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null) return;
            if (context.Phase != EventPhase.TurnStart) return;
            if (trigger != CombatTrigger.OnPostAction) return;   // 한 트리거만 (방송 1회당 1번)
            if (context.SourceUnit != owner || context.IsSimulation) return;

            HealTarget(owner, HealAmount);
        }
    }

    /// <summary>
    /// [사냥] 공격 행동을 마친 뒤, 그 행동으로 처치한 몬스터 1명당 최대·현재 체력 +2 (영구).
    /// 처치 수는 AutoAttackSystem이 스코프 종료 직후에 센다 — 유물·디버프(중독) 피해 처치는 들어가지 않는다.
    /// </summary>
    [System.Serializable]
    public class HuntModifier : CommonModifier, IAttackActionFinishedListener
    {
        private const int PerKill = 2;

        public override string ModifierName => "사냥";
        public override ModifierFamily Family => ModifierFamily.Survival;
        public override string Description
            => $"공격으로 몬스터를 처치하면 처치 1명당 최대 체력과 현재 체력이 {PerKill} 증가합니다.";

        public void OnAttackActionFinished(Character character, AttackActionResult result)
        {
            if (character != owner || owner == null || !owner.IsAlive) return;
            if (result.Kills <= 0) return;
            RaiseMaxHp(PerKill * result.Kills);
        }
    }
}
```

- [ ] **Step 4: 레지스트리 등록**

`ModifierRegistry.cs`에서

```csharp
            () => new Common.OffenseDefenseModifier(),
```

다음에 추가:

```csharp
            () => new Common.SturdyBodyModifier(),
            () => new Common.NaturalHealingModifier(),
            () => new Common.HuntModifier(),
```

- [ ] **Step 5: 컴파일 PASS**

Run: 헤드리스 컴파일 → `RESULT PASS`. 자가 테스트: pending user run.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/Data/Modifiers/Common/SurvivalModifiers.cs \
  Assets/Scripts/Data/Modifiers/ModifierRegistry.cs \
  Assets/Scripts/Editor/CommonModifierSelfTests.cs
git commit -m "$(cat <<'EOF'
feat(modifier): 생존 계열 — 완강한 신체(획득 시 최대 체력 +10)·자연 치유(턴 시작 +2)·사냥(처치당 최대 체력 +2)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 11: 레지스트리 17종 검증 + 최종 감사

**Files:**
- Modify: `Assets/Scripts/Editor/CommonModifierSelfTests.cs`
- (확인만) `Assets/Scripts/Data/Modifiers/ModifierRegistry.cs`

- [ ] **Step 1: 테스트 작성**

`// [tests-run]` 위에:

```csharp
                TestRegistryHas17WithFamilies();
```

`// [tests-body]` 위에:

```csharp
        // ── 레지스트리 ────────────────────────────────────────

        private static void TestRegistryHas17WithFamilies()
        {
            var all = ModifierRegistry.CreateAll();
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

```

- [ ] **Step 2: 레지스트리 최종 형태 확인**

`ModifierRegistry.cs`의 `Factories`가 정확히 다음과 같아야 한다 (다르면 이 형태로 맞춘다):

```csharp
        private static readonly System.Func<CharacterModifier>[] Factories =
        {
            // 위치
            () => new Common.JointTacticsModifier(),
            () => new Common.LoneWolfModifier(),
            () => new Common.DuelModifier(),
            () => new Common.MobileArmorModifier(),
            () => new Common.RescueTeamModifier(),
            // 주사위
            () => new Common.LowRollGuardModifier(),
            () => new Common.ReserveChargeModifier(),
            () => new Common.PreparationModifier(),
            () => new Common.PaybackModifier(),
            // 콤보
            () => new Common.GrandFinaleModifier(),
            () => new Common.FractureModifier(),
            () => new Common.PoisonedWeaponModifier(),
            () => new Common.BasicTrainingModifier(),
            // 생존
            () => new Common.OffenseDefenseModifier(),
            () => new Common.SturdyBodyModifier(),
            () => new Common.NaturalHealingModifier(),
            () => new Common.HuntModifier(),
        };
```

- [ ] **Step 3: 최종 감사**

```bash
grep -rnE "ZoneCrossModifier|MomentumStrikeModifier|ExtremeResonanceModifier|SafetyNetModifier|ChainReactionModifier|OpeningBarrierModifier|UnyieldingModifier|IComboBreakListener|StackableCommonModifier|StackCount\(|IsPrimary\(" Assets/Scripts --include=*.cs | grep -v "CommonModifierSelfTests.cs"
grep -rn "Stats.Attack\b\|Stats?.Attack\b" Assets/Scripts --include=*.cs
grep -n "Charge" Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/EffectData.cs
```

Expected: 첫 grep 출력 없음(테스트 파일의 삭제 확인 문자열만 제외). 둘째 grep 출력 없음(캐릭터 피해 경로는 전부 `EffectiveAttack`; 몬스터 정보 패널의 `s?.Attack`은 이 패턴에 걸리지 않으며 그대로 둔다). 셋째 grep은 enum 마지막 값 `Charge` 한 줄.

- [ ] **Step 4: 컴파일 PASS**

Run: 헤드리스 컴파일 → `RESULT PASS`. 자가 테스트 3종(Common Modifier·Reward Flow·Combat Redesign): pending user run.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Editor/CommonModifierSelfTests.cs Assets/Scripts/Data/Modifiers/ModifierRegistry.cs
git commit -m "$(cat <<'EOF'
test(modifier): 레지스트리 17종·계열 분포 5/4/4/4·삭제 7종 부재 자가 테스트

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

## 사용자 확인 항목 (스펙 §5)

1. 에디터 자가 테스트: `DiceOrbit → Run Common Modifier Self-Tests` / `Run Reward Flow Self-Tests` / `Run Combat Redesign Self-Tests` 전부 통과. Unity가 새 `.cs.meta`(ChargeStatus·ModifierListeners·CommonModifierSelfTests)를 그대로 받아들이는지(재생성되면 커밋).
2. 플레이:
   - 합류/독립/듀얼 +15%가 피해 숫자·행동 예고에 반영
   - 예비 충전·사전 준비·앙갚음으로 충전이 쌓이고(최대 3, 아이콘 표시) 강화 공격 후 1 줄어듦, 다음 전투 시작 시 이전 충전이 없음(사전 준비 충전만 남음)
   - 기동 장갑·저속 방호·공방 일체·대단원 방어도가 다음 플레이어 턴 시작에 사라짐 (몬스터 방어도 동작은 이전과 같음)
   - 독 바른 무기 중독 1, 구조대 회복 2, 사냥 처치 시 최대 체력 +2, 완강한 신체 +10이 이어하기 후 두 번 적용되지 않음
   - 보상 카드: 보유한 종류는 "보유 중"으로 선택 불가, 초상 핍 ●/○

## 결정 사항 (스펙 해석)

- **충전 제거 위치:** `StartCombat`이 아니라 `BroadcastCombatStart` 맨 앞 + `EndCombat`. 실제 순서가 방송 → `StartCombat`이라 스펙 문구대로면 사전 준비 충전이 지워진다.
- **충전 보정 조건:** 스펙의 `ComboStage >= 0`에 `CurrentSource == Owner`를 더했다(다른 유닛의 행동 스코프 안 피해 배제).
- **D2 범위:** 캐릭터만 `SourceUnit == Owner`로 초기화. 몬스터 조건은 기존 그대로(MonsterStats엔 Owner 미주입) — 동작 불변. Owner 주입은 `Character.InitializeStats`.
- **중복 장착 방어:** `CanApplyTo` 외에 `ModifierManager.Add/Acquire`도 같은 타입을 거부(경고 로그). 옛 세이브의 중첩은 하나만 복원된다(세이브 검증 실패로 보지 않음 — 삭제된 7종만 D9).
- **처치 수:** 파이프라인 수정 없이 행동 중 타격을 시도한 대상의 스코프 종료 직후 생존 여부로 센다. 마무리 효과(`OnStageCompleted`)·중독 틱은 제외. 행동 안에서 다른 리액터(예: 적중 시 유물 추가 피해)가 낸 처치는 구분하지 않고 포함된다.
- **이동 완료 통지 위치:** `ResolveRoutine`의 턴당 1회 가드 뒤(이동 직후·공격 전). 출발 구역 = 이동 전 기록 경로의 첫 구역.
- **강화 조건 실패:** `plan.Kind == Basic && !EmpoweredIsNotCombo`(설계 위반 경로는 제외). 조건 성공 후 표적 없음(`ComboNoTarget`)은 실패가 아니다.
- **공격 행동 종료 통지:** 표적이 없어 행동이 열리지 않으면 통지하지 않는다(공방 일체·독 바른 무기 미발동). 공방 일체·대단원은 추가로 `Targets.Count > 0` 확인.
- **앙갚음 중복 방지:** 행동 스코프 ID 기준, 스코프 밖 지연 타격은 (같은 몬스터, 같은 프레임)을 한 행동으로 본다.
- **균열 예고:** 시뮬레이션은 "첫 대상"을 알 수 없어 현재 방어도 0인 모든 대상에 +15%로 근사(광역 예고에서 과대 표시 가능, 실제 피해는 정확). 예고의 방어도 감소(−4) 미반영 — 스펙 §4 범위 밖.
- **듀얼:** 대상 구역은 새 `CombatZoneManager.GetZoneOfMonster`(스폰 배정 기준). 구역 수가 2 이하라 양옆이 대상 자신이면 무시. 대상이 몬스터가 아니거나 구역 미상이면 보정 없음.
- **보상 카드 문구:** 보유 → "보유 중", 그 밖의 장착 불가 → "장착 불가", 아니면 "신규". 핍은 최대 1개.
- **수치 출처:** 시트값 — 합류/독립 10/12% → 15%, 저속 방호 3 → 4, 대단원 15% → 25%(+방어도 4), 앙갚음은 피해 보정 → 충전 1(계열 생존 → 주사위).
