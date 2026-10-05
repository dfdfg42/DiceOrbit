# UI 전용 스프라이트 전환 설계 — 재활용 스킨 부품을 역할별 전용 아트로

작성 2026-09-26 · **구현됨 2026-09-26** (카탈로그 59 항목 전부 전용 아트, 점검기 이슈 0, 공용 스프라이트 17장 삭제). 사용자 요청: "UI에 별 붙어 있는 것들 없애자. 그전에 UI들 전부 재활용한 게 있는 것 같은데 캡처해서 전부 다 전용으로 뽑자. 크레딧 신경 쓰지 말고."
기준 스타일: `2026-09-25-ui-reskin-uiskin-higgsfield-design.md`(크림 종이 + 굵은 잉크 외곽선), 참조 REF_STYLE.

## 0. 문제 — 재활용 현황 (캡처 `review/inv_*.png` 15장 + 씬 스캔, 2026-09-26)

| 공용 부품 | 늘려 쓰는 곳 | 문제 |
|---|---|---|
| `panel.png` | 전투 정보 패널 433×931, 이벤트 710×907, 보상 1160×640, 결과 760×440, 모집 시트 980×620, 상점 단계 1100×420, 튜토리얼 프롬프트 560×260 — **7곳** | 같은 모서리·같은 분홍 별 2개가 모든 화면에 반복. 세로 패널에 가로 패널 비율 |
| `chip.png` | HUD 골드바 380×66, 턴 배지 120×66, 화면 제목(Act 1 / 상점 / 동료를 고르세요), 선반 라벨 150×40, 이름표, 섹션 칩, 보상 골드바 440×60·이름표 172×50, 상점 골드 알약 240×48 — **9역할** | 제목·수치·이름·라벨이 전부 같은 알약 |
| `button_primary/secondary_*` | 턴 종료, 이동/취소 260×90, 상점 교체/떠나기 300×72, 상품 태그 168×180, 이벤트 선택지 610×100, 보상 계속, 결과 다시 시작 300×78, 모집 취소/선택, 튜토리얼 시작/건너뛰기, 메인메뉴 4개 521×124 — **13역할** | 크기가 2~4배 차이 나는데 같은 알약을 늘림. 상품 태그는 정사각인데 알약 |
| `card.png` | 보상 종이 1146×626, 보상 카드 184×184, 속성 카드 243×81, 주사위 툴팁, 노드 카드 | 큰 종이와 작은 카드가 같은 테두리 |
| `divider.png` | 정보 패널·모집 시트 구분선 | 가운데 별 |
| `UI 이미지/주사위 패널.png` | 주사위 트레이(DicePanel), 주사위 툴팁(DHT_Panel), 타일 카드(TileCard) | 별 2개 + 세 곳 재활용 |
| `tooltip.png` | 행동 라벨, 튜토리얼 말풍선 | — |
| `slot.png` | HUD 물약 홈, 이벤트 주사위 홈 | — |

## 1. 결정 (사용자 답변 2026-09-26)

1. **프레임은 고정 비율 전용 일러스트.** 각 패널을 실제 비율로 뽑아 늘리지 않고(Simple, preserveAspect) 배치. 제목 띠·클립·리본 같은 고유 요소 허용. 단 **화면 크기에 묶인 두 프레임**(정보 패널: 화면 높이 / 주사위 트레이: 화면 폭)은 장식 띠를 보존하는 **한 방향 9-slice**(정보 패널 상·하 보존, 트레이 좌·우 보존)로 가운데 민무늬 구간만 늘린다. 내용이 동적으로 커지는 카드·칩·툴팁은 전용 스프라이트를 9-slice로.
2. **버튼은 역할별 전용 4상태 시트.** 한 이미지에 normal / hover / pressed / disabled 2×2 → PIL로 절단. 아이콘·크기 포함 고유 형태.
3. **화면 단위 초안(low 1k ×2) → 승인 → 고해상(high 2k 투명)**. 순서: 전투 → 모집 → 노드맵·보상 → 상점·이벤트·결과·튜토리얼 → 메인메뉴.
4. **별 장식 전면 제거.** 모든 프롬프트에 "no stars, no sparkles". 배경 일러스트의 별(상점 카운터 천, 창밖)은 UI가 아니므로 유지.
5. 유지: 몬스터 HP바·의도 말풍선(예전 것으로 복구, 사용자 결정), 아이콘(icon_coin·icon_potion_empty·icon_close), 노드맵 점(circle), 구역 플레이트, 배경.

## 2. 전용 카탈로그 (파트 → 파일 → 배치)

파일은 전부 `Assets/Sprites/UI Skin/` 아래, 이름 = `<화면>_<역할>.png`. 버튼 시트는 절단 후 `btn_<역할>_{normal,hover,pressed,disabled}.png`. 모드: **S** = Simple(고정 비율), **9** = 9-slice, **9V/9H** = 세로/가로 한 방향 9-slice.

### 2.1 전투 (14)
| 파트 | 파일 | 화면 크기 | 모드 | 사용처 | 형태 메모 |
|---|---|---|---|---|---|
| InfoFrame | `battle_info_frame` | 433×931 | 9V | InfoPanelCanvas/Panel | 캐릭터 시트: 상단 제목 띠(이름 칩이 걸릴 자리), 하단 잉크 장식 |
| DiceTray | `battle_dice_tray` | 1900×130 | 9H | GameCanvas/DicePanel | 나무·크림 트레이, 양끝 둥근 모서리, 주사위가 놓이는 홈 느낌 |
| TooltipFrame | `battle_tooltip_frame` | 동적 (패널 1차 자리) | 9 | TooltipCanvas/MainPanel (키워드 정의 툴팁) | 작은 정의 카드 |
| HudBar | `hud_bar` | 380×66 | S | RunHud TopBar | 왼쪽 동전 홈 + 오른쪽 물약 3홈 자리. **적용됨** — 홈 실측(800px 기준 ×0.475): 동전 홈 중심 36.6·지름 49, 물약 홈 중심 226/282/338·폭 49 → TopBar 패딩 좌 20, GoldText 143, PotionRow 간격 7, chipSize 49 |
| HudTurnBadge | `hud_turn_badge` | 120×66 | S | RunHud TurnChip | 둥근 턴 배지 |
| HudPotionSlot / HudPotionChip | `hud_potion_slot` / `hud_potion_chip` | 52×52 | S | PotionRow/Chip | 빈 홈 / 채워진 칩 |
| EndTurnButton | `btn_end_turn_*` | 220×56 | 시트 | DicePanel/End Turn Button | 도장 느낌, 파랑 |
| MoveButton / CancelActionButton | `btn_move_*` / `btn_cancel_action_*` | 260×90 | 시트 | CharacterActionPanel | 화살표 / X 아이콘 자리 |
| ActionLabel | `battle_action_label` | 134×34 동적 | 9 | MonsterActionLabel Bg | 작은 크림 라벨 |
| DiceTooltip | `battle_dice_tooltip` | 동적 | 9 | DHT_Panel | 6면 그리드 카드 |
| TileCard | `battle_tile_card` | 300×~300 | 9 | TileInfo TileCard | 타일 그림을 담는 액자 |
| AttrCard | `battle_attr_card` | 243×81 동적 | 9 | TileInfo AttrCard/Bg | 속성 카드 |
| NameChip / SectionChip | `chip_name` / `chip_section` | 동적 | 9 | 정보 패널·모집 시트 | 이름표 / 작은 탭 |
| Divider | `divider_ink` | 375×16 | S | 정보 패널·모집 시트 | 별 없는 잉크 선 |

### 2.2 모집 (5)
| RecruitSheet | `recruit_sheet` | 980×620 | S | RecuritUI/DetailRoot/Panel | 캐릭터 시트 + 클립 |
| RecruitTitleSign | `recruit_title_sign` | 360×56 | S | RecuritUI/TitleChip | 간판 |
| BottleNameTag | `recruit_name_tag` | 동적 | 9 | CharacterCard NameChip | 병 이름표 |
| RecruitCancel / RecruitSelect | `btn_recruit_cancel_*` / `btn_recruit_select_*` | 220×64 | 시트 | DetailRoot/Panel | 파랑 / 핑크 |

### 2.3 노드맵 · 보상 (9)
| ActSign | `map_act_sign` | 360×56 | S | NodeMapUI/TitleChip | 탑 팻말 |
| NodeCard | `map_node_card` | 72×72 (보스 101) | S | NodeMapUI 노드 | 작은 정사각 액자 |
| RewardFrame | `reward_frame` | 1160×640 | S | RewardUI/MainPanel (+Paper 합침) | 보상 두루마리, 제목 띠 |
| RewardGoldBar | `reward_gold_bar` | 440×60 | S | MainPanel/GoldBar | 동전 주머니 라벨 |
| RewardCard | `reward_card` | 184×184 | S | RewardTile/ImageTile | 정사각 카드 |
| RewardNameTag | `reward_name_tag` | 172×50 | S | RewardTile/NameBar | 카드 위 이름표 |
| RewardContinue | `btn_reward_continue_*` | 200×66 | 시트 | MainPanel/계속Button | 핑크 |
| UpgradeFrame | `reward_upgrade_frame` | 1150×600 | S | RewardUI/UpgradePanel | 강화판 |
| UpgradeCancel | `btn_upgrade_cancel_*` | 190×64 | 시트 | UpgradePanel/취소Button | 파랑 |

### 2.4 상점 · 이벤트 · 결과 · 튜토리얼 (14)
| ShopTitleSign | `shop_title_sign` | 220×56 | S | ShopUI/TitleChip | 가게 간판 |
| ShelfTag | `shop_shelf_tag` | 150×40 | S | PotionShelf/RelicShelf TitleChip | 선반 꼬리표 |
| ShopGoldPill | `shop_gold_pill` | 240×48 | S | ShopUI/GoldPill | 동전 알약 |
| GoodsTag | `btn_goods_*` | 168×180 | 시트 | PotionShelf/Items/Goods | 가격표 카드(세로) |
| ShopSwap / ShopLeave | `btn_shop_swap_*` / `btn_shop_leave_*` | 300×72 | 시트 | ShopUI | 핑크 / 파랑 |
| ShopStepFrame | `shop_step_frame` | 1100×420 | S | ShopUI/StepPanel (+BG 합침) | 카운터 영수증 |
| EventFrame | `event_frame` | 710×907 | 9V | EventUI/RightColumn | 책 페이지, 상단 제목 띠 |
| EventChoiceMain / EventChoiceSkip | `btn_event_main_*` / `btn_event_skip_*` | 610×100 | 시트 | ChoiceColumn | 넓은 선택지, 핑크 / 파랑 |
| ResultFrame | `result_frame` | 760×440 | S | _GameResultCanvas/Panel | 리본 제목 띠 |
| RestartButton | `btn_restart_*` | 300×78 | 시트 | RestartButton | 핑크 |
| TutorialPromptFrame | `tutorial_prompt_frame` | 560×260 | S | TutorialPromptUI/Img | 메모지 |
| TutorialStart / TutorialSkip | `btn_tutorial_start_*` / `btn_tutorial_skip_*` | 200×66 | 시트 | TutorialPromptUI | 핑크 / 파랑 |
| TutorialBubble | `tutorial_bubble` | 동적 | 9 | TutorialOverlayUI | 안내 말풍선 |

### 2.5 메인메뉴 (4)
| MenuStart / MenuContinue / MenuSettings / MenuQuit | `btn_menu_{start,continue,settings,quit}_*` | 521×124 | 시트 | MainMenu/Canvas | 라벨 이미지(`UI 이미지/Start Game` 등)는 유지, 판만 전용 |

합계: 프레임·카드 17, 칩·태그 12, 버튼 시트 19(= 76 상태 파일), 기타 3 → 생성 이미지 ≈ 48장.

## 3. 생성 방식

- 모델 `gpt_image_2_5`, 참조 `image_references` = REF_STYLE(`5d029aa6-…`) + **해당 요소의 현재 캡처 크롭**(업로드) — 비율·역할·글자 자리를 모델이 보게 한다.
- 초안: quality low / 1k / 화면 비율에 가장 가까운 aspect / count 2 → 사용자 승인(번호 확답 후) → 최종 high / 2k / `background: transparent`(프레임·칩) 또는 opaque 흰 배경 + 알파 트림(시트).
- 공통 프롬프트 골격: "Game UI element, flat cartoon, cream paper (#F8ECD4) fill, thick dark navy ink outline (#3B3652), soft inner shadow, **no stars, no sparkles, no text**, clean vector look, centered, plenty of margin". 역할별 형태 문장 + "empty interior where text goes".
- 버튼 시트: "2×2 sprite sheet of the SAME button in four states: normal, hover (brighter + white inner rim), pressed (darker, shifted down), disabled (desaturated gray, dashed outline); identical size and position in each cell; transparent background". 절단 = 알파 연결 성분 4개를 위치순 정렬(`tools/cut_button_sheet.py`).
- 후처리(`tools/finalize_ui_sprite.py`): 알파 트림 → 최대 변 제한(프레임 1200, 칩 600, 버튼 800) → 9-slice 파트는 경계 픽셀 산출 → `.meta` 스프라이트 설정(Mipmap + Trilinear, [[project-sprite-import]]).

## 4. 통합 구조 — UiSkin을 "역할 카탈로그"로

현재 `UiSkin`은 필드 10개(Panel/Card/Chip/…)와 `ApplyPanel()` 류 헬퍼. 이를 **역할 파트 열거 + 항목 목록**으로 바꾼다.

```csharp
public enum SkinPart { InfoFrame, DiceTray, ActionFrame, HudBar, HudTurnBadge, HudPotionSlot, HudPotionChip,
    ActionLabel, DiceTooltip, TileCard, AttrCard, NameChip, SectionChip, Divider,
    RecruitSheet, RecruitTitleSign, BottleNameTag, ActSign, NodeCard,
    RewardFrame, RewardGoldBar, RewardCard, RewardNameTag, UpgradeFrame,
    ShopTitleSign, ShelfTag, ShopGoldPill, ShopStepFrame, EventFrame, ResultFrame, TutorialPromptFrame, TutorialBubble,
    Scrim, ZonePlate }
public enum SkinButton { EndTurn, Move, CancelAction, RollDice, RecruitCancel, RecruitSelect, RewardContinue, UpgradeCancel,
    Goods, ShopSwap, ShopLeave, EventMain, EventSkip, Restart, TutorialStart, TutorialSkip, ShopChoice, ShopStepCancel,
    MenuStart, MenuContinue, MenuSettings, MenuQuit }
// 구현 중 추가된 역할(2026-09-26): RollDice(DicePanel/Roll Dice 220×56), ShopChoice(상점 단계 패널 선택 카드 240×200, AddChoiceButton), ShopStepCancel(StepPanel/CancelButton 200×56).
// 열거 값은 명시 고정(100번대 전투, 200 모집, 300 보상, 400 상점·이벤트·결과·튜토리얼, 500 메인메뉴, Scrim 900)이라 씬 직렬화가 안정적이다.
// 임시(Provisional) 항목: 전용 아트가 오기 전에는 구 공용 스프라이트를 꽂고 Provisional=true → 점검기가 "임시"로 보고 (조용한 폴백이 아니라 드러난 임시).

[Serializable] class SkinEntry { SkinPart part; Sprite sprite; SkinMode mode; float ppuMultiplier = 1f; }   // mode: Simple | Sliced
[Serializable] class SkinButtonEntry { SkinButton button; Sprite normal, hover, pressed, disabled; }

public Sprite GetSprite(SkinPart p); public void Apply(Image img, SkinPart p);           // 모드·ppu까지 적용, 비면 LogError(폴백 없음)
public SpriteState GetButtonState(SkinButton b); public void ApplyButton(Button btn, Image target, SkinButton b);
```

- `UiSkinImage` : `part` 필드만 유지(에디터에서 미리보기 적용). 새 컴포넌트 `UiSkinButton { SkinButton button; }`가 `Button.transition = SpriteSwap`과 `spriteState`를 스킨에서 채운다 — 씬에 상태 스프라이트 4개를 직접 배선하던 드롭인 방식을 대체.
- `UiSkinValidator` : 두 열거형 전수 검사(비면 이슈, Sliced인데 border 0이면 이슈). `UiSkinSelfTests` : Apply/ApplyButton/GetSprite 전수.
- 코드 호출부 교체: `ApplyPanel`→`Apply(img, SkinPart.ResultFrame)` 등(GameResultUI·TutorialPromptUI·TileInfoPanelUI·DiceHoverTooltipUI·NodeMapUI·RunHudUI·EventUI·MonsterActionLabel·TutorialOverlayUI·InfoPanelRows·BattleInfoPanelUI·CharacterSelectionUI).
- 씬 배선(RunCommand): 고정 프레임은 `Image.Type.Simple` + `preserveAspect` + rect를 스프라이트 비율로 재계산(앵커·자식 레이아웃 유지). 9V/9H는 Sliced + border. 버튼은 `UiSkinButton` 부착.
- 삭제: `panel/card/chip/tooltip/slot/divider/button_*/intent_bubble.png`, `UI 이미지/주사위 패널·패널 1차`, `UiSkin.IntentBubble`. 참조 0 확인 후 `git rm`.

## 5. 검증

- 화면 투어 15장 재캡처(`review/ded_*.png`) → 전후 비교표. 별 0개.
- `UiSkinValidator` 0 이슈, `UiSkinSelfTests.RunAll` 통과, 콘솔 에러 0.
- 늘어난 프레임에서 글자가 테두리·제목 띠를 침범하지 않는지 캡처로 확인(정보 패널 인셋 재조정 허용).

## 6. 순서 · 비용

T0 구조(§4) → T1 전투 → T2 모집 → T3 노드맵·보상 → T4 상점·이벤트·결과·튜토리얼 → T5 메인메뉴 → T6 정리(구 스프라이트 삭제·문서). 묶음마다 커밋.
비용 추정: 초안 48×0.5 + 최종 48×2.75 ≈ **156 크레딧**(재생성 여유 +20%). 잔액 20.5 → 사용자가 충전(2026-09-26 "부족하면 결제").

## 7. 범위 밖

몬스터 HP바·말풍선(예전 것 유지), 캐릭터 카드 병(`bottle_ink`), 구역 플레이트, 배경 일러스트, 아이콘, 노드 아이콘, 주사위 면.

## 8. 구현 결과 (2026-09-26)

- 생성: 초안 104장(전투 32 + 모집·노드맵·보상 28 + 나머지 44) → 최종 52장 + 재생성 1 + 직행 2 (버튼 시트 21 → 84 상태 파일). 총 ≈ 236 크레딧(생성 로그 25~33행).
- 배치 규칙(실측): 화면 크기에 묶인 프레임·동적 카드·칩·버튼은 전부 9-slice + `ppu = 스프라이트/rect` 로 '늘림 0' 근처에 두고, 장식(클립·밧줄·고리·리본·배지·꼬리·홈)은 경계 안에 넣어 보존. Simple은 정사각·고정 요소(HUD 바·턴 배지·물약 홈/칩·노드 카드·보상 카드·주사위 홈)만.
- 레이아웃 보정: 라벨을 장식 반대쪽으로 밀어내는 패딩(이름표 고리 38, 시트 클립 96, 제목 간판 밧줄 58, 행동 라벨 꼬리, 튜토리얼 배지 44, HUD 홈 실측 정렬).
- 남긴 것: 몬스터 HP바·의도 말풍선(예전 것, 사용자 결정), 아이콘, 노드맵 점, 구역 플레이트. `_Recovery` 복구 씬의 `패널 1차` 참조는 무시(사용 안 함).

## 후속 변경 (2026-10-04) — 머리띠 제거 · 타일 정보 패널 단순화

사용자 피드백: "패널 스프라이트 위쪽에 창 제목 표시줄 같은 헤더가 있는데 평평한 패널을 써야 한다", "카드(타일) 정보에 쓸데없이 패널이 많이 붙어 있다".

- **머리띠 제거 (8장, 제자리 덮어쓰기)** — `battle_tooltip_frame` · `battle_dice_tooltip` · `battle_info_frame` · `event_frame` · `recruit_sheet` · `reward_frame` · `shop_step_frame` · `tutorial_prompt_frame`.
  위쪽 띠와 그 아래 구분선(직선·물결선·그림자선)만 종이색으로 바꿨다. 외곽선·모서리·아래 그림자·종이 결과 장식(클립·핀·위쪽 탭·영수증 톱니·왼쪽 여백선)은 그대로다.
  도구: `Tools/flatten_panel_headers.py` (모서리 구역은 띠 색만 다시 칠하고, 띠 구역은 본문 행을 통째로 복사, 장식은 보존 상자로 지킨다. 다시 돌려도 결과가 같다). 힉스필드 크레딧 사용 없음. 9-slice 경계는 그대로 — 배치가 바뀌지 않는다.
  손대지 않은 것: `hud_turn_badge`(배지), `result_frame`(리본 배너) — 창 머리띠가 아니다.
- **`PlainPanel = 114` 추가** — 평평한 정보 카드 (행동 예고 카드 · 타일 정보 패널).
- **`TileCard = 109` · `AttrCard = 110` 삭제** (스프라이트 `battle_tile_card` · `battle_attr_card` 포함, 번호 재사용 금지) — 타일 정보 패널이 그림 액자 + 속성마다 카드에서 평평한 패널 한 장으로 바뀌어 쓰는 곳이 없다.
  처음엔 타일 그림까지 뺐다가 사용자 지적("타일 미리보기는 있어야 한다")으로 **그림은 되살렸다** — 액자 없이 패널 맨 위에 타일 그림 + 속성 아이콘만 (`Stack/TilePreview/TileImage/TileIconRow`).
- 현재 카탈로그: 파트 31(Scrim 포함) + 버튼 24. UI 스킨 점검 이슈 0, `UiSkinSelfTests` 통과.
- 확인: 모집 상세 시트 · 전투 정보 패널 · 타일 정보 패널 · 보상 · 이벤트 화면은 Play 캡처로 확인. 키워드 툴팁 · 주사위 툴팁 · 상점 교체 단계 · 튜토리얼 안내 프레임은 스프라이트 결과만 확인했고 화면에서는 보지 못했다.

## 후속 변경 (2026-10-04) — 전투 HUD 버튼 리워크 (주사위 굴리기 · 턴 종료)

사용자 피드백: "주사위 굴리기랑 턴 종료 버튼이 너무 구리다".

무엇이 문제였나 — 220×56짜리 민무늬 알약 두 개가 주사위 트레이 오른쪽 끝에 **트레이 테두리를 덮고** 얹혀 있었고, 라벨이 버튼 전체 폭에 가운데 정렬이라 "주사위 굴리기" 글자가 주사위 아이콘과 겹쳤다. 턴 종료의 아이콘은 뜻 없는 고리였다.

- **그림** — 알약 밖으로 물체가 튀어나온 통짜 일러스트(`SkinMode.Simple`, 9-slice 아님).
  `btn_roll_dice_*`(895×285): 분홍 알약 + 왼쪽 위로 튀어나온 주사위 두 개. `btn_end_turn_*`(895×291): **노랑** 알약 + 왼쪽에 걸친 모래시계.
  턴 종료를 하늘색에서 노랑으로 바꾼 이유: 캐릭터를 고르면 바로 옆에 하늘색 `취소` 버튼이 뜬다 — 분홍 = 진행(이동·굴리기) / 하늘 = 취소 / 노랑 = 턴 종료로 색을 나눴다.
- **상태 4장은 보통 상태 한 장에서 계산한다** — `Tools/make_hud_button_states.py`. 튀어나온 물체가 있는 그림을 상태마다 따로 생성하면 윤곽이 조금씩 달라 상태가 바뀔 때 덜컥거린다.
  hover = 살짝 밝게 + 바깥 흰 테, pressed = 살짝 어둡게 + 6px 내려앉음, disabled = 연보라 회색. 보통 상태 그림을 바꾸면 도구를 다시 돌린다 (둘레 투명 여백 8px 이상 필요).
- **배치** (`===Canvas/GameCanvas/DicePanel`)
  - 트레이 폭 1049 → **840**. 주사위 줄(`DiceContainer`)은 트레이 가운데로 (예전엔 버튼 자리를 비우느라 왼쪽으로 119 밀려 있었다).
  - `End Turn Button`: 트레이 **오른쪽 바깥**, 아래 크림 띠 위 (앵커 = 트레이 오른쪽 끝, 340×110.5). 캐릭터 행동 UI의 `취소`(x≈1250~)와 겹치지 않는다.
  - `Roll Dice`: 빈 트레이 **가운데** (340×108) — 주사위가 놓일 자리에서 굴리기를 권하고, 굴리면 그 자리에 주사위가 놓인다.
  - 라벨(`Text (TMP)`)은 버튼 전체가 아니라 **알약의 평평한 면**에만 앵커 (굴리기 x 0.279~0.950 · y 0.158~0.586, 턴 종료 x 0.263~0.950 · y 0.227~0.674), 글자 28 / 30.
  - 아래 크림 띠(`gamePanel bottom`)를 화면 왼쪽 끝부터 전체 폭으로 다시 앵커 — 예전엔 트레이 중심 기준으로 놓여 왼쪽 153px이 비어 있었다.
- 동작은 그대로다 (주사위는 턴 시작 때 자동으로 굴려지고 `Roll Dice`는 주사위가 놓이면 숨는다 — 전투 시작 연출 동안만 보인다).
- 생성 기록: 생성 로그 #38~#39 (초안 4장, 1.0 크레딧 — 최종 그림은 초안 #39에서 그대로 잘라 썼다. 화면 표시 크기가 원본의 약 0.38배라 고해상도 재생성이 필요 없었다).
- 확인: Play 캡처로 전투 시작(굴리기 + 턴 종료 비활성) · 굴리기 호버 · 내 턴 · 턴 종료 호버/눌림 · 캐릭터 선택(이동/취소와의 간격) · 몬스터 턴(비활성). 콘솔 오류 0, UI 스킨 점검 이슈 0, `UiSkinSelfTests` 통과.

## 후속 변경 (2026-10-05) — 주사위 굴리기 버튼 삭제 · 캐릭터 머리 위 행동 표시

- **주사위 굴리기 버튼을 없앴다** (사용자 결정: "어차피 직접 안 굴리니까"). 주사위는 턴 시작 때 자동으로 굴려진다.
  씬의 `DicePanel/Roll Dice`, `DiceUI.rollButton`·`CombatManager.rollDiceButton`과 클릭 처리, `SkinButton.RollDice = 103`(번호 재사용 금지),
  스프라이트 `btn_roll_dice_*` 4장을 지웠다. `Tools/make_hud_button_states.py`는 턴 종료만 다룬다. 버튼 카탈로그 24 → 23.
- **캐릭터 체력바 위 행동 표시 (`UI/CharacterActionStrip`)** — 그 전에는 흐린 아이콘 하나와 반투명 회색 네모 셋이라 거의 보이지 않았다.
  받침판 위에 `[날개 장화 | ◆ ◆ ★]`: 이동 토큰(쓸 수 있음 = 밝은 장화, 썼음 = 회색 장화) + 콤보 보석(채운 단계만큼 켜지고, 마지막 칸은 별).
  두 칸을 채우면 별 자리가 두근거리고(다음 공격이 마무리), 마무리 발동 때 셋 다 켜지며 팡, 끊기면 붉게 떨리고 꺼진다.
  그림 7장은 UiSkin의 「전투 머리 위 표시」 칸(`MoveToken`·`MoveTokenSpent`·`ComboGemEmpty/Lit`·`ComboFinisherEmpty/Lit`·`TokenPlate`),
  시트 절단은 `Tools/cut_hud_tokens.py` (회색 장화는 밝은 장화에서 계산 — 윤곽이 같다). 이름표는 표시줄 위로 올렸다.
- `UiSkinImage.borderScaleOverride` 추가 (아주 작은 패널용 9-slice 테두리 배수) — 씬의 UiSkinImage 29개에 기본값 0이 직렬화됐다.
- 생성 기록: 생성 로그 #43 (0.25 크레딧).
- 확인: Play 캡처로 표시줄의 0·1·2단계와 마무리 순간을 봤다. 이동을 쓴 뒤의 회색 장화, 끊김 연출은 화면으로 보지 못했다. UI 스킨 점검 이슈 0, `UiSkinSelfTests` 통과.
