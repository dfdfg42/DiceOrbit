using System;
using DiceOrbit.UI;
using DiceOrbit.UI.Skin;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.EditorTools
{
    /// <summary>
    /// 보상 화면("전리품 시트 3박자", 2026-10-03)의 씬 계층을 만든다 — 시트·박자 몸통·풋터·도장 + 템플릿 5종, RewardUI 슬롯 배선까지.
    /// 에디터 소유 패턴: 한 번 만들면 씬이 스타일을 소유한다. 이미 Sheet가 있으면 덮어쓰지 않는다
    /// (재생성하려면 RewardCanvas의 자식을 지우고 다시 실행).
    /// 메뉴 [DiceOrbit → Rebuild Reward UI Layout].
    /// </summary>
    public static class RewardUiScaffold
    {
        private const string DisplayFontPath = "Assets/TextMesh Pro/Fonts/에이투지체-4Regular SDF.asset";
        private const string BodyFontPath = "Assets/Fonts/Pretendard-Regular SDF.asset";
        private const string DieFacePath = "Assets/Sprites/Dice.png";

        private static readonly Vector2 SheetSize = new Vector2(1320f, 920f);
        private static readonly Color StampInk = new Color(0.878f, 0.337f, 0.478f);   // #E0567A — 도장 스프라이트와 같은 잉크

        private static TMP_FontAsset _display;
        private static TMP_FontAsset _body;
        private static Sprite _dieFace;
        private static UiSkin _skin;

        [MenuItem("DiceOrbit/Rebuild Reward UI Layout")]
        public static void RebuildFromMenu() => Rebuild();

        public static void Rebuild()
        {
            var ui = FindRewardUi();
            var so = new SerializedObject(ui);
            var canvas = so.FindProperty("rewardCanvas").objectReferenceValue as Canvas;
            if (canvas == null) throw new InvalidOperationException("[RewardUiScaffold] RewardUI.rewardCanvas가 비어 있다 — 캔버스를 먼저 배선할 것.");

            var root = (RectTransform)canvas.transform;
            if (root.Find("Sheet") != null)
            {
                Debug.LogWarning("[RewardUiScaffold] RewardCanvas/Sheet가 이미 있다 — 재생성하려면 RewardCanvas의 자식을 지우고 다시 실행할 것.");
                return;
            }

            _display = LoadOrThrow<TMP_FontAsset>(DisplayFontPath);
            _body = LoadOrThrow<TMP_FontAsset>(BodyFontPath);
            _dieFace = LoadOrThrow<Sprite>(DieFacePath);
            _skin = UiSkin.LoadOrThrow(UiSkin.ResourcePath);

            for (int i = root.childCount - 1; i >= 0; i--)   // 구 레이아웃(Backdrop/MainPanel/UpgradePanel) 제거
                UnityEngine.Object.DestroyImmediate(root.GetChild(i).gameObject);

            // ── 배경 스크림 ──
            var backdrop = Node("Backdrop", root);
            Stretch(backdrop);
            SkinImage(backdrop, SkinPart.Scrim).raycastTarget = true;

            // ── 시트 ──
            var sheet = Node("Sheet", root);
            Center(sheet, Vector2.zero, SheetSize);
            var sheetGroup = sheet.gameObject.AddComponent<CanvasGroup>();

            var frame = Node("Frame", sheet);
            Stretch(frame);
            SkinImage(frame, SkinPart.RewardFrame).raycastTarget = true;

            Text("Title", sheet, "승리!", _display, 46f, FontStyles.Bold, _skin.Ink, TextAlignmentOptions.Center, new Vector2(0f, -50f), new Vector2(700f, 64f));   // 물결 띠(약 88px) 안에

            var stepRow = Row("StepRow", sheet, new Vector2(0f, -150f), new Vector2(900f, 36f), 34f, TextAnchor.MiddleCenter);
            var lootRow = Row("LootRow", sheet, new Vector2(0f, -212f), new Vector2(1200f, 64f), 16f, TextAnchor.MiddleCenter);

            // "버릴 포션 고르기" 줄 — 박자 안내문과 같은 자리
            var discardRow = Node("DiscardRow", sheet);
            Top(discardRow, new Vector2(0f, -280f), new Vector2(1200f, 60f));
            Text("Label", discardRow, "가방이 가득 찼어요 — 버릴 포션을 고르세요", _body, 20f, FontStyles.Bold, _skin.Ink,
                TextAlignmentOptions.MidlineRight, new Vector2(-420f, -30f), new Vector2(380f, 40f));
            var discardChipRow = Row("DiscardChipRow", discardRow, new Vector2(195f, -30f), new Vector2(810f, 60f), 10f, TextAnchor.MiddleLeft);
            discardRow.gameObject.SetActive(false);

            // ── 강화 박자 ──
            var upgradeBody = Body("UpgradeBody", sheet, out var upgradeGroup);
            var upgradePrompt = Text("Prompt", upgradeBody, "모디파이어 하나를 골라 캐릭터에게 장착하세요", _body, 22f, FontStyles.Normal, _skin.InkMuted,
                TextAlignmentOptions.Center, new Vector2(0f, -280f), new Vector2(1100f, 40f));
            var cardRow = Row("CardRow", upgradeBody, new Vector2(0f, -478f), new Vector2(1200f, 320f), 26f, TextAnchor.MiddleCenter);
            var partyRow = Row("PartyRow", upgradeBody, new Vector2(0f, -706f), new Vector2(1200f, 130f), 36f, TextAnchor.MiddleCenter);
            upgradeBody.gameObject.SetActive(false);

            // ── 주사위 박자 ──
            var diceBody = Body("DiceBody", sheet, out var diceGroup);
            var dicePrompt = Text("Prompt", diceBody, "새 주사위와 바꿀 덱 주사위를 고르세요", _body, 22f, FontStyles.Normal, _skin.InkMuted,
                TextAlignmentOptions.Center, new Vector2(0f, -280f), new Vector2(1100f, 40f));
            // 새 주사위 → 내 덱: 한 줄로 묶어 가운데 정렬 (덱 장수에 따라 폭이 달라진다)
            // 폭은 부모(DiceRow)가 자식의 선호 폭으로 잡는다 — 덱 줄의 선호 폭 = 카드 폭 합. (자식에 ContentSizeFitter를 두면
            // 부모가 갱신 전 폭으로 배치해 덱 줄이 새 주사위 카드 위로 겹친다.)
            var diceRow = Row("DiceRow", diceBody, new Vector2(0f, -526f), new Vector2(1220f, 420f), 26f, TextAnchor.MiddleCenter);
            diceRow.GetComponent<HorizontalLayoutGroup>().childControlWidth = true;
            var newDieCard = BuildNewDieCard(diceRow);
            Text("Arrow", diceRow, "→", _body, 56f, FontStyles.Bold, _skin.InkMuted, TextAlignmentOptions.Center, Vector2.zero, new Vector2(60f, 80f));
            var deckRow = Row("DeckRow", diceRow, Vector2.zero, new Vector2(400f, 290f), 12f, TextAnchor.MiddleCenter);
            diceBody.gameObject.SetActive(false);

            // ── 마무리 박자 ──
            var wrapBody = Body("WrapBody", sheet, out var wrapGroup);
            var wrapMessage = Text("Message", wrapBody, "전리품을 챙겼습니다.", _body, 28f, FontStyles.Normal, _skin.Ink,
                TextAlignmentOptions.Center, new Vector2(0f, -520f), new Vector2(1000f, 200f));
            wrapBody.gameObject.SetActive(false);

            // ── 풋터 ──
            var hint = Text("Hint", sheet, "", _body, 19f, FontStyles.Normal, _skin.InkMuted, TextAlignmentOptions.MidlineLeft,
                new Vector2(-330f, -840f), new Vector2(560f, 60f));
            var secondary = FooterButton("SecondaryButton", sheet, SkinButton.RewardSkip, new Vector2(130f, -840f), new Vector2(220f, 62f), 26f, 0f, out var secondaryLabel);
            var primary = FooterButton("PrimaryButton", sheet, SkinButton.RewardContinue, new Vector2(440f, -840f), new Vector2(340f, 66f), 28f, 44f, out var primaryLabel);

            // ── 도장 ──
            var stamp = Node("Stamp", sheet);
            Top(stamp, new Vector2(0f, -490f), new Vector2(280f, 280f));
            stamp.localRotation = Quaternion.Euler(0f, 0f, 12f);
            SkinImage(stamp, SkinPart.RewardStamp).raycastTarget = false;
            var stampGroup = stamp.gameObject.AddComponent<CanvasGroup>();
            stampGroup.alpha = 0f; stampGroup.blocksRaycasts = false; stampGroup.interactable = false;
            var stampLabel = Text("Label", stamp, "장착!", _display, 60f, FontStyles.Bold, StampInk, TextAlignmentOptions.Center, Vector2.zero, Vector2.zero);
            Stretch(stampLabel.rectTransform);

            // ── 템플릿 (비활성 — 런타임에 복제) ──
            var templates = Node("Templates", root);
            Center(templates, Vector2.zero, new Vector2(100f, 100f));
            var lootChip = BuildLootChip(templates);
            var stepPip = BuildStepPip(templates);
            var modifierCard = BuildModifierCard(templates);
            var portrait = BuildPortrait(templates);
            var dieCard = BuildDeckDieCard(templates);
            templates.gameObject.SetActive(false);

            // ── RewardUI 슬롯 배선 ──
            Wire(ui,
                ("sheet", sheet), ("sheetGroup", sheetGroup), ("stepRow", stepRow), ("lootRow", lootRow),
                ("discardRow", discardRow.gameObject), ("discardChipRow", discardChipRow),
                ("upgradeBody", upgradeGroup), ("upgradePrompt", upgradePrompt), ("cardRow", cardRow), ("partyRow", partyRow),
                ("diceBody", diceGroup), ("dicePrompt", dicePrompt), ("newDieCard", newDieCard), ("deckRow", deckRow),
                ("wrapBody", wrapGroup), ("wrapMessage", wrapMessage),
                ("primaryButton", primary), ("primaryLabel", primaryLabel), ("secondaryButton", secondary), ("secondaryLabel", secondaryLabel), ("hintText", hint),
                ("stamp", stamp), ("stampGroup", stampGroup), ("stampLabel", stampLabel),
                ("lootChipTemplate", lootChip), ("stepPipTemplate", stepPip), ("modifierCardTemplate", modifierCard),
                ("portraitTemplate", portrait), ("dieCardTemplate", dieCard));

            EditorUtility.SetDirty(ui);
            EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
            Debug.Log("[RewardUiScaffold] 보상 UI 레이아웃 생성 완료 — 씬을 저장할 것.");
        }

        // ═══════════════════════════════════════════════════════
        // 템플릿
        // ═══════════════════════════════════════════════════════

        private static RewardLootChip BuildLootChip(RectTransform parent)
        {
            var root = Node("LootChip", parent);
            Center(root, Vector2.zero, new Vector2(262f, 64f));
            var group = root.gameObject.AddComponent<CanvasGroup>();

            var bg = Node("Bg", root);
            Stretch(bg);
            var bgImage = SkinImage(bg, SkinPart.RewardLootChip);
            bgImage.raycastTarget = true;

            var icon = Node("Icon", root);
            icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.anchoredPosition = new Vector2(36f, 1f);
            icon.sizeDelta = new Vector2(42f, 42f);
            var iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.preserveAspect = true; iconImage.raycastTarget = false;

            var label = Text("Label", root, "+50", _display, 24f, FontStyles.Bold, _skin.Ink, TextAlignmentOptions.Center, Vector2.zero, Vector2.zero);
            Stretch(label.rectTransform, 74f, 4f, 58f, 4f);
            label.enableAutoSizing = true; label.fontSizeMin = 15f; label.fontSizeMax = 24f;
            label.textWrappingMode = TextWrappingModes.NoWrap;

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = bgImage;
            button.transition = Selectable.Transition.None;
            var hover = root.gameObject.AddComponent<RewardHoverInfo>();

            var view = root.gameObject.AddComponent<RewardLootChip>();
            Wire(view, ("icon", iconImage), ("label", label), ("group", group), ("button", button), ("hover", hover));
            return view;
        }

        private static RewardStepPip BuildStepPip(RectTransform parent)
        {
            // 점 + 글자를 가로 레이아웃으로 묶고 폭을 글자에 맞춘다 — 글자 길이가 달라도 간격이 고르게
            var root = Node("StepPip", parent);
            Center(root, Vector2.zero, new Vector2(100f, 36f));
            var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
            var fitter = root.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var dot = Node("Dot", root);
            var dotImage = dot.gameObject.AddComponent<Image>();
            dotImage.raycastTarget = false;
            _skin.ApplyCircle(dotImage, _skin.PaperDeep);
            var dotSize = dot.gameObject.AddComponent<LayoutElement>();
            dotSize.preferredWidth = 16f; dotSize.preferredHeight = 16f;

            var label = Text("Label", root, "전리품", _body, 20f, FontStyles.Normal, _skin.InkMuted, TextAlignmentOptions.MidlineLeft, Vector2.zero, new Vector2(80f, 28f));
            label.textWrappingMode = TextWrappingModes.NoWrap;

            var view = root.gameObject.AddComponent<RewardStepPip>();
            Wire(view, ("dot", dotImage), ("label", label));
            return view;
        }

        private static RewardModifierCard BuildModifierCard(RectTransform parent)
        {
            // 카드 스프라이트 캔버스는 선택 테두리·리본만큼 여백을 가진다 (위 약 17, 옆·아래 약 12) + 안쪽 테두리선 약 26
            // — 내용은 그 안쪽에 놓는다.
            var root = Node("ModifierCard", parent);
            Center(root, Vector2.zero, new Vector2(300f, 320f));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            var button = CardButton(root, raycast: true);

            var icon = Node("FamilyIcon", root);
            Top(icon, new Vector2(0f, -68f), new Vector2(62f, 62f));
            var iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.preserveAspect = true; iconImage.raycastTarget = false;
            iconImage.sprite = _skin.FamilyDice;

            var family = Text("FamilyLabel", root, "주사위", _body, 15f, FontStyles.Normal, _skin.InkMuted, TextAlignmentOptions.Center, new Vector2(0f, -110f), new Vector2(216f, 20f));
            var name = Text("Name", root, "합류 전술", _display, 25f, FontStyles.Bold, _skin.Ink, TextAlignmentOptions.Center, new Vector2(0f, -138f), new Vector2(216f, 34f));
            name.textWrappingMode = TextWrappingModes.NoWrap; name.enableAutoSizing = true; name.fontSizeMin = 17f; name.fontSizeMax = 25f;
            var desc = Text("Desc", root, "주사위 눈이 4 이상이면 이동 후 자동공격 피해가 8% 증가합니다.", _body, 17f, FontStyles.Normal, _skin.Ink, TextAlignmentOptions.Top, new Vector2(0f, -204f), new Vector2(212f, 92f));
            desc.enableAutoSizing = true; desc.fontSizeMin = 13f; desc.fontSizeMax = 17f;
            var status = Text("Status", root, "전사 · 신규", _body, 16f, FontStyles.Bold, _skin.Modifier, TextAlignmentOptions.Center, new Vector2(0f, -268f), new Vector2(216f, 26f));
            status.textWrappingMode = TextWrappingModes.NoWrap;

            var view = root.gameObject.AddComponent<RewardModifierCard>();
            Wire(view, ("button", button), ("familyIcon", iconImage), ("familyLabel", family), ("nameText", name), ("descText", desc), ("statusText", status), ("group", group));
            return view;
        }

        private static RewardPartyPortrait BuildPortrait(RectTransform parent)
        {
            var root = Node("PartyPortrait", parent);
            Center(root, Vector2.zero, new Vector2(150f, 130f));
            var group = root.gameObject.AddComponent<CanvasGroup>();

            var frame = Node("Frame", root);
            Top(frame, new Vector2(0f, -48f), new Vector2(96f, 96f));
            var frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.raycastTarget = true;
            var button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = frameImage;
            frame.gameObject.AddComponent<UiSkinButton>().SetButton(SkinButton.RewardPortrait);
            var hover = frame.gameObject.AddComponent<RewardHoverInfo>();

            var face = Node("Face", frame);
            Center(face, new Vector2(0f, 4f), new Vector2(58f, 58f));
            var faceImage = face.gameObject.AddComponent<Image>();
            faceImage.preserveAspect = true; faceImage.raycastTarget = false;

            var name = Text("Name", root, "전사", _display, 19f, FontStyles.Bold, _skin.Ink, TextAlignmentOptions.Center, new Vector2(0f, -106f), new Vector2(150f, 22f));
            name.textWrappingMode = TextWrappingModes.NoWrap;
            var pips = Text("Pips", root, "모디파이어 0", _body, 14f, FontStyles.Normal, _skin.InkMuted, TextAlignmentOptions.Center, new Vector2(0f, -122f), new Vector2(150f, 16f));
            pips.textWrappingMode = TextWrappingModes.NoWrap;

            var view = root.gameObject.AddComponent<RewardPartyPortrait>();
            Wire(view, ("button", button), ("face", faceImage), ("nameText", name), ("pipsText", pips), ("group", group), ("hover", hover));
            return view;
        }

        /// <summary>내 덱 카드 (작은 카드, 클릭 = 교체 대상).</summary>
        private static RewardDieCard BuildDeckDieCard(RectTransform parent)
        {
            var root = Node("DieCard", parent);
            Center(root, Vector2.zero, new Vector2(188f, 290f));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            var button = CardButton(root, raycast: true);

            // 선택 리본(우상단)이 이름을 가리지 않게 이름은 리본 아래에서 시작
            var name = Text("Name", root, "표준 주사위", _display, 18f, FontStyles.Bold, _skin.Ink, TextAlignmentOptions.Center, new Vector2(0f, -84f), new Vector2(136f, 24f));
            name.textWrappingMode = TextWrappingModes.NoWrap; name.enableAutoSizing = true; name.fontSizeMin = 13f; name.fontSizeMax = 18f;
            var rarity = Text("Rarity", root, "일반", _body, 13f, FontStyles.Normal, _skin.InkMuted, TextAlignmentOptions.Center, new Vector2(0f, -102f), new Vector2(128f, 16f));
            var faces = FaceGrid(root, new Vector2(0f, -156f), 40f, 5f, 19f);
            var effect = Text("Effect", root, "사용 효과 없음", _body, 13f, FontStyles.Normal, _skin.InkMuted, TextAlignmentOptions.Top, new Vector2(0f, -228f), new Vector2(136f, 44f));
            effect.enableAutoSizing = true; effect.fontSizeMin = 10f; effect.fontSizeMax = 13f;

            var view = root.gameObject.AddComponent<RewardDieCard>();
            Wire(view, ("button", button), ("nameText", name), ("rarityText", rarity), ("effectText", effect), ("group", group));
            WireArray(view, "faceLabels", faces);
            WireFloat(view, "borderScaleOverride", 3.2f);   // 작은 카드 — 테두리·리본을 카드 크기에 맞게 가늘게
            return view;
        }

        /// <summary>새 주사위 카드 (큰 카드, 씬 고정 — 눌리지 않는다. 강조 테두리로 표시).</summary>
        private static RewardDieCard BuildNewDieCard(RectTransform parent)
        {
            var root = Node("NewDieCard", parent);
            Center(root, Vector2.zero, new Vector2(280f, 420f));
            var width = root.gameObject.AddComponent<LayoutElement>();   // DiceRow가 폭을 잡는다
            width.minWidth = 280f; width.preferredWidth = 280f;
            var group = root.gameObject.AddComponent<CanvasGroup>();
            var button = CardButton(root, raycast: false);

            Text("Tag", root, "새 주사위", _body, 17f, FontStyles.Bold, _skin.Modifier, TextAlignmentOptions.Center, new Vector2(0f, -72f), new Vector2(160f, 22f));
            var name = Text("Name", root, "전진 주사위", _display, 28f, FontStyles.Bold, _skin.Ink, TextAlignmentOptions.Center, new Vector2(0f, -108f), new Vector2(204f, 36f));
            name.textWrappingMode = TextWrappingModes.NoWrap; name.enableAutoSizing = true; name.fontSizeMin = 18f; name.fontSizeMax = 28f;
            var rarity = Text("Rarity", root, "일반", _body, 16f, FontStyles.Normal, _skin.InkMuted, TextAlignmentOptions.Center, new Vector2(0f, -138f), new Vector2(204f, 20f));
            var faces = FaceGrid(root, new Vector2(0f, -216f), 60f, 8f, 30f);
            var effect = Text("Effect", root, "사용 효과 없음", _body, 17f, FontStyles.Normal, _skin.Ink, TextAlignmentOptions.Top, new Vector2(0f, -312f), new Vector2(208f, 48f));
            effect.enableAutoSizing = true; effect.fontSizeMin = 12f; effect.fontSizeMax = 17f;
            var description = Text("Description", root, "", _body, 14f, FontStyles.Normal, _skin.InkMuted, TextAlignmentOptions.Top, new Vector2(0f, -362f), new Vector2(208f, 40f));
            description.enableAutoSizing = true; description.fontSizeMin = 11f; description.fontSizeMax = 14f;

            var view = root.gameObject.AddComponent<RewardDieCard>();
            Wire(view, ("button", button), ("nameText", name), ("rarityText", rarity), ("effectText", effect), ("descriptionText", description), ("group", group));
            WireArray(view, "faceLabels", faces);
            return view;
        }

        /// <summary>6면 격자 (3열 × 2행) — 칸 = 주사위 면 그림 + 숫자.</summary>
        private static TextMeshProUGUI[] FaceGrid(RectTransform parent, Vector2 pos, float cell, float gap, float fontSize)
        {
            var grid = Node("Faces", parent);
            Top(grid, pos, new Vector2(cell * 3f + gap * 2f, cell * 2f + gap));
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(cell, cell);
            layout.spacing = new Vector2(gap, gap);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            layout.childAlignment = TextAnchor.MiddleCenter;

            var labels = new TextMeshProUGUI[6];
            for (int i = 0; i < 6; i++)
            {
                var face = Node("Face" + i, grid);
                var image = face.gameObject.AddComponent<Image>();
                image.sprite = _dieFace; image.preserveAspect = true; image.raycastTarget = false;
                var label = Text("Value", face, (i + 1).ToString(), _body, fontSize, FontStyles.Bold, _skin.Ink, TextAlignmentOptions.Center, Vector2.zero, Vector2.zero);
                Stretch(label.rectTransform);
                labels[i] = label;
            }
            return labels;
        }

        // ═══════════════════════════════════════════════════════
        // 조립 도우미
        // ═══════════════════════════════════════════════════════

        /// <summary>카드 루트에 버튼을 달고, 배경은 레이아웃과 무관한 자식 Bg로 분리한다 (체크리스트 7).</summary>
        private static Button CardButton(RectTransform root, bool raycast)
        {
            var bg = Node("Bg", root);
            Stretch(bg);
            var image = bg.gameObject.AddComponent<Image>();
            image.raycastTarget = raycast;

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            root.gameObject.AddComponent<UiSkinButton>().SetButton(SkinButton.RewardChoiceCard);
            return button;
        }

        private static Button FooterButton(string name, RectTransform parent, SkinButton role, Vector2 pos, Vector2 size,
                                           float fontSize, float labelRightInset, out TextMeshProUGUI label)
        {
            var root = Node(name, parent);
            Top(root, pos, size);
            var image = root.gameObject.AddComponent<Image>();
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            root.gameObject.AddComponent<UiSkinButton>().SetButton(role);

            label = Text("Label", root, "계속", _display, fontSize, FontStyles.Bold, _skin.Ink, TextAlignmentOptions.Center, Vector2.zero, Vector2.zero);
            Stretch(label.rectTransform, 18f, 4f, 18f + labelRightInset, 6f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true; label.fontSizeMin = 16f; label.fontSizeMax = fontSize;
            return button;
        }

        private static RectTransform Body(string name, RectTransform sheet, out CanvasGroup group)
        {
            var body = Node(name, sheet);
            Stretch(body);
            group = body.gameObject.AddComponent<CanvasGroup>();
            return body;
        }

        /// <summary>가로 한 줄 컨테이너 — 자식 크기는 자식이 정한다 (템플릿 고정 크기).</summary>
        private static RectTransform Row(string name, RectTransform parent, Vector2 pos, Vector2 size, float spacing, TextAnchor alignment)
        {
            var row = Node(name, parent);
            Top(row, pos, size);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.childControlWidth = false; layout.childControlHeight = false;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
            return row;
        }

        private static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>부모 위쪽 중앙 기준 배치 — pos.y는 아래로 음수, 요소 중심 좌표.</summary>
        private static void Top(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static void Center(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static void Stretch(RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        private static Image SkinImage(RectTransform rt, SkinPart part)
        {
            var image = rt.gameObject.AddComponent<Image>();
            rt.gameObject.AddComponent<UiSkinImage>().SetPart(part);
            return image;
        }

        private static TextMeshProUGUI Text(string name, RectTransform parent, string text, TMP_FontAsset font, float size, FontStyles style,
                                            Color color, TextAlignmentOptions alignment, Vector2 pos, Vector2 rectSize)
        {
            var rt = Node(name, parent);
            Top(rt, pos, rectSize);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return tmp;
        }

        private static void Wire(UnityEngine.Object target, params (string field, UnityEngine.Object value)[] pairs)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in pairs)
            {
                var prop = so.FindProperty(field);
                if (prop == null) throw new InvalidOperationException($"[RewardUiScaffold] {target.GetType().Name}.{field} 필드가 없다.");
                if (value == null) throw new InvalidOperationException($"[RewardUiScaffold] {target.GetType().Name}.{field}에 넣을 값이 null이다.");
                prop.objectReferenceValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireArray(UnityEngine.Object target, string field, UnityEngine.Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) throw new InvalidOperationException($"[RewardUiScaffold] {target.GetType().Name}.{field} 필드가 없다.");
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireFloat(UnityEngine.Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) throw new InvalidOperationException($"[RewardUiScaffold] {target.GetType().Name}.{field} 필드가 없다.");
            prop.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T LoadOrThrow<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException($"[RewardUiScaffold] {path} 를 {typeof(T).Name}(으)로 불러올 수 없다.");
            return asset;
        }

        private static RewardUI FindRewardUi()
        {
            var scene = EditorSceneManager.GetActiveScene();
            foreach (var ui in Resources.FindObjectsOfTypeAll<RewardUI>())
                if (ui.gameObject.scene == scene) return ui;
            throw new InvalidOperationException($"[RewardUiScaffold] 활성 씬 '{scene.name}'에 RewardUI가 없다.");
        }
    }
}
