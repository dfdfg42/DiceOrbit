# 캐릭터 시트 동기화 B — 공용 모디파이어 17종 + 충전 (설계)

> 상태: 확정 (2026-10-09, 토론 결과) · 출처: `Dice Orbit-게임 컨텐츠 마스터 리스트_261003.xlsx`(이하 "시트")의 「모디파이어」 공용 17종, 「버프·디버프」의 충전·공통 규칙
> 선행: A(캐릭터 수치, `2026-10-09-character-sheet-sync-A-design.md`). 후속: C(직업 고유 16종 — 충전을 마나흡수가 사용)
> 브랜치: `feature/character-sheet-sync-b-20261009` (A 브랜치 위). 심사 잠금으로 로컬 커밋만.

## 1. 목표

공용 모디파이어를 현재 12종에서 시트의 17종으로 개편하고, 시트의 장착 규칙(같은 종류 중복 불가)과 충전 자원, 캐릭터 일시 방어도의 턴 초기화를 도입한다.

## 2. 확정된 결정

| # | 결정 | 근거 |
|---|---|---|
| D1 | 같은 종류는 캐릭터당 1개만 장착. 장착 수 제한 없음. 3중첩(`StackCount`/`IsPrimary`) 제거 | 시트 장착 규칙 "같은 종류 중복 장착 불가" |
| D2 | 캐릭터 일시 방어도는 **플레이어 턴 시작 시 0** | 사용자 결정. 현재 캐릭터 방어도는 초기화 코드가 없어 무한 누적(UnitStats TODO). 시트 "일시 방어도"·마탄사수 패시브가 턴 소멸을 전제. 몬스터(자기 턴 시작 시 0)와 대칭 |
| D3 | 충전은 상태이상 `ChargeStatus`로 구현 (새 `EffectType.Charge`는 enum **끝에** 추가) | 상태 아이콘 줄에 자동 노출, 파이프라인 계산 재사용, 확장 시스템에 얹는 선호 원칙 |
| D4 | 충전: 최대 3스택(획득 경로 합계), 스택당 강화 공격 피해 +15%, 강화 공격 행동을 마친 뒤 1스택 소비 | 시트 「버프·디버프」 충전 |
| D5 | 보상 카드 계열: 기존 4계열·아이콘 유지. 위치 5 / 주사위 4 / 콤보 4 / 생존 4 (§3.3) | 사용자 결정. 새 아트 불필요 |
| D6 | 기본기 단련은 **유효 공격력**(기본 공격력 + 모디파이어 보너스)으로 계산. 저장 필드를 바꾸지 않는다 | 공격력은 세이브에 저장되지 않고 프리셋에서 재유도 → 필드를 바꾸면 이어하기 시 소실 |
| D7 | 완강한 신체·사냥의 최대 체력 증가는 **획득 시 1회 / 처치 시** 실제 `MaxHP`를 올린다. 이어하기 복원 경로에선 다시 적용하지 않는다 | `MaxHP`는 세이브에 저장됨. 복원 시 `Modifiers.Add`가 다시 불리므로 "획득"과 "복원"을 분리 |
| D8 | 새 사건은 `AutoAttackSystem`이 리스너 인터페이스로 통지 (DIM 미사용) | 기존 `IDiceUseListener` 패턴. 2026-07-21 죽은 훅 사고 재발 방지 |
| D9 | 삭제된 7종을 장착한 옛 세이브는 복원 검증 실패 → 새 게임 폴백 | 개발 중. 별칭 매핑은 YAGNI |

### 미기재 항목에 대한 결정 (시트 "미기재")

| 항목 | 결정 |
|---|---|
| 독 바른 무기 — 광역 공격 시 | 공격한 대상마다 중독 1. 공격 종료 시점에 죽은 대상은 건너뜀 |
| 앙갚음 — 적의 다단 공격 | 적 공격 행동 1회당 최대 1번 충전 (행동 ID로 중복 방지) |
| 구조대 — 현재 체력 동률 | 동률 아군 중 무작위 1명 (시트 명시) |
| 균열 — 대상이 여럿 | "첫 대상" = 행동에서 처음 피해 계산에 들어온 대상 1명 |

## 3. 변경 내용

### 3.1 공통 기반

- `CharacterModifier`
  - 새 virtual `int AttackBonus => 0` (기본기 단련이 2를 반환)
  - 새 virtual `void OnAcquired(Character owner) { }` — 보상/상점/디버그 획득 경로에서만 호출. 복원(세이브) 경로에선 호출 안 함
  - `CanApplyTo` 기본 구현을 "같은 타입을 이미 갖고 있지 않으면 true"로 (D1). 직업 전용 등 추가 조건은 자식이 `base.CanApplyTo(c) && …`로 확장
- `StackableCommonModifier` → `CommonModifier`로 단순화: `StackCount`/`IsPrimary`/`MaxStacks` 제거, `GrantArmor`·`Category => Generic` 유지. 클래스 이름 변경은 SerializeReference 대상이 아니므로 안전(모디파이어는 세이브에 타입명으로만 저장되고 이 베이스 이름은 저장되지 않음)
- `ModifierManager`: 획득용 `Acquire(mod)` 추가 = `Add(mod)` + `mod.OnAcquired(owner)`. 보상(`RewardUI`)·상점(`ShopUI`)·디버그(`BattleStageDebugManager`)는 `Acquire`, 세이브 복원(`PartyManager.Apply`)은 `Add` 유지
- `CharacterStats`: `int EffectiveAttack` = `Attack` + 장착 모디파이어 `AttackBonus` 합. 읽는 곳 4곳을 교체 — 기본공격(`AutoAttackSystem`), 콤보 단계 피해(`ComboActiveSkill.CalculateStageDamage`), 행동 예고(`ActionForecaster`), 정보 패널(`UnitInfoBuilder`)
- 캐릭터 일시 방어도 초기화(D2): 캐릭터 `TurnStart` 사건에서 `TempArmor = 0`. 현재 `UnitStats.OnTurnEvent`의 몬스터 전용 초기화 옆에서 소유자 확인(`SourceUnit == Owner`)과 함께 처리 — TODO 주석 해소

### 3.2 충전 (`ChargeStatus`)

- 위치: `Combat/Effects/`에 새 파일 `ChargeStatus.cs` (상태이상 골격과 같은 폴더)
- `EffectType.Charge`를 enum **맨 끝**에 추가 (정수 직렬화 보호), `StatusEffectManager.CreateEffect`에 케이스 추가
- 스택형(Value = 스택 수), Duration -1
- `public const int MaxStacks = 3`, `public const float PercentPerStack = 15f`
- `static void Grant(Unit unit, int stacks)`: 기존 스택과 합쳐 3을 넘지 않게 부여 (상한은 이 진입점 한 곳에서 보장). 획득 알림 버블
- `static void ConsumeOne(Unit unit)`: 1 감소, 0이면 제거
- `OnAttack(OnCalculateOutput)`: `SourceUnit == Owner`, `!IsDirectHpLoss`, `AttackActionScope.IsActive && CurrentInfo.ComboStage >= 0`(강화 공격)일 때 `OutputValue *= 1 + 0.15 × Value`. 시뮬레이션에서도 같은 보정(부수효과 없음)
- 소비: `AutoAttackSystem`의 강화 공격 완료 지점(스코프 종료·`OnStageCompleted` 직후)에서 `ChargeStatus.ConsumeOne(character)` — 마나흡수(C)의 "소비 후 획득" 순서가 여기에 맞물린다
- 전투 시작 시 이전 전투의 충전은 남기지 않는다: `CombatManager.StartCombat`에서 파티의 충전 제거 (사전 준비의 전투 시작 충전은 그 뒤 `CombatStart` 사건으로 들어오므로, 제거는 사건 방송보다 **먼저** 해야 한다 — 구현 시 순서 확인)
- 키워드 사전 `TooltipKeywordDatabase`에 "충전" 항목, `TooltipKeywordFormatter` 이름 매핑 추가: "강화 공격의 피해가 스택당 15% 증가합니다. 강화 공격을 마친 뒤 1스택을 소비합니다. 최대 3스택."

### 3.3 17종 (계열 · 동작)

| 계열 | 이름 (클래스) | 동작 | 구현 지점 |
|---|---|---|---|
| 위치 | 합류 전술 (`JointTacticsModifier`, 유지) | 공격할 때 같은 구역에 다른 살아 있는 아군 → 피해 +15% | OnAttack 계산 |
| 위치 | 독립 행동 (`LoneWolfModifier`, 유지) | 같은 구역에 다른 아군 없음 → +15% | OnAttack 계산 |
| 위치 | 듀얼 (`DuelModifier`, 신규) | 공격 대상의 양옆 구역에 다른 살아 있는 몬스터가 없으면 +15% (같은 구역 몬스터는 보지 않음) | OnAttack 계산 |
| 위치 | 기동 장갑 (`MobileArmorModifier`, 신규) | 다른 구역으로 이동을 마치면 방어도 4. 공격할 몬스터가 없어도 발동 | 이동 완료 리스너 |
| 위치 | 구조대 (`RescueTeamModifier`, 신규) | 행동(이동만 해도 포함)을 마친 뒤 같은 구역에서 현재 체력이 가장 낮은 아군 1명(자신 포함, 동률 무작위) 체력 +2. 최대 체력 초과 회복 없음, 최대 체력이면 대상에서 제외 | 행동 종료 리스너, 회복은 `HealContext`로 파이프라인 경유 |
| 주사위 | 저속 방호 (`LowRollGuardModifier`, 유지) | 눈 3 이하 사용 시 방어도 4 | 주사위 사용 리스너 (기존) |
| 주사위 | 예비 충전 (`ReserveChargeModifier`, 신규) | 사용한 주사위가 강화 공격 조건을 못 맞추면 충전 1 (기본공격 실행 여부 무관) | 조건 실패 리스너 |
| 주사위 | 사전 준비 (`PreparationModifier`, 신규) | 전투 시작 시 충전 1 | OnTurnEvent(CombatStart) |
| 주사위 | 앙갚음 (`PaybackModifier`, 재작성) | 적의 공격으로 체력을 잃으면 충전 1. 방어도만 깎이면 미발동. 적 공격 행동당 1회 | OnAttack(OnPostAction) `Target == owner && SourceUnit is Monster && IsEffected && !IsDirectHpLoss`, 행동 ID 중복 방지 |
| 콤보 | 대단원 (`GrandFinaleModifier`, 유지) | 3단계 강화 공격 피해 +25%, 그 공격을 마친 뒤 방어도 4 (3단계가 실제 실행돼야 함) | OnAttack 계산 + 공격 행동 종료 리스너 |
| 콤보 | 균열 (`FractureModifier`, 신규) | 공격 전 첫 대상의 일시 방어도 4 제거(행동당 1회). 처음부터 방어도가 없었으면 그 대상에 대한 피해 +15%. 이 효과로 0이 된 경우는 +15% 없음 | OnAttack(OnPreAction)에서 제거·기록(행동 ID), OnCalculateOutput에서 보정. 시뮬레이션은 현재 방어도 0 여부로 판정, 상태 변경 없음 |
| 콤보 | 독 바른 무기 (`PoisonedWeaponModifier`, 신규) | 공격을 마친 뒤 공격한 몬스터마다 중독 1 | 공격 행동 종료 리스너 |
| 콤보 | 기본기 단련 (`BasicTrainingModifier`, 신규) | 기본 공격력 +2 | `AttackBonus => 2` |
| 생존 | 공방 일체 (`OffenseDefenseModifier`, 신규) | 공격 행동을 마친 뒤 방어도 3 | 공격 행동 종료 리스너 |
| 생존 | 완강한 신체 (`SturdyBodyModifier`, 신규) | 최대 체력·현재 체력 +10 | `OnAcquired` |
| 생존 | 자연 치유 (`NaturalHealingModifier`, 신규) | 플레이어 턴 시작 시 체력 +2 (최대 체력 초과 없음) | OnTurnEvent(TurnStart, 자기 턴, 한 트리거만) |
| 생존 | 사냥 (`HuntModifier`, 신규) | 공격 행동을 마친 뒤, 그 행동으로 처치한 몬스터 1명당 최대·현재 체력 +2 (영구). 유물·디버프 피해 처치 제외 | 공격 행동 종료 리스너(처치 수) |

삭제: 경계 돌파(`ZoneCrossModifier`), 관성 타격(`MomentumStrikeModifier`), 극점 공명(`ExtremeResonanceModifier`), 안전장치(`SafetyNetModifier`), 연쇄 반응(`ChainReactionModifier`), 선제 방벽(`OpeningBarrierModifier`), 불굴(`UnyieldingModifier`). `IComboBreakListener`는 안전장치 전용이었으므로 함께 제거.

### 3.4 새 통지 지점 (`AutoAttackSystem`)

리스너 인터페이스(각 1메서드)를 `Data/Modifiers/Common/`의 기존 리스너 옆에 둔다. `AutoAttackSystem`은 캐릭터의 장착 모디파이어 중 해당 인터페이스를 구현한 것에 통지한다.

| 인터페이스 | 시점 | 인자 |
|---|---|---|
| `IDiceUseListener` (기존) | 이동+공격 처리 시작 | 캐릭터, 눈 |
| `IMoveFinishedListener` | 이동을 마친 직후(공격 전) | 캐릭터, 출발 구역, 도착 구역 |
| `IComboGateFailedListener` | 강화 조건 실패 판정 직후 | 캐릭터 |
| `IAttackActionFinishedListener` | 공격 행동(기본/강화)이 끝난 뒤, 스코프 종료 후 | 캐릭터, 결과(강화 단계 또는 -1, 공격한 대상 목록, 처치 수) |
| `IActionFinishedListener` | 이동+공격 처리 전체가 끝난 뒤(공격이 없어도) | 캐릭터 |

처치 수: 공격 행동 동안 `OnHit`/`OnPostAction`에서 `SourceUnit == 캐릭터 && IsEffected && !Target.IsAlive`인 대상을 행동 ID 기준으로 모은다(파이프라인 수정 없이 `AutoAttackSystem`이 행동 동안의 대상 생존 여부를 전후 비교하는 방식도 가능 — 계획 단계에서 단순한 쪽 선택).

통지 순서(강화 공격): 스코프 종료 → `OnStageCompleted` → **충전 1 소비** → `IAttackActionFinishedListener` → 콤보 진행 보고 → … → `IActionFinishedListener`.

### 3.5 레지스트리·보상·세이브

- `ModifierRegistry.Factories`를 17종으로 교체
- 각 모디파이어 `Family`를 §3.3대로
- `Editor/RewardFlowSelfTests.cs`의 `TestEveryRegistryModifierHasFamilyAndLabel`: "계열당 정확히 3종" → "모든 레지스트리 모디파이어가 None이 아닌 4계열 중 하나, 4계열 모두 1종 이상"
- 보상 카드의 중첩 표시(`CountOn` 핍): 중복 불가가 되므로 보유 여부만 의미 있음 — 최소 변경으로 동작 유지(0/1)
- 세이브: 저장 형식 변경 없음. 삭제된 타입명은 검증 실패(D9)

## 4. 범위 밖

- 직업 고유 16종, 상태 강화 공통 규칙(고전압·고순도 촉매 등) — C
- 행동 예고에 방어도·회복·충전 획득 표시 (행동 종료 후 효과라 원래 예고 대상 아님)
- 시트 "검토 보류"·다른 미기재 규칙

## 5. 검증

1. 헤드리스 컴파일 (scratchpad `compile_check.py`)
2. 에디터 자가 테스트 (사용자 실행)
   - 충전: Grant가 3을 넘지 않음, ConsumeOne 후 0이면 제거, 강화 공격에만 +15%/스택
   - 장착: 같은 종류 두 번째는 CanApplyTo false
   - 방어도: 캐릭터 TurnStart 후 TempArmor 0
   - 유효 공격력: 기본기 단련 장착 시 +2, 미장착 시 기본값
   - 레지스트리: 17종, 모두 계열 있음
3. 사용자 플레이 확인
   - 합류/독립/듀얼 +15%가 피해 숫자·행동 예고에 반영
   - 예비 충전·사전 준비·앙갚음으로 충전이 쌓이고(최대 3) 강화 공격 후 1 줄어듦, 아이콘 표시
   - 기동 장갑·저속 방호·공방 일체·대단원 방어도가 다음 플레이어 턴 시작에 사라짐
   - 독 바른 무기 중독 1, 구조대 회복 2, 사냥 처치 시 최대 체력 +2, 완강한 신체 +10이 이어하기 후에도 두 번 적용되지 않음

## 6. 구현 결과 (2026-10-09)

구현 커밋 (`git log --oneline 7208a4e2..HEAD`, 오래된 순):

- `b7a89263` feat(status): 충전(ChargeStatus) — 최대 3스택, 강화 공격 +15%/스택, EffectType.Charge(끝에 추가) + 키워드 사전
- `44e4c7ee` feat(combat): 캐릭터 일시 방어도를 플레이어 턴 시작에 0으로 — Stats.Owner 주입, UnitStats TODO 해소
- `a264070d` feat(modifier): 베이스 확장 — AttackBonus·OnAcquired·같은 종류 중복 금지, Acquire/Add 분리, 유효 공격력 4곳 반영
- `9fec6d22` feat(combat): 자동공격 새 통지 — 이동 완료·강화 조건 실패·공격 행동 종료(처치 수)·행동 종료, 강화 공격 후 충전 1 소비
- `0648b289` refactor(modifier): 3중첩 제거 — CommonModifier, 7종·콤보 끊김 통지 삭제, 합류/독립 15%·저속 방호 4·대단원 25%, 앙갚음=충전, 공방 일체
- `34e41492` feat(modifier): 보상·상점·디버그 획득은 Acquire(OnAcquired 1회), 세이브 복원은 Add 유지
- `65a91538` feat(modifier): 위치 계열 — 듀얼(대상 양옆 구역 비면 +15%)·기동 장갑(구역 이동 시 방어도 4)·구조대(같은 구역 최저 체력 +2), GetZoneOfMonster
- `b146bbca` feat(modifier): 주사위 계열 — 예비 충전(조건 실패 시 충전 1)·사전 준비(전투 시작 충전 1), 충전은 방송 전·전투 종료 시 제거
- `9939a010` feat(modifier): 콤보 계열 — 대단원 방어도 4, 균열(첫 대상 방어도 -4/무방어 +15%)·독 바른 무기(대상마다 중독 1)·기본기 단련(공격력 +2)
- `8798136d` feat(modifier): 생존 계열 — 완강한 신체(획득 시 최대 체력 +10)·자연 치유(턴 시작 +2)·사냥(처치당 최대 체력 +2)
- `19281e8a` test(modifier): 레지스트리 17종·계열 분포 5/4/4/4·삭제 7종 부재 자가 테스트

검증 상태

- 헤드리스 컴파일: PASS (errors 0)
- 에디터 자가 테스트 3종(Common Modifier · Reward Flow · Combat Redesign): 사용자 실행 대기 (pending user run). 메뉴 `DiceOrbit → Run Common Modifier Self-Tests` 등
- 최종 감사 grep: 삭제된 7종 이름·`IComboBreakListener`·`StackableCommonModifier`·`StackCount(`·`IsPrimary(`는 테스트 파일의 삭제 확인 문자열을 제외하면 코드에 없음. 캐릭터 피해 경로는 전부 `EffectiveAttack`. `EffectType.Charge`는 enum 끝

사용자 플레이 확인 목록 (§5-3)

1. 합류/독립/듀얼 +15%가 피해 숫자·행동 예고에 반영되는지
2. 예비 충전·사전 준비·앙갚음으로 충전이 쌓이고(최대 3) 강화 공격 후 1 줄어드는지, 아이콘이 표시되는지
3. 기동 장갑·저속 방호·공방 일체·대단원 방어도가 다음 플레이어 턴 시작에 사라지는지
4. 독 바른 무기 중독 1, 구조대 회복 2, 사냥 처치 시 최대 체력 +2가 동작하는지
5. 완강한 신체 +10이 이어하기(세이브 복원) 후에도 두 번 적용되지 않는지
6. 자연 치유가 플레이어 턴 시작에 체력 2를 회복하고 최대 체력을 넘지 않는지
