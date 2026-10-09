# 캐릭터 시트 동기화 C — 직업 고유 모디파이어 16종 (설계)

> 상태: 확정 (2026-10-09, 토론 결과) · 출처: `Dice Orbit-게임 컨텐츠 마스터 리스트_261003.xlsx`(이하 "시트") 「모디파이어」 직업별 고유 중 현존 4직업(전사·도적·마법사·연금술사) 16종, 「버프·디버프」 공통 규칙(효과 강화·이전 턴)
> 선행: A(`2026-10-09-character-sheet-sync-A-design.md`), B(`2026-10-09-character-sheet-sync-B-design.md` — 충전·리스너·CommonModifier)
> 브랜치: `feature/character-sheet-sync-c-20261009` (B 브랜치 위). 심사 잠금으로 로컬 커밋만.

## 1. 목표

현존 4직업의 고유 모디파이어 16종을 구현해 보상 풀에 넣는다. 공용 17종과 같은 장착 규칙(같은 종류 1개)에 "해당 직업 전용"이 더해진다.

## 2. 확정된 결정

| # | 결정 | 근거 |
|---|---|---|
| D1 | 직업 판별 = `Stats.SourcePreset.SaveId` (`Warrior`/`Rogue`/`Mage`/`Alchemist`, 서수 비교). `CanApplyTo = base.CanApplyTo(c) && IsJob(c, id)` | 세이브에도 쓰이는 안정 ID (SaveIdValidator가 중복 검사) |
| D2 | 보상 카드 계열은 기존 4계열에 배정(§3.2), 카드 상태 줄에 "○○ 전용" 표시 | 사용자 결정. 새 아트·5번째 계열 불필요, RewardFlowSelfTests(4계열) 유지 |
| D3 | **수치 변경형**(견고한 방진·노련한 협공·추가 시약·여분의 비수·대지의 룬 방어도)은 해당 패시브/스킬이 주인 모디파이어를 조회해 값을 바꾼다(pull) | 값이 한 곳에서 계산돼 피해·행동 예고·툴팁 설명이 자동으로 일치 |
| D4 | **동작형**은 B의 리스너(`IAttackActionFinishedListener` 등)·파이프라인 반응(`OnAttack`/`OnTurnEvent`)·`CommonModifier` 헬퍼 사용. DIM 미사용 | B와 같은 패턴 |
| D5 | **상태 강화 공통 규칙**(고전압·고순도 촉매): 감전/촉매의 `Modify`가 계산할 때마다 "살아 있는 파티원 중 강화 모디파이어 보유자가 있나"를 조회해 실효 % = max(Value, 40). 부여 시점에도 강화 중이면 40으로 부여해 아이콘 수치를 맞춘다 | 시트 "부여 출처와 관계없이 적용, 강화한 캐릭터 사망 시 초기화" — 살아 있는 보유자만 조회하므로 사망 시 자동 해제. 재부여는 큰 값 유지(A의 D6)라 안전 |
| D6 | 직업 고유도 모디파이어 세이브 ID = 타입명(B와 동일). 레지스트리 = 공용 17 + 고유 16 = 33 | |

### 미기재 항목 결정

| 항목 | 결정 |
|---|---|
| 시약 분배 — 이미 촉매 보유 아군 | 추첨에서 제외. 대상 없으면 전달 없음. 분배로 받은 촉매는 다시 분배하지 않음 |
| 독침 — 3단계 중독 2배와의 순서 / 사망 대상 | 독침(타격마다)이 먼저, 2배(`OnStageCompleted`)가 나중 → 독침 중독도 2배. 타격으로 죽은 대상엔 부여 안 함 |
| 앙갚음(전사) — 파생 피해 적용 범위 | 전사의 **공격 행동** 피해에만 +15%. 유물·디버프 피해 제외 |
| 앙갚음(전사) — 체력 손실 범위 | 원인 무관(중독 등 직접 체력 손실 포함), 방어도만 감소는 제외 (시트 공통 규칙 "이전 턴") |
| 마법의 땅 — 시각 | 새 타일 속성의 시각 항목은 기존 타일 아이콘을 다른 색으로 임시 사용 (전용 아트는 후속) |

## 3. 변경 내용

### 3.1 공통 기반

- `JobModifier` 추상 베이스 (`CommonModifier` 상속): `protected abstract string JobId { get; }`, `CanApplyTo` = base && 직업 일치, 표시용 `JobLabel`(전사/도적/마법사/연금술사)
- 정적 조회 헬퍼 `JobModifierQueries` (또는 `CharacterModifier` 확장): `Has<T>(Character)` — 주인이 T를 장착했나; `AnyAliveHas<T>()` — 살아 있는 파티원 중 T 보유자가 있나
- 보상 카드: 직업 고유 모디파이어면 상태 줄에 "○○ 전용"을 붙인다 (`RewardUI`·`RewardModifierCard.SetStatus` 경로, 최소 변경)
- 레지스트리에 16종 추가 (§3.2 순서)

### 3.2 16종 (계열 · 동작 · 구현 지점)

| 직업 | 이름 (클래스) | 계열 | 동작 | 구현 지점 |
|---|---|---|---|---|
| 전사 | 견고한 방진 (`SteadfastFormationModifier`) | 위치 | 방진의 아군 1명당 피해 감소 10 → 15% | `WarriorGuardPassive`가 조회 (피해 계산·구역 상태 표시·설명) |
| 전사 | 전우애 (`ComradeshipModifier`) | 위치 | 같은 구역에 다른 살아 있는 아군 2명 이상이면 기본 공격력 +1 (조건 깨지면 즉시 해제, 미중첩) | `AttackBonus` 게터에서 현재 구역 아군 수를 실시간 계산 |
| 전사 | 앙갚음 (`WarriorPaybackModifier`) | 생존 | 직전 라운드(이전 플레이어 턴 시작 ~ 몬스터 턴 종료)에 체력을 잃었으면 이번 라운드 전사의 공격 행동 피해 +15%. 전투 시작 판정 없음 | `OnAttack(OnPostAction)`로 주인 체력 손실 기록(`Target == owner && IsEffected`, 직접 손실 포함), 주인 TurnStart에 "이번 라운드 활성 = 직전 기록" 후 기록 초기화, CombatStart에 둘 다 초기화. 계산은 `IsOwnAttackCalculation` + 행동 스코프 주체 확인 |
| 전사 | 대지의 룬 (`EarthRuneModifier`) | 콤보 | 3단계 [대지 가르기] 피해 +15%, 아군 일시 방어도 5 → 8 | 피해: `OnAttack`(3단계). 방어도: `WarriorGreatswordActive`가 조회해 부여량 +3 |
| 도적 | 노련한 협공 (`VeteranFlankModifier`) | 위치 | 협공 아군 1명당 20 → 25% | `RogueFlankPassive`가 조회 |
| 도적 | 여분의 비수 (`SpareDaggerModifier`) | 콤보 | 3단계 타격 4 → 5회 (다른 타격 증가보다 먼저) | `RogueAmbushActive`의 3단계 타격 수 계산이 조회 |
| 도적 | 일제 타격 (`VolleyStrikeModifier`) | 콤보 | 3단계 타격을 1회로 바꾸고, 줄어든 타격 1회당 피해 +110% (최종 타격 수 n → 1회, 배율 × (1 + 1.1 × (n − 1))). 4→1: +330%, 5→1: +440% | 같은 3단계 계산에서 "여분의 비수 적용 → (다른 증가) → 일제 타격 전환" 순서로 타격 수와 배율을 결정 |
| 도적 | 독침 (`VenomStingModifier`) | 콤보 | 공격 직전 대상 중독 10 이상이면, 그 강화 공격의 실제 타격 직후마다 중독 1 (행동 중 10 도달은 소급 안 함, 타격 수 증가 없음) | 행동 ID별로 대상의 첫 `OnPreAction`에서 조건 기록, 그 대상 타격의 `OnPostAction`(실제 처리, 시뮬레이션 아님)에서 중독 1 |
| 마법사 | 고전압 (`HighVoltageModifier`) | 콤보 | 감전 25 → 40% (출처 무관, 보유 마법사 사망 시 해제) | D5: `ShockStatus.Modify`가 `AnyAliveHas<HighVoltageModifier>()`로 실효 % 결정, 부여 시 40 |
| 마법사 | 회로 공명 (`CircuitResonanceModifier`) | 위치 | 아군이 있는 구역(마법사 구역 포함, 같은 구역 여러 명은 1개)이 3개 이상이면 자신의 강화 공격 피해 +15% | `OnAttack` 계산, `MageCircuit.GetCircuitZones` 개수 |
| 마법사 | 마법의 땅 (`MagicLandModifier`) | 위치 | 전투 시작 시 무작위 구역 1개의 모든 타일에 마법의 땅 설치. 마법사가 그 타일에서 공격하면 피해 +15% (다단·광역 전체). 기존 타일과 공존, 전투 종료 시 삭제. 공격 범위·아군 점유는 바꾸지 않음 | 새 `TileAttributeType.MagicLand`(enum **끝**), `MagicLandTile`. 설치: `OnTurnEvent(CombatStart)`. 삭제: 전투 시작 시 이전 것 제거 + 전투 종료(`CombatManager.OnCombatEnd` 또는 B의 전투 종료 정리 지점). 보정: `OnAttack` 계산에서 주인 `CurrentTile`이 마법의 땅이면 ×1.15 |
| 마법사 | 마나흡수 (`ManaAbsorbModifier`) | 콤보 | 3단계 피해 +15%. 그 공격의 충전 1 소비 후, 마법사가 직접 처치한 몬스터 수만큼 충전 획득(최대 3). 디버프·유물 처치 불인정, 소급 없음 | 피해: `OnAttack`(3단계). 충전: `IAttackActionFinishedListener` (B에서 소비가 리스너보다 먼저 → "소비 후 획득" 보장), `result.ComboStage == 2 && Kills > 0` |
| 연금술사 | 추가 시약 (`ExtraReagentModifier`) | 위치 | 시약 타일 3 → 4개 | `ReagentPrepPassive` 배치 개수가 조회 |
| 연금술사 | 고순도 촉매 (`HighPurityCatalystModifier`) | 콤보 | 촉매 25 → 40% (출처 무관, 보유 연금술사 사망 시 해제) | D5: `CatalystStatus.Modify` 실효 %, `ReagentPrepPassive.CatalystPercent`가 강화 시 40 반환(부여·예고·설명 일치) |
| 연금술사 | 시약 분배 (`ReagentShareModifier`) | 위치 | 시약 타일에서 촉매를 얻으면 같은 구역의 무작위 다른 아군 1명에게도 촉매 (§2 미기재 결정) | `ReagentTile.TryCatalyze`가 타일 주인 연금술사의 보유 여부를 조회해 추가 부여 |
| 연금술사 | 약화 가스 (`WeakeningGasModifier`) | 콤보 | 3단계 피해 +15%, 모든 몬스터에게 약화 25% (기존 고양 유지) | 피해: `OnAttack`(3단계). 약화: `IAttackActionFinishedListener` (`ComboStage == 2`) → 살아 있는 모든 몬스터 |

계열 합계(고유): 위치 8 / 콤보 7 / 생존 1 / 주사위 0. 전체(공용 포함): 위치 13 / 주사위 4 / 콤보 11 / 생존 5.

### 3.3 3단계 피해 +15% 공통

대지의 룬·마나흡수·약화 가스의 "3단계 강화 공격 피해 +15%"는 B의 대단원과 같은 조건(주인의 공격 계산 + 행동 스코프 주체가 주인 + `ComboStage == 2`)으로 구현한다. 공통 헬퍼로 묶어도 된다.

### 3.4 도적 3단계 타격 계산

`RogueAmbushActive`의 3단계 타격 수·배율을 한 메서드에서 계산한다: 기본 4 → 여분의 비수면 5 → 일제 타격이면 (타격 1, 배율 × (1 + 1.1 × (n − 1))). 실제 실행(`AttackPlan.Hits`→`GetStageHits`), 행동 예고, 스킬 설명이 같은 값을 쓰도록 주인(슬롯 소유자) 정보를 전달한다. `GetStageHits(stage)`에 주인이 없으면 시그니처 확장 또는 슬롯 소유자 주입 — 계획에서 기존 호출부를 모두 맞춘다.

## 4. 범위 밖

- 다른 9직업의 고유 모디파이어
- 마법의 땅 전용 아트
- 시트 "검토 보류" 항목

## 5. 검증

1. 헤드리스 컴파일
2. 에디터 자가 테스트(사용자 실행): 직업 제한(다른 직업은 `CanApplyTo` false), 일제 타격 배율(4→1 = ×4.3, 5→1 = ×5.4), 여분의 비수 타격 5, 고전압·고순도 촉매 실효 %(보유자 생존 40 / 사망 25), 전우애 조건, 레지스트리 33종·모두 계열 있음
3. 사용자 플레이 확인: 직업별 4종이 해당 직업에게만 보상으로 나오고 "○○ 전용" 표시, 각 효과 수치, 마법의 땅 타일 표시·삭제, 마나흡수 충전 획득, 앙갚음(전사) 다음 라운드 보너스
