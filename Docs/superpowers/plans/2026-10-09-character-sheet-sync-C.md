# 캐릭터 시트 동기화 C Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 현존 4직업(전사·도적·마법사·연금술사)의 고유 모디파이어 16종을 구현해 보상 풀에 넣는다. 공용 17종과 같은 장착 규칙(같은 종류 1개)에 "해당 직업 전용"(D1)이 더해지고, 보상 카드 상태 줄에 "○○ 전용"이 붙는다(D2). 레지스트리는 공용 17 + 고유 16 = 33종(D6).

**Architecture:** `JobModifier`(`CommonModifier` 상속)가 `CanApplyTo = base && 직업 일치`를 담당하고, 정적 `JobModifierQueries`가 "주인이 T를 장착했나"(`Has<T>`)·"살아 있는 파티원 중 T 보유자가 있나"(`AnyAliveHas<T>`)를 답한다. **수치 변경형**(견고한 방진·노련한 협공·추가 시약·여분의 비수·일제 타격·대지의 룬 방어도)은 패시브/스킬이 주인 모디파이어를 조회(pull)해 값 하나로 피해·행동 예고·설명을 맞춘다(D3). 도적 3단계는 `ComboActiveSkill`에 주인을 받는 `GetStageHits(source, stage)`/`GetStageMultiplier(source, stage)`/`GetStageDescription(source, stage)` 가상 오버로드를 추가하고 `AttackPlan`이 `Source`를 들고 다녀 실행·예고·설명이 같은 값을 쓴다. **상태 강화**(고전압·고순도 촉매)는 `StatusEnhancement`가 감전/촉매의 실효 %를 계산 시점마다 `max(Value, 40)`로 정하고, 상태 생성자도 같은 값으로 부여한다(D5). **동작형**은 B의 리스너(`IAttackActionFinishedListener`)·파이프라인 훅(`OnAttack`/`OnTurnEvent`)·`CommonModifier` 도우미를 쓴다(D4, DIM 미사용). 마법의 땅은 새 `TileAttributeType.MagicLand`(enum **끝**)와 `MagicLandTile`로 얹고 전투 종료 정리는 `CombatManager.EndCombat`(B의 충전 정리 옆)에서 한다.

**Tech Stack:** Unity 6000.3.8f1 · C# · Git LFS
**Spec:** Docs/superpowers/specs/2026-10-09-character-sheet-sync-C-design.md
**규칙:** 심사 잠금 — 로컬 커밋만, push 금지. 커밋 메시지 끝에 `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
---

## 파일 구조

| 파일 | 변경 | 책임 |
|---|---|---|
| `Assets/Scripts/Data/Modifiers/Job.meta` | 생성 | 새 폴더 메타 |
| `Assets/Scripts/Data/Modifiers/Job/JobModifier.cs` (+`.meta`) | 생성 | `JobIds`(SaveId·한국어 라벨), `JobModifier` 베이스(직업 제한·3단계/강화 공격 판정·"○○ 전용" 문구), `JobModifierQueries`(`IsJob`·`Has<T>`·`AnyAliveHas<T>`·같은 구역 다른 아군 수) |
| `Assets/Scripts/Data/Modifiers/Job/StatusEnhancement.cs` (+`.meta`) | 생성 | 감전·촉매 실효 % (강화 시 max(Value, 40)) |
| `Assets/Scripts/Data/Modifiers/Job/WarriorModifiers.cs` (+`.meta`) | 생성 | 견고한 방진·전우애·앙갚음(전사)·대지의 룬 |
| `Assets/Scripts/Data/Modifiers/Job/RogueModifiers.cs` (+`.meta`) | 생성 | 노련한 협공·여분의 비수·일제 타격·독침 |
| `Assets/Scripts/Data/Modifiers/Job/MageModifiers.cs` (+`.meta`) | 생성 | 고전압·회로 공명·마법의 땅·마나흡수 |
| `Assets/Scripts/Data/Modifiers/Job/AlchemistModifiers.cs` (+`.meta`) | 생성 | 추가 시약·고순도 촉매·시약 분배·약화 가스 |
| `Assets/Scripts/Data/Modifiers/ModifierRegistry.cs` | 수정 | 직업 고유 16종 팩토리 (직업별 구역 주석 아래) |
| `Assets/Scripts/Data/Modifiers/ModifierFamily.cs` | 수정 | 주석(33종 분포) |
| `Assets/Scripts/UI/Reward/RewardUI.cs` | 수정 | 카드 상태 줄에 "○○ 전용" |
| `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Effects/ActionStatuses.cs` | 수정 | `ShockStatus`·`CatalystStatus` 실효 %·부여 시 강화값 |
| `Assets/Scripts/Data/Character Preset/Mage/MageEnergyBallActive.cs` | 수정 | 감전 부여·설명에 실효 % |
| `Assets/Scripts/Data/Character Preset/Alchemist/ReagentPrepPassive.cs` | 수정 | `CatalystPercent` 실효 %, 시약 타일 수(추가 시약), `OwnerCharacter` |
| `Assets/Scripts/Data/Character Preset/Alchemist/ReagentTile.cs` | 수정 | 촉매 부여 도우미, 시약 분배 |
| `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Skills/ComboActiveSkill.cs` | 수정 | 주인을 받는 타격 수·배율·설명 가상 오버로드, 단계 피해·미리보기·설명이 사용 |
| `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/AttackPlan.cs` | 수정 | `Source` 필드, `Hits`가 주인 기준 |
| `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/AutoAttackSystem.cs` | 수정 | `PlanAttack`이 `Source` 기록 |
| `Assets/Scripts/Data/Character Preset/Rogue/RogueAmbushActive.cs` | 수정 | 3단계 타격 수·배율 계산(여분의 비수 → 일제 타격) |
| `Assets/Scripts/Data/Character Preset/Rogue/RogueFlankPassive.cs` | 수정 | 아군 1명당 % (노련한 협공) |
| `Assets/Scripts/Data/Character Preset/Warrior/WarriorGuardPassive.cs` | 수정 | 아군 1명당 감소 % (견고한 방진) |
| `Assets/Scripts/Data/Character Preset/Warrior/WarriorGreatswordActive.cs` | 수정 | 3단계 아군 방어도(대지의 룬 +3) |
| `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/TileAttribute.cs` | 수정 | `TileAttributeType.MagicLand`(끝), 표시 이름 |
| `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/MagicLandTile.cs` (+`.meta`) | 생성 | 마법의 땅 타일 속성 + 전체 제거 |
| `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/CombatManager.cs` | 수정 | 전투 종료 시 마법의 땅 제거 |
| `Assets/Resources/UI/TileAttributeVisualDatabase.asset` (LFS) | 수정 | `type: 19` 항목(예리함 아이콘을 보라색으로 임시 사용) |
| `Assets/Scripts/Editor/JobModifierSelfTests.cs` (+`.meta`) | 생성 | 직업 고유 모디파이어 자가 테스트 (메뉴 `DiceOrbit → Run Job Modifier Self-Tests`) |
| `Assets/Scripts/Editor/CommonModifierSelfTests.cs` | 수정 | 공용 17종 검증이 직업 고유를 제외하고 센다 |
| `Docs/superpowers/specs/2026-10-09-character-sheet-sync-C-design.md` | 수정 | 계열 합계 정정 + 구현 결과 섹션 |

## 검증 도구

**헤드리스 컴파일** (csproj가 낡아도 스크립트가 새 파일을 주입·복원한다. Git Bash):

```bash
PYTHONIOENCODING=utf-8 python "C:/Users/dfdfg/AppData/Local/Temp/claude/C--Users-dfdfg-UnityProjects-Dice-Orbit/0bedfca3-5b1e-4056-9b75-2a944d05bce1/scratchpad/compile_check.py"
```

마지막 줄이 `RESULT PASS`면 통과, `RESULT FAIL`이면 위에 찍힌 `: error ` 줄을 고친다. (계획 작성 시점 기준선: PASS.)

**에디터 자가 테스트:** 에이전트는 Unity 에디터에 접근할 수 없다. 테스트는 TDD로 작성하되 실행은 사용자 몫이다 — 메뉴 `DiceOrbit → Run Job Modifier Self-Tests`(이 계획에서 새로 만듦), `DiceOrbit → Run Common Modifier Self-Tests`, `DiceOrbit → Run Reward Flow Self-Tests`, `DiceOrbit → Run Combat Redesign Self-Tests`. 각 태스크의 "테스트 실행" 단계는 **"pending user run"** 으로 기록하고 진행한다. 에이전트 쪽 "실패 확인"은 헤드리스 컴파일 FAIL(아직 없는 타입·멤버 참조)로 대신한다.

**새 `.cs` 파일의 `.meta`** (기존 프로젝트의 2줄 형식):

```bash
python -c "import uuid,sys; open(sys.argv[1],'w',newline='\n').write('fileFormatVersion: 2\nguid: %s\n' % uuid.uuid4().hex)" "<파일>.cs.meta"
```

**새 폴더의 `.meta`** (`Assets/Scripts/Data/Modifiers/Common.meta`와 같은 형식):

```bash
python -c "import uuid,sys; open(sys.argv[1],'w',newline='\n').write('fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' % uuid.uuid4().hex)" "<폴더>.meta"
```

**인코딩:** 모든 `.cs`/`.md`는 UTF-8(BOM 없음), 줄 끝 LF(수정 대상 파일 전부 LF 확인함). Write/Edit 도구는 그대로 UTF-8로 쓴다.

**공통 규칙 (모든 태스크):**
- `TileAttributeType`·`EffectType`은 끝에만 추가한다(현재 `TileAttributeType` 마지막 = `Vitality` = 18 → `MagicLand` = 19).
- 모디파이어/패시브 훅은 베이스에 선언된 virtual의 `public override`로만 구현한다. 공격 행동 종료는 `IAttackActionFinishedListener`(B)로 받는다. 인터페이스 기본 메서드에 기대지 않는다.
- UnityEngine.Object(`Character`·`Monster`·`TileData`·`CharacterPreset`)는 `?.`/`??` 대신 명시적 `!= null`로 검사한다.
- 직업 판별은 `Stats.SourcePreset.SaveId`의 서수 비교(`"Warrior"`/`"Rogue"`/`"Mage"`/`"Alchemist"` — 각 프리셋 `.asset`의 `saveId` 값 확인함).
- 레지스트리 항목은 각 직업 구역 주석 아래, 스펙 §3.2 표 순서대로 넣는다.

---

### Task 1: `JobModifier` 베이스 + 조회 헬퍼 + "○○ 전용" + 자가 테스트 골격

**Files:**
- Create: `Assets/Scripts/Data/Modifiers/Job.meta`
- Create: `Assets/Scripts/Data/Modifiers/Job/JobModifier.cs` (+`.meta`)
- Create: `Assets/Scripts/Editor/JobModifierSelfTests.cs` (+`.meta`)
- Modify: `Assets/Scripts/Data/Modifiers/ModifierRegistry.cs` (헤더 주석 13~14행, `Factories` 끝 `HuntModifier` 줄 다음)
- Modify: `Assets/Scripts/UI/Reward/RewardUI.cs` (using 10행 다음, `RefreshUpgrade` 582~585행)
- Modify: `Assets/Scripts/Editor/CommonModifierSelfTests.cs` (`TestRegistryHas17WithFamilies` 첫 줄, 337행)

- [ ] **Step 1: 실패하는 테스트 작성 — 자가 테스트 파일 생성**

`Assets/Scripts/Editor/JobModifierSelfTests.cs` (이후 태스크는 `// [tests-run]` 마커 위에 호출 줄을, `// [tests-body]` 마커 위에 메서드를 추가한다):

```csharp
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
```

`.meta` 생성:

```bash
python -c "import uuid,sys; open(sys.argv[1],'w',newline='\n').write('fileFormatVersion: 2\nguid: %s\n' % uuid.uuid4().hex)" "Assets/Scripts/Editor/JobModifierSelfTests.cs.meta"
```

- [ ] **Step 2: 컴파일로 실패 확인**

Run: 헤드리스 컴파일. Expected: `RESULT FAIL` — `DiceOrbit.Data.Modifiers.Job`·`JobModifier`·`JobIds`·`JobModifierQueries` 미정의(CS0234/CS0246).

- [ ] **Step 3: 폴더 메타 + `JobModifier.cs` 생성**

```bash
python -c "import uuid,sys; open(sys.argv[1],'w',newline='\n').write('fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' % uuid.uuid4().hex)" "Assets/Scripts/Data/Modifiers/Job.meta"
```

`Assets/Scripts/Data/Modifiers/Job/JobModifier.cs`:

```csharp
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Core.Zones;
using DiceOrbit.Data.Modifiers.Common;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.Modifiers.Job
{
    /// <summary>직업 판별 ID(CharacterPreset.SaveId)와 표시 라벨 (2026-10-09 C, D1).</summary>
    public static class JobIds
    {
        public const string Warrior   = "Warrior";
        public const string Rogue     = "Rogue";
        public const string Mage      = "Mage";
        public const string Alchemist = "Alchemist";

        /// <summary>보상 카드 "○○ 전용"에 쓰는 한국어 직업 이름. 모르는 ID는 그대로 돌려준다.</summary>
        public static string Label(string jobId) => jobId switch
        {
            Warrior   => "전사",
            Rogue     => "도적",
            Mage      => "마법사",
            Alchemist => "연금술사",
            _         => jobId ?? string.Empty,
        };
    }

    /// <summary>
    /// 직업 고유 모디파이어 골격 (2026-10-09 C). 공용과 같은 장착 규칙(같은 종류 1개)에 "해당 직업 전용"이 더해진다.
    /// 직업 판별은 Stats.SourcePreset.SaveId 서수 비교(D1). 세이브 ID는 공용과 같이 구체 타입명(D6).
    /// </summary>
    [System.Serializable]
    public abstract class JobModifier : CommonModifier
    {
        /// <summary>이 모디파이어를 쓸 수 있는 직업의 SaveId (JobIds 상수).</summary>
        protected abstract string JobId { get; }

        /// <summary>표시용 직업 이름 (전사/도적/마법사/연금술사).</summary>
        public string JobLabel => JobIds.Label(JobId);

        public override bool CanApplyTo(Character character)
            => base.CanApplyTo(character) && JobModifierQueries.IsJob(character, JobId);

        /// <summary>owner의 강화 공격(행동 스코프 주체가 owner, ComboStage ≥ 0) 피해 계산인가.</summary>
        protected bool IsOwnEmpoweredCalculation(CombatTrigger trigger, AttackContext context)
            => IsOwnAttackCalculation(trigger, context)
               && AttackActionScope.IsActive
               && AttackActionScope.CurrentSource == owner
               && AttackActionScope.CurrentInfo.ComboStage >= 0;

        /// <summary>owner의 3단계 강화 공격 피해 계산인가 (스펙 §3.3 — B의 대단원과 같은 조건).</summary>
        protected bool IsOwnStage3Calculation(CombatTrigger trigger, AttackContext context)
            => IsOwnEmpoweredCalculation(trigger, context)
               && AttackActionScope.CurrentInfo.ComboStage == 2;

        /// <summary>보상 카드 상태 줄에 직업 고유면 " · ○○ 전용"을 붙인다 (D2, 순수 — 자가 테스트 대상).</summary>
        public static string AppendJobTag(string status, CharacterModifier modifier)
            => modifier is JobModifier job ? $"{status} · {job.JobLabel} 전용" : status;
    }

    /// <summary>직업 고유 모디파이어 조회 (D3 pull·D5 상태 강화가 쓴다). 저장 없이 질의 시점에 계산한다.</summary>
    public static class JobModifierQueries
    {
        /// <summary>캐릭터의 직업 SaveId가 jobId와 정확히 같은가 (서수 비교, 프리셋 없으면 false).</summary>
        public static bool IsJob(Character character, string jobId)
        {
            if (character == null || character.Stats == null || character.Stats.SourcePreset == null) return false;
            return string.Equals(character.Stats.SourcePreset.SaveId, jobId, System.StringComparison.Ordinal);
        }

        /// <summary>캐릭터가 장착한 T 인스턴스 (없으면 null).</summary>
        public static T Get<T>(Character character) where T : CharacterModifier
        {
            if (character == null || character.Stats == null || character.Stats.Modifiers == null) return null;
            var mods = character.Stats.Modifiers.Modifiers;
            if (mods == null) return null;

            for (int i = 0; i < mods.Count; i++)
                if (mods[i] is T found) return found;
            return null;
        }

        /// <summary>주인이 T를 장착했나.</summary>
        public static bool Has<T>(Character character) where T : CharacterModifier => Get<T>(character) != null;

        /// <summary>살아 있는 파티원 중 T 보유자가 있나 (PartyManager 기준, 없으면 false).</summary>
        public static bool AnyAliveHas<T>() where T : CharacterModifier
            => AnyAliveHas<T>(PartyManager.Instance != null ? PartyManager.Instance.Party : null);

        /// <summary>주어진 파티 중 살아 있는 T 보유자가 있나 (순수 — 자가 테스트 대상).</summary>
        public static bool AnyAliveHas<T>(IEnumerable<Character> party) where T : CharacterModifier
        {
            if (party == null) return false;
            foreach (var c in party)
                if (c != null && c.Stats != null && c.IsAlive && Has<T>(c)) return true;
            return false;
        }

        /// <summary>self와 같은 구역에 있는 '다른' 살아 있는 아군 수 (전우애). 구역 판정 불가면 0.</summary>
        public static int CountOtherAlliesInMyZone(Character self)
        {
            var zones = CombatZoneManager.Instance;
            var party = PartyManager.Instance;
            if (zones == null || party == null || self == null) return 0;

            int myZone = zones.GetZoneOf(self);
            if (myZone < 0) return 0;

            int count = 0;
            foreach (var ally in party.GetAliveCharacters())
            {
                if (ally == null || ally == self) continue;
                if (zones.GetZoneOf(ally) == myZone) count++;
            }
            return count;
        }
    }
}
```

`.meta` 생성:

```bash
python -c "import uuid,sys; open(sys.argv[1],'w',newline='\n').write('fileFormatVersion: 2\nguid: %s\n' % uuid.uuid4().hex)" "Assets/Scripts/Data/Modifiers/Job/JobModifier.cs.meta"
```

- [ ] **Step 4: 레지스트리 — 직업별 구역 주석**

`ModifierRegistry.cs`에서

```csharp
        // 2026-10-09 B — 시트 공용 17종 (위치 5 / 주사위 4 / 콤보 4 / 생존 4). 같은 종류는 캐릭터당 1개(CanApplyTo).
        // 캐릭터 전용 시그니처는 전부 폐기됐다. 삭제된 타입명을 담은 옛 세이브는 Validate에서 실패 → 새 게임 폴백(D9).
```

를

```csharp
        // 2026-10-09 B — 시트 공용 17종 (위치 5 / 주사위 4 / 콤보 4 / 생존 4). 같은 종류는 캐릭터당 1개(CanApplyTo).
        // 2026-10-09 C — 직업 고유 16종(전사·도적·마법사·연금술사 각 4). CanApplyTo가 직업을 확인한다(JobModifier) → 공용 + 고유 = 33종.
        // 캐릭터 전용 시그니처는 전부 폐기됐다. 삭제된 타입명을 담은 옛 세이브는 Validate에서 실패 → 새 게임 폴백(D9).
```

로, 그리고

```csharp
            () => new Common.HuntModifier(),
        };
```

를

```csharp
            () => new Common.HuntModifier(),

            // ── 직업 고유 (2026-10-09 C) — 스펙 §3.2 표 순서 ──
            // 전사
            // 도적
            // 마법사
            // 연금술사
        };
```

로 바꾼다.

- [ ] **Step 5: 보상 카드 상태 줄 "○○ 전용"**

`RewardUI.cs` using 블록에서 `using DiceOrbit.Data.Modifiers.Common;` 다음 줄에 추가:

```csharp
using DiceOrbit.Data.Modifiers.Job;
```

`RefreshUpgrade`에서

```csharp
                string status = owned ? $"{who} · 보유 중"
                              : !can ? $"{who} · 장착 불가"
                              : $"{who} · 신규";
                _cards[i].SetStatus(status, can);
```

를

```csharp
                string status = owned ? $"{who} · 보유 중"
                              : !can ? $"{who} · 장착 불가"
                              : $"{who} · 신규";
                status = JobModifier.AppendJobTag(status, offer);   // 직업 고유면 " · ○○ 전용" (2026-10-09 C, D2)
                _cards[i].SetStatus(status, can);
```

로 바꾼다.

- [ ] **Step 6: 공용 17종 테스트가 직업 고유를 빼고 세게**

`CommonModifierSelfTests.cs`의 `TestRegistryHas17WithFamilies`에서

```csharp
            var all = ModifierRegistry.CreateAll();
            Check(all.Count == 17, $"공용 모디파이어 17종 (실제 {all.Count})");
```

를

```csharp
            // 직업 고유(2026-10-09 C)는 JobModifierSelfTests가 검증한다 — 여기선 공용 17종만 센다.
            var all = ModifierRegistry.CreateAll().FindAll(m => !(m is DiceOrbit.Data.Modifiers.Job.JobModifier));
            Check(all.Count == 17, $"공용 모디파이어 17종 (실제 {all.Count})");
```

로 바꾼다.

- [ ] **Step 7: 컴파일 PASS 확인**

Run: 헤드리스 컴파일. Expected: `RESULT PASS`.

- [ ] **Step 8: 테스트 실행** — `DiceOrbit → Run Job Modifier Self-Tests`, `Run Common Modifier Self-Tests` 전부 통과. **pending user run.**

- [ ] **Step 9: 커밋**

```bash
git add Assets/Scripts/Data/Modifiers/Job.meta \
  Assets/Scripts/Data/Modifiers/Job/JobModifier.cs Assets/Scripts/Data/Modifiers/Job/JobModifier.cs.meta \
  Assets/Scripts/Editor/JobModifierSelfTests.cs Assets/Scripts/Editor/JobModifierSelfTests.cs.meta \
  Assets/Scripts/Data/Modifiers/ModifierRegistry.cs Assets/Scripts/UI/Reward/RewardUI.cs \
  Assets/Scripts/Editor/CommonModifierSelfTests.cs
git commit -m "$(cat <<'EOF'
feat(modifier): 직업 고유 모디파이어 골격 — JobModifier(직업 제한)·JobModifierQueries·보상 카드 "○○ 전용"

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---
### Task 2: 상태 강화 공통 규칙(D5) + 고전압 + 고순도 촉매

**Files:**
- Create: `Assets/Scripts/Data/Modifiers/Job/StatusEnhancement.cs` (+`.meta`)
- Create: `Assets/Scripts/Data/Modifiers/Job/MageModifiers.cs` (+`.meta`)
- Create: `Assets/Scripts/Data/Modifiers/Job/AlchemistModifiers.cs` (+`.meta`)
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Effects/ActionStatuses.cs` (using 3행 다음, `ShockStatus` 73~83행, `CatalystStatus` 85~102행)
- Modify: `Assets/Scripts/Data/Character Preset/Mage/MageEnergyBallActive.cs` (using, `OnStageHitLanded`, `GetStageDescription`)
- Modify: `Assets/Scripts/Data/Character Preset/Alchemist/ReagentPrepPassive.cs` (using, `CatalystPercent` 25~26행)
- Modify: `Assets/Scripts/Data/Modifiers/ModifierRegistry.cs`
- Modify: `Assets/Scripts/Editor/JobModifierSelfTests.cs`

**이 태스크의 이후 추가 규칙:** `MageModifiers.cs`·`AlchemistModifiers.cs`(그리고 이후 태스크에서 만드는 `WarriorModifiers.cs`·`RogueModifiers.cs`)에 클래스를 "파일 끝에 추가"할 때는, 파일 마지막의 `    }` + 개행 + `}`(마지막 클래스의 닫는 괄호와 네임스페이스의 닫는 괄호 — 0열 `}`는 파일에 하나뿐이라 유일하다)를 찾아 그 두 줄 사이에 빈 줄 + 새 클래스를 넣는다.

- [ ] **Step 1: 실패하는 테스트 작성**

`JobModifierSelfTests.cs`의 `// [tests-run]` 위에:

```csharp
                TestStatusEnhancementPercent();
                TestEnhancerAliveOrDead();
                TestShockWithoutEnhancerUnchanged();
```

`// [tests-body]` 위에:

```csharp
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
```

- [ ] **Step 2: 컴파일로 실패 확인**

Run: 헤드리스 컴파일. Expected: `RESULT FAIL` — `StatusEnhancement`·`HighVoltageModifier`·`HighPurityCatalystModifier` 미정의.

- [ ] **Step 3: `StatusEnhancement.cs` 생성**

`Assets/Scripts/Data/Modifiers/Job/StatusEnhancement.cs`:

```csharp
using System.Collections.Generic;
using DiceOrbit.Core;
using UnityEngine;

namespace DiceOrbit.Data.Modifiers.Job
{
    /// <summary>
    /// 상태 강화 공통 규칙 (2026-10-09 C, D5 — 시트 「버프·디버프」 "효과 강화").
    /// 감전·촉매의 실효 %를 계산할 때마다 "살아 있는 파티원 중 강화 모디파이어 보유자가 있나"를 조회해
    /// 있으면 max(Value, 40)으로 쓴다. 부여 출처와 무관하고, 보유자가 쓰러지면 조회에서 빠져 자동으로 해제된다.
    /// 상태 생성자도 같은 값으로 부여해 아이콘 수치를 맞춘다 — 재부여는 큰 값 유지(A의 D6)라 안전하다.
    /// </summary>
    public static class StatusEnhancement
    {
        /// <summary>강화된 감전·촉매 수치(%).</summary>
        public const int EnhancedPercent = 40;

        /// <summary>지금 파티(PartyManager) 기준으로 이 상태가 강화 중인가.</summary>
        public static bool IsEnhanced(EffectType type)
            => IsEnhanced(type, PartyManager.Instance != null ? PartyManager.Instance.Party : null);

        /// <summary>주어진 파티 기준으로 이 상태가 강화 중인가 (순수 — 자가 테스트 대상).</summary>
        public static bool IsEnhanced(EffectType type, IEnumerable<Character> party) => type switch
        {
            EffectType.Shock    => JobModifierQueries.AnyAliveHas<HighVoltageModifier>(party),
            EffectType.Catalyst => JobModifierQueries.AnyAliveHas<HighPurityCatalystModifier>(party),
            _ => false,
        };

        /// <summary>지금 파티 기준 실효 %.</summary>
        public static int EffectivePercent(EffectType type, int value) => EffectivePercent(value, IsEnhanced(type));

        /// <summary>주어진 파티 기준 실효 % (순수).</summary>
        public static int EffectivePercent(EffectType type, int value, IEnumerable<Character> party)
            => EffectivePercent(value, IsEnhanced(type, party));

        /// <summary>강화 중이면 max(value, 40), 아니면 value (순수).</summary>
        public static int EffectivePercent(int value, bool enhanced) => enhanced ? Mathf.Max(value, EnhancedPercent) : value;
    }
}
```

`.meta`:

```bash
python -c "import uuid,sys; open(sys.argv[1],'w',newline='\n').write('fileFormatVersion: 2\nguid: %s\n' % uuid.uuid4().hex)" "Assets/Scripts/Data/Modifiers/Job/StatusEnhancement.cs.meta"
```

- [ ] **Step 4: `MageModifiers.cs` 생성 (고전압)**

`Assets/Scripts/Data/Modifiers/Job/MageModifiers.cs` (using은 이후 태스크의 회로 공명·마법의 땅·마나흡수까지 미리 포함):

```csharp
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Core.Zones;
using DiceOrbit.Data.Modifiers.Common;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using UnityEngine;

namespace DiceOrbit.Data.Modifiers.Job
{
    /// <summary>
    /// [고전압] 감전 25 → 40% (출처 무관). 효과는 StatusEnhancement가 감전 계산·부여 때 이 모디파이어를 조회해 낸다(D5) —
    /// 이 클래스 자체엔 훅이 없다. 보유 마법사가 쓰러지면 조회에서 빠져 원래대로 돌아간다.
    /// </summary>
    [System.Serializable]
    public class HighVoltageModifier : JobModifier
    {
        protected override string JobId => JobIds.Mage;
        public override string ModifierName => "고전압";
        public override ModifierFamily Family => ModifierFamily.Combo;
        public override string Description
            => $"감전으로 늘어나는 받는 피해가 25%에서 {StatusEnhancement.EnhancedPercent}%로 강화됩니다. 누가 부여한 감전이든 적용되며, 이 마법사가 쓰러지면 원래대로 돌아갑니다.";
    }
}
```

`.meta`:

```bash
python -c "import uuid,sys; open(sys.argv[1],'w',newline='\n').write('fileFormatVersion: 2\nguid: %s\n' % uuid.uuid4().hex)" "Assets/Scripts/Data/Modifiers/Job/MageModifiers.cs.meta"
```

- [ ] **Step 5: `AlchemistModifiers.cs` 생성 (고순도 촉매)**

`Assets/Scripts/Data/Modifiers/Job/AlchemistModifiers.cs` (using은 이후 태스크의 추가 시약·시약 분배·약화 가스까지 미리 포함):

```csharp
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Modifiers.Common;
using DiceOrbit.Systems.Effects;
using UnityEngine;

namespace DiceOrbit.Data.Modifiers.Job
{
    /// <summary>
    /// [고순도 촉매] 촉매 25 → 40% (출처 무관). StatusEnhancement가 촉매 계산·부여 때 조회한다(D5).
    /// ReagentPrepPassive.CatalystPercent도 같은 값을 돌려줘 시약 타일 부여·행동 예고·설명이 일치한다.
    /// </summary>
    [System.Serializable]
    public class HighPurityCatalystModifier : JobModifier
    {
        protected override string JobId => JobIds.Alchemist;
        public override string ModifierName => "고순도 촉매";
        public override ModifierFamily Family => ModifierFamily.Combo;
        public override string Description
            => $"촉매로 늘어나는 다음 자동공격 피해가 25%에서 {StatusEnhancement.EnhancedPercent}%로 강화됩니다. 누가 얻은 촉매든 적용되며, 이 연금술사가 쓰러지면 원래대로 돌아갑니다.";
    }
}
```

`.meta`:

```bash
python -c "import uuid,sys; open(sys.argv[1],'w',newline='\n').write('fileFormatVersion: 2\nguid: %s\n' % uuid.uuid4().hex)" "Assets/Scripts/Data/Modifiers/Job/AlchemistModifiers.cs.meta"
```

- [ ] **Step 6: 감전·촉매가 실효 %를 쓰게**

`ActionStatuses.cs` using 블록 끝(`using DiceOrbit.Data;` 다음)에 추가:

```csharp
using DiceOrbit.Data.Modifiers.Job;
```

`ShockStatus` 전체

```csharp
    /// <summary>감전 — 이번 라운드 동안 이 유닛이 '받는' 공격 피해 +Value% (기본 25). 디버프 피해엔 적용 안 됨.</summary>
    public class ShockStatus : RoundStatus
    {
        public ShockStatus(int value) : base(EffectType.Shock, value) { }

        protected override bool AppliesTo(AttackContext context)
            => context.Target == Owner && context.SourceUnit != null;

        protected override void Modify(AttackContext context)
            => context.OutputValue *= 1f + Value / 100f;
    }
```

를

```csharp
    /// <summary>
    /// 감전 — 이번 라운드 동안 이 유닛이 '받는' 공격 피해 +Value% (기본 25). 디버프 피해엔 적용 안 됨.
    /// 실효 %는 계산할 때마다 StatusEnhancement가 정한다 — 고전압 보유 마법사가 살아 있으면 max(Value, 40) (2026-10-09 C, D5).
    /// </summary>
    public class ShockStatus : RoundStatus
    {
        // 부여 시점에도 강화 중이면 40으로 부여해 아이콘 수치를 맞춘다 (D5)
        public ShockStatus(int value) : base(EffectType.Shock, StatusEnhancement.EffectivePercent(EffectType.Shock, value)) { }

        protected override bool AppliesTo(AttackContext context)
            => context.Target == Owner && context.SourceUnit != null;

        protected override void Modify(AttackContext context)
            => context.OutputValue *= 1f + StatusEnhancement.EffectivePercent(Type, Value) / 100f;
    }
```

로, `CatalystStatus`에서

```csharp
        public CatalystStatus(int value) : base(EffectType.Catalyst, value) { }
```

를

```csharp
        // 부여 시점에도 강화 중이면 40으로 부여 (고순도 촉매, 2026-10-09 C D5)
        public CatalystStatus(int value) : base(EffectType.Catalyst, StatusEnhancement.EffectivePercent(EffectType.Catalyst, value)) { }
```

로,

```csharp
        protected override bool AppliesTo(AttackContext context)
            => AttackActionScope.IsActive
               && AttackActionScope.CurrentSource == Owner
               && context.SourceUnit == Owner
               && Owner is Character;

        protected override void Modify(AttackContext context)
            => context.OutputValue *= 1f + Value / 100f;
    }
```

를

```csharp
        protected override bool AppliesTo(AttackContext context)
            => AttackActionScope.IsActive
               && AttackActionScope.CurrentSource == Owner
               && context.SourceUnit == Owner
               && Owner is Character;

        // 실효 % — 고순도 촉매 보유 연금술사가 살아 있으면 max(Value, 40) (D5)
        protected override void Modify(AttackContext context)
            => context.OutputValue *= 1f + StatusEnhancement.EffectivePercent(Type, Value) / 100f;
    }
```

로 바꾼다.

- [ ] **Step 7: 마법사 감전 부여·설명 = 실효 %**

`MageEnergyBallActive.cs` using 블록에서 `using DiceOrbit.Data.Passives;` 다음 줄에 추가:

```csharp
using DiceOrbit.Data.Modifiers.Job;
```

```csharp
        public override string GetStageName(int stage)
            => stage switch { 0 => "비전 화살", 1 => "연쇄 번개", _ => "궤도 붕괴" };
```

바로 위에 추가:

```csharp
        /// <summary>지금 부여할 감전 수치 — 고전압 보유 마법사가 살아 있으면 40 (2026-10-09 C, D5). 부여 버블·설명이 같은 값을 쓴다.</summary>
        private int ShockPercentNow => StatusEnhancement.EffectivePercent(EffectType.Shock, shockPercent);

```

`OnStageHitLanded`의

```csharp
            if (stage >= 1 && shockPercent > 0)
                GrantStatus(target, EffectType.Shock, shockPercent);
```

를

```csharp
            if (stage >= 1 && shockPercent > 0)
                GrantStatus(target, EffectType.Shock, ShockPercentNow);
```

로, `GetStageDescription`의 두 곳 `감전 {shockPercent}%를 부여합니다.`를 각각 `감전 {ShockPercentNow}%를 부여합니다.`로 바꾼다 (1단계·스펙 문구는 그대로).

- [ ] **Step 8: 시약 촉매 수치 = 실효 %**

`ReagentPrepPassive.cs` using 블록 끝(`using DiceOrbit.Data.Tile;` 다음)에 추가:

```csharp
using DiceOrbit.Data.Modifiers.Job;
```

```csharp
        /// <summary>시약 타일이 밟힐 때 부여할 촉매 수치(%). ReagentTile이 읽는다.</summary>
        public int CatalystPercent => Mathf.Max(0, catalystPercent);
```

를

```csharp
        /// <summary>
        /// 시약 타일이 밟힐 때 부여할 촉매 수치(%). ReagentTile이 읽는다(부여·행동 예고·설명).
        /// 고순도 촉매 보유 연금술사가 살아 있으면 40 (2026-10-09 C, D5).
        /// </summary>
        public int CatalystPercent => StatusEnhancement.EffectivePercent(EffectType.Catalyst, Mathf.Max(0, catalystPercent));
```

로 바꾼다.

- [ ] **Step 9: 레지스트리 등록**

`ModifierRegistry.cs`에서

```csharp
            // 마법사
```

다음 줄에 `            () => new Job.HighVoltageModifier(),`를,

```csharp
            // 연금술사
```

다음 줄에 `            () => new Job.HighPurityCatalystModifier(),`를 추가한다.

- [ ] **Step 10: 컴파일 PASS 확인** — Run: 헤드리스 컴파일. Expected: `RESULT PASS`.

- [ ] **Step 11: 테스트 실행** — `Run Job Modifier Self-Tests`, `Run Combat Redesign Self-Tests`(감전·촉매 회귀) 통과. **pending user run.**

- [ ] **Step 12: 커밋**

```bash
git add Assets/Scripts/Data/Modifiers/Job/StatusEnhancement.cs Assets/Scripts/Data/Modifiers/Job/StatusEnhancement.cs.meta \
  Assets/Scripts/Data/Modifiers/Job/MageModifiers.cs Assets/Scripts/Data/Modifiers/Job/MageModifiers.cs.meta \
  Assets/Scripts/Data/Modifiers/Job/AlchemistModifiers.cs Assets/Scripts/Data/Modifiers/Job/AlchemistModifiers.cs.meta \
  Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Effects/ActionStatuses.cs \
  "Assets/Scripts/Data/Character Preset/Mage/MageEnergyBallActive.cs" \
  "Assets/Scripts/Data/Character Preset/Alchemist/ReagentPrepPassive.cs" \
  Assets/Scripts/Data/Modifiers/ModifierRegistry.cs Assets/Scripts/Editor/JobModifierSelfTests.cs
git commit -m "$(cat <<'EOF'
feat(modifier): 상태 강화 공통 규칙(감전·촉매 실효 40%) + 고전압·고순도 촉매

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---
### Task 3: 도적 3단계 타격 계산 배관(주인 전달) + 여분의 비수 + 일제 타격

**Files:**
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Skills/ComboActiveSkill.cs` (27행 `GetStageHits` 다음, 39행 `GetStageDescription` 다음, 51~52행, 58~62행, 105~110행)
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/AttackPlan.cs` (42~43행)
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/AutoAttackSystem.cs` (`PlanAttack` 106행)
- Modify: `Assets/Scripts/Data/Character Preset/Rogue/RogueAmbushActive.cs`
- Create: `Assets/Scripts/Data/Modifiers/Job/RogueModifiers.cs` (+`.meta`)
- Modify: `Assets/Scripts/Data/Modifiers/ModifierRegistry.cs`
- Modify: `Assets/Scripts/Editor/JobModifierSelfTests.cs`

**설계:** `GetStageHits(stage)`는 주인을 모른다. 기존 시그니처를 바꾸지 않고(전사·마법사·연금술사·테스트 호출부 무수정) 주인을 받는 **가상 오버로드** `GetStageHits(Character source, int stage)`·`GetStageMultiplier(Character source, int stage)`·`GetStageDescription(Character source, int stage)`를 추가해 기본은 주인 없는 버전에 위임한다. 실행 경로는 `AttackPlan.Hits`(새 `Source` 필드) → 실행 루프·`ActionForecaster`(둘 다 `plan.Hits`)와 `CalculateStageDamage`(→ `ApplyStageHit`·예고·튜토리얼) → 배율 오버로드, 미리보기 `BuildPreview`·정보 패널 `GetDynamicDescription(source)`가 같은 값을 쓴다. `RogueAmbushActive`만 3단계에서 오버라이드한다.

- [ ] **Step 1: 실패하는 테스트 작성**

`JobModifierSelfTests.cs`의 `// [tests-run]` 위에:

```csharp
                TestRogueStage3Resolution();
                TestRogueStage3UsesOwner();
```

`// [tests-body]` 위에:

```csharp
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
```

- [ ] **Step 2: 컴파일로 실패 확인**

Run: 헤드리스 컴파일. Expected: `RESULT FAIL` — `RogueAmbushActive.ResolveStage3`·`SpareDaggerModifier`·`VolleyStrikeModifier`·`AttackPlan.Source`·`GetStageHits(Character, int)` 미정의.

- [ ] **Step 3: `ComboActiveSkill` — 주인 기준 가상 오버로드**

```csharp
        /// <summary>대상 '하나당' 타격 횟수 (도적 다단 공격용). 기본 1.</summary>
        public virtual int GetStageHits(int stage) => 1;
```

를

```csharp
        /// <summary>대상 '하나당' 타격 횟수 (도적 다단 공격용). 기본 1.</summary>
        public virtual int GetStageHits(int stage) => 1;

        /// <summary>
        /// 주인(슬롯 소유자) 기준 타격 수 (2026-10-09 C — 여분의 비수·일제 타격). 실행(AttackPlan.Hits)·행동 예고·미리보기가 쓴다.
        /// 기본은 주인과 무관한 GetStageHits(stage). source가 null이어도 동작해야 한다.
        /// </summary>
        public virtual int GetStageHits(Character source, int stage) => GetStageHits(stage);

        /// <summary>주인 기준 타격 1회 배율 (일제 타격). 단계 피해(CalculateStageDamage)가 쓴다. 기본은 GetStageMultiplier(stage).</summary>
        public virtual float GetStageMultiplier(Character source, int stage) => GetStageMultiplier(stage);
```

로,

```csharp
        /// <summary>단계 1개의 설명 한 줄. "[이름] 무엇을 얼마나" 형식 — 목록·현재 단계 표시가 공유한다.</summary>
        public abstract string GetStageDescription(int stage);
```

를

```csharp
        /// <summary>단계 1개의 설명 한 줄. "[이름] 무엇을 얼마나" 형식 — 목록·현재 단계 표시가 공유한다.</summary>
        public abstract string GetStageDescription(int stage);

        /// <summary>주인 기준 단계 설명 (전투 중 정보 패널 — 모디파이어로 바뀐 수치 반영). 기본은 GetStageDescription(stage).</summary>
        public virtual string GetStageDescription(Character source, int stage) => GetStageDescription(stage);
```

로,

```csharp
        public override string GetDynamicDescription(Character source)
            => GetStageDescription(PeekStage(source));
```

를

```csharp
        public override string GetDynamicDescription(Character source)
            => GetStageDescription(source, PeekStage(source));
```

로,

```csharp
            int attack = source != null && source.Stats != null ? source.Stats.EffectiveAttack : 0;
            return Mathf.Max(1, Mathf.RoundToInt(attack * Mathf.Max(0.05f, GetStageMultiplier(stage))));
```

를

```csharp
            int attack = source != null && source.Stats != null ? source.Stats.EffectiveAttack : 0;
            return Mathf.Max(1, Mathf.RoundToInt(attack * Mathf.Max(0.05f, GetStageMultiplier(source, stage))));
```

로,

```csharp
            int stage = PeekStage(source);
            return $"{stage + 1}단계 [{GetStageName(stage)}] 예상 피해: {CalculateStageDamage(source, stage)}"
                   + (GetStageHits(stage) > 1 ? $" x{GetStageHits(stage)}타" : string.Empty);
```

를

```csharp
            int stage = PeekStage(source);
            int hits = GetStageHits(source, stage);
            return $"{stage + 1}단계 [{GetStageName(stage)}] 예상 피해: {CalculateStageDamage(source, stage)}"
                   + (hits > 1 ? $" x{hits}타" : string.Empty);
```

로 바꾼다.

- [ ] **Step 4: `AttackPlan.Source` + `Hits`가 주인 기준**

`AttackPlan.cs`에서

```csharp
        /// <summary>대상 하나당 타격 횟수.</summary>
        public int Hits => Kind == AttackPlanKind.Combo && ComboSkill != null ? Mathf.Max(1, ComboSkill.GetStageHits(Stage)) : 1;
```

를

```csharp
        /// <summary>계획의 주체(슬롯 소유자) — 주인 기준 타격 수(여분의 비수·일제 타격, 2026-10-09 C)에 쓴다. PlanAttack이 채운다.</summary>
        public Character Source;

        /// <summary>대상 하나당 타격 횟수 — 실행 루프와 행동 예고가 같이 읽는다.</summary>
        public int Hits => Kind == AttackPlanKind.Combo && ComboSkill != null ? Mathf.Max(1, ComboSkill.GetStageHits(Source, Stage)) : 1;
```

로 바꾼다.

`AutoAttackSystem.cs`의 `PlanAttack`에서

```csharp
            var plan = new AttackPlan();
```

를

```csharp
            var plan = new AttackPlan { Source = character };
```

로 바꾼다.

- [ ] **Step 5: `RogueModifiers.cs` 생성 (여분의 비수·일제 타격)**

`Assets/Scripts/Data/Modifiers/Job/RogueModifiers.cs` (using은 이후 태스크의 노련한 협공·독침까지 미리 포함):

```csharp
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Modifiers.Common;
using DiceOrbit.Systems.Effects;
using UnityEngine;

namespace DiceOrbit.Data.Modifiers.Job
{
    /// <summary>
    /// [여분의 비수] 3단계 [죽음의 촉매] 타격 4 → 5회. RogueAmbushActive의 3단계 계산이 조회한다(D3) —
    /// 다른 타격 증가보다 먼저, 일제 타격 전환보다 먼저 적용된다(스펙 §3.4).
    /// </summary>
    [System.Serializable]
    public class SpareDaggerModifier : JobModifier
    {
        public const int ExtraHits = 1;

        protected override string JobId => JobIds.Rogue;
        public override string ModifierName => "여분의 비수";
        public override ModifierFamily Family => ModifierFamily.Combo;
        public override string Description
            => $"3단계 강화 공격 [죽음의 촉매]의 타격 횟수가 {ExtraHits}회 늘어납니다.";
    }

    /// <summary>
    /// [일제 타격] 3단계 타격을 1회로 바꾸고, 줄어든 타격 1회당 피해 +110% — 최종 타격 수 n → 1회, 배율 × (1 + 1.1 × (n − 1)).
    /// 4→1 = ×4.3, 5→1(여분의 비수) = ×5.4. RogueAmbushActive의 3단계 계산이 조회한다(D3).
    /// </summary>
    [System.Serializable]
    public class VolleyStrikeModifier : JobModifier
    {
        /// <summary>줄어든 타격 1회당 배율 가산 (+110%).</summary>
        public const float BonusPerRemovedHit = 1.1f;

        protected override string JobId => JobIds.Rogue;
        public override string ModifierName => "일제 타격";
        public override ModifierFamily Family => ModifierFamily.Combo;
        public override string Description
            => $"3단계 강화 공격 [죽음의 촉매]가 한 번의 타격으로 바뀌고, 줄어든 타격 1회마다 그 타격의 피해가 {BonusPerRemovedHit * 100f:0.#}% 증가합니다.";
    }
}
```

`.meta`:

```bash
python -c "import uuid,sys; open(sys.argv[1],'w',newline='\n').write('fileFormatVersion: 2\nguid: %s\n' % uuid.uuid4().hex)" "Assets/Scripts/Data/Modifiers/Job/RogueModifiers.cs.meta"
```

- [ ] **Step 6: `RogueAmbushActive` — 3단계 계산 한 곳**

using 블록에서 `using DiceOrbit.Data.Skills;` 다음 줄에 추가:

```csharp
using DiceOrbit.Data.Modifiers.Job;
```

```csharp
        public override int GetStageHits(int stage)
            => stage switch { 0 => Mathf.Max(1, stage1Hits), 1 => Mathf.Max(1, stage2Hits), _ => Mathf.Max(1, stage3Hits) };
```

를

```csharp
        public override int GetStageHits(int stage)
            => stage switch { 0 => Mathf.Max(1, stage1Hits), 1 => Mathf.Max(1, stage2Hits), _ => Mathf.Max(1, stage3Hits) };

        // ── 3단계 주인 기준 (2026-10-09 C — 여분의 비수·일제 타격, 스펙 §3.4) ──
        // 실행(AttackPlan.Hits)·피해(CalculateStageDamage)·행동 예고·미리보기·정보 패널이 모두 아래 한 계산을 쓴다.

        public override int GetStageHits(Character source, int stage)
        {
            if (stage != StageCount - 1) return GetStageHits(stage);
            ResolveStage3For(source, out int hits, out _);
            return hits;
        }

        public override float GetStageMultiplier(Character source, int stage)
        {
            if (stage != StageCount - 1) return GetStageMultiplier(stage);
            ResolveStage3For(source, out _, out float scale);
            return GetStageMultiplier(stage) * scale;
        }

        public override string GetStageDescription(Character source, int stage)
        {
            if (stage != StageCount - 1) return GetStageDescription(stage);
            ResolveStage3For(source, out int hits, out float scale);
            return FormatStage3Description(stage3Multiplier * scale, hits);
        }

        /// <summary>
        /// 3단계 타격 수·배율 (순수 — 자가 테스트 대상). 순서: 기본 → 여분의 비수(+1) → (다른 타격 증가 자리) → 일제 타격 전환.
        /// 일제 타격이면 최종 타격 수 n을 1회로 바꾸고 배율 × (1 + 1.1 × (n − 1)).
        /// </summary>
        public static void ResolveStage3(int baseHits, bool spareDagger, bool volley, out int hits, out float multiplierScale)
        {
            int n = Mathf.Max(1, baseHits);
            if (spareDagger) n += SpareDaggerModifier.ExtraHits;
            // 다른 타격 증가가 생기면 여기에 — 일제 타격 전환보다 먼저.

            if (volley)
            {
                hits = 1;
                multiplierScale = 1f + VolleyStrikeModifier.BonusPerRemovedHit * (n - 1);
            }
            else
            {
                hits = n;
                multiplierScale = 1f;
            }
        }

        private void ResolveStage3For(Character source, out int hits, out float multiplierScale)
            => ResolveStage3(stage3Hits,
                             JobModifierQueries.Has<SpareDaggerModifier>(source),
                             JobModifierQueries.Has<VolleyStrikeModifier>(source),
                             out hits, out multiplierScale);

        private static string FormatStage3Description(float multiplier, int hits)
            => $"[죽음의 촉매] 공격력의 {multiplier * 100f:0.#}%만큼 {hits}회 피해를 준 뒤 대상의 중독을 2배로 늘립니다.";
```

로 바꾸고, `GetStageDescription(int stage)`의 마지막 줄

```csharp
                _ => $"[죽음의 촉매] 공격력의 {stage3Multiplier * 100f:0.#}%만큼 {Mathf.Max(1, stage3Hits)}회 피해를 준 뒤 대상의 중독을 2배로 늘립니다.",
```

를

```csharp
                _ => FormatStage3Description(stage3Multiplier, Mathf.Max(1, stage3Hits)),
```

로 바꾼다. (문구는 그대로 — 기존 설명과 같은 출력.)

- [ ] **Step 7: 레지스트리 등록**

`ModifierRegistry.cs`에서

```csharp
            // 도적
```

다음 줄에 추가 (노련한 협공은 Task 4에서 이 두 줄 **위**, 독침은 아래에 들어간다):

```csharp
            () => new Job.SpareDaggerModifier(),
            () => new Job.VolleyStrikeModifier(),
```

- [ ] **Step 8: 컴파일 PASS 확인** — Run: 헤드리스 컴파일. Expected: `RESULT PASS`. (기존 `GetStageHits(stage)` 호출부 — `CombatRedesignSelfTests`·`ActionForecaster`(→`plan.Hits`)·`TutorialScenario`(→`CalculateStageDamage`) — 는 수정 없이 컴파일돼야 한다.)

- [ ] **Step 9: 테스트 실행** — `Run Job Modifier Self-Tests`, `Run Combat Redesign Self-Tests`(도적 타수 2/3/4·총 배율 회귀), `DiceOrbit → Run ActionForecast Self-Tests` 통과. **pending user run.**

- [ ] **Step 10: 커밋**

```bash
git add Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Skills/ComboActiveSkill.cs \
  Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/AttackPlan.cs \
  Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/AutoAttackSystem.cs \
  "Assets/Scripts/Data/Character Preset/Rogue/RogueAmbushActive.cs" \
  Assets/Scripts/Data/Modifiers/Job/RogueModifiers.cs Assets/Scripts/Data/Modifiers/Job/RogueModifiers.cs.meta \
  Assets/Scripts/Data/Modifiers/ModifierRegistry.cs Assets/Scripts/Editor/JobModifierSelfTests.cs
git commit -m "$(cat <<'EOF'
feat(modifier): 도적 3단계 타격 계산을 주인 기준으로(실행·예고·설명 일치) + 여분의 비수·일제 타격

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---
### Task 4: 노련한 협공 + 독침

**Files:**
- Modify: `Assets/Scripts/Data/Character Preset/Rogue/RogueFlankPassive.cs` (using, 설명 24~25행, `GetPassiveZoneStatus` 36~37행, `OnAttack` 55행)
- Modify: `Assets/Scripts/Data/Modifiers/Job/RogueModifiers.cs` (파일 끝에 2개 클래스)
- Modify: `Assets/Scripts/Data/Modifiers/ModifierRegistry.cs`
- Modify: `Assets/Scripts/Editor/JobModifierSelfTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

`// [tests-run]` 위에:

```csharp
                TestVeteranFlankPercent();
                TestVenomStingPerHit();
```

`// [tests-body]` 위에:

```csharp
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
```

- [ ] **Step 2: 컴파일로 실패 확인**

Run: 헤드리스 컴파일. Expected: `RESULT FAIL` — `RogueFlankPassive.EffectivePerAllyPercent`·`VeteranFlankModifier`·`VenomStingModifier` 미정의.

- [ ] **Step 3: 모디파이어 2종 — `RogueModifiers.cs` 파일 끝에 추가**

(Task 2의 "파일 끝에 추가" 규칙 — 마지막 `    }` 과 `}` 사이)

```csharp

    /// <summary>[노련한 협공] [협공]의 같은 구역 다른 아군 1명당 피해 증가 20 → 25%. RogueFlankPassive가 조회한다(D3).</summary>
    [System.Serializable]
    public class VeteranFlankModifier : JobModifier
    {
        public const float PerAllyPercent = 25f;

        protected override string JobId => JobIds.Rogue;
        public override string ModifierName => "노련한 협공";
        public override ModifierFamily Family => ModifierFamily.Position;
        public override string Description
            => $"[협공]으로 같은 구역의 다른 아군 1명마다 늘어나는 피해가 20%에서 {PerAllyPercent:0.#}%로 커집니다.";
    }

    /// <summary>
    /// [독침] 공격 직전 대상의 중독이 10 이상이면, 그 강화 공격의 실제 타격 직후마다 중독 1.
    /// - 조건은 행동(행동 ID)마다 대상의 첫 OnPreAction에서 기록한다 — 행동 중 10 도달은 소급하지 않는다.
    /// - 부여는 그 대상 타격의 OnPostAction(실제 처리, 시뮬레이션 아님). 회피된 타격은 OnPostAction이 오지 않는다.
    /// - 타격으로 쓰러진 대상엔 부여하지 않는다. 타격 수는 늘리지 않는다.
    /// - 도적 3단계 '중독 2배'(OnStageCompleted, 스코프 종료 후)보다 먼저 들어가므로 독침 중독도 2배가 된다.
    /// </summary>
    [System.Serializable]
    public class VenomStingModifier : JobModifier
    {
        public const int PoisonThreshold = 10;
        public const int PoisonPerHit = 1;

        [System.NonSerialized] private int _actionId;
        [System.NonSerialized] private Dictionary<Unit, bool> _qualified;

        protected override string JobId => JobIds.Rogue;
        public override string ModifierName => "독침";
        public override ModifierFamily Family => ModifierFamily.Combo;
        public override string Description
            => $"공격 직전 대상의 중독이 {PoisonThreshold} 이상이면 그 강화 공격이 적중할 때마다 중독을 {PoisonPerHit} 부여합니다.";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null || context.SourceUnit != owner) return;
            if (context.IsDirectHpLoss || context.IsSimulation) return;
            if (trigger != CombatTrigger.OnPreAction && trigger != CombatTrigger.OnPostAction) return;
            if (!AttackActionScope.IsActive || AttackActionScope.CurrentSource != owner) return;
            if (AttackActionScope.CurrentInfo.ComboStage < 0) return;   // 강화 공격만
            if (!(context.Target is Monster target)) return;

            int id = AttackActionScope.CurrentActionId;
            if (_qualified == null) _qualified = new Dictionary<Unit, bool>();
            if (id != _actionId)
            {
                _actionId = id;
                _qualified.Clear();
            }

            if (trigger == CombatTrigger.OnPreAction)
            {
                if (!_qualified.ContainsKey(target))
                    _qualified[target] = target.StatusEffects != null
                                         && target.StatusEffects.GetEffectValue(EffectType.Poison) >= PoisonThreshold;
                return;
            }

            // OnPostAction — 실제로 처리된 타격 직후
            if (_qualified.TryGetValue(target, out bool qualified) && qualified && target.IsAlive)
                GrantStatusTo(target, EffectType.Poison, PoisonPerHit);
        }
    }
```

- [ ] **Step 4: `RogueFlankPassive`가 조회**

using 블록 끝(`using DiceOrbit.Data;` 다음)에 추가:

```csharp
using DiceOrbit.Data.Modifiers.Job;
```

```csharp
        public override int Priority => 99;
```

다음에 추가:

```csharp

        /// <summary>지금 아군 1명당 % — 노련한 협공 보유 시 25 (2026-10-09 C, D3). 피해·구역 상태·설명이 같은 값을 쓴다.</summary>
        private float PerAllyPercent
            => EffectivePerAllyPercent(perAllyBonusPercent,
                                       owner is Character rogue && JobModifierQueries.Has<VeteranFlankModifier>(rogue));

        /// <summary>노련한 협공이면 max(기본, 25) (순수 — 자가 테스트 대상).</summary>
        public static float EffectivePerAllyPercent(float basePercent, bool veteran)
            => veteran ? Mathf.Max(basePercent, VeteranFlankModifier.PerAllyPercent) : basePercent;
```

그리고 세 곳을 바꾼다:

```csharp
            => $"같은 구역에 있는 다른 아군 1명마다 도적이 주는 공격 피해가 {perAllyBonusPercent:0.#}% 증가합니다.";
```
→
```csharp
            => $"같은 구역에 있는 다른 아군 1명마다 도적이 주는 공격 피해가 {PerAllyPercent:0.#}% 증가합니다.";
```

```csharp
            => FormatZoneStatus(owner is Character rogue ? CountOtherAlliesInMyZone(rogue) : 0, perAllyBonusPercent);
```
→
```csharp
            => FormatZoneStatus(owner is Character rogue ? CountOtherAlliesInMyZone(rogue) : 0, PerAllyPercent);
```

```csharp
            float percent = perAllyBonusPercent * allyCount;
```
→
```csharp
            float percent = PerAllyPercent * allyCount;
```

- [ ] **Step 5: 레지스트리 등록**

`ModifierRegistry.cs`에서

```csharp
            // 도적
            () => new Job.SpareDaggerModifier(),
            () => new Job.VolleyStrikeModifier(),
```

를

```csharp
            // 도적
            () => new Job.VeteranFlankModifier(),
            () => new Job.SpareDaggerModifier(),
            () => new Job.VolleyStrikeModifier(),
            () => new Job.VenomStingModifier(),
```

로 바꾼다.

- [ ] **Step 6: 컴파일 PASS 확인** — Run: 헤드리스 컴파일. Expected: `RESULT PASS`.

- [ ] **Step 7: 테스트 실행** — `Run Job Modifier Self-Tests`, `Run PassiveZone Self-Tests`(협공 구역 상태 문구 회귀). **pending user run.**

- [ ] **Step 8: 커밋**

```bash
git add "Assets/Scripts/Data/Character Preset/Rogue/RogueFlankPassive.cs" \
  Assets/Scripts/Data/Modifiers/Job/RogueModifiers.cs \
  Assets/Scripts/Data/Modifiers/ModifierRegistry.cs Assets/Scripts/Editor/JobModifierSelfTests.cs
git commit -m "$(cat <<'EOF'
feat(modifier): 도적 고유 — 노련한 협공(아군 1명당 25%)·독침(중독 10 이상 대상에 타격마다 중독 1)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---
### Task 5: 견고한 방진 + 전우애

**Files:**
- Create: `Assets/Scripts/Data/Modifiers/Job/WarriorModifiers.cs` (+`.meta`)
- Modify: `Assets/Scripts/Data/Character Preset/Warrior/WarriorGuardPassive.cs` (using, 설명 26~27행, `GetPassiveZoneStatus` 48행, `OnAttack` 79행)
- Modify: `Assets/Scripts/Data/Modifiers/ModifierRegistry.cs`
- Modify: `Assets/Scripts/Editor/JobModifierSelfTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

`// [tests-run]` 위에:

```csharp
                TestSteadfastFormationPercent();
                TestComradeshipCondition();
```

`// [tests-body]` 위에:

```csharp
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
```

- [ ] **Step 2: 컴파일로 실패 확인**

Run: 헤드리스 컴파일. Expected: `RESULT FAIL` — `WarriorGuardPassive.EffectivePerAllyPercent`·`SteadfastFormationModifier`·`ComradeshipModifier` 미정의.

- [ ] **Step 3: `WarriorModifiers.cs` 생성**

`Assets/Scripts/Data/Modifiers/Job/WarriorModifiers.cs` (using은 Task 6의 앙갚음(전사)·대지의 룬까지 미리 포함):

```csharp
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Modifiers.Common;
using DiceOrbit.Systems.Effects;
using UnityEngine;

namespace DiceOrbit.Data.Modifiers.Job
{
    /// <summary>[견고한 방진] [방진]의 같은 구역 다른 아군 1명당 받는 피해 감소 10 → 15%. WarriorGuardPassive가 조회한다(D3).</summary>
    [System.Serializable]
    public class SteadfastFormationModifier : JobModifier
    {
        public const float PerAllyPercent = 15f;

        protected override string JobId => JobIds.Warrior;
        public override string ModifierName => "견고한 방진";
        public override ModifierFamily Family => ModifierFamily.Position;
        public override string Description
            => $"[방진]으로 같은 구역의 다른 아군 1명마다 줄어드는 받는 피해가 10%에서 {PerAllyPercent:0.#}%로 커집니다.";
    }

    /// <summary>
    /// [전우애] 같은 구역에 다른 살아 있는 아군이 2명 이상이면 기본 공격력 +1 (미중첩).
    /// 저장 없이 AttackBonus 게터에서 지금 구역 아군 수를 센다 — 조건이 깨지면 즉시 해제되고,
    /// 행동 예고(PretendAt)·정보 패널·실제 피해가 같은 값을 본다. 저장 필드(Attack)는 바꾸지 않는다(B의 D6).
    /// </summary>
    [System.Serializable]
    public class ComradeshipModifier : JobModifier
    {
        public const int Bonus = 1;
        public const int RequiredAllies = 2;

        protected override string JobId => JobIds.Warrior;
        public override string ModifierName => "전우애";
        public override ModifierFamily Family => ModifierFamily.Position;
        public override string Description
            => $"같은 구역에 다른 아군이 {RequiredAllies}명 이상 있으면 기본 공격력이 {Bonus} 증가합니다. 조건이 깨지면 바로 사라집니다.";

        public override int AttackBonus
            => owner != null ? BonusFor(JobModifierQueries.CountOtherAlliesInMyZone(owner)) : 0;

        /// <summary>같은 구역 다른 아군 수 → 공격력 보너스 (순수 — 자가 테스트 대상).</summary>
        public static int BonusFor(int otherAllies) => otherAllies >= RequiredAllies ? Bonus : 0;
    }
}
```

`.meta`:

```bash
python -c "import uuid,sys; open(sys.argv[1],'w',newline='\n').write('fileFormatVersion: 2\nguid: %s\n' % uuid.uuid4().hex)" "Assets/Scripts/Data/Modifiers/Job/WarriorModifiers.cs.meta"
```

- [ ] **Step 4: `WarriorGuardPassive`가 조회**

using 블록 끝(`using DiceOrbit.Data;` 다음)에 추가:

```csharp
using DiceOrbit.Data.Modifiers.Job;
```

```csharp
        public override int Priority => 100;
```

다음에 추가:

```csharp

        /// <summary>지금 아군 1명당 감소 % — 견고한 방진 보유 시 15 (2026-10-09 C, D3). 피해 계산·구역 상태·설명이 같은 값을 쓴다.</summary>
        private float PerAllyPercent
            => EffectivePerAllyPercent(perAllyReductionPercent,
                                       owner is Character guard && JobModifierQueries.Has<SteadfastFormationModifier>(guard));

        /// <summary>견고한 방진이면 max(기본, 15) (순수 — 자가 테스트 대상).</summary>
        public static float EffectivePerAllyPercent(float basePercent, bool steadfast)
            => steadfast ? Mathf.Max(basePercent, SteadfastFormationModifier.PerAllyPercent) : basePercent;
```

그리고 세 곳을 바꾼다:

```csharp
            => $"전사와 같은 구역에 있는 다른 아군 1명마다 해당 구역의 모든 아군(전사 포함)이 받는 공격 피해가 {perAllyReductionPercent:0.#}% 감소합니다.";
```
→
```csharp
            => $"전사와 같은 구역에 있는 다른 아군 1명마다 해당 구역의 모든 아군(전사 포함)이 받는 공격 피해가 {PerAllyPercent:0.#}% 감소합니다.";
```

```csharp
            return FormatZoneStatus(allyCount, perAllyReductionPercent);
```
→
```csharp
            return FormatZoneStatus(allyCount, PerAllyPercent);
```

```csharp
            float reduction = ReductionPercent(allyCount, perAllyReductionPercent);
```
→
```csharp
            float reduction = ReductionPercent(allyCount, PerAllyPercent);
```

- [ ] **Step 5: 레지스트리 등록**

`ModifierRegistry.cs`에서

```csharp
            // 전사
```

다음 줄에 추가 (Task 6의 앙갚음(전사)·대지의 룬은 이 두 줄 아래):

```csharp
            () => new Job.SteadfastFormationModifier(),
            () => new Job.ComradeshipModifier(),
```

- [ ] **Step 6: 컴파일 PASS 확인** — Run: 헤드리스 컴파일. Expected: `RESULT PASS`.

- [ ] **Step 7: 테스트 실행** — `Run Job Modifier Self-Tests`, `Run PassiveZone Self-Tests`(방진 구역 상태 문구 회귀). **pending user run.**

- [ ] **Step 8: 커밋**

```bash
git add Assets/Scripts/Data/Modifiers/Job/WarriorModifiers.cs Assets/Scripts/Data/Modifiers/Job/WarriorModifiers.cs.meta \
  "Assets/Scripts/Data/Character Preset/Warrior/WarriorGuardPassive.cs" \
  Assets/Scripts/Data/Modifiers/ModifierRegistry.cs Assets/Scripts/Editor/JobModifierSelfTests.cs
git commit -m "$(cat <<'EOF'
feat(modifier): 전사 고유 — 견고한 방진(아군 1명당 15%)·전우애(같은 구역 아군 2명 이상 공격력 +1)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---
### Task 6: 앙갚음(전사) + 대지의 룬

**Files:**
- Modify: `Assets/Scripts/Data/Modifiers/Job/WarriorModifiers.cs` (파일 끝에 2개 클래스)
- Modify: `Assets/Scripts/Data/Character Preset/Warrior/WarriorGreatswordActive.cs` (using, `ResolveStageTargets` 다음, `OnStageCompleted`, `GetStageDescription`)
- Modify: `Assets/Scripts/Data/Modifiers/ModifierRegistry.cs`
- Modify: `Assets/Scripts/Editor/JobModifierSelfTests.cs`

**라운드 판정 설계:** 스펙은 "주인 TurnStart에 활성 = 직전 기록 후 기록 초기화"를 제안하지만, 같은 TurnStart 방송 안에서 중독 틱(`PoisonStatus`, `OnPreAction`)과 모디파이어(둘 다 Priority 10, 정렬 불안정)의 순서가 보장되지 않아 그 라운드 첫 중독 손실이 엉뚱한 라운드에 들어갈 수 있다. 대신 **`CombatManager.TurnCount`(= 라운드 번호)** 로 기록한다 — `StartPlayerTurn`이 캐릭터 `OnStartTurn`보다 먼저 `turnCount++` 하므로 "라운드 N = 플레이어 턴 N 시작 ~ 몬스터 턴 N 종료"와 정확히 맞고, 순서에 무관하다. 손실 라운드 = 현재 라운드 − 1이면 활성. 전투 시작 방송(`CombatStart`)에서 기록을 지운다(`StartCombat`이 `turnCount = 0`으로 되돌리므로 이전 전투 기록이 새 전투 1라운드로 새지 않게).

- [ ] **Step 1: 실패하는 테스트 작성**

`// [tests-run]` 위에:

```csharp
                TestWarriorPaybackRounds();
                TestWarriorPaybackHooks();
                TestEarthRune();
```

`// [tests-body]` 위에:

```csharp
        // ── 전사 생존·콤보 ────────────────────────────────────

        private static void TestWarriorPaybackRounds()
        {
            var payback = new WarriorPaybackModifier();
            Check(!payback.IsActiveInRound(1), "기록 없음 → 비활성 (전투 시작 판정 없음)");
            payback.RecordLoss(3);
            Check(payback.IsActiveInRound(4), "3라운드에 체력 손실 → 4라운드 활성");
            Check(!payback.IsActiveInRound(3) && !payback.IsActiveInRound(5), "같은 라운드·두 라운드 뒤엔 비활성");
            payback.ResetRounds();
            Check(!payback.IsActiveInRound(4), "초기화 → 비활성");
        }

        private static void TestWarriorPaybackHooks()
        {
            var warrior = NewCharacter("PaybackWarrior", JobIds.Warrior);
            var enemy = NewMonster("PaybackEnemy");
            var payback = new WarriorPaybackModifier();
            Check(payback.CanApplyTo(warrior) && !payback.CanApplyTo(NewCharacter("PaybackMage", JobIds.Mage)), "앙갚음(전사) = 전사 전용");
            Check(payback.Family == ModifierFamily.Survival, "앙갚음(전사) = 생존 계열");
            Check(payback.ModifierName != new PaybackModifier().ModifierName, "공용 앙갚음과 표시 이름이 구분된다");
            warrior.Stats.Modifiers.Add(payback);

            // 매니저 없음 = 지금 라운드 0
            payback.OnAttack(CombatTrigger.OnPostAction, new AttackContext(enemy, warrior, "막힘", 5f) { IsEffected = false });
            Check(!payback.IsActiveInRound(1), "방어도만 감소(IsEffected=false) → 기록 없음");
            payback.OnAttack(CombatTrigger.OnPostAction, new AttackContext(enemy, warrior, "예고", 5f) { IsSimulation = true, IsEffected = true });
            Check(!payback.IsActiveInRound(1), "시뮬레이션 → 기록 없음");
            payback.OnAttack(CombatTrigger.OnPostAction, new AttackContext(null, warrior, "중독", 3f) { IsDirectHpLoss = true, IsEffected = true });
            Check(payback.IsActiveInRound(1), "직접 체력 손실(중독)도 기록 — 0라운드 손실 → 1라운드 활성");

            // 피해 보정 — 지금 라운드(0)에서 활성이려면 -1라운드 기록
            payback.RecordLoss(-1);
            AttackActionScope.ResetAll();
            AttackActionScope.Begin(warrior, new AttackActionInfo { ComboStage = -1, IsAutoAttack = true });
            var basic = new AttackContext(warrior, enemy, "기본", 10f);
            payback.OnAttack(CombatTrigger.OnCalculateOutput, basic);
            AttackActionScope.End();
            Check(Mathf.Approximately(basic.OutputValue, 11.5f), $"활성 라운드의 공격 행동 피해 +15% (실제 {basic.OutputValue})");

            var outside = new AttackContext(warrior, enemy, "유물", 10f);
            payback.OnAttack(CombatTrigger.OnCalculateOutput, outside);
            Check(Mathf.Approximately(outside.OutputValue, 10f), "행동 스코프 밖(유물·디버프) 피해엔 보정 없음");

            payback.OnTurnEvent(CombatTrigger.OnPreAction, new TurnEventContext(warrior, warrior, EventPhase.CombatStart));
            Check(!payback.IsActiveInRound(0), "CombatStart 방송 → 기록 초기화");
        }

        private static void TestEarthRune()
        {
            Check(WarriorGreatswordActive.AllyArmorFor(5, false) == 5 && WarriorGreatswordActive.AllyArmorFor(5, true) == 8,
                  "대지 가르기 아군 방어도 5 → 8");
            Check(WarriorGreatswordActive.AllyArmorFor(0, true) == 0, "방어도 부여가 꺼져 있으면(0) 룬도 0");

            var skill = new WarriorGreatswordActive();
            var warrior = NewCharacter("RuneWarrior", JobIds.Warrior);
            var enemy = NewMonster("RuneEnemy");
            var rune = new EarthRuneModifier();
            Check(rune.CanApplyTo(warrior) && rune.Family == ModifierFamily.Combo, "대지의 룬 = 전사 전용·콤보");
            Check(skill.GetStage3AllyArmor(warrior) == 5, "룬 없음: 방어도 5");
            warrior.Stats.Modifiers.Add(rune);
            Check(skill.GetStage3AllyArmor(warrior) == 8, "룬: 방어도 8");
            Check(skill.GetStageDescription(warrior, 2).Contains("방어도 8"), "주인 기준 설명에도 방어도 8");

            AttackActionScope.ResetAll();
            AttackActionScope.Begin(warrior, new AttackActionInfo { ComboStage = 2, IsAutoAttack = true });
            var stage3 = new AttackContext(warrior, enemy, "대지 가르기", 10f);
            rune.OnAttack(CombatTrigger.OnCalculateOutput, stage3);
            AttackActionScope.End();
            Check(Mathf.Approximately(stage3.OutputValue, 11.5f), $"3단계 피해 +15% (실제 {stage3.OutputValue})");

            AttackActionScope.Begin(warrior, new AttackActionInfo { ComboStage = 1, IsAutoAttack = true });
            var stage2 = new AttackContext(warrior, enemy, "균열 베기", 10f);
            rune.OnAttack(CombatTrigger.OnCalculateOutput, stage2);
            AttackActionScope.End();
            Check(Mathf.Approximately(stage2.OutputValue, 10f), "2단계엔 보정 없음");
        }
```

- [ ] **Step 2: 컴파일로 실패 확인**

Run: 헤드리스 컴파일. Expected: `RESULT FAIL` — `WarriorPaybackModifier`·`EarthRuneModifier`·`WarriorGreatswordActive.AllyArmorFor`/`GetStage3AllyArmor` 미정의.

- [ ] **Step 3: 모디파이어 2종 — `WarriorModifiers.cs` 파일 끝에 추가**

```csharp

    /// <summary>
    /// [앙갚음(전사)] 직전 라운드(이전 플레이어 턴 시작 ~ 몬스터 턴 종료)에 체력을 잃었으면 이번 라운드 전사의 공격 행동 피해 +15%.
    /// - 손실 기록: 주인이 대상인 타격의 OnPostAction에서 IsEffected(체력이 실제로 줄었음)면 지금 라운드 번호를 남긴다.
    ///   원인 무관(중독 등 직접 체력 손실 포함 — 파이프라인은 직접 손실도 OnPostAction을 통지한다), 방어도만 깎인 경우 제외.
    /// - 라운드 번호 = CombatManager.TurnCount (StartPlayerTurn이 캐릭터 턴 시작 처리보다 먼저 올린다). 손실 라운드 = 지금 − 1이면 활성.
    /// - 보정: 행동 스코프 주체가 주인인 공격 행동(기본/강화)만 — 유물·디버프 피해 제외. 전투 시작 판정은 없다(CombatStart에서 기록 삭제).
    /// - 공용 [앙갚음](충전)과 다른 종류라 함께 장착할 수 있어 표시 이름에 "(전사)"를 붙인다.
    /// </summary>
    [System.Serializable]
    public class WarriorPaybackModifier : JobModifier
    {
        public const float Percent = 15f;

        [System.NonSerialized] private bool _hasLoss;
        [System.NonSerialized] private int _lossRound;

        protected override string JobId => JobIds.Warrior;
        public override string ModifierName => "앙갚음(전사)";
        public override ModifierFamily Family => ModifierFamily.Survival;
        public override string Description
            => $"직전 라운드에 체력을 잃었다면 이번 라운드 동안 공격 피해가 {Percent:0.#}% 증가합니다. 방어도만 깎인 경우는 세지 않습니다.";

        /// <summary>round 라운드에 체력을 잃었다고 기록한다.</summary>
        public void RecordLoss(int round)
        {
            _hasLoss = true;
            _lossRound = round;
        }

        /// <summary>round 라운드에 보너스가 켜져 있나 — 직전 라운드에 손실 기록이 있으면 (순수 — 자가 테스트 대상).</summary>
        public bool IsActiveInRound(int round) => _hasLoss && _lossRound == round - 1;

        /// <summary>기록 삭제 (전투 시작).</summary>
        public void ResetRounds()
        {
            _hasLoss = false;
            _lossRound = 0;
        }

        private static int CurrentRound => CombatManager.Instance != null ? CombatManager.Instance.TurnCount : 0;

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null) return;
            if (context.Phase != EventPhase.CombatStart) return;
            if (trigger != CombatTrigger.OnPreAction) return;   // 방송 1회당 1번만
            if (context.SourceUnit != owner || context.IsSimulation) return;

            ResetRounds();
        }

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null) return;

            if (trigger == CombatTrigger.OnPostAction)
            {
                if (context.Target == owner && context.IsEffected && !context.IsSimulation)
                    RecordLoss(CurrentRound);
                return;
            }

            if (!IsOwnAttackCalculation(trigger, context)) return;
            if (!AttackActionScope.IsActive || AttackActionScope.CurrentSource != owner) return;
            if (!IsActiveInRound(CurrentRound)) return;

            context.OutputValue *= 1f + Percent / 100f;
        }
    }

    /// <summary>
    /// [대지의 룬] 3단계 [대지 가르기] 피해 +15%, 아군 일시 방어도 5 → 8.
    /// 피해는 OnAttack(3단계 조건, 스펙 §3.3), 방어도는 WarriorGreatswordActive가 조회해 +3 (D3).
    /// </summary>
    [System.Serializable]
    public class EarthRuneModifier : JobModifier
    {
        public const float Percent = 15f;
        public const int ExtraAllyArmor = 3;

        protected override string JobId => JobIds.Warrior;
        public override string ModifierName => "대지의 룬";
        public override ModifierFamily Family => ModifierFamily.Combo;
        public override string Description
            => $"3단계 강화 공격 [대지 가르기]의 피해가 {Percent:0.#}% 증가하고, 아군에게 주는 일시 방어도가 {ExtraAllyArmor} 늘어납니다.";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (!IsOwnStage3Calculation(trigger, context)) return;
            context.OutputValue *= 1f + Percent / 100f;
        }
    }
```

- [ ] **Step 4: `WarriorGreatswordActive`가 대지의 룬 방어도를 조회**

using 블록에서 `using DiceOrbit.Data.Skills;` 다음 줄에 추가:

```csharp
using DiceOrbit.Data.Modifiers.Job;
```

`OnStageCompleted` 전체

```csharp
        public override void OnStageCompleted(Character source, List<Unit> targets, int stage)
        {
            if (stage != StageCount - 1 || stage3AllyArmor <= 0) return;

            // 대지 가르기 — 살아 있는 모든 아군에게 임시 방어도. 캐릭터 방어도는 소모될 때까지 유지되므로
            // '최소한 다음 적 공격까지'가 자연히 보장된다.
            foreach (var ally in AllLivingAllies())
            {
                if (ally == null || ally.Stats == null) continue;
                ally.Stats.TempArmor += stage3AllyArmor;
                UI.CombatNotifier.NotifyStatus(ally, $"방어도 +{stage3AllyArmor}", new Color(0.65f, 0.8f, 1f));
            }
        }
```

를

```csharp
        /// <summary>3단계 후 아군 일시 방어도 — 대지의 룬 보유 시 +3 (2026-10-09 C, D3). 부여·설명이 같은 값을 쓴다.</summary>
        public int GetStage3AllyArmor(Character source)
            => AllyArmorFor(stage3AllyArmor, JobModifierQueries.Has<EarthRuneModifier>(source));

        /// <summary>기본 방어도 + 대지의 룬 (순수 — 자가 테스트 대상). 기본이 0 이하(부여 꺼짐)면 0.</summary>
        public static int AllyArmorFor(int baseArmor, bool earthRune)
            => baseArmor <= 0 ? 0 : baseArmor + (earthRune ? EarthRuneModifier.ExtraAllyArmor : 0);

        public override void OnStageCompleted(Character source, List<Unit> targets, int stage)
        {
            if (stage != StageCount - 1) return;
            int armor = GetStage3AllyArmor(source);
            if (armor <= 0) return;

            // 대지 가르기 — 살아 있는 모든 아군에게 임시 방어도. 캐릭터 방어도는 다음 플레이어 턴 시작에 초기화되므로(B의 D2)
            // '최소한 다음 적 공격까지'가 보장된다.
            foreach (var ally in AllLivingAllies())
            {
                if (ally == null || ally.Stats == null) continue;
                ally.Stats.TempArmor += armor;
                UI.CombatNotifier.NotifyStatus(ally, $"방어도 +{armor}", new Color(0.65f, 0.8f, 1f));
            }
        }
```

로 바꾼다. `GetStageDescription(int stage)`의 마지막 줄

```csharp
                _ => $"[대지 가르기] 모든 몬스터에게 공격력의 {stage3Multiplier * 100f:0.#}%만큼 피해를 주고 모든 아군에게 일시 방어도 {stage3AllyArmor}을 부여합니다.",
            };
```

를

```csharp
                _ => FormatStage3Description(stage3AllyArmor),
            };

        /// <summary>주인 기준 3단계 설명 — 대지의 룬 방어도 반영 (정보 패널).</summary>
        public override string GetStageDescription(Character source, int stage)
            => stage == StageCount - 1 ? FormatStage3Description(GetStage3AllyArmor(source)) : GetStageDescription(stage);

        private string FormatStage3Description(int allyArmor)
            => $"[대지 가르기] 모든 몬스터에게 공격력의 {stage3Multiplier * 100f:0.#}%만큼 피해를 주고 모든 아군에게 일시 방어도 {allyArmor}을 부여합니다.";
```

로 바꾼다. (기존 문구와 같은 출력.)

- [ ] **Step 5: 레지스트리 등록**

`ModifierRegistry.cs`에서

```csharp
            () => new Job.ComradeshipModifier(),
```

다음 줄에 추가:

```csharp
            () => new Job.WarriorPaybackModifier(),
            () => new Job.EarthRuneModifier(),
```

- [ ] **Step 6: 컴파일 PASS 확인** — Run: 헤드리스 컴파일. Expected: `RESULT PASS`.

- [ ] **Step 7: 테스트 실행** — `Run Job Modifier Self-Tests`, `Run Combat Redesign Self-Tests`(전사 단계표 회귀). **pending user run.**

- [ ] **Step 8: 커밋**

```bash
git add Assets/Scripts/Data/Modifiers/Job/WarriorModifiers.cs \
  "Assets/Scripts/Data/Character Preset/Warrior/WarriorGreatswordActive.cs" \
  Assets/Scripts/Data/Modifiers/ModifierRegistry.cs Assets/Scripts/Editor/JobModifierSelfTests.cs
git commit -m "$(cat <<'EOF'
feat(modifier): 전사 고유 — 앙갚음(전사)(직전 라운드 체력 손실 시 +15%)·대지의 룬(3단계 +15%, 아군 방어도 8)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---
### Task 7: 마법의 땅 — 새 타일 속성(enum 끝) + 시각 항목 + 모디파이어

**Files:**
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/TileAttribute.cs` (enum 27행 `Vitality` 다음, `GetDisplayName` 151~152행)
- Create: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/MagicLandTile.cs` (+`.meta`)
- Modify: `Assets/Scripts/Data/Modifiers/Job/MageModifiers.cs` (파일 끝에 1개 클래스)
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/CombatManager.cs` (`EndCombat`의 `ClearPartyCharge();` 줄 다음)
- Modify: `Assets/Resources/UI/TileAttributeVisualDatabase.asset` (LFS, 파일 끝에 항목 1개)
- Modify: `Assets/Scripts/Data/Modifiers/ModifierRegistry.cs`
- Modify: `Assets/Scripts/Editor/JobModifierSelfTests.cs`

**설계:** 피해 보정은 타일 속성이 아니라 모디파이어가 낸다(스펙: "OnAttack 계산에서 주인 CurrentTile이 마법의 땅이면 ×1.15") — 다른 캐릭터가 그 타일에 서도 보정이 없어야 하고, 행동 예고의 `PretendAt`(도착 타일 가정)과 실제 피해가 같은 판정을 쓰게 된다. `MagicLandTile`은 표식·툴팁·버블만 맡고 반응하지 않는다. 설치: 주인의 `CombatStart` 방송(`OnPreAction` 1회). 삭제: 설치 직전 자기 것 정리(`ClearFrom`, `EndCombat`을 거치지 않는 디버그 시작 경로 대비) + `CombatManager.EndCombat`에서 전부 정리(`ClearAll`, B의 충전 정리 옆). 타일 속성은 타입별 1개라 기존 속성(시약·이벤트 타일 등)과 공존한다.

- [ ] **Step 1: 실패하는 테스트 작성**

`// [tests-run]` 위에:

```csharp
                TestMagicLandTileAndModifier();
```

`// [tests-body]` 위에:

```csharp
        // ── 마법의 땅 ─────────────────────────────────────────

        private static void TestMagicLandTileAndModifier()
        {
            Check((int)TileAttributeType.MagicLand == 19, "MagicLand는 enum 끝(19) — 기존 .asset의 정수 인덱스 불변");
            var tile = new MagicLandTile(null);
            Check(tile.Type == TileAttributeType.MagicLand && tile.Duration == -1 && tile.Value == 15,
                  "마법의 땅: 지속 영구(전투 종료 시 별도 삭제), 값 15");
            Check(tile.GetDisplayName() == "마법의 땅", $"표시 이름 (실제 {tile.GetDisplayName()})");
            Check(tile.GetDescription().Contains("15%"), "설명에 15%");

            var db = Resources.Load<DiceOrbit.UI.TileAttributeVisualDatabase>("UI/TileAttributeVisualDatabase");
            Check(db != null && db.TryGet(TileAttributeType.MagicLand, out _),
                  "시각 DB에 마법의 땅 항목 (없으면 버블 대신 에러 로그)");

            var mage = NewCharacter("LandMage", JobIds.Mage);
            var land = new MagicLandModifier();
            Check(land.CanApplyTo(mage) && !land.CanApplyTo(NewCharacter("LandRogue", JobIds.Rogue)), "마법의 땅 = 마법사 전용");
            Check(land.Family == ModifierFamily.Position, "마법의 땅 = 위치 계열");
            Check(!MagicLandModifier.IsOnMagicLand(mage) && !MagicLandModifier.IsOnMagicLand(null), "타일 없음 → 마법의 땅 아님");

            mage.Stats.Modifiers.Add(land);
            var enemy = NewMonster("LandEnemy");
            var ctx = new AttackContext(mage, enemy, "비전 화살", 10f);
            land.OnAttack(CombatTrigger.OnCalculateOutput, ctx);
            Check(Mathf.Approximately(ctx.OutputValue, 10f), "마법의 땅 밖 → 보정 없음");
            // 타일 위 +15%는 TileData·버블 매니저가 필요해 플레이 확인 항목으로 둔다 (§사용자 확인).
        }
```

- [ ] **Step 2: 컴파일로 실패 확인**

Run: 헤드리스 컴파일. Expected: `RESULT FAIL` — `TileAttributeType.MagicLand`·`MagicLandTile`·`MagicLandModifier` 미정의.

- [ ] **Step 3: `TileAttributeType.MagicLand` (끝에만) + 표시 이름**

`TileAttribute.cs`에서

```csharp
        Vitality,       // 활력: 통과/턴 종료 시 모든 식물 몬스터 활력 -1, 영구 (Wave7 농장)
    }
```

를

```csharp
        Vitality,       // 활력: 통과/턴 종료 시 모든 식물 몬스터 활력 -1, 영구 (Wave7 농장)
        MagicLand,      // 마법의 땅: 마법사 [마법의 땅]이 전투 시작 시 한 구역에 설치, 그 위 마법사 공격 피해 +15%, 전투 종료 시 삭제 (2026-10-09 C) — 반드시 enum 끝에 유지
    }
```

로, `GetDisplayName`에서

```csharp
                TileAttributeType.Frost => "빙결",
                _ => Type.ToString()
```

를

```csharp
                TileAttributeType.Frost => "빙결",
                TileAttributeType.MagicLand => "마법의 땅",
                _ => Type.ToString()
```

로 바꾼다.

- [ ] **Step 4: `MagicLandTile.cs` 생성**

`Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/MagicLandTile.cs`:

```csharp
using DiceOrbit.Core;
using DiceOrbit.Data.Modifiers.Job;
using UnityEngine;

namespace DiceOrbit.Data.Tile
{
    /// <summary>
    /// 마법의 땅 (2026-10-09 C) — 마법사 [마법의 땅] 모디파이어가 전투 시작 시 무작위 구역 하나의 모든 타일에 설치한다.
    /// 피해 보정은 MagicLandModifier.OnAttack이 주인의 CurrentTile을 보고 낸다 — 이 속성은 표식·툴팁·버블만 맡고 반응하지 않는다.
    /// 다른 타일 속성과 공존(타입별 1개)하고, 공격 범위·아군 점유는 바꾸지 않는다.
    /// 전투 종료 시 CombatManager.EndCombat이 ClearAll로 지운다. 시각 항목은 예리함 아이콘을 보라색으로 임시 사용(전용 아트는 후속).
    /// </summary>
    public class MagicLandTile : TileAttribute
    {
        /// <summary>설치한 모디파이어 — 다음 전투 시작 때 자기 타일만 지우는 데 쓴다 (null 허용).</summary>
        public readonly MagicLandModifier Source;

        public MagicLandTile(MagicLandModifier source)
            : base(TileAttributeType.MagicLand, Mathf.RoundToInt(MagicLandModifier.Percent), -1, false)
        {
            Source = source;
        }

        public override string GetDescription()
            => $"마법사가 이 타일에서 공격하면 주는 피해가 {Value}% 증가합니다. 전투가 끝나면 사라집니다.";

        /// <summary>궤도의 모든 마법의 땅을 지운다 (전투 종료).</summary>
        public static void ClearAll()
        {
            var orbit = GameManager.Instance != null ? GameManager.Instance.GetOrbitManager() : null;
            if (orbit == null || orbit.Tiles == null) return;

            foreach (var tile in orbit.Tiles)
                if (tile != null) tile.RemoveAttributeType(TileAttributeType.MagicLand);
        }

        /// <summary>source가 설치한 마법의 땅만 지운다 (설치 직전 정리 — 다른 마법사의 것은 남긴다).</summary>
        public static void ClearFrom(MagicLandModifier source)
        {
            var orbit = GameManager.Instance != null ? GameManager.Instance.GetOrbitManager() : null;
            if (orbit == null || orbit.Tiles == null) return;

            foreach (var tile in orbit.Tiles)
            {
                if (tile == null) continue;
                foreach (var attribute in tile.GetAttributes())   // 스냅샷 — 순회 중 제거 안전
                {
                    if (attribute is MagicLandTile land && land.Source == source)
                    {
                        tile.RemoveAttribute(attribute);
                        break;
                    }
                }
            }
        }
    }
}
```

`.meta`:

```bash
python -c "import uuid,sys; open(sys.argv[1],'w',newline='\n').write('fileFormatVersion: 2\nguid: %s\n' % uuid.uuid4().hex)" "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/MagicLandTile.cs.meta"
```

- [ ] **Step 5: `MagicLandModifier` — `MageModifiers.cs` 파일 끝에 추가**

```csharp

    /// <summary>
    /// [마법의 땅] 전투 시작 시 무작위 구역 1개의 모든 타일에 마법의 땅 설치. 마법사가 그 타일에서 공격하면 피해 +15%(다단·광역 전체).
    /// 설치: 주인의 CombatStart 방송(OnPreAction 1회, 살아 있는 파티원에게만 방송됨). 삭제: 설치 직전 자기 것 + CombatManager.EndCombat.
    /// 보정: 주인의 공격 계산(IsOwnAttackCalculation)에서 주인 CurrentTile이 마법의 땅이면 ×1.15 — 행동 예고는 PretendAt으로 도착 타일을 본다.
    /// </summary>
    [System.Serializable]
    public class MagicLandModifier : JobModifier
    {
        public const float Percent = 15f;

        protected override string JobId => JobIds.Mage;
        public override string ModifierName => "마법의 땅";
        public override ModifierFamily Family => ModifierFamily.Position;
        public override string Description
            => $"전투 시작 시 무작위 구역 하나의 모든 타일에 마법의 땅을 설치합니다. 마법의 땅 위에서 공격하면 피해가 {Percent:0.#}% 증가하며, 마법의 땅은 전투가 끝나면 사라집니다.";

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null) return;
            if (context.Phase != EventPhase.CombatStart) return;
            if (trigger != CombatTrigger.OnPreAction) return;   // 방송 1회당 1번만
            if (context.SourceUnit != owner || context.IsSimulation) return;

            Install();
        }

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (!IsOwnAttackCalculation(trigger, context)) return;
            if (!IsOnMagicLand(owner)) return;

            context.OutputValue *= 1f + Percent / 100f;
        }

        /// <summary>캐릭터가 지금(예고 중이면 가정한 타일에) 마법의 땅 위에 서 있나.</summary>
        public static bool IsOnMagicLand(Character character)
        {
            if (character == null) return false;
            var tile = character.CurrentTile;
            return tile != null && tile.HasAttribute(TileAttributeType.MagicLand);
        }

        private void Install()
        {
            MagicLandTile.ClearFrom(this);   // 이전 전투 잔여 정리 (EndCombat을 거치지 않은 시작 경로 대비)

            var zones = CombatZoneManager.Instance;
            if (zones == null) return;

            int zone = Random.Range(0, zones.ZoneCount);
            var tiles = zones.GetTilesInZone(zone);
            if (tiles == null) return;

            foreach (var tile in tiles)
                if (tile != null) tile.AddAttribute(new MagicLandTile(this));
        }
    }
```

- [ ] **Step 6: 전투 종료 시 정리**

`CombatManager.cs`의 `EndCombat`에서

```csharp
            ClearPartyCharge();    // 충전은 전투를 넘어 이월되지 않는다 (방송 없는 디버그 시작 경로 포함)
```

다음 줄에 추가:

```csharp
            DiceOrbit.Data.Tile.MagicLandTile.ClearAll();   // 마법의 땅은 그 전투 한정 (2026-10-09 C)
```

- [ ] **Step 7: 시각 DB 항목 (`type: 19`, 예리함 아이콘 보라색 임시)**

`TileAttributeVisualDatabase.asset`은 LFS 추적 파일이지만 워킹트리는 실제 YAML(LF, 마지막 줄 `    description: ` + 개행)이다. 기존 항목과 같은 형식으로 끝에 붙인다(예리함 `type: 10`의 아이콘 GUID 재사용):

```bash
python - <<'EOF'
p = "Assets/Resources/UI/TileAttributeVisualDatabase.asset"
s = open(p, encoding="utf-8", newline="").read()
assert "\n  - type: 19\n" not in s, "이미 MagicLand(19) 항목이 있다"
assert s.endswith("    description: \n"), repr(s[-80:])
assert "guid: 7a77fb522eb7fea4786c78c267335fc0" in s, "예리함 아이콘 GUID가 DB에 없다 — 다른 기존 아이콘으로 바꿀 것"
s += ("  - type: 19\n"
      "    icon: {fileID: 21300000, guid: 7a77fb522eb7fea4786c78c267335fc0, type: 3}\n"
      "    iconTint: {r: 0.72, g: 0.45, b: 1, a: 1}\n"
      "    shortLabel: ML\n"
      "    description: \n")
open(p, "w", encoding="utf-8", newline="").write(s)
print("OK")
EOF
tail -6 Assets/Resources/UI/TileAttributeVisualDatabase.asset
```

Expected: `OK`, 마지막 5줄이 위 항목.

- [ ] **Step 8: 레지스트리 등록**

`ModifierRegistry.cs`에서

```csharp
            () => new Job.HighVoltageModifier(),
```

다음 줄에 추가 (Task 8의 회로 공명은 이 줄 **위**(고전압 다음), 마나흡수는 아래에 들어간다):

```csharp
            () => new Job.MagicLandModifier(),
```

- [ ] **Step 9: 감사 — enum 끝 추가가 기존 인덱스를 밀지 않았는지**

```bash
grep -n "Vitality,\|MagicLand," Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/TileAttribute.cs
grep -rn "installType\|targetTileAttribute" Assets --include=*.asset | head
```

Expected: `MagicLand,`가 `Vitality,` 바로 다음 줄(마지막). `.asset`의 기존 정수 값은 그대로(끝에 추가했으므로 재매핑 불필요).

- [ ] **Step 10: 컴파일 PASS 확인** — Run: 헤드리스 컴파일. Expected: `RESULT PASS`.

- [ ] **Step 11: 테스트 실행** — `Run Job Modifier Self-Tests`, `Run ActionForecast Self-Tests`. **pending user run.** (Unity가 DB `.asset`을 다시 직렬화하면 diff를 확인 — 인스펙터에서 항목 19가 보이면 정상.)

- [ ] **Step 12: 커밋**

```bash
git add Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/TileAttribute.cs \
  Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/MagicLandTile.cs \
  Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/MagicLandTile.cs.meta \
  Assets/Scripts/Data/Modifiers/Job/MageModifiers.cs \
  Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/CombatManager.cs \
  Assets/Resources/UI/TileAttributeVisualDatabase.asset \
  Assets/Scripts/Data/Modifiers/ModifierRegistry.cs Assets/Scripts/Editor/JobModifierSelfTests.cs
git commit -m "$(cat <<'EOF'
feat(modifier): 마법사 고유 — 마법의 땅(전투 시작 시 한 구역 설치, 그 위 공격 +15%, 전투 종료 시 삭제) + TileAttributeType.MagicLand(끝)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---
### Task 8: 회로 공명 + 마나흡수

**Files:**
- Modify: `Assets/Scripts/Data/Modifiers/Job/MageModifiers.cs` (파일 끝에 2개 클래스)
- Modify: `Assets/Scripts/Data/Modifiers/ModifierRegistry.cs`
- Modify: `Assets/Scripts/Editor/JobModifierSelfTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

`// [tests-run]` 위에:

```csharp
                TestCircuitResonance();
                TestManaAbsorb();
```

`// [tests-body]` 위에:

```csharp
        // ── 마법사 위치·콤보 ──────────────────────────────────

        private static void TestCircuitResonance()
        {
            Check(!CircuitResonanceModifier.Qualifies(2) && CircuitResonanceModifier.Qualifies(3) && CircuitResonanceModifier.Qualifies(4),
                  "아군이 있는 구역(마법사 구역 포함) 3개 이상");

            var mage = NewCharacter("ResonMage", JobIds.Mage);
            var resonance = new CircuitResonanceModifier();
            Check(resonance.CanApplyTo(mage) && !resonance.CanApplyTo(NewCharacter("ResonAlch", JobIds.Alchemist)), "회로 공명 = 마법사 전용");
            Check(resonance.Family == ModifierFamily.Position, "회로 공명 = 위치 계열");
            mage.Stats.Modifiers.Add(resonance);

            var enemy = NewMonster("ResonEnemy");
            AttackActionScope.ResetAll();
            AttackActionScope.Begin(mage, new AttackActionInfo { ComboStage = 0, IsAutoAttack = true });
            var ctx = new AttackContext(mage, enemy, "비전 화살", 10f);
            resonance.OnAttack(CombatTrigger.OnCalculateOutput, ctx);
            AttackActionScope.End();
            Check(Mathf.Approximately(ctx.OutputValue, 10f), "구역 판정 불가(회로 0개) → 보정 없음");
        }

        private static void TestManaAbsorb()
        {
            Check(ManaAbsorbModifier.ChargeGainFor(0) == 0 && ManaAbsorbModifier.ChargeGainFor(2) == 2
                  && ManaAbsorbModifier.ChargeGainFor(5) == 3, "처치 수만큼, 최대 3");

            var mage = NewCharacter("ManaMage", JobIds.Mage);
            var fx = NewEffects(mage);
            var absorb = new ManaAbsorbModifier();
            Check(absorb.CanApplyTo(mage) && absorb.Family == ModifierFamily.Combo, "마나흡수 = 마법사 전용·콤보");
            mage.Stats.Modifiers.Add(absorb);
            var enemy = NewMonster("ManaEnemy");
            var targets = new List<Unit> { enemy };

            absorb.OnAttackActionFinished(mage, new AttackActionResult(1, targets, 2));
            Check(!fx.HasEffect(EffectType.Charge), "2단계 처치는 충전 없음");
            absorb.OnAttackActionFinished(mage, new AttackActionResult(2, targets, 0));
            Check(!fx.HasEffect(EffectType.Charge), "3단계 처치 0 → 충전 없음");
            absorb.OnAttackActionFinished(mage, new AttackActionResult(2, targets, 2));
            Check(fx.GetEffectValue(EffectType.Charge) == 2, $"3단계 2명 처치 → 충전 2 (실제 {fx.GetEffectValue(EffectType.Charge)})");
            absorb.OnAttackActionFinished(NewCharacter("OtherMage", JobIds.Mage), new AttackActionResult(2, targets, 1));
            Check(fx.GetEffectValue(EffectType.Charge) == 2, "다른 캐릭터의 공격 결과는 무시");

            AttackActionScope.ResetAll();
            AttackActionScope.Begin(mage, new AttackActionInfo { ComboStage = 2, IsAutoAttack = true });
            var stage3 = new AttackContext(mage, enemy, "궤도 붕괴", 10f);
            absorb.OnAttack(CombatTrigger.OnCalculateOutput, stage3);
            AttackActionScope.End();
            Check(Mathf.Approximately(stage3.OutputValue, 11.5f), $"3단계 피해 +15% (실제 {stage3.OutputValue})");
        }
```

- [ ] **Step 2: 컴파일로 실패 확인**

Run: 헤드리스 컴파일. Expected: `RESULT FAIL` — `CircuitResonanceModifier`·`ManaAbsorbModifier` 미정의.

- [ ] **Step 3: 모디파이어 2종 — `MageModifiers.cs` 파일 끝에 추가**

```csharp

    /// <summary>
    /// [회로 공명] 아군이 있는 구역(마법사 구역 포함, 같은 구역 여러 명은 1개)이 3개 이상이면 자신의 강화 공격 피해 +15%.
    /// 구역 수 = MageCircuit.GetCircuitZones(마력 회로와 같은 계산) — 질의 시점 계산이라 이동·사망이 즉시 반영된다.
    /// </summary>
    [System.Serializable]
    public class CircuitResonanceModifier : JobModifier
    {
        public const float Percent = 15f;
        public const int RequiredZones = 3;

        protected override string JobId => JobIds.Mage;
        public override string ModifierName => "회로 공명";
        public override ModifierFamily Family => ModifierFamily.Position;
        public override string Description
            => $"아군이 있는 구역(마법사 구역 포함)이 {RequiredZones}개 이상이면 강화 공격의 피해가 {Percent:0.#}% 증가합니다.";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (!IsOwnEmpoweredCalculation(trigger, context)) return;
            if (!Qualifies(MageCircuit.GetCircuitZones(owner).Count)) return;

            context.OutputValue *= 1f + Percent / 100f;
        }

        /// <summary>회로 구역 수가 조건을 만족하나 (순수 — 자가 테스트 대상).</summary>
        public static bool Qualifies(int circuitZoneCount) => circuitZoneCount >= RequiredZones;
    }

    /// <summary>
    /// [마나흡수] 3단계 강화 공격 피해 +15%. 그 공격의 충전 1 소비 후, 마법사가 그 공격으로 직접 처치한 몬스터 수만큼 충전 획득(최대 3).
    /// - "소비 후 획득": AutoAttackSystem이 ChargeStatus.ConsumeOne을 IAttackActionFinishedListener 통지보다 먼저 부른다(B).
    /// - 처치 수 = AttackActionResult.Kills(스코프 종료 직후 기준) — 디버프·유물 처치는 들어가지 않고, 소급도 없다.
    /// </summary>
    [System.Serializable]
    public class ManaAbsorbModifier : JobModifier, IAttackActionFinishedListener
    {
        public const float Percent = 15f;
        public const int MaxChargeGain = 3;

        protected override string JobId => JobIds.Mage;
        public override string ModifierName => "마나흡수";
        public override ModifierFamily Family => ModifierFamily.Combo;
        public override string Description
            => $"3단계 강화 공격의 피해가 {Percent:0.#}% 증가합니다. 그 공격으로 처치한 몬스터 1명마다 충전을 1 얻습니다(최대 {MaxChargeGain}).";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (!IsOwnStage3Calculation(trigger, context)) return;
            context.OutputValue *= 1f + Percent / 100f;
        }

        public void OnAttackActionFinished(Character character, AttackActionResult result)
        {
            if (owner == null || character != owner || !owner.IsAlive) return;
            if (result.ComboStage != 2) return;

            int gain = ChargeGainFor(result.Kills);
            if (gain > 0) GrantCharge(gain);
        }

        /// <summary>처치 수 → 충전 획득량 (최대 3, 순수 — 자가 테스트 대상). 충전 상한 3은 ChargeStatus.Grant가 따로 보장한다.</summary>
        public static int ChargeGainFor(int kills) => Mathf.Clamp(kills, 0, MaxChargeGain);
    }
```

- [ ] **Step 4: 레지스트리 등록**

`ModifierRegistry.cs`에서

```csharp
            () => new Job.HighVoltageModifier(),
            () => new Job.MagicLandModifier(),
```

를

```csharp
            () => new Job.HighVoltageModifier(),
            () => new Job.CircuitResonanceModifier(),
            () => new Job.MagicLandModifier(),
            () => new Job.ManaAbsorbModifier(),
```

로 바꾼다.

- [ ] **Step 5: 컴파일 PASS 확인** — Run: 헤드리스 컴파일. Expected: `RESULT PASS`.

- [ ] **Step 6: 테스트 실행** — `Run Job Modifier Self-Tests`. **pending user run.**

- [ ] **Step 7: 커밋**

```bash
git add Assets/Scripts/Data/Modifiers/Job/MageModifiers.cs \
  Assets/Scripts/Data/Modifiers/ModifierRegistry.cs Assets/Scripts/Editor/JobModifierSelfTests.cs
git commit -m "$(cat <<'EOF'
feat(modifier): 마법사 고유 — 회로 공명(회로 3구역 이상 강화 공격 +15%)·마나흡수(3단계 +15%, 처치당 충전)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---
### Task 9: 추가 시약 + 시약 분배 + 약화 가스

**Files:**
- Modify: `Assets/Scripts/Data/Modifiers/Job/AlchemistModifiers.cs` (파일 끝에 3개 클래스)
- Modify: `Assets/Scripts/Data/Character Preset/Alchemist/ReagentPrepPassive.cs` (`CatalystPercent` 다음, 설명 32행, `PlaceReagentTiles` 70행)
- Modify: `Assets/Scripts/Data/Character Preset/Alchemist/ReagentTile.cs` (using, `TryCatalyze` 전체)
- Modify: `Assets/Scripts/Data/Modifiers/ModifierRegistry.cs`
- Modify: `Assets/Scripts/Editor/JobModifierSelfTests.cs`

**시약 분배 해석:** "같은 구역" = 촉매가 생긴 **시약 타일의 구역**(이동 중 통과한 타일 기준 — 받는 아군이 아직 이동 중이라 '도착 구역'은 그 순간 정해지지 않았다). 받은 본인·이미 촉매가 있는 아군·쓰러진 아군은 추첨에서 제외, 대상이 없으면 전달 없음. 분배로 받은 촉매는 타일을 거치지 않으므로 다시 분배되지 않는다. 타일 주인 연금술사가 살아 있을 때만 분배한다(D5와 같은 "보유자 생존" 원칙).

- [ ] **Step 1: 실패하는 테스트 작성**

`// [tests-run]` 위에:

```csharp
                TestExtraReagentCount();
                TestReagentSharePick();
                TestWeakeningGas();
```

`// [tests-body]` 위에:

```csharp
        // ── 연금술사 ──────────────────────────────────────────

        private static void TestExtraReagentCount()
        {
            Check(ReagentPrepPassive.TileCountFor(3, false) == 3 && ReagentPrepPassive.TileCountFor(3, true) == 4, "시약 타일 3 → 4");
            Check(ReagentPrepPassive.TileCountFor(-1, false) == 0, "음수 설정은 0");
            var alch = NewCharacter("ExtraAlch", JobIds.Alchemist);
            var extra = new ExtraReagentModifier();
            Check(extra.CanApplyTo(alch) && !extra.CanApplyTo(NewCharacter("ExtraMage", JobIds.Mage)), "추가 시약 = 연금술사 전용");
            Check(extra.Family == ModifierFamily.Position, "추가 시약 = 위치 계열");
        }

        private static void TestReagentSharePick()
        {
            var mover = NewCharacter("ShareMover", JobIds.Warrior);
            NewEffects(mover);
            var holder = NewCharacter("ShareHolder", JobIds.Rogue);
            NewEffects(holder).AddEffect(StatusEffectManager.CreateEffect(EffectType.Catalyst, 25, -1));
            var free = NewCharacter("ShareFree", JobIds.Mage);
            NewEffects(free);
            var dead = NewCharacter("ShareDead", JobIds.Alchemist);
            NewEffects(dead);
            dead.Stats.CurrentHP = 0;

            var candidates = new List<Character> { mover, holder, free, dead };
            bool alwaysFree = true;
            for (int i = 0; i < 10; i++)
                alwaysFree &= ReagentShareModifier.PickRecipient(candidates, mover) == free;
            Check(alwaysFree, "받은 본인·촉매 보유·사망 아군 제외 → 남은 1명만 뽑힌다");
            Check(ReagentShareModifier.PickRecipient(new List<Character> { mover, holder }, mover) == null, "대상 없으면 전달 없음");
            Check(ReagentShareModifier.PickRecipient(null, mover) == null, "후보 null → 없음");

            var share = new ReagentShareModifier();
            Check(share.CanApplyTo(NewCharacter("ShareAlch", JobIds.Alchemist)) && share.Family == ModifierFamily.Position,
                  "시약 분배 = 연금술사 전용·위치");
        }

        private static void TestWeakeningGas()
        {
            var alch = NewCharacter("GasAlch", JobIds.Alchemist);
            var gas = new WeakeningGasModifier();
            Check(gas.CanApplyTo(alch) && gas.Family == ModifierFamily.Combo, "약화 가스 = 연금술사 전용·콤보");
            alch.Stats.Modifiers.Add(gas);
            var enemy = NewMonster("GasEnemy");
            var enemyFx = NewEffects(enemy);

            AttackActionScope.ResetAll();
            AttackActionScope.Begin(alch, new AttackActionInfo { ComboStage = 2, IsAutoAttack = true });
            var stage3 = new AttackContext(alch, enemy, "현자의 폭탄", 10f);
            gas.OnAttack(CombatTrigger.OnCalculateOutput, stage3);
            AttackActionScope.End();
            Check(Mathf.Approximately(stage3.OutputValue, 11.5f), $"3단계 피해 +15% (실제 {stage3.OutputValue})");

            AttackActionScope.Begin(alch, new AttackActionInfo { ComboStage = 0, IsAutoAttack = true });
            var stage1 = new AttackContext(alch, enemy, "산성 플라스크", 10f);
            gas.OnAttack(CombatTrigger.OnCalculateOutput, stage1);
            AttackActionScope.End();
            Check(Mathf.Approximately(stage1.OutputValue, 10f), "1단계엔 보정 없음");

            gas.OnAttackActionFinished(alch, new AttackActionResult(2, new List<Unit> { enemy }, 0));
            Check(!enemyFx.HasEffect(EffectType.Weaken), "CombatManager 없음 → 아무 일 없음(예외 없음) — 실제 약화 살포는 플레이 확인");
        }
```

- [ ] **Step 2: 컴파일로 실패 확인**

Run: 헤드리스 컴파일. Expected: `RESULT FAIL` — `ReagentPrepPassive.TileCountFor`·`ExtraReagentModifier`·`ReagentShareModifier`·`WeakeningGasModifier` 미정의.

- [ ] **Step 3: 모디파이어 3종 — `AlchemistModifiers.cs` 파일 끝에 추가**

```csharp

    /// <summary>[추가 시약] 전투 시작 시 설치하는 시약 타일 3 → 4개. ReagentPrepPassive의 배치 개수가 조회한다(D3).</summary>
    [System.Serializable]
    public class ExtraReagentModifier : JobModifier
    {
        public const int ExtraTiles = 1;

        protected override string JobId => JobIds.Alchemist;
        public override string ModifierName => "추가 시약";
        public override ModifierFamily Family => ModifierFamily.Position;
        public override string Description
            => $"전투 시작 시 설치하는 시약 타일이 {ExtraTiles}개 늘어납니다.";
    }

    /// <summary>
    /// [시약 분배] 아군이 시약 타일에서 촉매를 얻으면 그 타일 구역의 무작위 다른 아군 1명에게도 촉매.
    /// ReagentTile.TryCatalyze가 타일 주인 연금술사(살아 있음)의 보유 여부를 조회해 PickRecipient로 대상을 고른다.
    /// 이미 촉매가 있는 아군은 제외, 대상 없으면 전달 없음, 분배로 받은 촉매는 다시 분배하지 않음(스펙 §2).
    /// </summary>
    [System.Serializable]
    public class ReagentShareModifier : JobModifier
    {
        protected override string JobId => JobIds.Alchemist;
        public override string ModifierName => "시약 분배";
        public override ModifierFamily Family => ModifierFamily.Position;
        public override string Description
            => "아군이 시약 타일에서 촉매를 얻으면 그 구역의 다른 아군 1명(무작위, 이미 촉매가 있는 아군 제외)도 촉매를 얻습니다.";

        /// <summary>
        /// 분배 대상 1명 — exclude(촉매를 받은 본인)·쓰러진 아군·이미 촉매가 있는 아군·상태 관리자가 없는 유닛 제외, 무작위.
        /// 없으면 null (순수 — 자가 테스트 대상).
        /// </summary>
        public static Character PickRecipient(IReadOnlyList<Character> candidates, Character exclude)
        {
            if (candidates == null) return null;

            var pool = new List<Character>();
            foreach (var c in candidates)
            {
                if (c == null || c == exclude || c.Stats == null || !c.IsAlive) continue;
                if (c.StatusEffects == null || c.StatusEffects.HasEffect(EffectType.Catalyst)) continue;
                pool.Add(c);
            }
            return pool.Count == 0 ? null : pool[Random.Range(0, pool.Count)];
        }
    }

    /// <summary>
    /// [약화 가스] 3단계 강화 공격 피해 +15%, 그 공격을 마친 뒤 살아 있는 모든 몬스터에게 약화 25%.
    /// 3단계 기존 효과(아군 고양 — AlchemistThrowActive.OnStageCompleted)는 그대로 유지된다.
    /// </summary>
    [System.Serializable]
    public class WeakeningGasModifier : JobModifier, IAttackActionFinishedListener
    {
        public const float Percent = 15f;
        public const int WeakenPercent = 25;

        protected override string JobId => JobIds.Alchemist;
        public override string ModifierName => "약화 가스";
        public override ModifierFamily Family => ModifierFamily.Combo;
        public override string Description
            => $"3단계 강화 공격의 피해가 {Percent:0.#}% 증가하고, 그 공격을 마친 뒤 모든 몬스터에게 약화 {WeakenPercent}%를 부여합니다.";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (!IsOwnStage3Calculation(trigger, context)) return;
            context.OutputValue *= 1f + Percent / 100f;
        }

        public void OnAttackActionFinished(Character character, AttackActionResult result)
        {
            if (owner == null || character != owner || !owner.IsAlive) return;
            if (result.ComboStage != 2) return;

            var cm = CombatManager.Instance;
            if (cm == null || cm.ActiveMonsters == null) return;

            foreach (var monster in cm.ActiveMonsters)
                if (monster != null && monster.IsAlive)
                    GrantStatusTo(monster, EffectType.Weaken, WeakenPercent);
        }
    }
```

- [ ] **Step 4: `ReagentPrepPassive` — 시약 타일 수 + 주인 노출**

```csharp
        public int CatalystPercent => StatusEnhancement.EffectivePercent(EffectType.Catalyst, Mathf.Max(0, catalystPercent));
```

(Task 2에서 바꾼 줄) 다음에 추가:

```csharp

        /// <summary>시약 타일을 설치한 연금술사 — ReagentTile의 시약 분배가 모디파이어 보유를 조회한다.</summary>
        public Character OwnerCharacter => owner as Character;

        /// <summary>지금 설치할 시약 타일 수 — 추가 시약 보유 시 +1 (2026-10-09 C, D3). 배치·설명이 같은 값을 쓴다.</summary>
        public int ReagentTileCount
            => TileCountFor(reagentTileCount, owner is Character ch && JobModifierQueries.Has<ExtraReagentModifier>(ch));

        /// <summary>기본 개수 + 추가 시약 (순수 — 자가 테스트 대상).</summary>
        public static int TileCountFor(int baseCount, bool extraReagent)
            => Mathf.Max(0, baseCount) + (extraReagent ? ExtraReagentModifier.ExtraTiles : 0);
```

그리고 두 곳을 바꾼다:

```csharp
            return $"전투 시작 시 시약 타일 {reagentTileCount}개를 설치합니다. "
```
→
```csharp
            return $"전투 시작 시 시약 타일 {ReagentTileCount}개를 설치합니다. "
```

```csharp
            int count = Mathf.Clamp(reagentTileCount, 0, tiles.Count);
```
→
```csharp
            int count = Mathf.Clamp(ReagentTileCount, 0, tiles.Count);
```

- [ ] **Step 5: `ReagentTile` — 촉매 부여 도우미 + 시약 분배**

using 블록

```csharp
using DiceOrbit.Data.Passives;
```

를

```csharp
using System.Collections.Generic;
using DiceOrbit.Data.Modifiers.Job;
using DiceOrbit.Data.Passives;
```

로, `TryCatalyze` 전체

```csharp
        private void TryCatalyze(Core.Character character)
        {
            if (character == null || !character.IsAlive || character.StatusEffects == null) return;

            int percent = ownerPassive != null ? ownerPassive.CatalystPercent : 25;
            character.StatusEffects.AddEffect(
                Systems.Effects.StatusEffectManager.CreateEffect(EffectType.Catalyst, percent, -1));

            var data = UI.TooltipKeywordFormatter.BuildStatusDisplayData(EffectType.Catalyst.ToString(), percent, -1);
            UI.CombatNotifier.NotifyStatus(character, data.Name, data.Color);
            // 타일은 제거하지 않는다 — 영구 유지가 이 타일의 정체성이다.
        }
```

를

```csharp
        private void TryCatalyze(Core.Character character)
        {
            if (character == null || !character.IsAlive || character.StatusEffects == null) return;

            int percent = ownerPassive != null ? ownerPassive.CatalystPercent : 25;
            GrantCatalyst(character, percent);
            // 타일은 제거하지 않는다 — 영구 유지가 이 타일의 정체성이다.

            ShareCatalyst(character, percent);
        }

        private static void GrantCatalyst(Core.Character character, int percent)
        {
            character.StatusEffects.AddEffect(
                Systems.Effects.StatusEffectManager.CreateEffect(EffectType.Catalyst, percent, -1));

            var data = UI.TooltipKeywordFormatter.BuildStatusDisplayData(EffectType.Catalyst.ToString(), percent, -1);
            UI.CombatNotifier.NotifyStatus(character, data.Name, data.Color);
        }

        /// <summary>
        /// [시약 분배] (2026-10-09 C) — 타일 주인 연금술사가 살아 있고 시약 분배를 장착했으면, 이 타일 구역의
        /// 무작위 다른 아군 1명(이미 촉매 보유 제외)에게도 같은 수치의 촉매. 분배받은 촉매는 타일을 거치지 않아 다시 분배되지 않는다.
        /// </summary>
        private void ShareCatalyst(Core.Character receiver, int percent)
        {
            var alchemist = ownerPassive != null ? ownerPassive.OwnerCharacter : null;
            if (alchemist == null || !alchemist.IsAlive) return;
            if (!JobModifierQueries.Has<ReagentShareModifier>(alchemist)) return;

            var zones = Core.Zones.CombatZoneManager.Instance;
            var party = Core.PartyManager.Instance;
            if (zones == null || party == null || Owner == null) return;

            int zone = zones.GetZoneOfTile(Owner);
            if (zone < 0) return;

            var sameZone = new List<Core.Character>();
            foreach (var ally in party.GetAliveCharacters())
                if (ally != null && zones.GetZoneOf(ally) == zone) sameZone.Add(ally);

            var recipient = ReagentShareModifier.PickRecipient(sameZone, receiver);
            if (recipient != null) GrantCatalyst(recipient, percent);
        }
```

로 바꾼다.

- [ ] **Step 6: 레지스트리 등록**

`ModifierRegistry.cs`에서

```csharp
            // 연금술사
            () => new Job.HighPurityCatalystModifier(),
```

를

```csharp
            // 연금술사
            () => new Job.ExtraReagentModifier(),
            () => new Job.HighPurityCatalystModifier(),
            () => new Job.ReagentShareModifier(),
            () => new Job.WeakeningGasModifier(),
```

로 바꾼다.

- [ ] **Step 7: 컴파일 PASS 확인** — Run: 헤드리스 컴파일. Expected: `RESULT PASS`.

- [ ] **Step 8: 테스트 실행** — `Run Job Modifier Self-Tests`. **pending user run.**

- [ ] **Step 9: 커밋**

```bash
git add Assets/Scripts/Data/Modifiers/Job/AlchemistModifiers.cs \
  "Assets/Scripts/Data/Character Preset/Alchemist/ReagentPrepPassive.cs" \
  "Assets/Scripts/Data/Character Preset/Alchemist/ReagentTile.cs" \
  Assets/Scripts/Data/Modifiers/ModifierRegistry.cs Assets/Scripts/Editor/JobModifierSelfTests.cs
git commit -m "$(cat <<'EOF'
feat(modifier): 연금술사 고유 — 추가 시약(4개)·시약 분배(같은 구역 아군 1명 촉매)·약화 가스(3단계 +15%, 전체 약화)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---
### Task 10: 레지스트리 33종·직업 제한·계열 검증 + 최종 감사 + 스펙 결과

**Files:**
- Modify: `Assets/Scripts/Editor/JobModifierSelfTests.cs`
- Modify: `Assets/Scripts/Data/Modifiers/ModifierFamily.cs` (요약 주석 3~4행)
- Modify: `Docs/superpowers/specs/2026-10-09-character-sheet-sync-C-design.md` (§3.2 아래 계열 합계 줄, 파일 끝)

**계열 집계 정정:** 스펙 §3.2 표의 개별 배정을 세면 고유 16종 = **위치 7**(견고한 방진·전우애·노련한 협공·회로 공명·마법의 땅·추가 시약·시약 분배) / **콤보 8**(대지의 룬·여분의 비수·일제 타격·독침·고전압·마나흡수·고순도 촉매·약화 가스) / 생존 1(앙갚음(전사))이다. 표 아래 합계 줄("위치 8 / 콤보 7", 전체 "위치 13 / 콤보 11")은 집계 오류다 — 사용자 결정(D2)인 표의 개별 배정을 따르고, 전체 = 위치 12 / 주사위 4 / 콤보 12 / 생존 5로 검증한다.

- [ ] **Step 1: 실패하는 테스트 작성**

`// [tests-run]` 위에:

```csharp
                TestRegistryHas33WithJobRules();
```

`// [tests-body]` 위에:

```csharp
        // ── 레지스트리 (D6) ───────────────────────────────────

        private static void TestRegistryHas33WithJobRules()
        {
            var all = ModifierRegistry.CreateAll();
            Check(all.Count == 33, $"공용 17 + 직업 고유 16 = 33종 (실제 {all.Count})");

            var jobs = new[] { JobIds.Warrior, JobIds.Rogue, JobIds.Mage, JobIds.Alchemist };
            var probes = new Dictionary<string, Character>();
            foreach (var id in jobs) probes[id] = NewCharacter("Reg" + id, id);

            var perJob = new Dictionary<string, int>();
            var perFamily = new Dictionary<ModifierFamily, int>();
            var jobFamily = new Dictionary<ModifierFamily, int>();
            var names = new HashSet<string>();

            foreach (var m in all)
            {
                string type = m.GetType().Name;
                Check(m.Family != ModifierFamily.None, $"{type}: 계열 지정됨");
                Check(m.Category == ModifierCategory.Generic, $"{type}: Generic");
                Check(names.Add(m.ModifierName), $"{type}: 표시 이름 중복 없음 ({m.ModifierName})");
                Check(!string.IsNullOrEmpty(m.Description), $"{type}: 설명 있음");
                Check(ModifierRegistry.Exists(type), $"{type}: 세이브 ID(타입명)로 조회됨");
                perFamily.TryGetValue(m.Family, out int f);
                perFamily[m.Family] = f + 1;

                if (!(m is JobModifier job)) continue;

                jobFamily.TryGetValue(m.Family, out int jf);
                jobFamily[m.Family] = jf + 1;

                int owners = 0;
                foreach (var id in jobs)
                {
                    bool own = JobIds.Label(id) == job.JobLabel;
                    if (own) owners++;
                    Check(job.CanApplyTo(probes[id]) == own,
                          $"{type}: {JobIds.Label(id)}에게 {(own ? "장착 가능" : "장착 불가")}");
                }
                Check(owners == 1, $"{type}: 정확히 한 직업 전용");
                perJob.TryGetValue(job.JobLabel, out int j);
                perJob[job.JobLabel] = j + 1;
            }

            foreach (var id in jobs)
            {
                perJob.TryGetValue(JobIds.Label(id), out int n);
                Check(n == 4, $"{JobIds.Label(id)} 고유 4종 (실제 {n})");
            }

            int Count(Dictionary<ModifierFamily, int> d, ModifierFamily fam) => d.TryGetValue(fam, out int v) ? v : 0;
            Check(Count(jobFamily, ModifierFamily.Position) == 7 && Count(jobFamily, ModifierFamily.Combo) == 8
                  && Count(jobFamily, ModifierFamily.Survival) == 1 && Count(jobFamily, ModifierFamily.Dice) == 0,
                  $"고유 계열 위치7/콤보8/생존1/주사위0 (실제 {Count(jobFamily, ModifierFamily.Position)}/{Count(jobFamily, ModifierFamily.Combo)}/{Count(jobFamily, ModifierFamily.Survival)}/{Count(jobFamily, ModifierFamily.Dice)})");
            Check(Count(perFamily, ModifierFamily.Position) == 12 && Count(perFamily, ModifierFamily.Dice) == 4
                  && Count(perFamily, ModifierFamily.Combo) == 12 && Count(perFamily, ModifierFamily.Survival) == 5,
                  $"전체 계열 위치12/주사위4/콤보12/생존5 (실제 {Count(perFamily, ModifierFamily.Position)}/{Count(perFamily, ModifierFamily.Dice)}/{Count(perFamily, ModifierFamily.Combo)}/{Count(perFamily, ModifierFamily.Survival)})");

            // 보상 3택 — 도적만 있는 파티에는 다른 직업 고유가 절대 제시되지 않는다
            var rogueParty = new List<Character> { probes[JobIds.Rogue] };
            bool onlyRogueOrCommon = true;
            foreach (var offer in ModifierRegistry.GetRandomChoicesForParty(rogueParty, 33))
                if (offer is JobModifier job && job.JobLabel != "도적") onlyRogueOrCommon = false;
            Check(onlyRogueOrCommon, "도적 파티 보상 풀 = 공용 + 도적 고유만");
            Check(ModifierRegistry.GetRandomChoicesForParty(rogueParty, 33).Count == 21, "도적 파티 보상 풀 크기 17 + 4 = 21");
        }
```

- [ ] **Step 2: 컴파일 + 레지스트리 수 확인**

Run: 헤드리스 컴파일. Expected: `RESULT PASS` (Task 2~9에서 16종이 모두 등록돼 있으면 테스트 코드는 바로 컴파일된다 — 실패 확인은 다음 명령의 개수로 대신한다).

```bash
grep -c "() => new" Assets/Scripts/Data/Modifiers/ModifierRegistry.cs
grep -n "() => new Job\." Assets/Scripts/Data/Modifiers/ModifierRegistry.cs
```

Expected: `33`, 그리고 `Job.` 16줄이 전사(견고한 방진·전우애·앙갚음(전사)·대지의 룬) → 도적(노련한 협공·여분의 비수·일제 타격·독침) → 마법사(고전압·회로 공명·마법의 땅·마나흡수) → 연금술사(추가 시약·고순도 촉매·시약 분배·약화 가스) 순서. 하나라도 빠졌으면 해당 태스크의 레지스트리 단계를 다시 한다.

- [ ] **Step 3: `ModifierFamily` 주석**

```csharp
    /// 공용 17종 = 위치 5 / 주사위 4 / 콤보 4 / 생존 4 (2026-10-09 B). 계열이 없는 모디파이어(None)는 보상 카드로 제시할 수 없다.
```

를

```csharp
    /// 공용 17종 = 위치 5 / 주사위 4 / 콤보 4 / 생존 4 (2026-10-09 B), 직업 고유 16종 = 위치 7 / 콤보 8 / 생존 1 (C)
    /// → 전체 33종 = 위치 12 / 주사위 4 / 콤보 12 / 생존 5. 계열이 없는 모디파이어(None)는 보상 카드로 제시할 수 없다.
```

로 바꾼다.

- [ ] **Step 4: 최종 감사**

```bash
# 1) enum 끝 추가 확인
grep -n "Vitality,\|MagicLand," Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/TileAttribute.cs
# 2) 새 파일 메타 전부 존재
for f in Assets/Scripts/Data/Modifiers/Job/*.cs Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/MagicLandTile.cs Assets/Scripts/Editor/JobModifierSelfTests.cs; do [ -f "$f.meta" ] || echo "MISSING META: $f"; done; [ -f Assets/Scripts/Data/Modifiers/Job.meta ] || echo "MISSING META: Job folder"
# 3) 미완 흔적 없음
grep -rn "TODO\|FIXME\|구현 예정\|NotImplemented" Assets/Scripts/Data/Modifiers/Job Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/MagicLandTile.cs || echo "clean"
# 4) DIM·new 숨김 금지 — 모디파이어 훅은 모두 override
grep -n "public void OnAttack\|public void OnTurnEvent\|public new " Assets/Scripts/Data/Modifiers/Job/*.cs || echo "no hidden hooks"
# 5) UnityEngine.Object에 ?. / ?? 사용 없음 (새 파일)
grep -n "CurrentTile?\.\|SourcePreset?\.\|Instance?\." Assets/Scripts/Data/Modifiers/Job/*.cs Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/MagicLandTile.cs || echo "no null-propagation on Unity objects"
# 6) BOM 없음
for f in Assets/Scripts/Data/Modifiers/Job/*.cs Assets/Scripts/Editor/JobModifierSelfTests.cs Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/MagicLandTile.cs; do head -c3 "$f" | od -An -tx1 | grep -q "ef bb bf" && echo "BOM: $f"; done; echo "bom check done"
# 7) 작업 트리 — 이 계획의 파일만 남아 있지 않은지(새 파일 미추적 여부)
git status --short Assets/Scripts Assets/Resources/UI Docs/superpowers
```

Expected: 1) `MagicLand,`가 마지막, 2) 출력 없음, 3) `clean`, 4) `no hidden hooks`, 5) 출력 없음 또는 메시지, 6) `bom check done`만, 7) 이 태스크에서 바꾼 파일만 표시.

- [ ] **Step 5: 스펙 — 계열 합계 정정 + 구현 결과 섹션**

`Docs/superpowers/specs/2026-10-09-character-sheet-sync-C-design.md`에서

```markdown
계열 합계(고유): 위치 8 / 콤보 7 / 생존 1 / 주사위 0. 전체(공용 포함): 위치 13 / 주사위 4 / 콤보 11 / 생존 5.
```

를

```markdown
계열 합계(고유): 위치 7 / 콤보 8 / 생존 1 / 주사위 0. 전체(공용 포함): 위치 12 / 주사위 4 / 콤보 12 / 생존 5. (2026-10-09 구현 시 정정 — 초안의 "위치 8 / 콤보 7"은 위 표의 개별 배정과 맞지 않는 집계 오류였다. 표의 배정을 따른다.)
```

로 바꾸고, 파일 끝에 다음 섹션을 붙인다. 커밋 목록은 아래 명령 출력으로 채운다(계획 커밋 다음부터 지금까지):

```bash
git log --oneline --reverse "$(git log --format=%h -1 --grep='docs(plan): 캐릭터 시트 동기화 C 구현 계획')..HEAD"
```

```markdown

## 6. 구현 결과 (2026-10-09)

### 커밋

(위 `git log` 출력 9줄 — Task 1~9 — 을 `- <해시> <제목>` 형식으로 붙인다. Task 10 커밋은 이 섹션을 담으므로 목록에 없다.)

### 검증 상태

- 헤드리스 컴파일: PASS (Task 1~10 각 단계)
- 에디터 자가 테스트: **사용자 실행 대기** — `DiceOrbit → Run Job Modifier Self-Tests`(신규), `Run Common Modifier Self-Tests`, `Run Reward Flow Self-Tests`, `Run Combat Redesign Self-Tests`, `Run ActionForecast Self-Tests`, `Run PassiveZone Self-Tests`
- 새 `.meta`(Job 폴더·JobModifier·StatusEnhancement·직업별 4파일·MagicLandTile·JobModifierSelfTests)는 직접 만들었다 — Unity가 다시 만들면 그 결과를 커밋한다.
- `TileAttributeVisualDatabase.asset`(LFS)에 손으로 `type: 19` 항목을 붙였다 — 인스펙터에서 항목이 보이는지 확인하고, 재직렬화 diff가 생기면 커밋한다.

### 구현 중 결정

- 앙갚음(전사) 라운드 판정은 TurnStart 훅 순서 대신 `CombatManager.TurnCount`(라운드 번호)로 한다 — 같은 TurnStart 방송 안의 중독 틱과 순서 의존이 없다.
- 앙갚음(전사)의 표시 이름은 "앙갚음(전사)" — 공용 [앙갚음]과 함께 장착할 수 있어 목록에서 구분한다.
- 시약 분배의 "같은 구역" = 촉매가 생긴 시약 타일의 구역. 타일 주인 연금술사가 살아 있을 때만 분배한다.
- 도적 3단계는 `ComboActiveSkill`에 주인을 받는 가상 오버로드(타격 수·배율·설명)를 추가하고 `AttackPlan.Source`로 전달 — 기존 시그니처·호출부는 그대로다.
- 수치 변경형(협공·방진)은 `max(기본, 강화값)` — 디자이너가 기본값을 강화값 이상으로 올려도 모디파이어가 손해가 되지 않는다.
- 마법의 땅 피해 보정은 타일이 아니라 모디파이어가 낸다(주인 전용, 행동 예고 `PretendAt`과 일치). 시각은 예리함 아이콘을 보라색으로 임시 사용.

### 플레이 확인 목록

1. 보상: 직업별 4종이 그 직업이 파티에 있을 때만 나오고, 카드 상태 줄에 "○○ 전용"이 붙는다(다른 직업 초상 선택 시 "장착 불가 · ○○ 전용").
2. 전사: 견고한 방진 구역 상태 "-15%/명", 전우애(같은 구역 아군 2명 이상) 공격력 +1이 정보 패널·피해에 반영되고 흩어지면 즉시 사라짐, 앙갚음(전사) — 체력을 잃은 다음 라운드에만 +15%, 대지의 룬 — 3단계 +15%·아군 방어도 8.
3. 도적: 노련한 협공 "+25%/명", 여분의 비수 3단계 5타, 일제 타격 3단계 1타(4→1 ×4.3 / 5→1 ×5.4)가 행동 예고·실제 피해·정보 패널에서 같음, 독침 — 중독 10 이상 대상에게 타격마다 중독 +1, 이후 3단계 2배에 포함.
4. 마법사: 고전압 — 감전 아이콘 40, 받는 피해 +40%, 마법사가 쓰러지면 25로 계산, 회로 공명(아군 구역 3개 이상), 마법의 땅 — 전투 시작 시 한 구역 타일에 보라색 표식, 그 위에서 공격 시 +15%, 전투 종료 후 사라짐, 마나흡수 — 3단계로 처치하면 충전 획득(충전 1 소비 뒤).
5. 연금술사: 추가 시약 4개, 고순도 촉매 — 촉매 40%(아이콘·예고·피해), 연금술사 사망 시 25로 계산, 시약 분배 — 같은 구역 다른 아군 1명에게 촉매(이미 가진 아군 제외), 약화 가스 — 3단계 +15%, 이후 모든 몬스터 약화 25%(아군 고양 유지).
```

- [ ] **Step 6: 테스트 실행** — `Run Job Modifier Self-Tests`(33종 검증 포함), `Run Reward Flow Self-Tests`(4계열 모두 1종 이상 — 그대로 통과해야 함), `Run Common Modifier Self-Tests`. **pending user run.**

- [ ] **Step 7: 커밋**

```bash
git add Assets/Scripts/Editor/JobModifierSelfTests.cs Assets/Scripts/Data/Modifiers/ModifierFamily.cs \
  Docs/superpowers/specs/2026-10-09-character-sheet-sync-C-design.md
git commit -m "$(cat <<'EOF'
test(modifier): 레지스트리 33종·직업 제한·계열 분포(전체 12/4/12/5) 자가 테스트 + 스펙 구현 결과

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

## 사용자 확인 항목 (스펙 §5)

1. 에디터 자가 테스트: `DiceOrbit → Run Job Modifier Self-Tests`(신규) / `Run Common Modifier Self-Tests` / `Run Reward Flow Self-Tests` / `Run Combat Redesign Self-Tests` / `Run ActionForecast Self-Tests` / `Run PassiveZone Self-Tests` 전부 통과. Unity가 새 `.meta`를 그대로 받아들이는지(재생성되면 커밋), `TileAttributeVisualDatabase` 인스펙터에 항목 19(MagicLand)가 보이는지.
2. 플레이: 스펙 §6 "플레이 확인 목록" 1~5.

## 결정 사항 (스펙 해석)

- **계열 집계:** 스펙 §3.2 표의 개별 배정(위치 7 / 콤보 8 / 생존 1)을 따른다. 표 아래 합계 줄(위치 8 / 콤보 7)은 집계 오류로 보고 Task 10에서 정정한다. 전체 = 위치 12 / 주사위 4 / 콤보 12 / 생존 5.
- **"○○ 전용" 표시:** `JobModifier.AppendJobTag`가 기존 상태 문구 뒤에 " · ○○ 전용"을 붙인다(보유 중/장착 불가/신규 모두). `RewardModifierCard`는 바꾸지 않는다(최소 변경).
- **조회 헬퍼 위치:** `JobModifierQueries`(정적 클래스). `AnyAliveHas<T>`는 파티 목록을 받는 순수 오버로드를 두어 매니저 없이 테스트한다.
- **상태 강화(D5):** 실효 %는 `StatusEnhancement.EffectivePercent`(감전 → 고전압, 촉매 → 고순도 촉매) 한 곳. 부여 시점 40은 `ShockStatus`/`CatalystStatus` 생성자에서 적용해 모든 부여 경로(마법사·시약 타일·시약 분배·향후 출처)가 자동으로 맞는다. 마법사 버블·설명, `ReagentPrepPassive.CatalystPercent`도 같은 값.
- **도적 3단계 배관:** 기존 `GetStageHits(stage)`를 바꾸지 않고 `GetStageHits(source, stage)`/`GetStageMultiplier(source, stage)`/`GetStageDescription(source, stage)` 가상 오버로드 + `AttackPlan.Source`. 실행 루프·행동 예고는 `plan.Hits`, 피해는 `CalculateStageDamage`(→ 배율 오버로드), 미리보기·정보 패널도 주인 기준. 일제 타격 배율은 타격 1회 배율에 곱한다(4→1: 100% × 4.3 = 430%).
- **수치 변경형:** 협공 25·방진 15는 `max(기본, 강화값)`, 시약 +1·대지의 룬 +3·여분의 비수 +1은 가산. 대지의 룬 방어도는 기본 방어도가 0(부여 꺼짐)이면 0.
- **앙갚음(전사):** 라운드 = `CombatManager.TurnCount`(손실 라운드 = 현재 − 1이면 활성). 스펙의 "TurnStart에 활성 확정 후 초기화"와 결과는 같고, 같은 TurnStart 방송의 중독 틱(OnPreAction, 같은 Priority 10 — 정렬 순서 미보장)과의 순서 문제가 없다. 손실 기록 = 주인이 대상인 `OnPostAction`에서 `IsEffected`(직접 손실 포함, 방어도만 감소 제외, 시뮬레이션 제외). 보정 = 행동 스코프 주체가 주인인 공격 행동(기본/강화)만. 표시 이름 "앙갚음(전사)"(공용 앙갚음과 동시 장착 가능해 구분).
- **독침:** "강화 공격" = `ComboStage ≥ 0`(1~3단계 모두). 조건 기록 = 행동 ID별 대상의 첫 `OnPreAction`(회피 판정 전), 부여 = 그 대상 타격의 `OnPostAction`(회피된 타격은 통지 없음 → 부여 없음), 쓰러진 대상 제외.
- **회로 공명:** "자신의 강화 공격" = 행동 스코프 주체가 마법사이고 `ComboStage ≥ 0`. 구역 수 = `MageCircuit.GetCircuitZones(mage).Count`.
- **마법의 땅:** 구역 무작위 = `Random.Range(0, ZoneCount)`(몬스터 유무 무관). 타일 위 판정은 주인의 `CurrentTile` — 다른 아군이 서도 효과 없음. 설치 직전 자기 것만 정리(`ClearFrom`), 전투 종료 시 전부 정리(`ClearAll`). 시각 항목은 예리함(`type: 10`) 아이콘 GUID를 보라색 틴트로 재사용.
- **마나흡수:** 처치 수 = B의 `AttackActionResult.Kills`(스코프 종료 직후, 마무리 효과·중독 틱 제외). 획득량 `min(처치, 3)`, 충전 상한 3은 `ChargeStatus.Grant`가 별도 보장.
- **시약 분배:** "같은 구역" = 촉매가 생긴 시약 타일의 구역. 받은 본인·촉매 보유·사망·상태 관리자 없는 아군 제외. 타일 주인 연금술사가 살아 있을 때만.
- **약화 가스:** 3단계 공격 행동 종료 통지(`ComboStage == 2`)에서 살아 있는 모든 몬스터에 약화 25%(라운드형 — B/A 규칙대로 몬스터 턴 종료에 제거). 주인이 공격 도중 쓰러졌으면 미발동.
- **자가 테스트 정리:** 상태 버블이 에디트 모드에서 만드는 `FloatingPopupQueue` 오브젝트도 테스트 끝에 파괴한다(기존 B 테스트엔 없는 정리).

