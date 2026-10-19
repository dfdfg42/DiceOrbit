using System;
using System.IO;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DiceOrbit.EditorTools
{
    /// <summary>Imports complete Higgsfield bottle sprites and checks recruitment without selecting recruits or saving a run.</summary>
    public static class RecruitBottleLabelSetup
    {
        const string PrefabPath = "Assets/Prefabs/CharacterCard.prefab";
        const string Pending = "RecruitBottleLabel.Capture";
        const string Output = "_workspace/2026-10-19-recruit-bottles";
        static int phase;
        static double next, deadline;
        static CharacterCard selected;
        static Vector2 originalPosition;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void PrepareCaptureInput()
        {
            if (SessionState.GetBool(Pending, false) && Mouse.current == null)
                InputSystem.AddDevice<Mouse>();
        }

        public static void ApplyAndCapture()
        {
            string[] sides = { "left", "center", "right" };
            var sprites = sides.Select(side => ImportBottle("Assets/Sprites/UI Skin/recruit_bottle_" + side + ".png")).ToArray();
            // Measured centers of the blank labels in the generated source images (pixels, top-left origin).
            Vector2[] centers = { new Vector2(333, 860), new Vector2(341, 890), new Vector2(332, 891) };
            float[] angles = { 0f, 0f, 0f };
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var label = (RectTransform)root.transform.Find("BottleLabel");
                var oldBackground = label.Find("Bg");
                if (oldBackground != null) Object.DestroyImmediate(oldBackground.gameObject);
                label.sizeDelta = new Vector2(240, 64);
                var name = label.GetComponentInChildren<TextMeshProUGUI>();
                name.fontSize = 28;
                name.fontStyle = FontStyles.Normal;
                var card = root.GetComponent<CharacterCard>();
                var data = new SerializedObject(card);
                var appearances = data.FindProperty("bottleAppearances");
                appearances.arraySize = 3;
                var button = root.transform.Find("SelectButton").GetComponent<Image>();
                button.sprite = sprites[1];
                // Characters sit inside the glass; the bottle's baked label renders in front of them.
                root.transform.Find("Portrait").SetAsFirstSibling();
                for (int i = 0; i < 3; i++)
                {
                    var sprite = sprites[i];
                    float scale = Mathf.Min(button.rectTransform.rect.width / sprite.rect.width, button.rectTransform.rect.height / sprite.rect.height);
                    var center = new Vector2(centers[i].x, sprite.texture.height - centers[i].y);
                    Vector2 position = button.rectTransform.anchoredPosition + (center - sprite.rect.center) * scale;
                    var entry = appearances.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("sprite").objectReferenceValue = sprite;
                    entry.FindPropertyRelative("namePosition").vector2Value = position;
                    entry.FindPropertyRelative("nameAngle").floatValue = angles[i];
                    if (i == 1) label.anchoredPosition = position;
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Capture();
        }

        static Sprite ImportBottle(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var texture = new Texture2D(2, 2);
            texture.LoadImage(File.ReadAllBytes(path));
            int left = texture.width, right = 0, bottom = texture.height, top = 0;
            var pixels = texture.GetPixels32();
            for (int y = 0; y < texture.height; y++)
                for (int x = 0; x < texture.width; x++)
                    if (pixels[y * texture.width + x].a > 32)
                    { left = Mathf.Min(left, x); right = Mathf.Max(right, x); bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y); }
            if (right <= left || top <= bottom) throw new InvalidOperationException("Empty bottle image.");
            if (texture.GetPixel(texture.width / 2, texture.height / 2).a > .5f)
                throw new InvalidOperationException("Bottle interior is opaque: " + path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
#pragma warning disable 618
            importer.spritesheet = new[] { new SpriteMetaData { name = "Bottle", rect = new Rect(left, bottom, right - left + 1, top - bottom + 1), pivot = new Vector2(.5f, .5f), alignment = 0 } };
#pragma warning restore 618
            importer.SaveAndReimport();
            Object.DestroyImmediate(texture);
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single();
        }

        public static void Capture()
        {
            Directory.CreateDirectory(Output);
            EditorSceneManager.OpenScene("Assets/Scenes/BattleScene.unity", OpenSceneMode.Single);
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        static void Resume()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
                if (Mouse.current == null) InputSystem.AddDevice<Mouse>();
                phase = 0; next = EditorApplication.timeSinceStartup + 2; deadline = next + 60;
                EditorApplication.update += Tick;
            };
        }

        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Recruitment check timed out.");
                if (EditorApplication.timeSinceStartup < next) return;
                if (phase == 0)
                {
                    GameFlowManager.Instance.ChangeState(GameState.Recruit);
                    phase++; next = EditorApplication.timeSinceStartup + 3; return;
                }
                var ui = Object.FindAnyObjectByType<CharacterSelectionUI>();
                if (phase == 1)
                {
                    var cards = ui.GetComponentsInChildren<CharacterCard>();
                    if (cards.Length != 3 || cards.Any(c => c.transform.Find("StatsText") != null)) throw new InvalidOperationException("Recruit cards or HP removal failed.");
                    foreach (var card in cards)
                        if (card.transform.Find("BottleLabel") == null || card.transform.Find("BottleLabel/Bg") != null)
                            throw new InvalidOperationException("Name anchor is missing or separate paper label remains.");
                    if (cards.Select(c => c.transform.Find("SelectButton").GetComponent<Image>().sprite).Distinct().Count() != 3)
                        throw new InvalidOperationException("Expected distinct left, center and right bottle sprites.");
                    if (cards.Select(c => Mathf.RoundToInt(((RectTransform)c.transform).anchoredPosition.x)).Distinct().Count() != 3)
                        throw new InvalidOperationException("Bottles overlap before selection.");
                    CaptureUI("selection.png");
                    selected = cards[1];
                    originalPosition = ((RectTransform)selected.transform).anchoredPosition;
                    selected.GetComponentInChildren<Button>().onClick.Invoke();
                    phase++; next = EditorApplication.timeSinceStartup + 2; return;
                }
                if (phase == 2)
                {
                    var data = new SerializedObject(ui);
                    var detail = (GameObject)data.FindProperty("detailRoot").objectReferenceValue;
                    if (!detail.activeInHierarchy) throw new InvalidOperationException("Bottle selection did not open detail.");
                    CaptureUI("detail.png");
                    ((Button)data.FindProperty("cancelButton").objectReferenceValue).onClick.Invoke();
                    phase++; next = EditorApplication.timeSinceStartup + 2; return;
                }
                if (Vector2.Distance(originalPosition, ((RectTransform)selected.transform).anchoredPosition) > .1f) throw new InvalidOperationException("Bottle did not return after cancel.");
                Debug.Log("[RecruitBottleLabel] PASS: real recruitment screen, three front labels, no card HP, select/detail/cancel return.");
                Finish(0);
            }
            catch (Exception ex) { Debug.LogException(ex); Finish(5); }
        }

        static void CaptureUI(string filename)
        {
            var camera = Camera.main;
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1f; }
            Canvas.ForceUpdateCanvases();
            WitchTowerRoomBuilder.Capture(camera, Path.Combine(Output, filename));
        }

        static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(Pending, false);
            EditorApplication.Exit(code);
        }
    }
}
