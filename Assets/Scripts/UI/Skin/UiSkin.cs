using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.Data.Modifiers;

namespace DiceOrbit.UI.Skin
{
    /// <summary>
    /// 스킨 역할 파트 — 한 파트 = 한 용도 = 한 전용 스프라이트 (전용 스프라이트 스펙 2026-09-26 §4).
    /// 값은 명시 고정(씬 직렬화 안정). Scrim은 스프라이트 없이 색만.
    /// </summary>
    public enum SkinPart
    {
        // 전투
        InfoFrame = 100,        // 오른쪽 정보 패널 프레임 (세로 9-slice)
        DiceTray = 101,         // 하단 주사위 트레이 (가로 9-slice)
        TooltipFrame = 102,     // 키워드 정의 툴팁
        HudBar = 103,           // HUD 골드·물약 바
        HudTurnBadge = 104,     // 턴 배지
        HudPotionSlot = 105,    // 빈 물약 홈
        HudPotionChip = 106,    // 채워진 물약·유물 칩
        ActionLabel = 107,      // 몬스터 행동 라벨
        DiceTooltip = 108,      // 주사위 6면 툴팁 카드
        // 109~110 (TileCard·AttrCard)은 타일 정보 패널 단순화(2026-10-04)로 삭제 — 번호 재사용 금지
        NameChip = 111,         // 이름표 (정보 패널·모집 시트)
        SectionChip = 112,      // 섹션 탭 칩
        Divider = 113,          // 구분선
        PlainPanel = 114,       // 평평한 패널 — 전투 중 떠 있는 정보 카드 (행동 예고·타일 정보)
        // 모집
        RecruitSheet = 200,
        RecruitTitleSign = 201,
        BottleNameTag = 202,
        // 노드맵 · 보상
        ActSign = 300,
        NodeCard = 301,
        RewardFrame = 302,
        // 303~306 (RewardGoldBar·RewardCard·RewardNameTag·UpgradeFrame)은 보상 리워크(2026-10-03)로 삭제 — 번호 재사용 금지
        RewardLootChip = 307,   // 전리품 칩 (골드·유물·포션 공용 — 둥근 아이콘 홈)
        RewardStamp = 308,      // 확정 도장 테두리 (글자는 TMP)
        // 상점 · 이벤트 · 결과 · 튜토리얼
        ShopTitleSign = 400,
        ShelfTag = 401,
        ShopGoldPill = 402,
        ShopStepFrame = 403,
        EventFrame = 404,
        EventDieSlot = 405,
        ResultFrame = 406,
        TutorialPromptFrame = 407,
        TutorialBubble = 408,
        // 색만
        Scrim = 900,
    }

    /// <summary>버튼 역할 — 역할마다 전용 4상태 스프라이트.</summary>
    public enum SkinButton
    {
        EndTurn = 100, Move = 101, CancelAction = 102,   // 103(RollDice)은 삭제 — 주사위는 자동으로 굴린다 (번호 재사용 금지)
        RecruitCancel = 200, RecruitSelect = 201,
        RewardContinue = 300, RewardSkip = 301, RewardChoiceCard = 302, RewardPortrait = 303,   // 주 버튼 · 보조(건너뛰기) · 선택 카드(선택 = Pressed) · 파티 초상 프레임
        Goods = 400, ShopSwap = 401, ShopLeave = 402, EventMain = 403, EventSkip = 404, Restart = 405, TutorialStart = 406, TutorialSkip = 407, ShopChoice = 408, ShopStepCancel = 409,
        MenuStart = 500, MenuContinue = 501, MenuSettings = 502, MenuQuit = 503,
    }

    /// <summary>Sliced = 9-slice로 늘림, Simple = 고정 비율(preserveAspect) 일러스트.</summary>
    public enum SkinMode { Sliced = 0, Simple = 1 }

    [Serializable]
    public class SkinEntry
    {
        public SkinPart Part;
        public Sprite Sprite;
        public SkinMode Mode = SkinMode.Sliced;
        [Tooltip("9-slice 경계를 화면에서 줄이는 배수 (2 = 절반)")]
        public float PixelsPerUnitMultiplier = 1f;
        [Tooltip("전용 아트가 아직 없어 임시 스프라이트를 꽂아 둔 상태 — 점검에서 이슈로 보고")]
        public bool Provisional;
    }

    [Serializable]
    public class SkinButtonEntry
    {
        public SkinButton Button;
        public Sprite Normal;
        public Sprite Hover;
        public Sprite Pressed;
        public Sprite Disabled;
        public SkinMode Mode = SkinMode.Sliced;
        public float PixelsPerUnitMultiplier = 1f;
        public bool Provisional;
    }

    /// <summary>
    /// UI 스킨 단일 권위 (리스킨 스펙 2026-09-25 §3, 전용 스프라이트 스펙 2026-09-26 §4).
    /// 팔레트 + 역할 파트 카탈로그(<see cref="Parts"/>) + 역할 버튼 카탈로그(<see cref="Buttons"/>) + 아이콘.
    /// 코드 생성 UI는 <see cref="Apply(Image, SkinPart)"/>/<see cref="ApplyButton(Button, SkinButton)"/>로, 씬 UI는 UiSkinImage/UiSkinButton으로 역할만 지정한다.
    /// 에셋이 없거나 파트가 비면 예외 — 다른 스프라이트로 조용히 굴러가지 않는다. 점검: 「도구/Dice Orbit/UI 스킨 점검」.
    /// </summary>
    [CreateAssetMenu(fileName = "UiSkin", menuName = "Dice Orbit/UI/UI Skin")]
    public class UiSkin : ScriptableObject
    {
        public const string ResourcePath = "UI/UiSkin";
        private static UiSkin _current;

        /// <summary>Resources/UI/UiSkin.asset. 없으면 InvalidOperationException.</summary>
        public static UiSkin Current
        {
            get
            {
                if (_current == null) _current = LoadOrThrow(ResourcePath);
                return _current;
            }
        }

        public static UiSkin LoadOrThrow(string resourcePath)
        {
            var skin = Resources.Load<UiSkin>(resourcePath);
            if (skin == null)
                throw new InvalidOperationException(
                    $"[UiSkin] Resources/{resourcePath}.asset 이 없습니다. " +
                    "Create > Dice Orbit > UI > UI Skin 으로 만들어 Assets/Resources/UI/UiSkin.asset 에 두세요.");
            return skin;
        }

        [Header("팔레트")]
        public Color Paper     = new Color(0.980f, 0.953f, 0.878f);        // #FAF3E0 크림 종이
        public Color PaperDeep = new Color(0.910f, 0.859f, 0.765f);        // #E8DBC3 칩
        public Color Ink       = new Color(0.294f, 0.259f, 0.361f);        // #4B425C 잉크
        public Color InkMuted  = new Color(0.42f, 0.40f, 0.36f);           // 설명 회색
        public Color Accent    = new Color(0.79f, 0.61f, 0.25f);           // 골드
        public Color Primary   = new Color(1.00f, 0.65f, 0.95f);           // #FFA6F2 핑크
        public Color Secondary = new Color(0.65f, 0.87f, 1.00f);           // #A6DDFF 하늘
        public Color Danger    = new Color(0.773f, 0.227f, 0.227f);        // #C53A3A HP
        public Color Dice      = new Color(0.16f, 0.42f, 0.65f);           // 주사위 청색
        public Color Modifier  = new Color(0.42f, 0.28f, 0.72f);           // #6A48B8 보라
        public Color Passive   = new Color(0.514f, 0.624f, 0.557f);        // #839F8E 세이지
        public Color Scrim     = new Color(0.043f, 0.051f, 0.078f, 0.94f); // 어두운 반투명 배경 (0.85는 뒤 궤도가 비쳐 0.94로, 2026-09-25)

        [Header("역할 파트 카탈로그 (SkinPart마다 1항목)")]
        public List<SkinEntry> Parts = new List<SkinEntry>();

        [Header("역할 버튼 카탈로그 (SkinButton마다 1항목, 4상태)")]
        public List<SkinButtonEntry> Buttons = new List<SkinButtonEntry>();

        [Header("아이콘")]
        public Sprite Coin;
        public Sprite PotionSlotEmpty;
        public Sprite Close;

        [Header("모디파이어 계열 아이콘 (보상 카드)")]
        public Sprite FamilyPosition;
        public Sprite FamilyDice;
        public Sprite FamilyCombo;
        public Sprite FamilySurvival;

        [Header("전투 머리 위 표시 — 이동 토큰 · 콤보 보석 (CharacterActionStrip, Tools/cut_hud_tokens.py)")]
        public Sprite MoveToken;            // 이번 턴에 아직 움직일 수 있다
        public Sprite MoveTokenSpent;       // 이동을 썼다
        public Sprite ComboGemEmpty;        // 콤보 1·2단계 자리 — 비어 있음
        public Sprite ComboGemLit;          // 채워짐
        public Sprite ComboFinisherEmpty;   // 3단계(마무리) 자리 — 별
        public Sprite ComboFinisherLit;
        public Sprite TokenPlate;           // 받침판 (9-slice 캡슐)

        [Header("도형")]
        public Sprite Circle;   // 흰 원 — 경로 점·배지 링·상태 칩처럼 틴트가 필요한 원형 전용

        [Header("월드 데칼")]
        public Sprite ZonePlate;      // 구역 플레이트 — 사분면 조각, pivot (0,0), PPU 128 = 16×16 유닛. 틴트용 흰 바탕

        // ── 파트 ──────────────────────────────────────────────

        /// <summary>파트 항목 조회. 없거나 스프라이트가 비면 예외 (폴백 없음). Scrim은 항목이 없다 — <see cref="Apply"/>를 쓸 것.</summary>
        public SkinEntry GetEntry(SkinPart part)
        {
            if (part == SkinPart.Scrim)
                throw new InvalidOperationException("[UiSkin] Scrim은 스프라이트 항목이 없습니다 (색만) — Apply(image, SkinPart.Scrim)을 쓰세요.");
            if (!TryGetEntry(part, out var entry))
                throw new InvalidOperationException($"[UiSkin] 파트 {part} 항목이 카탈로그에 없습니다 — 「도구/Dice Orbit/UI 스킨 점검」으로 확인하세요.");
            if (entry.Sprite == null)
                throw new InvalidOperationException($"[UiSkin] 파트 {part} 스프라이트가 비어 있습니다 — 「도구/Dice Orbit/UI 스킨 점검」으로 확인하세요.");
            return entry;
        }

        /// <summary>항목이 있으면 true (스프라이트가 비어 있어도 true). 점검기·에디터 도구용.</summary>
        public bool TryGetEntry(SkinPart part, out SkinEntry entry)
        {
            for (int i = 0; i < Parts.Count; i++)
                if (Parts[i] != null && Parts[i].Part == part) { entry = Parts[i]; return true; }
            entry = null;
            return false;
        }

        public Sprite GetSprite(SkinPart part) => part == SkinPart.Scrim ? null : GetEntry(part).Sprite;

        /// <summary>역할 파트를 Image에 적용 — 모드(Sliced/Simple)·9-slice 배수까지. Scrim은 색만.</summary>
        public void Apply(Image image, SkinPart part)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (part == SkinPart.Scrim)
            {
                image.sprite = null;
                image.type = Image.Type.Simple;
                image.color = Scrim;
                return;
            }
            var entry = GetEntry(part);
            ApplyEntry(image, entry.Sprite, entry.Mode, entry.PixelsPerUnitMultiplier);
        }

        /// <summary>항목 추가 또는 교체 — 에디터 배선 도구용.</summary>
        public SkinEntry SetEntry(SkinPart part, Sprite sprite, SkinMode mode, float pixelsPerUnitMultiplier = 1f, bool provisional = false)
        {
            if (part == SkinPart.Scrim) throw new ArgumentException("Scrim은 항목을 두지 않습니다", nameof(part));
            if (!TryGetEntry(part, out var entry)) { entry = new SkinEntry { Part = part }; Parts.Add(entry); }
            entry.Sprite = sprite; entry.Mode = mode; entry.PixelsPerUnitMultiplier = pixelsPerUnitMultiplier; entry.Provisional = provisional;
            return entry;
        }

        // ── 버튼 ──────────────────────────────────────────────

        /// <summary>버튼 항목 조회. 없거나 4상태 중 하나라도 비면 예외.</summary>
        public SkinButtonEntry GetButton(SkinButton button)
        {
            if (!TryGetButton(button, out var entry))
                throw new InvalidOperationException($"[UiSkin] 버튼 {button} 항목이 카탈로그에 없습니다 — 「도구/Dice Orbit/UI 스킨 점검」으로 확인하세요.");
            RequireButtonSprite(entry.Normal,   button, "Normal");
            RequireButtonSprite(entry.Hover,    button, "Hover");
            RequireButtonSprite(entry.Pressed,  button, "Pressed");
            RequireButtonSprite(entry.Disabled, button, "Disabled");
            return entry;
        }

        public bool TryGetButton(SkinButton button, out SkinButtonEntry entry)
        {
            for (int i = 0; i < Buttons.Count; i++)
                if (Buttons[i] != null && Buttons[i].Button == button) { entry = Buttons[i]; return true; }
            entry = null;
            return false;
        }

        /// <summary>버튼에 역할 4상태 스프라이트를 꽂는다 (SpriteSwap). targetGraphic이 없으면 같은 오브젝트의 Image.</summary>
        public void ApplyButton(Button button, SkinButton role)
        {
            if (button == null) throw new ArgumentNullException(nameof(button));
            var entry = GetButton(role);

            var image = button.targetGraphic as Image;
            if (image == null) image = button.GetComponent<Image>();
            if (image == null)
                throw new InvalidOperationException($"[UiSkin] 버튼 '{button.name}'에 Image가 없어 스프라이트를 꽂을 수 없습니다.");

            ApplyEntry(image, entry.Normal, entry.Mode, entry.PixelsPerUnitMultiplier);
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = entry.Hover,
                selectedSprite = entry.Hover,
                pressedSprite = entry.Pressed,
                disabledSprite = entry.Disabled,
            };
        }

        /// <summary>
        /// 선택 상태가 유지되는 버튼(보상 카드·파티 초상)용 — 선택이면 Pressed 스프라이트를 바탕·호버로 쓴다.
        /// 선택 해제는 같은 메서드에 false (보통 상태로 되돌린다).
        /// </summary>
        public void ApplyButtonSelected(Button button, SkinButton role, bool selected)
        {
            ApplyButton(button, role);
            if (!selected) return;

            var entry = GetButton(role);
            ((Image)button.targetGraphic).sprite = entry.Pressed;
            var state = button.spriteState;
            state.highlightedSprite = entry.Pressed;
            state.selectedSprite = entry.Pressed;
            button.spriteState = state;
        }

        public SkinButtonEntry SetButton(SkinButton button, Sprite normal, Sprite hover, Sprite pressed, Sprite disabled,
                                         SkinMode mode, float pixelsPerUnitMultiplier = 1f, bool provisional = false)
        {
            if (!TryGetButton(button, out var entry)) { entry = new SkinButtonEntry { Button = button }; Buttons.Add(entry); }
            entry.Normal = normal; entry.Hover = hover; entry.Pressed = pressed; entry.Disabled = disabled;
            entry.Mode = mode; entry.PixelsPerUnitMultiplier = pixelsPerUnitMultiplier; entry.Provisional = provisional;
            return entry;
        }

        // ── 기타 ──────────────────────────────────────────────

        /// <summary>틴트 가능한 흰 원 — 경로 점·배지 링·상태 칩 전용.</summary>
        public void ApplyCircle(Image image, Color tint)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            Require(Circle, nameof(Circle));
            image.sprite = Circle;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = tint;
        }

        /// <summary>모디파이어 계열 아이콘. 계열이 None이거나 스프라이트가 비면 예외 (폴백 없음).</summary>
        public Sprite GetFamilyIcon(ModifierFamily family)
        {
            switch (family)
            {
                case ModifierFamily.Position: Require(FamilyPosition, nameof(FamilyPosition)); return FamilyPosition;
                case ModifierFamily.Dice:     Require(FamilyDice,     nameof(FamilyDice));     return FamilyDice;
                case ModifierFamily.Combo:    Require(FamilyCombo,    nameof(FamilyCombo));    return FamilyCombo;
                case ModifierFamily.Survival: Require(FamilySurvival, nameof(FamilySurvival)); return FamilySurvival;
                default:
                    throw new InvalidOperationException($"[UiSkin] 계열 {family}에는 아이콘이 없습니다 — 보상 카드로 제시할 모디파이어는 계열을 지정해야 합니다.");
            }
        }

        /// <summary>구역 플레이트 스프라이트 (월드 SpriteRenderer용). 비면 예외.</summary>
        public Sprite GetZonePlate()
        {
            Require(ZonePlate, nameof(ZonePlate));
            return ZonePlate;
        }

        private static void ApplyEntry(Image image, Sprite sprite, SkinMode mode, float ppuMultiplier)
        {
            image.sprite = sprite;
            image.color = Color.white;   // 스프라이트가 색을 가진다 — 틴트 금지
            if (mode == SkinMode.Simple)
            {
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
            }
            else
            {
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = ppuMultiplier <= 0f ? 1f : ppuMultiplier;
            }
        }

        private static void RequireButtonSprite(Sprite sprite, SkinButton button, string state)
        {
            if (sprite == null)
                throw new InvalidOperationException($"[UiSkin] 버튼 {button}.{state} 스프라이트가 비어 있습니다 — 「도구/Dice Orbit/UI 스킨 점검」으로 확인하세요.");
        }

        private static void Require(Sprite sprite, string field)
        {
            if (sprite == null)
                throw new InvalidOperationException(
                    $"[UiSkin] {field} 스프라이트가 비어 있습니다 — 「도구/Dice Orbit/UI 스킨 점검」으로 확인하세요.");
        }
    }
}
