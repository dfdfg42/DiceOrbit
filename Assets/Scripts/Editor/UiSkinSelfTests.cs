using System;
using DiceOrbit.UI.Skin;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.EditorTools
{
    /// <summary>
    /// UiSkin 순수 로직 자가 테스트 (2026-09-25 리스킨 2단계 → 2026-09-26 역할 카탈로그). 씬·플레이 모드 불필요.
    /// 메뉴 [DiceOrbit → Run UiSkin Self-Tests] 또는 MCP RunCommand에서 RunAll() 호출.
    /// </summary>
    public static class UiSkinSelfTests
    {
        private static int _failures;

        [MenuItem("DiceOrbit/Run UiSkin Self-Tests")]
        public static void RunFromMenu() => RunAll();

        public static bool RunAll()
        {
            _failures = 0;

            TestLoadOrThrowOnMissingAsset();
            TestApplySlicedSetsSpriteTypeAndWhite();
            TestApplySimpleKeepsAspect();
            TestApplyThrowsWhenPartMissing();
            TestApplyThrowsWhenSpriteEmpty();
            TestApplyButtonSetsSpriteSwap();
            TestApplyButtonThrowsWhenStateMissing();
            TestApplyButtonSelectedUsesPressedAsBase();
            TestGetSpriteCoversEveryPart();
            TestValidatorReportsEmptySkin();
            TestValidatorPassesCompleteSkin();
            TestValidatorFlagsProvisionalAndDuplicate();
            TestScrimPartIsColorOnly();
            TestApplyCircleKeepsTint();
            TestUiSkinImageAppliesPart();
            TestUiSkinButtonAppliesRole();
            TestZonePlateRequire();

            if (_failures == 0) Debug.Log("[SelfTest] 전체 PASS — UiSkin");
            else Debug.LogError($"[SelfTest] 실패 {_failures}건 — 위 로그 확인");
            return _failures == 0;
        }

        private static void Check(bool condition, string label)
        {
            if (condition) return;
            _failures++;
            Debug.LogError("[SelfTest] FAIL: " + label);
        }

        private static Sprite MakeSprite(bool withBorder)
        {
            var tex = new Texture2D(8, 8);
            var border = withBorder ? new Vector4(2, 2, 2, 2) : Vector4.zero;
            return Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        private static UiSkin MakeCompleteSkin()
        {
            var s = ScriptableObject.CreateInstance<UiSkin>();
            foreach (SkinPart part in Enum.GetValues(typeof(SkinPart)))
            {
                if (part == SkinPart.Scrim) continue;
                s.SetEntry(part, MakeSprite(true), SkinMode.Sliced);
            }
            foreach (SkinButton button in Enum.GetValues(typeof(SkinButton)))
                s.SetButton(button, MakeSprite(true), MakeSprite(true), MakeSprite(true), MakeSprite(true), SkinMode.Sliced);
            s.Coin = MakeSprite(false); s.PotionSlotEmpty = MakeSprite(false); s.Close = MakeSprite(false);
            s.FamilyPosition = MakeSprite(false); s.FamilyDice = MakeSprite(false); s.FamilyCombo = MakeSprite(false); s.FamilySurvival = MakeSprite(false);
            s.MoveToken = MakeSprite(false); s.MoveTokenSpent = MakeSprite(false);
            s.ComboGemEmpty = MakeSprite(false); s.ComboGemLit = MakeSprite(false);
            s.ComboFinisherEmpty = MakeSprite(false); s.ComboFinisherLit = MakeSprite(false); s.TokenPlate = MakeSprite(false);
            s.Circle = MakeSprite(false);
            s.ZonePlate = MakeSprite(false);
            return s;
        }

        private static int ExpectedEmptyIssueCount()
            => (Enum.GetValues(typeof(SkinPart)).Length - 1) + Enum.GetValues(typeof(SkinButton)).Length + 16;   // 아이콘 3 + 계열 아이콘 4 + 머리 위 표시 7 + 원 + 플레이트

        private static void TestLoadOrThrowOnMissingAsset()
        {
            bool threw = false;
            try { UiSkin.LoadOrThrow("UI/DoesNotExist_UiSkin"); }
            catch (InvalidOperationException) { threw = true; }
            Check(threw, "없는 스킨 경로는 InvalidOperationException (폴백 없음)");
        }

        private static void TestApplySlicedSetsSpriteTypeAndWhite()
        {
            var skin = MakeCompleteSkin();
            skin.GetEntry(SkinPart.PlainPanel).PixelsPerUnitMultiplier = 2f;
            var go = new GameObject("skin-test", typeof(Image));
            var img = go.GetComponent<Image>();
            img.color = Color.red;
            skin.Apply(img, SkinPart.PlainPanel);
            Check(img.sprite == skin.GetSprite(SkinPart.PlainPanel), "Apply(PlainPanel): 스프라이트 지정");
            Check(img.type == Image.Type.Sliced, "Apply(PlainPanel): Image.Type.Sliced");
            Check(Mathf.Approximately(img.pixelsPerUnitMultiplier, 2f), "Apply(PlainPanel): pixelsPerUnitMultiplier 항목값");
            Check(img.color == Color.white, "Apply: 색 = white (틴트 금지)");
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(skin);
        }

        private static void TestApplySimpleKeepsAspect()
        {
            var skin = MakeCompleteSkin();
            skin.SetEntry(SkinPart.RewardFrame, MakeSprite(false), SkinMode.Simple);
            var go = new GameObject("skin-simple", typeof(Image));
            var img = go.GetComponent<Image>();
            skin.Apply(img, SkinPart.RewardFrame);
            Check(img.type == Image.Type.Simple && img.preserveAspect, "Apply(Simple 파트): Simple + preserveAspect");
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(skin);
        }

        private static void TestApplyThrowsWhenPartMissing()
        {
            var skin = ScriptableObject.CreateInstance<UiSkin>();
            var go = new GameObject("skin-test", typeof(Image));
            bool threw = false;
            try { skin.Apply(go.GetComponent<Image>(), SkinPart.PlainPanel); }
            catch (InvalidOperationException) { threw = true; }
            Check(threw, "항목 없는 파트 Apply는 InvalidOperationException (폴백 없음)");
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(skin);
        }

        private static void TestApplyThrowsWhenSpriteEmpty()
        {
            var skin = ScriptableObject.CreateInstance<UiSkin>();
            skin.SetEntry(SkinPart.PlainPanel, null, SkinMode.Sliced);
            var go = new GameObject("skin-test", typeof(Image));
            bool threw = false;
            try { skin.Apply(go.GetComponent<Image>(), SkinPart.PlainPanel); }
            catch (InvalidOperationException) { threw = true; }
            Check(threw, "스프라이트 빈 파트 Apply는 InvalidOperationException (폴백 없음)");
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(skin);
        }

        private static void TestApplyButtonSetsSpriteSwap()
        {
            var skin = MakeCompleteSkin();
            var go = new GameObject("skin-btn", typeof(Image), typeof(Button));
            var btn = go.GetComponent<Button>();
            skin.ApplyButton(btn, SkinButton.EndTurn);
            var set = skin.GetButton(SkinButton.EndTurn);
            Check(btn.transition == Selectable.Transition.SpriteSwap, "ApplyButton: transition = SpriteSwap");
            Check(btn.spriteState.highlightedSprite == set.Hover, "ApplyButton: highlighted = Hover");
            Check(btn.spriteState.pressedSprite == set.Pressed, "ApplyButton: pressed = Pressed");
            Check(btn.spriteState.disabledSprite == set.Disabled, "ApplyButton: disabled = Disabled");
            Check(btn.targetGraphic is Image tg && tg.sprite == set.Normal && tg.type == Image.Type.Sliced, "ApplyButton: targetGraphic = Normal, Sliced");
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(skin);
        }

        private static void TestApplyButtonSelectedUsesPressedAsBase()
        {
            var skin = ScriptableObject.CreateInstance<UiSkin>();
            Sprite n = MakeSprite(true), h = MakeSprite(true), p = MakeSprite(true), d = MakeSprite(true);
            skin.SetButton(SkinButton.RewardChoiceCard, n, h, p, d, SkinMode.Sliced);
            var go = new GameObject("selectable", typeof(Image), typeof(Button));
            var btn = go.GetComponent<Button>();
            var img = go.GetComponent<Image>();

            skin.ApplyButtonSelected(btn, SkinButton.RewardChoiceCard, true);
            Check(img.sprite == p, "선택: 바탕 = Pressed");
            Check(btn.spriteState.highlightedSprite == p, "선택: 호버도 Pressed (선택이 풀려 보이지 않게)");
            Check(btn.spriteState.disabledSprite == d, "선택: 비활성 스프라이트는 그대로");

            skin.ApplyButtonSelected(btn, SkinButton.RewardChoiceCard, false);
            Check(img.sprite == n, "해제: 바탕 = Normal");
            Check(btn.spriteState.highlightedSprite == h, "해제: 호버 = Hover");

            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(skin);
        }

        private static void TestApplyButtonThrowsWhenStateMissing()
        {
            var skin = MakeCompleteSkin();
            skin.GetButton(SkinButton.Move).Disabled = null;
            var go = new GameObject("skin-btn", typeof(Image), typeof(Button));
            bool threw = false;
            try { skin.ApplyButton(go.GetComponent<Button>(), SkinButton.Move); }
            catch (InvalidOperationException) { threw = true; }
            Check(threw, "상태 하나라도 빈 버튼 ApplyButton은 InvalidOperationException");
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(skin);
        }

        private static void TestGetSpriteCoversEveryPart()
        {
            var skin = MakeCompleteSkin();
            foreach (SkinPart part in Enum.GetValues(typeof(SkinPart)))
            {
                if (part == SkinPart.Scrim) { Check(skin.GetSprite(part) == null, "GetSprite(Scrim) == null (색만)"); continue; }
                Check(skin.GetSprite(part) != null, $"GetSprite({part}) != null");
            }
            foreach (SkinButton button in Enum.GetValues(typeof(SkinButton)))
                Check(skin.GetButton(button).Normal != null, $"GetButton({button}).Normal != null");
            UnityEngine.Object.DestroyImmediate(skin);
        }

        private static void TestValidatorReportsEmptySkin()
        {
            var skin = ScriptableObject.CreateInstance<UiSkin>();
            var issues = UiSkinValidator.Validate(skin);
            int expected = ExpectedEmptyIssueCount();
            Check(issues.Count == expected, $"빈 스킨 이슈 {expected}건 (파트 + 버튼 + 아이콘·도형), 실제 {issues.Count}");
            UnityEngine.Object.DestroyImmediate(skin);
        }

        private static void TestValidatorPassesCompleteSkin()
        {
            var skin = MakeCompleteSkin();
            var issues = UiSkinValidator.Validate(skin);
            Check(issues.Count == 0, "완전한 스킨 이슈 0 — " + string.Join(" / ", issues));
            UnityEngine.Object.DestroyImmediate(skin);
        }

        private static void TestValidatorFlagsProvisionalAndDuplicate()
        {
            var skin = MakeCompleteSkin();
            skin.GetEntry(SkinPart.EventFrame).Provisional = true;
            skin.GetButton(SkinButton.Goods).Provisional = true;
            skin.Parts.Add(new SkinEntry { Part = SkinPart.Divider, Sprite = MakeSprite(true) });
            var issues = UiSkinValidator.Validate(skin);
            Check(issues.Count == 3, "임시 파트 1 + 임시 버튼 1 + 중복 1 = 이슈 3, 실제 " + issues.Count + " — " + string.Join(" / ", issues));
            UnityEngine.Object.DestroyImmediate(skin);
        }

        private static void TestScrimPartIsColorOnly()
        {
            var skin = MakeCompleteSkin();
            var go = new GameObject("skin-scrim", typeof(Image));
            var img = go.GetComponent<Image>();
            img.sprite = skin.GetSprite(SkinPart.InfoFrame);
            skin.Apply(img, SkinPart.Scrim);
            Check(img.sprite == null && img.type == Image.Type.Simple && img.color == skin.Scrim, "Apply(Scrim): sprite null·Simple·Scrim 색");
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(skin);
        }

        private static void TestApplyCircleKeepsTint()
        {
            var skin = MakeCompleteSkin();
            var go = new GameObject("skin-circle", typeof(Image));
            var img = go.GetComponent<Image>();
            skin.ApplyCircle(img, Color.red);
            Check(img.sprite == skin.Circle && img.type == Image.Type.Simple && img.preserveAspect && img.color == Color.red, "ApplyCircle: Circle·Simple·preserveAspect·틴트 유지");
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(skin);
        }

        private static void TestUiSkinImageAppliesPart()
        {
            // 실제 Resources/UI/UiSkin.asset을 쓴다 (씬 배치 컴포넌트의 통합 확인)
            var go = new GameObject("skin-image", typeof(Image), typeof(DiceOrbit.UI.UiSkinImage));
            var comp = go.GetComponent<DiceOrbit.UI.UiSkinImage>();
            comp.SetPart(SkinPart.PlainPanel);
            var img = go.GetComponent<Image>();
            Check(img.sprite == UiSkin.Current.GetSprite(SkinPart.PlainPanel), "UiSkinImage.SetPart(PlainPanel): 카탈로그 스프라이트");
            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void TestUiSkinButtonAppliesRole()
        {
            var go = new GameObject("skin-button", typeof(Image), typeof(Button), typeof(DiceOrbit.UI.UiSkinButton));
            var comp = go.GetComponent<DiceOrbit.UI.UiSkinButton>();
            comp.SetButton(SkinButton.EndTurn);
            var btn = go.GetComponent<Button>();
            var entry = UiSkin.Current.GetButton(SkinButton.EndTurn);
            Check(btn.transition == Selectable.Transition.SpriteSwap && btn.spriteState.pressedSprite == entry.Pressed, "UiSkinButton.SetButton(EndTurn): SpriteSwap + 카탈로그 상태");
            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void TestZonePlateRequire()
        {
            var empty = ScriptableObject.CreateInstance<UiSkin>();
            bool threw = false;
            try { empty.GetZonePlate(); }
            catch (InvalidOperationException) { threw = true; }
            Check(threw, "빈 ZonePlate로 GetZonePlate는 InvalidOperationException (폴백 없음)");
            UnityEngine.Object.DestroyImmediate(empty);

            var skin = MakeCompleteSkin();
            Check(skin.GetZonePlate() == skin.ZonePlate, "GetZonePlate: 채워진 스프라이트 반환");
            UnityEngine.Object.DestroyImmediate(skin);
        }
    }
}
