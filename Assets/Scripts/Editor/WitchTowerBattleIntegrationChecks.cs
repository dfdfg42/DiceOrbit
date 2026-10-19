using System;
using System.IO;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Core.Run;
using DiceOrbit.Core.Zones;
using DiceOrbit.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DiceOrbit.EditorTools
{
    /// <summary>Editor-only integration smoke check. No preview bootstrap and no save-writing menu actions.</summary>
    public static class WitchTowerBattleIntegrationChecks
    {
        const string Pending = "WitchTower.ProductionCheck";
        const string Output = "_workspace/2026-10-18-witch-tower/production";
        static readonly string[] SaveFiles = { "run_save.json", "run_save.bak", "run_save.tmp", "run_save.corrupt.json" };
        static int phase, flowId;
        static double deadline;
        static Character[] party;
        static Mouse mouse;

        public static void ApplyAndCapture()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Run this smoke check in batch mode.");
            WitchTowerRoomBuilder.ApplyToBattleScene();
            VerifyAndCapture();
        }

        /// <summary>Verify the saved production scene without regenerating or changing its assets.</summary>
        public static void VerifyAndCapture()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Run this smoke check in batch mode.");
            if (!ZonePlateSelfTests.RunAll() || !PassiveZoneSelfTests.RunAll())
                throw new InvalidOperationException("Zone self checks failed.");
            // Reopen from disk: validate what players will actually load.
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/BattleScene.unity", OpenSceneMode.Single);
            if (Object.FindAnyObjectByType<WitchTowerPreviewEntry>(FindObjectsInactive.Include) != null ||
                Object.FindAnyObjectByType<GameFlowManager>() == null || Object.FindAnyObjectByType<RunManager>() == null)
                throw new InvalidOperationException("Production game flow or bootstrap isolation is invalid.");
            int rooms = scene.GetRootGameObjects().Count(g =>
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g) == "Assets/Art/WitchTower/WitchTowerRoom.prefab");
            if (rooms != 1) throw new InvalidOperationException($"Expected one room prefab, found {rooms}.");
            if (!EditorBuildSettings.scenes.Any(s => s.enabled && s.path == scene.path))
                throw new InvalidOperationException("BattleScene is not enabled in build settings.");
            foreach (string file in SaveFiles) SessionState.SetString(Pending + file, SaveFingerprint(file));
            Directory.CreateDirectory(Output);
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        static void Resume()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
                phase = 0;
                deadline = EditorApplication.timeSinceStartup + 70;
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            };
        }

        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Production combat smoke check timed out.");
                if (phase == 0)
                {
                    if (Time.timeSinceLevelLoad < .5f || GameFlowManager.Instance == null) return;
                    if (Mouse.current == null) mouse = InputSystem.AddDevice<Mouse>();
                    flowId = GameFlowManager.Instance.GetInstanceID();
                    phase = 1;
                    SceneManager.LoadScene("BattleScene");
                    return;
                }
                if (phase == 1)
                {
                    if (SceneManager.GetActiveScene().name != "BattleScene" || Time.timeSinceLevelLoad < .5f) return;
                    var orbit = Object.FindAnyObjectByType<OrbitManager>();
                    if (orbit == null || orbit.Tiles.Count != 20) return;
                    if (GameFlowManager.Instance.GetInstanceID() != flowId)
                        throw new InvalidOperationException("Main menu game flow did not survive the scene transition.");
                    var spawner = Object.FindAnyObjectByType<CharacterSpawner>();
                    party = new[] { "Warrior", "Mage" }.Select((name, i) => spawner.Spawn(
                        AssetDatabase.LoadAssetAtPath<CharacterPreset>($"Assets/Scripts/Data/Character Preset/{name}/{name}.asset"), i, 2)).ToArray();
                    phase = 2;
                    return;
                }
                if (phase == 2)
                {
                    if (party.Any(c => c == null || c.CurrentTile == null)) return;
                    var run = RunManager.Instance;
                    if (!run.StartRun()) throw new InvalidOperationException("Scene's configured act did not start.");
                    // Use production node selection/encounter routing, without entering Map (autosave).
                    var node = run.GetSelectableNodes().First(n => n.IsCombat);
                    GameFlowManager.Instance.OnNodeSelected(node.Id);
                    phase = 3;
                    return;
                }
                var combat = CombatManager.Instance;
                if (combat == null || !combat.PlayerTurnActive || Time.timeSinceLevelLoad < 14) return;
                if (GameFlowManager.Instance.CurrentState != GameState.Combat ||
                    combat.CurrentEncounter != RunManager.Instance.CurrentNode.Encounter || combat.ActiveMonsters.Count == 0)
                    throw new InvalidOperationException("Production node did not route into its encounter.");
                if (Object.FindAnyObjectByType<WitchTowerPreviewEntry>(FindObjectsInactive.Include) != null)
                    throw new InvalidOperationException("A preview bootstrap leaked into production.");
                var room = GameObject.Find("Witch Tower — 3D room");
                if (room == null || room.GetComponentsInChildren<WitchTowerIdleMotion>().Length != 87)
                    throw new InvalidOperationException("Room or its idle animations are missing.");
                var bg = Object.FindAnyObjectByType<BackgroundManager>(FindObjectsInactive.Include);
                if (bg != null && bg.gameObject.activeInHierarchy)
                    throw new InvalidOperationException("Original background is still active.");
                var plates = new SpriteRenderer[CombatZoneManager.Instance.ZoneCount];
                for (int zone = 0; zone < plates.Length; zone++)
                    if (!ZonePlateRenderer.Instance.TryGetPlate(zone, out plates[zone]) ||
                        plates[zone].sharedMaterial.shader.name != "DiceOrbit/Witch Tower/Inlaid Monster Zone")
                        throw new InvalidOperationException("Production plate is missing its theme.");
                WitchTowerRoomBuilder.VerifyZoneCoverage(plates);
                WitchTowerRoomBuilder.Capture(Camera.main, Path.Combine(Output, "battle-camera.png"));
                foreach (string file in SaveFiles)
                    if (SaveFingerprint(file) != SessionState.GetString(Pending + file, ""))
                        throw new InvalidOperationException("Smoke check changed a run save: " + file);
                Debug.Log($"[WitchTower] PRODUCTION PASS: MainMenu -> BattleScene -> configured map node -> first player turn; tiles=20, party={party.Length}, monsters={combat.ActiveMonsters.Count}, room=1, idleTargets=87; no preview bootstrap, run saves unchanged.");
                Finish(0);
            }
            catch (Exception ex) { Debug.LogException(ex); Finish(5); }
        }

        static string SaveFingerprint(string name)
        {
            string path = Path.Combine(Application.persistentDataPath, name);
            if (!File.Exists(path)) return "missing";
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
        }

        static void Finish(int exitCode)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(Pending, false);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            EditorApplication.Exit(exitCode);
        }
    }
}
