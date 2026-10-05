#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DiceOrbit.UI.Skin;
using UnityEditor;
using UnityEngine;

namespace DiceOrbit.EditorTools
{
    /// <summary>
    /// UiSkin 에셋 점검 — 역할 파트·버튼 열거 전수: 항목 없음, 스프라이트 없음, 임시(Provisional) 스프라이트, Sliced인데 9-slice 경계 0,
    /// 텍스처 타입이 Sprite가 아님, 중복 항목. 메뉴 「도구/Dice Orbit/UI 스킨 점검」. 순수 부분(Validate)은 자가 테스트 대상.
    /// </summary>
    public static class UiSkinValidator
    {
        [MenuItem("도구/Dice Orbit/UI 스킨 점검")]
        public static void ValidateFromMenu()
        {
            UiSkin skin;
            try { skin = UiSkin.LoadOrThrow(UiSkin.ResourcePath); }
            catch (InvalidOperationException e) { Debug.LogError(e.Message); return; }

            var issues = Validate(skin);
            foreach (var issue in issues) Debug.LogError("[UiSkin] " + issue);
            if (issues.Count == 0) Debug.Log("[UiSkin] 점검 완료 — 이슈 0");
            else Debug.LogError($"[UiSkin] 이슈 {issues.Count}건 — 위 로그 확인");
        }

        /// <summary>이슈 목록을 돌려준다. 비어 있으면 정상. 빈 스킨 = 파트(Scrim 제외) + 버튼 + 아이콘 3 + 계열 아이콘 4 + 원 + 플레이트 건.</summary>
        public static List<string> Validate(UiSkin skin)
        {
            var issues = new List<string>();

            var seenParts = new HashSet<SkinPart>();
            foreach (var e in skin.Parts)
            {
                if (e == null) continue;
                if (!seenParts.Add(e.Part)) issues.Add($"{e.Part}: 항목 중복");
            }
            foreach (SkinPart part in Enum.GetValues(typeof(SkinPart)))
            {
                if (part == SkinPart.Scrim) continue;
                if (!skin.TryGetEntry(part, out var entry)) { issues.Add($"{part}: 항목 없음"); continue; }
                if (!CheckPresent(issues, entry.Sprite, part.ToString())) continue;
                if (entry.Provisional) issues.Add($"{part}: 임시 스프라이트 ({AssetDatabase.GetAssetPath(entry.Sprite)}) — 전용 아트로 교체 필요");
                if (entry.Mode == SkinMode.Sliced) CheckBorder(issues, entry.Sprite, part.ToString());
            }

            var seenButtons = new HashSet<SkinButton>();
            foreach (var b in skin.Buttons)
            {
                if (b == null) continue;
                if (!seenButtons.Add(b.Button)) issues.Add($"{b.Button}: 버튼 항목 중복");
            }
            foreach (SkinButton button in Enum.GetValues(typeof(SkinButton)))
            {
                if (!skin.TryGetButton(button, out var entry)) { issues.Add($"{button}: 버튼 항목 없음"); continue; }
                bool ok = true;
                ok &= CheckState(issues, entry, entry.Normal,   button, "Normal");
                ok &= CheckState(issues, entry, entry.Hover,    button, "Hover");
                ok &= CheckState(issues, entry, entry.Pressed,  button, "Pressed");
                ok &= CheckState(issues, entry, entry.Disabled, button, "Disabled");
                if (ok && entry.Provisional) issues.Add($"{button}: 임시 버튼 스프라이트 — 전용 아트로 교체 필요");
            }

            CheckPresent(issues, skin.Coin,            nameof(skin.Coin));
            CheckPresent(issues, skin.PotionSlotEmpty, nameof(skin.PotionSlotEmpty));
            CheckPresent(issues, skin.Close,           nameof(skin.Close));
            CheckPresent(issues, skin.FamilyPosition,  nameof(skin.FamilyPosition));
            CheckPresent(issues, skin.FamilyDice,      nameof(skin.FamilyDice));
            CheckPresent(issues, skin.FamilyCombo,     nameof(skin.FamilyCombo));
            CheckPresent(issues, skin.FamilySurvival,  nameof(skin.FamilySurvival));
            CheckPresent(issues, skin.MoveToken,          nameof(skin.MoveToken));
            CheckPresent(issues, skin.MoveTokenSpent,     nameof(skin.MoveTokenSpent));
            CheckPresent(issues, skin.ComboGemEmpty,      nameof(skin.ComboGemEmpty));
            CheckPresent(issues, skin.ComboGemLit,        nameof(skin.ComboGemLit));
            CheckPresent(issues, skin.ComboFinisherEmpty, nameof(skin.ComboFinisherEmpty));
            CheckPresent(issues, skin.ComboFinisherLit,   nameof(skin.ComboFinisherLit));
            CheckPresent(issues, skin.TokenPlate,         nameof(skin.TokenPlate));
            CheckPresent(issues, skin.Circle,          nameof(skin.Circle));
            CheckPresent(issues, skin.ZonePlate,       nameof(skin.ZonePlate));
            return issues;
        }

        private static bool CheckState(List<string> issues, SkinButtonEntry entry, Sprite sprite, SkinButton button, string state)
        {
            string field = $"{button}.{state}";
            if (!CheckPresent(issues, sprite, field)) return false;
            if (entry.Mode == SkinMode.Sliced) CheckBorder(issues, sprite, field);
            return true;
        }

        private static void CheckBorder(List<string> issues, Sprite sprite, string field)
        {
            if (sprite.border == Vector4.zero)
                issues.Add($"{field}: Sliced인데 9-slice 경계가 0 — 임포터 Sprite Border를 설정하세요 ({AssetDatabase.GetAssetPath(sprite)})");
        }

        private static bool CheckPresent(List<string> issues, Sprite sprite, string field)
        {
            if (sprite == null) { issues.Add($"{field}: 비어 있음"); return false; }
            var path = AssetDatabase.GetAssetPath(sprite);
            if (!string.IsNullOrEmpty(path)
                && AssetImporter.GetAtPath(path) is TextureImporter importer
                && importer.textureType != TextureImporterType.Sprite)
                issues.Add($"{field}: 텍스처 타입이 Sprite가 아님 ({path})");
            return true;
        }
    }
}
#endif
