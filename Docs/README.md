# Dice Orbit — 문서 인덱스

---

## 🏗 시스템 설계 (현재 구조)

핵심 게임플레이 시스템이 **현재 어떻게 동작하는지** 설명하는 문서.

| 문서 | 내용 |
|---|---|
| [project_structure.md](project_structure.md) | 스크립트 폴더 구조 + 전투 아키텍처 개요 |
| [run_structure_system.md](run_structure_system.md) | **런 구조 (노드맵)** — 맵 생성/경제/파티/점감 부활/유물/포션/이벤트/상점 (2026-07) |
| [dice_deck_system.md](dice_deck_system.md) | **주사위 덱** — 소유 덱/커스텀 면/사용 효과/호버 툴팁/보상 교체/이벤트 효과 부여 (2026-07-29) |
| [reward_screen_system.md](reward_screen_system.md) | **보상 화면** — 전리품 시트 3박자(자동 수령 → 강화 공용 3택 → 주사위 교체), RewardRoller/RewardFlow, 씬 계층·스캐폴드·확장 지점 (2026-10-03) |
| [skill_system_structure.md](skill_system_structure.md) | 전투 도메인 클래스 다이어그램, 스킬/패시브/컨텍스트 구조 |
| [skill_targeting_system.md](skill_targeting_system.md) | 액티브 스킬 타겟 선택 시스템 (OneEnemy~MultiTile) |
| [combat_reactor_dispatch.md](combat_reactor_dispatch.md) | ICombatReactor DIM 타입별 디스패치 (OnAttack/OnHeal/OnMove/OnTurnEvent) |
| [modifireSystem.md](modifireSystem.md) | 스킬 런타임 갱신(Modifier) 시스템 — Signature/Generic, 휘발성 컨텍스트 |
| [action_forecast_system.md](action_forecast_system.md) | **행동 예고** — 주사위를 고르는 순간 경로·공격 종류·대상별 예상 피해·콤보 변화·경로 타일 효과·도착지 위험을 예고. 실행과 같은 코드(PlanAttack·SimulateCalculation·ResolveDamage)를 "도착했다고 치고" 호출, 타일 예고 훅 (2026-10-04) |
| [hit_feel_system.md](hit_feel_system.md) | **타격감** — 한 방의 여섯 겹(임팩트·플래시·히트스톱·밀림·카메라 트라우마·타격음), 타격 등급(HitTier), HitFeelProfile, 렌더 전용 변형, 몬스터 처치 '베어 가르기'(참격 + 두 조각, 2026-10-05), 확장 지점 (2026-10-03) |
| [nodemap_ambience.md](nodemap_ambience.md) | **노드맵 배경** — 장식 애니메이션(통짜 배경에서 물체를 '깃털 조각'으로 떠내 제자리에서 살짝 움직인다: 촛불·나침반·깃펜·김·고양이·반짝이) + 지도 두루마리(배경에서 떼어 낸 스프라이트, 봉 밑으로 말려 들어가는 지도). 도구·배치표·스캐폴드·AmbientMotion/AmbientTwinkle (2026-10-04) |
| [combat_floating_notification_system.md](combat_floating_notification_system.md) | 패시브/상태이상 발동 시 플로팅 알림 버블 |
| [Pipeline.md](Pipeline.md) | 전투 파이프라인 플로차트 (mermaid) |
| [TurnSystem.md](TurnSystem.md) | 턴 시스템 플로차트 (mermaid) |

---

## 📖 구현 가이드

새 스킬·패시브·VFX를 추가할 때 참고하는 레퍼런스.

| 문서 | 내용 |
|---|---|
| [combat_pipeline_authoring_guide.md](combat_pipeline_authoring_guide.md) | 스킬/패시브/상태이상/이펙트 확장 실전 가이드 |
| [vfx_system_design.md](vfx_system_design.md) | 캐릭터 스킬 VFX 구조 및 CombatVfxProfile 사용법 |
| [animator_setup_guide.md](animator_setup_guide.md) | 캐릭터/몬스터 Animator 파라미터 설정 가이드 |

---

## 🎨 UI / 비주얼

| 문서 | 내용 |
|---|---|
| [battle_info_panel_system.md](battle_info_panel_system.md) | **전투 정보 시스템** — 우측 정보 패널·타일 패널·커서 요약 툴팁·키워드 링크 호버 (2026-07) |
| [battle_visual_language.md](battle_visual_language.md) | **전투 시각 언어 사전** — 의미→형태 표 (펄스/셰브런/리프트/브래킷/조준 아크), 공용 부품 |
| [description_style.md](description_style.md) | **설명 글 쓰는 법** — 한두 줄·명사형·수치는 +N% 형식, 설명이 사는 곳 표(기술·패시브·타일·모디파이어·주사위 효과·포션·키워드·이벤트), 형식 자가 테스트 (2026-10-05) |
| [editor_owned_ui_pattern.md](editor_owned_ui_pattern.md) | **에디터 소유 UI 패턴** — [기본 레이아웃 생성]+폴백+프리팹 슬롯+SubclassPicker, 새 UI 체크리스트 |

---

## 📐 설계안 · 구현 계획 (기록)

특정 변경을 위한 **시점 기록물**(설계안 / 구현 계획). 현재 코드가 아니라 "그때의 계획"을 담고 있으므로, 각 문서 상단의 갱신 노트를 함께 볼 것. 현재 동작은 위 *시스템 설계* 문서를 참고.

| 문서 | 내용 | 상태 |
|---|---|---|
| [combat_context_action_merge_design.md](combat_context_action_merge_design.md) | CombatContext/CombatAction 병합 + 서브클래스 데이터모델 **설계안** | 구현됨 (리액터는 이후 DIM으로 발전 → combat_reactor_dispatch.md) |
| [combat_context_action_merge_plan.md](combat_context_action_merge_plan.md) | 위 병합의 단계별 **구현 계획** | 완료 |
| [implementation_plan.md](implementation_plan.md) | 스킬 시스템 Active/Passive 분리 리팩토링 계획 | 완료 (SkillAsset안 → CharacterActiveSkill로 대체) |
| [AttackDesign.md](AttackDesign.md) | 공격 의도 표시 설계 매트릭스 | 대부분 구현됨 (→ skill_targeting_system.md) |
| [tile_attribute_visual_plan.md](tile_attribute_visual_plan.md) | 타일 속성 3D 오브젝트/VFX 부착 구조 제안 | 미채택 (→ TileAttributeBubbleManager) |
| [ui_hover_tooltip_system_design.md](ui_hover_tooltip_system_design.md) | 구 Hover 툴팁 UI 설계 (키워드·상태이상 패널) | 대체됨 (→ battle_info_panel_system.md — 경량 ShowPinned만 존치) |
| [superpowers/specs/2026-07-05-battle-info-panel-design.md](superpowers/specs/2026-07-05-battle-info-panel-design.md) | 전투 정보 패널 **설계안** | 구현됨 (→ battle_info_panel_system.md) |
| [superpowers/specs/2026-07-07-run-structure-node-map-design.md](superpowers/specs/2026-07-07-run-structure-node-map-design.md) | 런 구조·노드맵 **기획 스펙** (의도의 진실 소스) | 구현됨 (→ run_structure_system.md) |
| [superpowers/specs/2026-07-29-dice-deck-design.md](superpowers/specs/2026-07-29-dice-deck-design.md) | 주사위 덱 **설계안** (3 Phase) | 구현됨 (→ dice_deck_system.md) |
| [superpowers/plans/2026-07-29-dice-deck-phase1.md](superpowers/plans/2026-07-29-dice-deck-phase1.md) | 주사위 덱 Phase 1 **구현 계획** | 완료 |
| [superpowers/specs/2026-08-21-auto-combat-redesign-design.md](superpowers/specs/2026-08-21-auto-combat-redesign-design.md) | **전투 개편 설계** — 사분면 구역 + 자동 공격 + 강화 공격 (템포 개선) | 구현됨 (Phase 1~4). 의도의 진실 소스 |
| [superpowers/plans/2026-08-21-auto-combat-phase1-2.md](superpowers/plans/2026-08-21-auto-combat-phase1-2.md) | 구역 시스템 + 자동 공격 **구현 계획** | 완료 (피해 산식은 이후 변경) |
| [superpowers/plans/2026-08-21-auto-combat-phase3a-passives.md](superpowers/plans/2026-08-21-auto-combat-phase3a-passives.md) | 위치 패시브 3종 **구현 계획** | 완료 (연금술사는 Phase 3b로 보류) |
| [superpowers/specs/2026-09-25-ui-reskin-uiskin-higgsfield-design.md](superpowers/specs/2026-09-25-ui-reskin-uiskin-higgsfield-design.md) | **UI 통일 리스킨 설계** — 크림 종이 + 굵은 외곽선, UiSkin 시스템, 힉스필드 생성 | 진행 중 (1~4단계 완료, 플레이 확인 대기) |
| [superpowers/plans/2026-09-25-ui-reskin-phase1-2-style-tile-core-skin.md](superpowers/plans/2026-09-25-ui-reskin-phase1-2-style-tile-core-skin.md) | 스타일 타일 + 코어 세트 + UiSkin **구현 계획** | 완료 |
| [superpowers/plans/2026-09-25-ui-reskin-phase3-code-ui-migration.md](superpowers/plans/2026-09-25-ui-reskin-phase3-code-ui-migration.md) | 코드 UI 13파일 UiSkin 마이그레이션 + UiSkinImage **구현 계획** | 완료 |
| [superpowers/plans/2026-09-25-ui-reskin-phase4-scene-dropin.md](superpowers/plans/2026-09-25-ui-reskin-phase4-scene-dropin.md) | 씬 드롭인 20장 + 버튼 SpriteSwap **구현 계획** (드롭인 매핑표 포함) | 완료 |
| [superpowers/specs/2026-09-25-ui-layout-polish-design.md](superpowers/specs/2026-09-25-ui-layout-polish-design.md) | **UI 레이아웃·간격 정리** — 화면 투어 캡처 기반 발견→수정 표 (HUD 턴 칩, 노드맵 제목, 상점 선반/카드, 이벤트 여백, 결과창 패널, combatUI 배선) + §3 정보 패널 세로 흐름·타이포 위계·타일 패널 + §4 행동 라벨·타일 패널 그림·말풍선 꼬리(되돌림)·3D 타일 실험(폐기) | 완료 |
| [superpowers/specs/2026-09-25-monster-zone-intent-reskin-design.md](superpowers/specs/2026-09-25-monster-zone-intent-reskin-design.md) | **몬스터 영역 표시 리스킨 설계** — 구역 잉크 플레이트(힉스필드) + 의도 말풍선 + 공격 타일 밴드 | 구현됨 |
| [superpowers/plans/2026-09-25-monster-zone-intent-reskin.md](superpowers/plans/2026-09-25-monster-zone-intent-reskin.md) | 구역 플레이트·의도 말풍선 **구현 계획** (7 태스크) | 완료 |
| [superpowers/specs/2026-09-26-recruit-detail-reskin-design.md](superpowers/specs/2026-09-26-recruit-detail-reskin-design.md) | **모집 상세 화면 + 배경 리스킨 설계** — 크림 캐릭터 시트, 힉스필드 배경(옛 구도 참조), 캔버스 스케일 | 구현됨 |
| [superpowers/plans/2026-09-26-recruit-detail-reskin.md](superpowers/plans/2026-09-26-recruit-detail-reskin.md) | 모집 상세·배경 **구현 계획** (4 태스크) | 완료 |
| [superpowers/specs/2026-09-26-recruit-bottles-design.md](superpowers/specs/2026-09-26-recruit-bottles-design.md) | **모집 유리병 선택지 정리** — 잉크 유리병(힉스필드), 카운터 위 배치, 이름 칩·HP·제목 칩, 투명도 | 구현됨 |
| [superpowers/specs/2026-09-26-ui-dedicated-sprites-design.md](superpowers/specs/2026-09-26-ui-dedicated-sprites-design.md) | **UI 전용 스프라이트 전환** — 공용 8부품을 역할별 전용 59항목(프레임·칩·버튼 4상태)으로, UiSkin 역할 카탈로그, 별 장식 제거 | 구현됨 |
| [superpowers/plans/2026-09-26-ui-dedicated-sprites.md](superpowers/plans/2026-09-26-ui-dedicated-sprites.md) | 전용 스프라이트 **구현 계획** (구조 → 화면 5묶음 → 정리) | 완료 |
| [superpowers/plans/2026-09-25-ui-reskin-generation-log.md](superpowers/plans/2026-09-25-ui-reskin-generation-log.md) | 힉스필드 생성 로그 (프롬프트·job id·비용) | 기록 |

---

## 📊 데이터

| 파일 | 내용 |
|---|---|
| [combat_pipeline_matrix.csv](combat_pipeline_matrix.csv) | 전투 파이프라인 트리거 매트릭스 |
| [skill_authoring_sheet_template.csv](skill_authoring_sheet_template.csv) | 스킬 제작 시트 템플릿 |

---

## 🗄 보관 (감사·리포트)

| 문서 | 내용 |
|---|---|
| [deadcode_audit_2026-07.md](deadcode_audit_2026-07.md) | Assets/Scripts 레거시/데드코드 감사 리포트 (2026-07) |
