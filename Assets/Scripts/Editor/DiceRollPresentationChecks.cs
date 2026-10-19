using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceOrbit.Data;
using DiceOrbit.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace DiceOrbit.EditorTools
{
    /// <summary>Batch-only checks and captures. Never saves a scene or starts/saves a player run.</summary>
    public static class DiceRollPresentationChecks
    {
        const string Pending = "DiceRollChecks.Pending";
        const string Output = "_workspace/2026-10-19-dice-roll";
        static DiceUI ui;
        static DiceRollAnimator animator;
        static Transform container;
        static List<DiceData> data;
        static Random.State randomState;
        static int phase, complete, previousFrame, captureFrame;
        static float next, captureStart;
        static double deadline;

        public static void CheckContract()
        {
            try
            {
                if (typeof(DiceRollAnimator).Assembly.GetType("DiceOrbit.UI.DiceRollStage") == null)
                    throw new Exception("Expected a real 3D dice presentation stage; current animator only spins UI.");
                if (typeof(DiceRollAnimator).GetMethod("CancelAnimation") == null)
                    throw new Exception("Expected explicit cancellation before a reroll replaces its elements.");
                Debug.Log("DICE_ROLL_CONTRACT_PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogError("DICE_ROLL_CONTRACT_FAIL: " + e.Message); EditorApplication.Exit(1); }
        }

        public static void RunAll()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Run in an isolated batch project.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory(Output);
            Directory.CreateDirectory(Output + "/frames");
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void Resume()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
                phase = 0; previousFrame = -1; deadline = EditorApplication.timeSinceStartup + 180;
                EditorApplication.update += Tick;
            };
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Input()
        {
            if (SessionState.GetBool(Pending, false) && Mouse.current == null) InputSystem.AddDevice<Mouse>();
        }

        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        static void Setup()
        {
            var canvas = new GameObject("Check Canvas", typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080);
            var panel = new GameObject("Check Dice UI", typeof(RectTransform));
            panel.transform.SetParent(canvas.transform, false);
            ui = panel.AddComponent<DiceUI>();
            animator = panel.AddComponent<DiceRollAnimator>();
            var row = new GameObject("Check Slots", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            row.transform.SetParent(panel.transform, false);
            ((RectTransform)row.transform).sizeDelta = new Vector2(600,100);
            row.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            row.GetComponent<HorizontalLayoutGroup>().childControlWidth = false;
            row.GetComponent<HorizontalLayoutGroup>().childControlHeight = false;
            row.GetComponent<HorizontalLayoutGroup>().spacing = 12;
            container = row.transform;
            var serialized = new SerializedObject(ui);
            serialized.FindProperty("diceContainer").objectReferenceValue = container;
            serialized.FindProperty("diceElementPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DicePrefab.prefab");
            serialized.FindProperty("rollAnimator").objectReferenceValue = animator;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            ui.SetPanelVisible(true);
            var standard = AssetDatabase.LoadAssetAtPath<DieDefinitionSO>("Assets/Scripts/Data/Dices/Die.asset");
            var arcane = AssetDatabase.LoadAssetAtPath<DieDefinitionSO>("Assets/Scripts/Data/Dices/ArcaneDie.asset");
            int[] values = { 0, 12, -2, 6 };
            data = values.Select((value, index) => new DiceData(index, value, new DieInstance(index == 1 ? arcane : standard)
                { FaceOverride = new[] { 0, 12, -2, 6, 12, 3 } })).ToList();
            complete = 0;
            animator.OnAnimationComplete += () => complete++;
            Random.InitState(5821); randomState = Random.state;
            ui.DisplayDice(data);
        }

        static void CheckRestored()
        {
            Require(!animator.IsPlaying, "Animation did not finish.");
            var elements = container.GetComponentsInChildren<DiceElement>();
            Require(elements.Length == data.Count, "Dice were lost or duplicated.");
            foreach (var element in elements)
            {
                var group = element.GetComponent<CanvasGroup>();
                Require(group.alpha == 1 && group.interactable && group.blocksRaycasts, "Slot remained hidden or locked.");
                Require(element.transform.parent == container, "Slot was reparented.");
                Require(element.GetComponentInChildren<TMP_Text>().text == element.Data.Value.ToString(), "UI value differs from gameplay.");
            }
            Require(GameObject.Find("Dice Roll 3D Stage") == null && GameObject.Find("Dice Roll Overlay") == null, "Presentation objects leaked.");
        }

        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Dice checks timed out.");
                if (!EditorApplication.isPlaying || previousFrame == Time.frameCount) return;
                previousFrame = Time.frameCount;
                if (Time.time < next) return;
                switch (phase)
                {
                    case 0:
                        Setup(); phase = 1; next = Time.time + .6f; break;
                    case 1:
                        Require(animator.IsPlaying, "No center roll.");
                        Require(Random.state.Equals(randomState), "Presentation consumed gameplay random numbers.");
                        var faces = GameObject.Find("Dice Roll 3D Stage").GetComponentsInChildren<TMP_Text>().Where(t => t.name == "Result Face").ToArray();
                        Require(faces.Select(t => t.text).SequenceEqual(data.Select(d => d.Value.ToString())), "3D results do not match custom values.");
                        Require(container.GetComponentsInChildren<DiceElement>().All(e => !e.GetComponent<CanvasGroup>().blocksRaycasts), "Rolling dice accept clicks.");
                        var meshes = GameObject.Find("Dice Roll 3D Stage").GetComponentsInChildren<MeshFilter>();
                        Require(meshes.Any(m => m.sharedMesh.name == "Dice Rounded Cube" && m.sharedMesh.vertexCount > 24), "Rounded geometry missing.");
                        phase = 2; next = Time.time + 2.2f; break;
                    case 2:
                        CheckRestored();
                        Require(complete == 1, "Completion callback count incorrect.");
                        Require(Random.state.Equals(randomState), "Completed roll changed random state.");
                        Require(data.Select(d => d.Value).SequenceEqual(new[] {0,12,-2,6}), "Roll changed data.");
                        ui.DisplayDice(data); phase = 3; next = Time.time + .25f; break;
                    case 3:
                        ui.SetPanelVisible(false);
                        Require(!animator.IsPlaying && complete == 1, "Hide did not cancel silently.");
                        phase = 4; next = Time.time + .1f; break;
                    case 4:
                        CheckRestored(); ui.SetPanelVisible(true);
                        ui.DisplayDice(data); ui.DisplayDice(data);
                        phase = 5; next = Time.time + 3f; break;
                    case 5:
                        CheckRestored(); Require(complete == 2, "Reroll emitted stale completion.");
                        animator.PlayRollAnimation(new List<DiceElement>(), container);
                        Require(complete == 3 && !animator.IsPlaying, "Empty roll did not complete.");
                        // Resize while rolling and destroy the stage before the animator's cleanup.
                        var resizedCanvas = ui.GetComponentInParent<Canvas>();
                        resizedCanvas.renderMode = RenderMode.WorldSpace;
                        ((RectTransform)resizedCanvas.transform).sizeDelta = new Vector2(1080, 1920);
                        ui.DisplayDice(data);
                        phase = 8; next = Time.time + .25f; break;
                    case 8:
                        var stageRoot = GameObject.Find("Dice Roll 3D Stage");
                        Require(stageRoot != null, "Missing stage after aspect ratio change.");
                        Require(Mathf.Abs(stageRoot.GetComponentInChildren<Camera>().aspect - 1080f / 1920) < .01f, "Portrait camera aspect was not updated.");
                        Object.DestroyImmediate(stageRoot);
                        Object.DestroyImmediate(GameObject.Find("Dice Roll Overlay"));
                        animator.CancelAnimation(); animator.CancelAnimation();
                        phase = 9; next = Time.time + .1f; break;
                    case 9:
                        CheckRestored();
                        Require(!Resources.FindObjectsOfTypeAll<RenderTexture>().Any(t => t.name == "Dice Roll Texture"), "Render texture leaked after teardown.");
                        Debug.Log("DICE_ROLL_BEHAVIOR_PASS: exact custom faces; RNG unchanged; completion; cancellation; repeated reroll; aspect change; destruction-order cleanup.");
                        ui.DisplayDice(data);
                        phase = 10; next = Time.time + .2f; break;
                    case 10:
                        EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/WitchTowerPreview.unity", new LoadSceneParameters(LoadSceneMode.Single));
                        phase = 6; next = Time.time + 8; break;
                    case 6:
                        // The art preview starts an encounter without a run, so its authored recruit
                        // canvas is still enabled. Hide that overlay before reviewing combat UI.
                        Object.FindFirstObjectByType<CharacterSelectionUI>()?.Hide();
                        ui = Object.FindFirstObjectByType<DiceUI>();
                        Require(ui != null && Camera.main != null, "Battle preview did not initialize.");
                        animator = ui.GetComponent<DiceRollAnimator>();
                        ui.SetPanelVisible(true);
                        var standard = AssetDatabase.LoadAssetAtPath<DieDefinitionSO>("Assets/Scripts/Data/Dices/Die.asset");
                        data = new[] {2,5,3,6,4}.Select((v,i) => new DiceData(i,v,new DieInstance(standard))).ToList();
                        Time.captureDeltaTime = 1f / 30f;
                        ui.DisplayDice(data); captureStart = Time.time; captureFrame = 0;
                        phase = 7; next = 0; break;
                    case 7:
                        Capture("frames/" + captureFrame.ToString("D3") + ".png", 960, 540);
                        if (captureFrame == 15) Capture("rolling.png", 1920,1080);
                        if (captureFrame == 40) Capture("result.png", 1920,1080);
                        captureFrame++;
                        if (Time.time - captureStart > 3)
                        {
                            Require(!animator.IsPlaying, "Battle roll did not arrive.");
                            Capture("arrived.png",1920,1080);
                            Debug.Log("DICE_ROLL_CAPTURE_PASS: battle roll/result/arrival, " + captureFrame + " frames.");
                            Finish(0);
                        }
                        break;
                }
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }

        static void Capture(string file, int width, int height)
        {
            var camera = Camera.main;
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; }
            Canvas.ForceUpdateCanvases();
            var diceCamera = GameObject.Find("Dice Roll Camera")?.GetComponent<Camera>();
            if (diceCamera != null) diceCamera.Render();
            WitchTowerRoomBuilder.Capture(camera, Path.Combine(Output, file), width, height);
        }

        static void Finish(int code)
        {
            Time.captureDeltaTime = 0;
            EditorApplication.update -= Tick;
            SessionState.SetBool(Pending, false);
            EditorApplication.Exit(code);
        }
    }
}
