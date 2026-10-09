# 캐릭터 시트 동기화 A Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 현존 캐릭터 4종(전사·도적·마법사·연금술사)의 공격력·패시브·3단계 콤보 수치를 마스터 시트와 맞추고, 감전·고양·약화를 "다음 공격 1회"에서 "부여된 라운드 동안"으로 바꾼다.

**Architecture:** 새 추상 상태 `RoundStatus`(소모되지 않음)를 `OneShotActionStatus` 옆에 만들고 감전·고양·약화의 부모를 바꾼다. 제거는 `CombatManager`가 몬스터 턴 종료(타일 틱 직후)와 전투 시작에서 `StatusEffectManager.RemoveRoundStatuses()`로 일괄 처리한다. 수치는 프리셋 `.asset`(LFS, SerializeReference YAML)과 코드 기본값 두 곳을 함께 바꾼다.

**Tech Stack:** Unity 6000.3.8f1 · C# · Git LFS · Python 3(에셋 YAML 편집 도우미)

**Spec:** `Docs/superpowers/specs/2026-10-09-character-sheet-sync-A-design.md`

**규칙:** 심사 잠금 — 커밋은 로컬만. `git push` 금지. 커밋 메시지 끝에 `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

---

## 파일 구조

| 파일 | 변경 | 책임 |
|---|---|---|
| `Tools/asset_yaml_set.py` | 생성 | `.asset` YAML의 필드 한 줄을 안전하게 바꾸는 도우미 (rid 블록 또는 목록 항목 key로 위치 지정, 비ASCII는 `\uXXXX`로 기록) |
| `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Effects/ActionStatuses.cs` | 수정 | `RoundStatus` 추가, 감전·고양·약화 부모 변경 |
| `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Effects/StatusEffectManager.cs` | 수정 | 라운드형 재부여 시 큰 값 유지, `RemoveRoundStatuses()` |
| `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/CombatManager.cs` | 수정 | 라운드 종료·전투 시작에서 라운드형 상태 제거 |
| `Assets/Scripts/Data/Character Preset/Warrior/WarriorGreatswordActive.cs` + `Warrior.asset` | 수정 | 전사 공격력·콤보 |
| `Assets/Scripts/Data/Character Preset/Rogue/RogueAmbushActive.cs` + `RogueFlankPassive.cs` + `Rogue.asset` | 수정 | 도적 공격력·콤보·협공 |
| `Assets/Scripts/Data/Character Preset/Mage/MageEnergyBallActive.cs` + `Mage.asset` | 수정 | 마법사 콤보·감전 |
| `Assets/Scripts/Data/Character Preset/Alchemist/AlchemistThrowActive.cs` + `Alchemist.asset` | 수정 | 연금술사 공격력·콤보·고양 |
| `Assets/Resources/UI/TooltipKeywordDatabase.asset` | 수정 | 감전·고양·약화 키워드 설명 |
| `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Dices/DieEffect.cs` | 수정 | 집중 주사위(고양) 설명 |
| `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/EffectData.cs` | 수정 | enum 주석만 (값·순서 절대 변경 금지) |
| `Assets/Scripts/Core/Tutorial/TutorialScenario.cs` | 수정 | "협공 +30%" 문구 |
| `Assets/Scripts/Editor/CombatRedesignSelfTests.cs` | 수정 | 라운드형 상태 테스트 추가, 단계표·요약 단언 갱신 |

## 검증 도구

**자가 테스트 실행:** Unity 에디터 메뉴 `DiceOrbit → Run Combat Redesign Self-Tests`. Unity MCP가 연결돼 있으면 `Unity_RunCommand`로 `DiceOrbit.EditorTools.CombatRedesignSelfTests.RunAll()` 호출 후 `Unity_GetConsoleLogs`로 `[SelfTest]` 줄 확인. 둘 다 불가하면 그 단계는 "사용자 확인 필요"로 기록하고 다음 단계로 간다.

**헤드리스 컴파일** (PowerShell, 프로젝트 루트):

```powershell
$msbuild = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
$fp = "C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Data\MonoBleedingEdge\lib\mono\4.7.1-api"
& $msbuild "Assembly-CSharp-Editor.csproj" /t:Build /p:Configuration=Debug "/p:FrameworkPathOverride=$fp" /nologo /v:minimal /clp:NoSummary | Select-String ": error "
```

에러 줄이 없으면 통과. `CS0234`/`CS0246`이 새 파일에서만 나면 csproj가 낡은 것이니 Unity를 한 번 열어 재생성한다(이 계획은 새 `.cs` 파일을 만들지 않으므로 보통 해당 없음).

---

### Task 1: 에셋 YAML 편집 도우미

**Files:**
- Create: `Tools/asset_yaml_set.py`

- [ ] **Step 1: 도우미 작성**

```python
"""
.asset(Unity YAML) 필드 한 줄 교체 도우미.

사용:
  python Tools/asset_yaml_set.py <asset> --rid <rid> <field> <value>      # references.RefIds 항목의 data 안
  python Tools/asset_yaml_set.py <asset> --top <field> <value>            # MonoBehaviour 최상위 필드
  python Tools/asset_yaml_set.py <asset> --entry-key <키> <field> <value> # '- key: <키>' 목록 항목 안
  python Tools/asset_yaml_set.py <asset> --rename-rid <rid> <old> <new> <value>  # 필드 이름 변경 + 값

- 비ASCII 문자열은 "\\uXXXX" 이스케이프된 큰따옴표 문자열로 쓴다 (Unity가 쓰는 형식).
- 기존 값이 여러 줄(큰따옴표 줄바꿈 연속)이어도 연속 줄을 지우고 한 줄로 쓴다.
- 대상 필드를 못 찾으면 비정상 종료한다 (조용히 넘어가지 않는다).
"""
import re
import sys

BS = chr(92)


def esc(value: str) -> str:
    if re.fullmatch(r"-?\d+(\.\d+)?", value):
        return value
    out = []
    for ch in value:
        if ch == '"':
            out.append(BS + '"')
        elif ch == BS:
            out.append(BS + BS)
        elif ord(ch) < 128:
            out.append(ch)
        else:
            out.append(BS + "u%04X" % ord(ch))
    return '"' + "".join(out) + '"'


def decode(s: str) -> str:
    return re.sub(re.escape(BS) + "u([0-9A-Fa-f]{4})", lambda m: chr(int(m.group(1), 16)), s)


def indent_of(line: str) -> int:
    return len(line) - len(line.lstrip(" "))


def find_block(lines, mode, locator):
    """(start, end) 줄 범위 — 그 안에서 필드를 찾는다."""
    if mode == "top":
        for i, l in enumerate(lines):
            if l.startswith("  references:"):
                return 0, i
        return 0, len(lines)
    if mode == "rid":
        for i, l in enumerate(lines):
            if re.match(r"^\s*- rid: " + re.escape(locator) + r"\s*$", l):
                base = indent_of(l)
                j = i + 1
                while j < len(lines) and not (indent_of(lines[j]) <= base and lines[j].strip().startswith("- ")):
                    j += 1
                return i, j
        sys.exit(f"rid {locator} not found")
    if mode == "entry":
        for i, l in enumerate(lines):
            m = re.match(r"^(\s*)- key: (.*)$", l)
            if m and decode(m.group(2).strip().strip('"')) == locator:
                base = len(m.group(1))
                j = i + 1
                while j < len(lines) and not (indent_of(lines[j]) <= base and lines[j].strip().startswith("- ")):
                    j += 1
                return i, j
        sys.exit(f"entry key {locator} not found")
    sys.exit("bad mode")


def set_field(lines, start, end, field, new_field, value):
    pat = re.compile(r"^(\s*)" + re.escape(field) + r":( .*)?$")
    for i in range(start, end):
        m = pat.match(lines[i])
        if not m:
            continue
        ind = len(m.group(1))
        j = i + 1
        # 큰따옴표 값의 연속 줄 제거 (더 깊게 들여쓰기되고 'name:' 꼴이 아닌 줄)
        while j < end and indent_of(lines[j]) > ind and not re.match(r"^\s*[\w-]+:( |$)", lines[j]):
            j += 1
        lines[i:j] = [" " * ind + new_field + ": " + esc(value)]
        return
    sys.exit(f"field {field} not found in block")


def main():
    args = sys.argv[1:]
    path, mode = args[0], args[1]
    with open(path, encoding="utf-8", newline="") as f:
        text = f.read()
    nl = "\r\n" if "\r\n" in text else "\n"
    lines = text.split(nl)
    if mode == "--top":
        s, e = find_block(lines, "top", None); field, value = args[2], args[3]; new = field
    elif mode == "--rid":
        s, e = find_block(lines, "rid", args[2]); field, value = args[3], args[4]; new = field
    elif mode == "--entry-key":
        s, e = find_block(lines, "entry", args[2]); field, value = args[3], args[4]; new = field
    elif mode == "--rename-rid":
        s, e = find_block(lines, "rid", args[2]); field, new, value = args[3], args[4], args[5]
    else:
        sys.exit("bad mode")
    set_field(lines, s, e, field, new, value)
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(nl.join(lines))
    print(f"ok {path} {new}={value}")


if __name__ == "__main__":
    main()
```

- [ ] **Step 2: 읽기 전용 확인 (임시 복사본에서)**

```bash
cp "Assets/Scripts/Data/Character Preset/Warrior/Warrior.asset" /tmp/w.asset
python Tools/asset_yaml_set.py /tmp/w.asset --top Attack 5
python Tools/asset_yaml_set.py /tmp/w.asset --rid 6187543656838987855 stage1Multiplier 1.2
diff "Assets/Scripts/Data/Character Preset/Warrior/Warrior.asset" /tmp/w.asset
```

Expected: `diff`가 `Attack: 6 → 5`, `stage1Multiplier: 1.5 → 1.2` 두 줄만 보여 준다.

- [ ] **Step 3: Commit**

```bash
git add Tools/asset_yaml_set.py
git commit -m "chore(tools): .asset YAML 필드 교체 도우미 (rid/최상위/목록 key 지정)"
```

---

### Task 2: RoundStatus와 감전·고양·약화 전환

**Files:**
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Effects/ActionStatuses.cs`
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Effects/StatusEffectManager.cs:27-47` (AddEffect), 파일 끝에 메서드 추가
- Test: `Assets/Scripts/Editor/CombatRedesignSelfTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성** — `CombatRedesignSelfTests.cs`의 `RunAll()`에 `TestRoundStatuses();`를 `TestActionScopeBookkeeping();` 다음 줄에 추가하고, 클래스 안에 아래 메서드를 추가한다. 파일 상단 `using`에 `using DiceOrbit.Core.Pipeline;`와 `using DiceOrbit.Data;`가 없으면 추가한다.

```csharp
        private static void TestRoundStatuses()
        {
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

            Object.DestroyImmediate(attackerGo);
            Object.DestroyImmediate(otherGo);
            Object.DestroyImmediate(targetGo);
        }
```

- [ ] **Step 2: 테스트 실패 확인**

헤드리스 컴파일을 돌린다.
Expected: `error CS1061: 'StatusEffectManager'에 'RemoveRoundStatuses'에 대한 정의가 없습니다` (아직 구현 전이라 컴파일 실패 = 실패하는 테스트).

- [ ] **Step 3: `RoundStatus` 추가 + 세 상태 전환** — `ActionStatuses.cs`의 `OneShotActionStatus` 클래스 닫는 중괄호 바로 뒤에 추가:

```csharp
    /// <summary>
    /// '부여된 라운드 동안' 모든 해당 공격에 적용되는 상태의 공통 골격 (2026-10-09, 시트 "이번 턴 동안").
    /// 라운드 = 플레이어 턴 + 몬스터 턴. 소모되지 않으며, 제거는 CombatManager가
    /// 몬스터 턴 종료·전투 시작에 StatusEffectManager.RemoveRoundStatuses()로 일괄 처리한다.
    /// 직접 체력 손실(중독)엔 개입하지 않는다. 비중첩 — 재부여 시 큰 값을 유지한다(StatusEffectManager.AddEffect).
    /// </summary>
    public abstract class RoundStatus : StatusEffect
    {
        protected RoundStatus(EffectType type, int value)
            : base(type, value, -1, isStackable: false) { }

        /// <summary>이 컨텍스트에 개입하는가 (소스/대상 매칭은 자식이 정의).</summary>
        protected abstract bool AppliesTo(AttackContext context);

        /// <summary>수치 보정 (자식이 정의).</summary>
        protected abstract void Modify(AttackContext context);

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (Owner == null || context.IsDirectHpLoss) return;
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (!AppliesTo(context)) return;
            Modify(context);
        }
    }
```

그리고 세 클래스를 아래로 교체한다 (`CatalystStatus`는 그대로 둔다):

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

```csharp
    /// <summary>고양 — 이번 라운드 동안 이 유닛이 주는 공격 피해 +Value% (기본 25).</summary>
    public class InspireStatus : RoundStatus
    {
        public InspireStatus(int value) : base(EffectType.Inspire, value) { }

        protected override bool AppliesTo(AttackContext context)
            => context.SourceUnit == Owner;

        protected override void Modify(AttackContext context)
            => context.OutputValue *= 1f + Value / 100f;
    }

    /// <summary>약화 — 이번 라운드 동안 이 유닛이 주는 공격 피해 −Value% (기본 25).</summary>
    public class WeakenStatus : RoundStatus
    {
        public WeakenStatus(int value) : base(EffectType.Weaken, value) { }

        protected override bool AppliesTo(AttackContext context)
            => context.SourceUnit == Owner;

        protected override void Modify(AttackContext context)
            => context.OutputValue *= UnityEngine.Mathf.Max(0f, 1f - Value / 100f);
    }
```

파일 맨 위 `OneShotActionStatus` 요약 주석의 첫 줄을 `/// '다음 공격 행동' 1회에만 적용되는 1회성 상태의 공통 골격 (2026-08-28). 현재 사용처: 촉매.`로 바꾼다.

- [ ] **Step 4: `StatusEffectManager` 수정** — `AddEffect`의 기존 효과 분기를 아래로 교체:

```csharp
            if (activeEffects.ContainsKey(newEffect.Type))
            {
                var existing = activeEffects[newEffect.Type];
                if (existing is RoundStatus)
                    existing.Value = Mathf.Max(existing.Value, newEffect.Value);   // 비중첩: 큰 값 유지 (시트 미중첩 규칙)
                else
                    existing.AddStack(newEffect.Value);
                existing.RefreshDuration(newEffect.Duration);
                Debug.Log($"[Status] refreshed {newEffect.Type} to {name}");
            }
```

`HasEffect` 메서드 바로 뒤에 추가:

```csharp
        /// <summary>라운드형 상태(감전·고양·약화)를 전부 제거 — 라운드 종료·전투 시작에 CombatManager가 호출.</summary>
        public void RemoveRoundStatuses()
        {
            var keys = activeEffects.Where(kv => kv.Value is RoundStatus).Select(kv => kv.Key).ToList();
            foreach (var key in keys) RemoveEffect(key);
        }
```

`CreateEffect`의 `// '다음 공격 행동' 1회성 상태 4종 …` 주석을 `// 촉매(1회성) + 감전·고양·약화(라운드형) — duration 무시(-1 고정), 제거는 각 골격 규칙을 따른다`로 바꾼다.

- [ ] **Step 5: 컴파일 + 테스트 통과 확인**

헤드리스 컴파일 → 에러 없음. 자가 테스트 실행 → `[SelfTest]`에 FAIL이 `TestRoundStatuses` 관련으로 없을 것. (`TestStageTables`·`TestSelectionActiveSummaries`의 기존 단언은 아직 옛 값이라 통과 상태여야 한다 — 수치는 아직 안 바꿨다.)

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Effects/ActionStatuses.cs Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Effects/StatusEffectManager.cs Assets/Scripts/Editor/CombatRedesignSelfTests.cs
git commit -m "feat(status): 감전·고양·약화를 라운드형(RoundStatus)으로 — 소모 없이 라운드 동안 적용, 재부여는 큰 값 유지"
```

---

### Task 3: 라운드 종료·전투 시작에서 제거

**Files:**
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/CombatManager.cs` (`StartCombat` 약 230행, `EndMonsterTurnRoutine` 약 698-701행, `TileTurnEnd` 옆에 헬퍼)

- [ ] **Step 1: 헬퍼 추가** — `private void TileTurnEnd()` 메서드 바로 위에 추가:

```csharp
        /// <summary>
        /// 라운드형 상태(감전·고양·약화) 일괄 제거 — 시트 "이번 턴 동안" = 플레이어 턴 + 몬스터 턴.
        /// 캐릭터는 전투 사이에 살아남으므로 전투 시작에도 호출해 이전 전투의 고양이 새지 않게 한다.
        /// </summary>
        private void ExpireRoundStatuses()
        {
            var party = PartyManager.Instance;
            if (party != null && party.Party != null)
                foreach (var character in party.Party)
                    if (character != null && character.StatusEffects != null)
                        character.StatusEffects.RemoveRoundStatuses();

            foreach (var monster in activeMonsters.ToList())
                if (monster != null && monster.StatusEffects != null)
                    monster.StatusEffects.RemoveRoundStatuses();
        }
```

(`using System.Linq;`가 파일 상단에 없으면 추가.)

- [ ] **Step 2: 라운드 종료에서 호출** — `EndMonsterTurnRoutine`의

```csharp
            {
                TileTurnEnd(); // Loop back to player
            }
```

를

```csharp
            {
                TileTurnEnd(); // Loop back to player
                ExpireRoundStatuses();
            }
```

로 바꾼다.

- [ ] **Step 3: 전투 시작에서 호출** — `StartCombat`의 `Systems.Effects.AttackActionScope.ResetAll();` 바로 다음 줄(첫 번째 등장, 약 231행)에 `ExpireRoundStatuses();`를 추가한다.

- [ ] **Step 4: 컴파일 확인** — 헤드리스 컴파일, 에러 없음.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/CombatManager.cs
git commit -m "feat(combat): 몬스터 턴 종료·전투 시작에 라운드형 상태 일괄 제거"
```

---

### Task 4: 전사

**Files:**
- Modify: `Assets/Scripts/Data/Character Preset/Warrior/WarriorGreatswordActive.cs`
- Modify: `Assets/Scripts/Data/Character Preset/Warrior/Warrior.asset` (LFS)
- Test: `Assets/Scripts/Editor/CombatRedesignSelfTests.cs` (`TestStageTables`)

- [ ] **Step 1: 테스트 단언 갱신** — `TestStageTables`의 전사 단언을 교체:

```csharp
            var warrior = new WarriorGreatswordActive();
            Check(Mathf.Approximately(warrior.GetStageMultiplier(0), 1.2f)
               && Mathf.Approximately(warrior.GetStageMultiplier(1), 1.2f)
               && Mathf.Approximately(warrior.GetStageMultiplier(2), 1.5f), "전사 배율 120/120/150%");
            Check(warrior.GetStageHits(0) == 1 && warrior.GetStageHits(2) == 1, "전사는 단타");
            Check(warrior.GetStageName(0) == "대검 베기" && warrior.GetStageName(1) == "균열 베기", "전사 1·2단계 이름 (시트)");
```

- [ ] **Step 2: 실패 확인** — 자가 테스트 실행 → `전사 배율 120/120/150%` FAIL.

- [ ] **Step 3: 코드 기본값·이름·설명** — `WarriorGreatswordActive.cs`에서:
  - 클래스 요약 2번째 줄 → `/// 1단계 대검 베기(현재 구역) → 2단계 균열 베기(현+양옆) → 3단계 대지 가르기(전장 + 아군 방어도). 수치는 마스터 시트 261003.`
  - 필드: `stage1Multiplier = 1.2f`, `stage2Multiplier = 1.2f`, `stage3Multiplier = 1.5f`, `stage3AllyArmor = 5`
  - 툴팁: `"1단계 [대검 베기] 현재 구역의 몬스터"`, `"2단계 [균열 베기] 현재 구역과 양옆 구역의 몬스터"`
  - `GetStageName` → `stage switch { 0 => "대검 베기", 1 => "균열 베기", _ => "대지 가르기" }`
  - `GetStageDescription`:

```csharp
                0 => $"[대검 베기] 현재 구역의 몬스터에게 공격력의 {stage1Multiplier * 100f:0.#}%만큼 피해를 줍니다.",
                1 => $"[균열 베기] 현재 구역과 양옆 구역의 몬스터에게 공격력의 {stage2Multiplier * 100f:0.#}%만큼 피해를 줍니다.",
                _ => $"[대지 가르기] 모든 몬스터에게 공격력의 {stage3Multiplier * 100f:0.#}%만큼 피해를 주고 모든 아군에게 일시 방어도 {stage3AllyArmor}을 부여합니다.",
```

- [ ] **Step 4: 에셋** — 프로젝트 루트에서:

```bash
A="Assets/Scripts/Data/Character Preset/Warrior/Warrior.asset"
R=6187543656838987855
python Tools/asset_yaml_set.py "$A" --top Attack 5
python Tools/asset_yaml_set.py "$A" --rid $R stage1Multiplier 1.2
python Tools/asset_yaml_set.py "$A" --rid $R stage2Multiplier 1.2
python Tools/asset_yaml_set.py "$A" --rid $R stage3Multiplier 1.5
python Tools/asset_yaml_set.py "$A" --rid $R stage3AllyArmor 5
python Tools/asset_yaml_set.py "$A" --rid $R description "눈이 4 이상인 주사위를 연속으로 사용하면 대검 베기 → 균열 베기 → 대지 가르기 순서로 3단계 콤보가 이어집니다."
git diff --stat -- "$A"
```

Expected: 각 줄 `ok …` 출력, diff는 Warrior.asset 한 파일.

- [ ] **Step 5: 테스트 통과 + 컴파일** — 헤드리스 컴파일 에러 없음, 자가 테스트에서 전사 관련 FAIL 없음.

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scripts/Data/Character Preset/Warrior/WarriorGreatswordActive.cs" "Assets/Scripts/Data/Character Preset/Warrior/Warrior.asset" Assets/Scripts/Editor/CombatRedesignSelfTests.cs
git commit -m "feat(warrior): 시트 수치 반영 — 공격력 5, 대검 베기 120 / 균열 베기 120 / 대지 가르기 150 + 방어도 5"
```

---

### Task 5: 도적

**Files:**
- Modify: `Assets/Scripts/Data/Character Preset/Rogue/RogueAmbushActive.cs`
- Modify: `Assets/Scripts/Data/Character Preset/Rogue/RogueFlankPassive.cs:12,20-21`
- Modify: `Assets/Scripts/Data/Character Preset/Rogue/Rogue.asset` (LFS)
- Modify: `Assets/Scripts/Core/Tutorial/TutorialScenario.cs:198,201`
- Test: `Assets/Scripts/Editor/CombatRedesignSelfTests.cs` (`TestStageTables`, `TestSelectionActiveSummaries`)

- [ ] **Step 1: 테스트 단언 갱신** — `TestStageTables`의 도적 4줄을 교체:

```csharp
            var rogue = new RogueAmbushActive();
            Check(rogue.GetStageHits(0) == 2 && rogue.GetStageHits(1) == 3 && rogue.GetStageHits(2) == 4, "도적 타수 2/3/4");
            Check(Mathf.Approximately(rogue.GetStageMultiplier(0) * rogue.GetStageHits(0), 1.2f), "쌍비수 총 120%");
            Check(Mathf.Approximately(rogue.GetStageMultiplier(1) * rogue.GetStageHits(1), 2.25f), "맹독비수 총 225%");
            Check(Mathf.Approximately(rogue.GetStageMultiplier(2) * rogue.GetStageHits(2), 4.0f), "죽음의 촉매 총 400%");
```

`TestSelectionActiveSummaries`의 도적 케이스를 `new[] { "2회", "3회", "4회", "중독", "2배" }`로 바꾼다.

- [ ] **Step 2: 실패 확인** — 자가 테스트 → `도적 타수 2/3/4` FAIL.

- [ ] **Step 3: 콤보 코드** — `RogueAmbushActive.cs`:
  - 클래스 요약 2번째 줄 → `/// 1단계 쌍비수(2타) → 2단계 맹독비수(3타 후 중독 5) → 3단계 죽음의 촉매(4타 후 중독 2배, 소비 없음). 수치는 마스터 시트 261003.`
  - 필드: `stage1Multiplier = 0.6f`, `stage2Multiplier = 0.75f`, `stage3Multiplier = 1.0f`, `stage3Hits = 4`
  - `stage2PoisonPerHit` 필드를 아래로 교체 (이름 변경 — 에셋은 Step 5에서 같이 바꾼다):

```csharp
        [Tooltip("2단계 행동을 마친 뒤 대상에게 부여할 중독 중첩 (타격 수와 무관)")]
        [SerializeField] private int stage2Poison = 5;
```

  - `OnStageHitLanded` 메서드를 **삭제**하고, `OnStageCompleted`를 아래로 교체:

```csharp
        public override void OnStageCompleted(Character source, List<Unit> targets, int stage)
        {
            // 맹독비수 — 행동을 마친 뒤 대상마다 중독 (시트: 3회 공격 후 중독 5)
            if (stage == 1)
            {
                if (stage2Poison <= 0) return;
                foreach (var target in targets)
                    GrantStatus(target, EffectType.Poison, stage2Poison);
                return;
            }

            if (stage != StageCount - 1) return;

            // 죽음의 촉매 — 공격이 끝난 뒤 대상의 현재 중독을 2배로 (소비하지 않는다).
            // AddEffect는 기존 중첩에 합산하므로 '현재치만큼 추가' = 정확히 2배.
            foreach (var target in targets)
            {
                if (target == null || !target.IsAlive || target.StatusEffects == null) continue;

                int current = target.StatusEffects.GetEffectValue(EffectType.Poison);
                if (current <= 0) continue;

                target.StatusEffects.AddEffect(
                    Systems.Effects.StatusEffectManager.CreateEffect(EffectType.Poison, current, -1));
                UI.CombatNotifier.NotifyStatus(target, $"중독 {current * 2} (2배!)", new Color(0.55f, 0.9f, 0.35f));
            }
        }
```

  - `GetSelectionSummary` → `$"{FormatDiceConditionNounPhrase()}를 연속으로 사용하면 공격 횟수가 2회, 3회, 4회로 늘어나며 중독을 부여하고 마지막 공격으로 중독을 2배로 늘립니다."`
  - `GetStageDescription`의 1·2 줄:

```csharp
                1 => $"[맹독비수] 공격력의 {stage2Multiplier * 100f:0.#}%만큼 {Mathf.Max(1, stage2Hits)}회 피해를 주고 중독을 {stage2Poison} 부여합니다.",
                _ => $"[죽음의 촉매] 공격력의 {stage3Multiplier * 100f:0.#}%만큼 {Mathf.Max(1, stage3Hits)}회 피해를 준 뒤 대상의 중독을 2배로 늘립니다.",
```

- [ ] **Step 4: 협공** — `RogueFlankPassive.cs`: 요약 줄 → `/// 다른 아군 1명당 도적이 주는 공격 피해 +20% (1/2/3명 = +20/+40/+60%). 도적 자신은 제외.`, 툴팁 → `"같은 구역의 '다른 아군' 1명당 피해 증가율(%). 예: 20이면 3명일 때 +60%"`, 필드 → `perAllyBonusPercent = 20f`.

- [ ] **Step 5: 에셋**

```bash
A="Assets/Scripts/Data/Character Preset/Rogue/Rogue.asset"
R=6187543787217879113
P=7688013382886359193
python Tools/asset_yaml_set.py "$A" --top Attack 5
python Tools/asset_yaml_set.py "$A" --rid $R stage1Multiplier 0.6
python Tools/asset_yaml_set.py "$A" --rid $R stage2Multiplier 0.75
python Tools/asset_yaml_set.py "$A" --rename-rid $R stage2PoisonPerHit stage2Poison 5
python Tools/asset_yaml_set.py "$A" --rid $R stage3Multiplier 1
python Tools/asset_yaml_set.py "$A" --rid $R stage3Hits 4
python Tools/asset_yaml_set.py "$A" --rid $P perAllyBonusPercent 20
python Tools/asset_yaml_set.py "$A" --rid $P description "같은 구역에 있는 다른 아군 1명마다 도적이 주는 공격 피해가 20% 증가합니다."
```

Expected: 각 줄 `ok …`.

- [ ] **Step 6: 튜토리얼 문구** — `TutorialScenario.cs` 198행 `협공 +30%` → `협공 +20%`, 201행 `도적의 <b>협공</b> 피해는 30% 증가합니다` → `20% 증가합니다`.

- [ ] **Step 7: 테스트 통과 + 컴파일** — 헤드리스 컴파일 에러 없음, 자가 테스트에서 도적 관련 FAIL 없음. (`TestTutorial*`이 협공 수치 문자열을 단언하면 같이 20으로 맞춘다.)

- [ ] **Step 8: Commit**

```bash
git add "Assets/Scripts/Data/Character Preset/Rogue/RogueAmbushActive.cs" "Assets/Scripts/Data/Character Preset/Rogue/RogueFlankPassive.cs" "Assets/Scripts/Data/Character Preset/Rogue/Rogue.asset" Assets/Scripts/Core/Tutorial/TutorialScenario.cs Assets/Scripts/Editor/CombatRedesignSelfTests.cs
git commit -m "feat(rogue): 시트 수치 반영 — 공격력 5, 협공 20%, 쌍비수 60x2 / 맹독비수 75x3 후 중독 5 / 죽음의 촉매 100x4"
```

---

### Task 6: 마법사

**Files:**
- Modify: `Assets/Scripts/Data/Character Preset/Mage/MageEnergyBallActive.cs`
- Modify: `Assets/Scripts/Data/Character Preset/Mage/Mage.asset` (LFS)
- Test: `Assets/Scripts/Editor/CombatRedesignSelfTests.cs` (`TestStageTables`)

- [ ] **Step 1: 테스트 단언 갱신**

```csharp
            var mage = new MageEnergyBallActive();
            Check(Mathf.Approximately(mage.GetStageMultiplier(0), 1.2f)
               && Mathf.Approximately(mage.GetStageMultiplier(1), 1.2f)
               && Mathf.Approximately(mage.GetStageMultiplier(2), 1.5f), "마법사 배율 120/120/150%");
```

- [ ] **Step 2: 실패 확인** — 자가 테스트 → `마법사 배율 120/120/150%` FAIL.

- [ ] **Step 3: 코드** — `MageEnergyBallActive.cs`:
  - 클래스 요약 3번째 줄 → `/// 1단계 비전 화살(단일) → 2단계 연쇄 번개(2체+감전) → 3단계 궤도 붕괴(회로 전 구역+감전). 수치는 마스터 시트 261003.`
  - 필드: `stage1Multiplier = 1.2f`, `stage2Multiplier = 1.2f`, `stage3Multiplier = 1.5f`, `shockPercent = 25`
  - `shockPercent` 툴팁 → `"감전 수치 — 이번 라운드 동안 대상이 받는 공격 피해 +% (2·3단계 공통)"`
  - `OnStageHitLanded`:

```csharp
        public override void OnStageHitLanded(Character source, Unit target, int stage, int hitIndex)
        {
            // 연쇄 번개·궤도 붕괴 — 적중한 대상마다 감전 (이번 라운드 동안 받는 공격 피해 증가)
            if (stage >= 1 && shockPercent > 0)
                GrantStatus(target, EffectType.Shock, shockPercent);
        }
```

  - `GetSelectionSummary` → `$"{FormatDiceConditionNounPhrase()}를 연속으로 사용하면 단일 적, 가까운 적 2명, 마력 회로 전체 순서로 공격하며 두 번째와 마지막 공격은 감전을 부여합니다."`
  - `GetStageDescription`의 1·2 줄:

```csharp
                1 => $"[연쇄 번개] 회로 안에서 가장 가까운 몬스터 최대 {Mathf.Max(1, stage2MaxTargets)}명에게 공격력의 {stage2Multiplier * 100f:0.#}%만큼 피해를 주고 감전 {shockPercent}%를 부여합니다.",
                _ => $"[궤도 붕괴] 회로에 연결된 모든 구역의 몬스터에게 공격력의 {stage3Multiplier * 100f:0.#}%만큼 피해를 주고 감전 {shockPercent}%를 부여합니다.",
```

- [ ] **Step 4: 에셋**

```bash
A="Assets/Scripts/Data/Character Preset/Mage/Mage.asset"
R=6187543656838987853
python Tools/asset_yaml_set.py "$A" --rid $R stage1Multiplier 1.2
python Tools/asset_yaml_set.py "$A" --rid $R stage2Multiplier 1.2
python Tools/asset_yaml_set.py "$A" --rid $R stage3Multiplier 1.5
python Tools/asset_yaml_set.py "$A" --rid $R shockPercent 25
```

- [ ] **Step 5: 테스트 통과 + 컴파일** — 에러·마법사 FAIL 없음. (요약 테스트의 마법사 키워드 `"단일", "2명", "마력 회로 전체", "감전"`은 새 요약에도 모두 있다.)

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scripts/Data/Character Preset/Mage/MageEnergyBallActive.cs" "Assets/Scripts/Data/Character Preset/Mage/Mage.asset" Assets/Scripts/Editor/CombatRedesignSelfTests.cs
git commit -m "feat(mage): 시트 수치 반영 — 비전 화살 120 / 연쇄 번개 120 / 궤도 붕괴 150 + 감전, 감전 25%"
```

---

### Task 7: 연금술사

**Files:**
- Modify: `Assets/Scripts/Data/Character Preset/Alchemist/AlchemistThrowActive.cs`
- Modify: `Assets/Scripts/Data/Character Preset/Alchemist/Alchemist.asset` (LFS)
- Test: `Assets/Scripts/Editor/CombatRedesignSelfTests.cs` (`TestStageTables`, `TestSelectionActiveSummaries`)

- [ ] **Step 1: 테스트 단언 갱신**

```csharp
            var alch = new AlchemistThrowActive();
            Check(Mathf.Approximately(alch.GetStageMultiplier(0), 1.2f)
               && Mathf.Approximately(alch.GetStageMultiplier(1), 1.2f)
               && Mathf.Approximately(alch.GetStageMultiplier(2), 1.5f), "연금 배율 120/120/150%");
```

요약 테스트의 연금술사 케이스는 그대로 (`"단일", "인접", "전장 전체", "약화", "고양"` — 새 요약에도 모두 포함).

- [ ] **Step 2: 실패 확인** — 자가 테스트 → `연금 배율 120/120/150%` FAIL.

- [ ] **Step 3: 코드** — `AlchemistThrowActive.cs`:
  - 클래스 요약 2번째 줄 → `/// 1단계 산성 플라스크(단일) → 2단계 촉매 살포(현+양옆+약화) → 3단계 대연성: 현자의 폭탄(전장+아군 고양). 수치는 마스터 시트 261003.`
  - 필드: `stage1Multiplier = 1.2f`, `stage2Multiplier = 1.2f`, `stage3Multiplier = 1.5f`, `inspirePercent = 25`
  - 툴팁: stage1 → `"1단계 [산성 플라스크] 현재 구역의 몬스터"`, weaken → `"약화 수치 — 이번 라운드 동안 대상이 주는 공격 피해 -% (2단계)"`, inspire → `"고양 수치 — 3단계 후 아군 전원이 이번 라운드 동안 주는 공격 피해 +%"`
  - `OnStageHitLanded`:

```csharp
        public override void OnStageHitLanded(Character source, Unit target, int stage, int hitIndex)
        {
            // 촉매 살포 — 적중한 모든 대상에게 약화 (이번 라운드 동안 주는 공격 피해 감소)
            if (stage == 1 && weakenPercent > 0)
                GrantStatus(target, EffectType.Weaken, weakenPercent);
        }
```

  - `GetSelectionSummary` → `$"{FormatDiceConditionNounPhrase()}를 연속으로 사용하면 단일 적, 인접 구역, 전장 전체 순서로 공격하며 두 번째 공격은 약화를, 마지막 공격은 모든 아군에게 고양을 부여합니다."`
  - `GetStageDescription`의 0·1 줄:

```csharp
                0 => $"[산성 플라스크] 현재 구역의 몬스터에게 공격력의 {stage1Multiplier * 100f:0.#}%만큼 피해를 줍니다.",
                1 => $"[촉매 살포] 현재 구역과 양옆 구역의 몬스터에게 공격력의 {stage2Multiplier * 100f:0.#}%만큼 피해를 주고 약화 {weakenPercent}%를 부여합니다.",
```

- [ ] **Step 4: 에셋**

```bash
A="Assets/Scripts/Data/Character Preset/Alchemist/Alchemist.asset"
R=6187543656838987849
python Tools/asset_yaml_set.py "$A" --top Attack 5
python Tools/asset_yaml_set.py "$A" --rid $R stage1Multiplier 1.2
python Tools/asset_yaml_set.py "$A" --rid $R stage2Multiplier 1.2
python Tools/asset_yaml_set.py "$A" --rid $R stage3Multiplier 1.5
python Tools/asset_yaml_set.py "$A" --rid $R inspirePercent 25
```

- [ ] **Step 5: 테스트 통과 + 컴파일** — 에러·연금 FAIL 없음.

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scripts/Data/Character Preset/Alchemist/AlchemistThrowActive.cs" "Assets/Scripts/Data/Character Preset/Alchemist/Alchemist.asset" Assets/Scripts/Editor/CombatRedesignSelfTests.cs
git commit -m "feat(alchemist): 시트 수치 반영 — 공격력 5, 산성 플라스크 120(약화 없음) / 촉매 살포 120 + 약화 / 현자의 폭탄 150 + 고양 25%"
```

---

### Task 8: 플레이어에게 보이는 설명문

**Files:**
- Modify: `Assets/Resources/UI/TooltipKeywordDatabase.asset` (LFS)
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Dices/DieEffect.cs:65,80`
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/EffectData.cs:40,42,43` (주석만)

- [ ] **Step 1: 키워드 사전**

```bash
D="Assets/Resources/UI/TooltipKeywordDatabase.asset"
python Tools/asset_yaml_set.py "$D" --entry-key 감전 description "이번 턴 동안 받는 공격 피해가 25% 증가합니다. 디버프 피해에는 적용되지 않습니다."
python Tools/asset_yaml_set.py "$D" --entry-key 고양 description "이번 턴 동안 주는 공격 피해가 25% 증가합니다."
python Tools/asset_yaml_set.py "$D" --entry-key 약화 description "이번 턴 동안 주는 공격 피해가 25% 감소합니다."
```

Expected: 세 줄 모두 `ok …`. 이어서 ASCII 확인:

```bash
PYTHONIOENCODING=utf-8 python - <<'PY'
import re
BS = chr(92)
s = open("Assets/Resources/UI/TooltipKeywordDatabase.asset", encoding="utf-8").read()
d = re.sub(re.escape(BS) + "u([0-9A-Fa-f]{4})", lambda m: chr(int(m.group(1), 16)), s)
for k in ["감전", "고양", "약화"]:
    i = d.find('key: "' + k)
    print(k, "이번 턴" in d[i:i+200], "25%" in d[i:i+200])
PY
```

Expected: 세 줄 모두 `True True`.

- [ ] **Step 2: 집중 주사위 설명** — `DieEffect.cs`:
  - 65행 요약 → `/// <summary>사용 시 사용자에게 고양(이번 턴 동안 주는 공격 피해 +N%) 부여 (집중 주사위).</summary>`
  - 80행 `Preview()` → `=> $"사용 시 해당 캐릭터가 이번 턴 동안 주는 공격 피해가 {percent}% 증가합니다.";`

- [ ] **Step 3: enum 주석** — `EffectData.cs`의 주석만 교체 (값·순서·이름 변경 금지):

```csharp
        Shock,              // 감전: 이번 라운드 동안 받는 공격 피해 +V% (RoundStatus, 2026-10-09)
```
```csharp
        Inspire,            // 고양: 이번 라운드 동안 주는 공격 피해 +V% (RoundStatus, 2026-10-09)
        Weaken,             // 약화: 이번 라운드 동안 주는 공격 피해 -V% (RoundStatus, 2026-10-09)
```

- [ ] **Step 4: 컴파일 확인** — 에러 없음.

- [ ] **Step 5: Commit**

```bash
git add Assets/Resources/UI/TooltipKeywordDatabase.asset Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Dices/DieEffect.cs Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/EffectData.cs
git commit -m "docs(ui): 감전·고양·약화 설명을 '이번 턴 동안 25%'로 — 키워드 사전·집중 주사위·enum 주석"
```

---

### Task 9: 전체 검증과 정리

**Files:**
- Modify: `Docs/superpowers/specs/2026-10-09-character-sheet-sync-A-design.md` (구현 결과 절 추가)

- [ ] **Step 1: 에셋 값 일괄 대조**

```bash
PYTHONIOENCODING=utf-8 python - <<'PY'
import re
exp = {
 "Warrior": {"Attack":"5","stage1Multiplier":"1.2","stage2Multiplier":"1.2","stage3Multiplier":"1.5","stage3AllyArmor":"5"},
 "Rogue":   {"Attack":"5","stage1Multiplier":"0.6","stage2Multiplier":"0.75","stage2Poison":"5","stage3Multiplier":"1","stage3Hits":"4","perAllyBonusPercent":"20"},
 "Mage":    {"Attack":"5","stage1Multiplier":"1.2","stage2Multiplier":"1.2","stage3Multiplier":"1.5","shockPercent":"25"},
 "Alchemist":{"Attack":"5","stage1Multiplier":"1.2","stage2Multiplier":"1.2","stage3Multiplier":"1.5","inspirePercent":"25","weakenPercent":"25"},
}
for c, fields in exp.items():
    s = open(f"Assets/Scripts/Data/Character Preset/{c}/{c}.asset", encoding="utf-8").read()
    for k, v in fields.items():
        m = re.search(r"^\s*" + k + r": (.+)$", s, re.M)
        got = m.group(1).strip() if m else None
        print(("OK  " if got == v else "BAD ") + f"{c}.{k} = {got} (expected {v})")
    print("stage2PoisonPerHit left:", "stage2PoisonPerHit" in s)
PY
```

Expected: 모든 줄 `OK`, `stage2PoisonPerHit left: False`.

- [ ] **Step 2: 컴파일 + 자가 테스트 전체** — 헤드리스 컴파일 에러 없음. `CombatRedesignSelfTests.RunAll()` → `[SelfTest] 전체 PASS`. `ActionForecastSelfTests`도 있으면 실행(메뉴 `DiceOrbit` 아래) → PASS. 실패하면 단언이 1회성 소모 전제였는지 확인하고, 라운드형 규칙에 맞게 단언을 고친 뒤 이 단계를 다시 한다.

- [ ] **Step 3: 구현 결과 기록** — 스펙 문서 끝에 추가:

```markdown
## 7. 구현 결과 (2026-10-09)

- 커밋: (이 계획의 Task 1~8 커밋 해시 나열)
- 자가 테스트: CombatRedesignSelfTests 전체 PASS / ActionForecastSelfTests (결과 기입)
- 사용자 플레이 확인: 대기 — §5-4 체크리스트
```

해시는 `git log --oneline -9`로 확인해 실제 값을 적는다.

- [ ] **Step 4: Commit**

```bash
git add Docs/superpowers/specs/2026-10-09-character-sheet-sync-A-design.md
git commit -m "docs(spec): 캐릭터 시트 동기화 A 구현 결과 기록"
```

- [ ] **Step 5: 사용자에게 플레이 확인 요청** — 스펙 §5-4 체크리스트 4항목을 그대로 전달한다.
