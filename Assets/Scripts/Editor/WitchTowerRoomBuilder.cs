using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Core.Run;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace DiceOrbit.EditorTools
{
    /// <summary>
    /// First art prototype: editable meshes and props, with a separate playable copy of BattleScene.
    /// All decoration stays outside the radius-17 battle floor. Floor is below the plates' y=-0.02 plane.
    /// Preview rebuilds stay separate. ApplyToBattleScene explicitly installs the approved art in gameplay.
    /// </summary>
    public static class WitchTowerRoomBuilder
    {
        const string Root = "Assets/Art/WitchTower";
        const string PreviewScene = "Assets/Scenes/WitchTowerPreview.unity";
        static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();
        static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
        static System.Random rng;
        static string captureDir;
        static double deadline;
        static bool captured;
        static Camera motionCamera;
        static int motionFrame;
        static double nextMotionFrame;
        static WitchTowerIdleMotion[] idleTargets;
        static Matrix4x4[] idleStartPoses;
        static bool[] idleObservedMotion;

        [MenuItem("DiceOrbit/Witch Tower/Build Preview Scene")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(Root + "/Meshes");
            AssetDatabase.Refresh();
            Mats.Clear(); Meshes.Clear(); rng = new System.Random(181026);
            Palette();
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/BattleScene.unity", OpenSceneMode.Single);
            RemoveExistingRoom();
            // Only the scene copy is saved; the source scene and the original illustration remain intact.
            var bg = Object.FindFirstObjectByType<BackgroundManager>(FindObjectsInactive.Include);
            if (bg != null) bg.gameObject.SetActive(false);
            var room = new GameObject("Witch Tower — 3D room");
            BuildFloor(room.transform);
            BuildWalls(room.transform);
            BuildFurniture(room.transform);
            AddInkSilhouettes(room.transform);
            ConfigureLighting();
            ConfigureZoneAppearance();
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(room, Root + "/WitchTowerRoom.prefab", InteractionMode.AutomatedAction);
            if (prefab == null) throw new InvalidOperationException("Room prefab was not saved.");
            var entry = new GameObject("Witch Tower — preview encounter (no run save)").AddComponent<WitchTowerPreviewEntry>();
            entry.party = new[] { "Warrior", "Mage", "Rogue", "Alchemist" }
                .Select(n => AssetDatabase.LoadAssetAtPath<CharacterPreset>($"Assets/Scripts/Data/Character Preset/{n}/{n}.asset")).ToArray();
            entry.encounter = new EncounterDefinition
            {
                MonsterPresets = new List<MonsterPreset>
                {
                    AssetDatabase.LoadAssetAtPath<MonsterPreset>("Assets/Scripts/Data/MonsterPresets/Wave0/GreenSlime/GreenSlime.asset"),
                    AssetDatabase.LoadAssetAtPath<MonsterPreset>("Assets/Scripts/Data/MonsterPresets/Wave0/BlueSlime/BlueSlime.asset"),
                    AssetDatabase.LoadAssetAtPath<MonsterPreset>("Assets/Scripts/Data/MonsterPresets/Wave1/Goblin/W1_Goblin.asset"),
                    AssetDatabase.LoadAssetAtPath<MonsterPreset>("Assets/Scripts/Data/MonsterPresets/Wave1/Skeleton/W1_SkeletonSoldier.asset")
                }
            };
            if (entry.party.Any(p => p == null) || entry.encounter.MonsterPresets.Any(p => p == null))
                throw new InvalidOperationException("Missing preview character/monster assets.");
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, PreviewScene);
            Debug.Log($"[WitchTower] Built {room.GetComponentsInChildren<MeshRenderer>().Length} editable mesh objects. Saved {PreviewScene}");
        }

        static void RemoveExistingRoom()
        {
            // Preview rebuilding after production integration must not duplicate the installed prefab.
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) == Root + "/WitchTowerRoom.prefab")
                    Object.DestroyImmediate(root);
        }

        [MenuItem("DiceOrbit/Witch Tower/Apply Approved Room to Battle Scene")]
        public static void ApplyToBattleScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/WitchTowerRoom.prefab");
            if (prefab == null) throw new InvalidOperationException("Build the approved room prefab first.");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/BattleScene.unity", OpenSceneMode.Single);
            if (Object.FindAnyObjectByType<WitchTowerPreviewEntry>(FindObjectsInactive.Include) != null)
                throw new InvalidOperationException("Production scene contains a preview encounter bootstrap.");
            RemoveExistingRoom();
            var room = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            room.name = "Witch Tower — 3D room";
            var bg = Object.FindFirstObjectByType<BackgroundManager>(FindObjectsInactive.Include);
            if (bg != null) bg.gameObject.SetActive(false);
            ConfigureLighting();
            ConfigureZoneAppearance();
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[WitchTower] Applied approved room, lighting and zones to Assets/Scenes/BattleScene.unity; original game flow retained.");
        }

        public static void BuildAndCapture()
        {
            if (!ZonePlateSelfTests.RunAll() || !PassiveZoneSelfTests.RunAll())
                throw new InvalidOperationException("Zone geometry/tint checks failed.");
            Build();
            captureDir = Path.GetFullPath("_workspace/2026-10-18-witch-tower/smooth-floor");
            Directory.CreateDirectory(captureDir);
            captured = false;
            SessionState.SetString("WitchTower.CaptureDir", captureDir);
            SessionState.SetBool("WitchTower.Capture", true);
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        static void ResumeCapture()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("WitchTower.Capture", false)) return;
                captureDir = SessionState.GetString("WitchTower.CaptureDir", "");
                deadline = EditorApplication.timeSinceStartup + 45;
                captured = false;
                EditorApplication.update -= CaptureWhenReady;
                EditorApplication.update += CaptureWhenReady;
            };
        }

        static void CaptureWhenReady()
        {
            if (captured) return;
            var combat = CombatManager.Instance;
            if (combat == null || !combat.PlayerTurnActive || Time.timeSinceLevelLoad < 12)
            {
                if (EditorApplication.timeSinceStartup > deadline)
                {
                    Debug.LogError("[WitchTower] Timed out waiting for the live encounter.");
                    SessionState.SetBool("WitchTower.Capture", false);
                    EditorApplication.Exit(2);
                }
                return;
            }
            captured = true;
            SessionState.SetBool("WitchTower.Capture", false);
            EditorApplication.update -= CaptureWhenReady;
            try
            {
                Capture(Camera.main, Path.Combine(captureDir, "battle-camera.png"));
                VerifyZonePresentation(Camera.main);
                var camera = Camera.main;
                var oldPosition = camera.transform.position;
                var oldRotation = camera.transform.rotation;
                camera.transform.position = new Vector3(0, 36, -34);
                camera.transform.LookAt(new Vector3(0, 1, 2));
                var roomRoot = GameObject.Find("Witch Tower — 3D room").transform;
                var otherRenderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                    .Where(r => r.enabled && !r.transform.IsChildOf(roomRoot)).ToArray();
                var otherCanvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.enabled).ToArray();
                foreach (var r in otherRenderers) r.enabled = false;
                foreach (var c in otherCanvases) c.enabled = false;
                Capture(camera, Path.Combine(captureDir, "room-overview.png"));
                camera.transform.position = new Vector3(-8, 10, -2);
                camera.transform.LookAt(new Vector3(-15, 3.5f, 12));
                Capture(camera, Path.Combine(captureDir, "room-detail.png"));
                camera.transform.position = new Vector3(9, 12, -8);
                camera.transform.LookAt(new Vector3(15, 2, 8));
                Capture(camera, Path.Combine(captureDir, "new-props.png"));
                camera.transform.position = new Vector3(11, 7.7f, -.3f);
                camera.transform.LookAt(new Vector3(18.6f, 3.7f, 8.5f));
                Capture(camera, Path.Combine(captureDir, "bookshelf-detail.png"));
                camera.transform.position = new Vector3(-9, 7.7f, 7);
                camera.transform.LookAt(new Vector3(-15.7f, 4.9f, 14.6f));
                Capture(camera, Path.Combine(captureDir, "window-detail.png"));
                foreach (var r in otherRenderers) r.enabled = true;
                foreach (var c in otherCanvases) c.enabled = true;
                camera.transform.SetPositionAndRotation(oldPosition, oldRotation);
                Debug.Log($"[WitchTower] LIVE CAPTURE OK: tiles={Object.FindFirstObjectByType<OrbitManager>().Tiles.Count}, monsters={combat.ActiveMonsters.Count}, party={PartyManager.Instance.Party.Count}, playerTurn={combat.PlayerTurnActive}");
                // Record actual Update-driven animation over time, including the bookshelf and cauldron.
                foreach (var r in otherRenderers) r.enabled = false;
                foreach (var c in otherCanvases) c.enabled = false;
                camera.transform.position = new Vector3(9, 12, -8);
                camera.transform.LookAt(new Vector3(15, 2, 8));
                motionCamera = camera;
                idleTargets = roomRoot.GetComponentsInChildren<WitchTowerIdleMotion>();
                idleStartPoses = idleTargets.Select(m => Matrix4x4.TRS(m.transform.localPosition, m.transform.localRotation, m.transform.localScale)).ToArray();
                idleObservedMotion = new bool[idleTargets.Length];
                motionFrame = 0;
                nextMotionFrame = EditorApplication.timeSinceStartup;
                Directory.CreateDirectory(Path.Combine(captureDir, "frames"));
                EditorApplication.update += CaptureMotion;
            }
            catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(3); }
        }

        static void CaptureMotion()
        {
            if (EditorApplication.timeSinceStartup < nextMotionFrame) return;
            try
            {
                Capture(motionCamera, Path.Combine(captureDir, "frames", $"{motionFrame:D3}.png"), 960, 540);
                // A looping bubble can be fully shrunk at both endpoints. Observe the whole clip.
                for (int i = 0; i < idleTargets.Length; i++)
                {
                    var target = idleTargets[i].transform;
                    idleObservedMotion[i] |= Matrix4x4.TRS(target.localPosition, target.localRotation, target.localScale) != idleStartPoses[i];
                }
                motionFrame++;
                nextMotionFrame = EditorApplication.timeSinceStartup + 1d / 12;
                if (motionFrame < 72) return;
                int moving = idleObservedMotion.Count(moved => moved);
                if (idleTargets.Length == 0 || moving != idleTargets.Length)
                    throw new InvalidOperationException($"Idle capture: {moving}/{idleTargets.Length} targets changed pose.");
                Debug.Log($"[WitchTower] IDLE CAPTURE OK: {moving}/{idleTargets.Length} targets animated over 72 live frames.");
                EditorApplication.update -= CaptureMotion;
                EditorApplication.Exit(0);
            }
            catch (Exception ex) { EditorApplication.update -= CaptureMotion; Debug.LogException(ex); EditorApplication.Exit(4); }
        }

        static void VerifyZonePresentation(Camera camera)
        {
            var zones = DiceOrbit.Core.Zones.CombatZoneManager.Instance;
            var renderer = ZonePlateRenderer.Instance;
            var owners = Enumerable.Range(0, zones.ZoneCount).Select(zones.GetOwner).ToArray();
            var colors = new Color[zones.ZoneCount];
            var plates = new SpriteRenderer[zones.ZoneCount];
            for (int i = 0; i < zones.ZoneCount; i++)
            {
                if (!renderer.TryGetPlate(i, out plates[i]) || owners[i] == null ||
                    plates[i].sharedMaterial.shader.name != "DiceOrbit/Witch Tower/Inlaid Monster Zone")
                    throw new InvalidOperationException("Zone appearance was not applied to a live owned plate.");
                colors[i] = plates[i].color;
            }
            VerifyZoneCoverage(plates);
            try
            {
                zones.ClearRegistrations();
                renderer.SendMessage("LateUpdate");
                if (plates.Any(p => p.color != plates[0].color) || plates[0].color.a >= colors[0].a)
                    throw new InvalidOperationException("Neutral zones did not lose their owner tint.");
                Capture(camera, Path.Combine(captureDir, "battle-neutral.png"));
                for (int i = 0; i < zones.ZoneCount; i++)
                    zones.RegisterMonster(owners[(i + 1) % owners.Length], i);
                renderer.SendMessage("LateUpdate");
                for (int i = 0; i < zones.ZoneCount; i++)
                    if (plates[i].color != colors[(i + 1) % colors.Length])
                        throw new InvalidOperationException("Owner tint did not follow a reassigned monster.");
            }
            finally
            {
                zones.ClearRegistrations();
                for (int i = 0; i < zones.ZoneCount; i++) zones.RegisterMonster(owners[i], i);
                renderer.SendMessage("LateUpdate");
            }

            PassiveZoneIndicator.EnsureInstance();
            var passive = PassiveZoneIndicator.Instance;
            try
            {
                passive.Show(PartyManager.Instance.Party[0]);
                for (int frame = 0; frame < 10; frame++) passive.SendMessage("LateUpdate");
                if (!passive.GetComponentsInChildren<LineRenderer>().Any(l => l.enabled))
                    throw new InvalidOperationException("Warrior passive outline was not visible.");
                if (!passive.GetComponentsInChildren<SpriteRenderer>().Any(s => s.sharedMaterial == renderer.PlateMaterial))
                    throw new InvalidOperationException("Passive fill did not inherit the floor material.");
                if (passive.GetComponentsInChildren<SpriteRenderer>().Any(s =>
                    s.sharedMaterial == renderer.PlateMaterial && s.transform.lossyScale != plates[0].transform.lossyScale))
                    throw new InvalidOperationException("Passive fill did not inherit the expanded plate size.");
                Capture(camera, Path.Combine(captureDir, "battle-passive.png"));
            }
            finally
            {
                passive.Hide();
                for (int frame = 0; frame < 10; frame++) passive.SendMessage("LateUpdate");
            }
            Debug.Log("[WitchTower] ZONE PRESENTATION OK: 4 themed plates, neutral tint, owner reassignment, passive outline/material.");
        }

        internal static void VerifyZoneCoverage(SpriteRenderer[] plates)
        {
            // Read the actual shader's alpha, independently of its contour math. This catches both
            // radial clipping and corners spilling across a sector's straight edge.
            var go = new GameObject("_ZoneCoverageCamera");
            var camera = go.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 17;
            camera.transform.SetPositionAndRotation(new Vector3(0, 40, 0), Quaternion.Euler(90, 0, 0));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << 31;
            camera.allowHDR = false;
            var rt = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(1024, 1024, TextureFormat.RGBA32, false, true);
            var active = RenderTexture.active;
            int samples = 0;
            float maxTileRadius = 0;
            try
            {
                camera.targetTexture = rt;
                for (int zone = 0; zone < plates.Length; zone++)
                {
                    var plate = plates[zone];
                    int oldLayer = plate.gameObject.layer;
                    try
                    {
                        plate.gameObject.layer = 31;
                        camera.Render();
                        RenderTexture.active = rt;
                        pixels.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
                        pixels.Apply();
                        foreach (var tile in DiceOrbit.Core.Zones.CombatZoneManager.Instance.GetTilesInZone(zone))
                        foreach (var corner in TileCornerResolver.ResolveTopCorners(tile, 0))
                        {
                            maxTileRadius = Mathf.Max(maxTileRadius, new Vector2(corner.x, corner.z).magnitude);
                            for (int direction = 0; direction < 8; direction++)
                            {
                                float angle = direction * Mathf.PI / 4;
                                var probe = corner + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * .06f;
                                var uv = camera.WorldToViewportPoint(probe);
                                if (pixels.GetPixelBilinear(uv.x, uv.y).a < plate.color.a * .9f)
                                    throw new InvalidOperationException($"Zone {zone} does not cover {tile.name} corner {corner} with .06 margin.");
                                samples++;
                            }
                        }
                    }
                    finally { plate.gameObject.layer = oldLayer; }
                }
                Debug.Log($"[WitchTower] ZONE COVERAGE OK: {samples} GPU alpha samples across 20 tiles / 80 corners with .06 margin; tile radius={maxTileRadius:F3}, plate radius=16.35, floor radius=17.");
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = active;
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(pixels);
                rt.Release(); Object.DestroyImmediate(rt);
            }
        }

        internal static void Capture(Camera camera, string path, int width = 1920, int height = 1080)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            var resolved = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            var previous = camera.targetTexture;
            var active = RenderTexture.active;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt;
                camera.Render();
                // Resolve MSAA before readback so fine ink contours remain smooth in the exported image.
                Graphics.Blit(rt, resolved);
                RenderTexture.active = resolved;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous; RenderTexture.active = active;
                Object.DestroyImmediate(tex);
                rt.Release(); Object.DestroyImmediate(rt);
                resolved.Release(); Object.DestroyImmediate(resolved);
            }
        }

        static void Palette()
        {
            Material("Floor", "77718D"); Material("FloorOuter", "8C839F"); Material("Mortar", "4E4564");
            Material("Stone0", "9386A5"); Material("Stone1", "9F90AE"); Material("Stone2", "887C9C");
            Material("WallMortar", "4E4564");
            Material("Trim", "D5BFAE"); Material("TrimShade", "A89197"); Material("Gold", "DDB16D");
            Material("Wood", "655071"); Material("WoodLight", "A67F91"); Material("WoodDark", "403747");
            Material("Pages", "E8D7B3"); Material("Wax", "FFE0AF"); Material("Cork", "B18464");
            Material("Ink", "302D49"); Material("Lavender", "BA9FD8"); Material("Rose", "DF8DA9");
            Material("Mint", "85C8B5"); Material("Blue", "819ACD"); Material("Leaf", "8BA899");
            Material("Pot", "B68DAD"); Material("Metal", "525169");
            Material("Window", "536D9F", 0.3f); Material("WindowLight", "86BDD0", 0.55f);
            Material("LiquidRose", "E794B7", 0.3f); Material("LiquidMint", "85DFCA", 0.3f);
            Material("LiquidPurple", "AC97ED", 0.35f); Material("Flame", "FFE0A1", 2f);
            Material("Crystal", "B49CE8", 0.5f);
            Material("Straw", "C8AB79"); Material("StrawShade", "A98C65");
            Material("Herb", "769E83"); Material("Mushroom", "BD7F91");
            Material("PageEdge", "BAA68E"); Material("BookLeather", "807087");
            Material("WindowFrame", "67566B"); Material("WindowStone", "C3A69B");
            Material("GlassIndigo", "495C8A"); Material("GlassBlue", "617FAD");
            Material("GlassTeal", "6594A6"); Material("GlassLilac", "8C80B3");
            Material("GlassPale", "A1BBCE"); Material("WindowMoon", "EAD8A9");
            foreach (var pair in Mats)
            {
                var mat = pair.Value;
                mat.shader = Shader.Find("DiceOrbit/Witch Tower/Painted Props");
                mat.SetFloat("_Pigment", pair.Key.StartsWith("Stone") ? .12f : .085f);
                mat.SetFloat("_WoodGrain", pair.Key.StartsWith("Wood") ? 1 : 0);
                EditorUtility.SetDirty(mat);
            }
            // Restore the first 3D preview's warm masonry and real architectural shadows.
            // Keep the painted treatment on the updated props, books and animated foliage.
            foreach (var name in new[] { "Stone0", "Stone1", "Stone2", "WallMortar", "Trim", "TrimShade", "WindowStone", "WindowFrame" })
            {
                var masonry = Mats[name];
                masonry.shader = Shader.Find("Universal Render Pipeline/Lit");
                masonry.SetFloat("_Smoothness", .18f);
                masonry.SetFloat("_Metallic", 0);
                masonry.SetFloat("_Cull", 2);
                masonry.DisableKeyword("_RECEIVE_SHADOWS_OFF");
                EditorUtility.SetDirty(masonry);
            }
            // The existing billboard sprites straddle y=0. Paint the floor without populating
            // the depth buffer, so it cannot clip their lower halves. Props still write depth.
            foreach (var name in new[] { "Floor", "FloorOuter", "Mortar" })
            {
                var floorColor = Mats[name].GetColor("_BaseColor");
                Mats[name].shader = Shader.Find("DiceOrbit/Witch Tower/Backdrop Floor");
                Mats[name].SetColor("_BaseColor", floorColor);
                Mats[name].SetFloat("_Pigment", name == "Floor" ? .025f : .06f);
                // These layers do not write depth; the foundation must draw before its inlays.
                Mats[name].renderQueue = name == "Mortar" ? 1980 : 1990;
                EditorUtility.SetDirty(Mats[name]);
            }
            // A softly luminous pane. An unlit face also avoids lighting coplanar arch masks.
            foreach (var name in new[] { "Window", "WindowLight", "GlassIndigo", "GlassBlue", "GlassTeal", "GlassLilac", "GlassPale", "WindowMoon" })
            {
                var color = Mats[name].GetColor("_BaseColor");
                Mats[name].shader = Shader.Find("Universal Render Pipeline/Unlit");
                Mats[name].SetColor("_BaseColor", color);
                Mats[name].SetFloat("_Cull", 0);
                EditorUtility.SetDirty(Mats[name]);
            }
        }

        static void ConfigureZoneAppearance()
        {
            var path = Root + "/Materials/InlaidMonsterZone.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("DiceOrbit/Witch Tower/Inlaid Monster Zone"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_StoneColor", AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Floor.mat").GetColor("_BaseColor"));
            material.SetFloat("_OwnerMix", .22f);
            material.SetFloat("_OuterRadius", 16.35f);
            material.SetFloat("_InnerRadius", 2.65f);
            material.SetFloat("_EdgeInset", .10f);
            EditorUtility.SetDirty(material);
            ZonePlateRenderer.EnsureInstance().PlateMaterial = material;
            // FullRect sprite is 16 units wide. Enlarge its drawing bounds to hold the new contour.
            ZonePlateRenderer.EnsureInstance().PlateScale = 1.05f;
        }

        static void Material(string name, string hex, float emission = 0)
        {
            var path = Root + "/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            mat.SetColor("_BaseColor", color); mat.SetFloat("_Smoothness", 0.18f); mat.enableInstancing = true;
            mat.SetColor("_EmissionColor", color * emission);
            if (emission > 0) mat.EnableKeyword("_EMISSION");
            else mat.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(mat); Mats[name] = mat;
        }

        static Transform Group(Transform parent, string name, Vector3 pos = default)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = pos; return t;
        }

        static WitchTowerIdleMotion Idle(Transform target, float period, Vector3 rotation = default,
            Vector3 position = default, Vector3 scale = default,
            WitchTowerIdleMotion.Motion motion = WitchTowerIdleMotion.Motion.Gentle)
        {
            var idle = target.gameObject.AddComponent<WitchTowerIdleMotion>();
            idle.period = period;
            idle.phase = (float)rng.NextDouble();
            idle.rotationAmplitude = rotation;
            idle.positionAmplitude = position;
            idle.scaleAmplitude = scale;
            idle.motion = motion;
            return idle;
        }

        static void AddInkSilhouettes(Transform room)
        {
            var path = Root + "/Materials/InkSilhouette.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("DiceOrbit/Witch Tower/Ink Silhouette"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_InkColor", new Color(.18f,.14f,.25f,1));
            mat.SetFloat("_Width", .035f);
            EditorUtility.SetDirty(mat);
            var finePath = Root + "/Materials/InkFine.mat";
            var fine = AssetDatabase.LoadAssetAtPath<Material>(finePath);
            if (fine == null) { fine = new Material(mat); AssetDatabase.CreateAsset(fine, finePath); }
            fine.CopyPropertiesFromMaterial(mat);
            fine.SetFloat("_Width", .012f);
            EditorUtility.SetDirty(fine);
            foreach (var filter in room.GetComponentsInChildren<MeshFilter>())
            {
                var source = filter.sharedMesh;
                // Quiet masonry and floor: outline the furniture silhouettes, not every brick.
                if (source.name.StartsWith("Arc_") || source.name.StartsWith("Window_") || source.name == "ArchWindow") continue;
                var renderer = filter.GetComponent<MeshRenderer>();
                var material = renderer.sharedMaterial.name;
                if (new[] { "Floor", "FloorOuter", "Mortar", "Flame", "WindowLight", "Ink", "PageEdge" }.Contains(material)) continue;
                // Fine tooling and page grooves already read as lines; an expanded hull would fill them in.
                var size = filter.transform.localScale;
                if (source.name == "BevelBox" && Mathf.Min(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)) < .045f) continue;
                if (renderer.bounds.size.magnitude < .45f) continue;
                var outline = Group(filter.transform, "Ink contour");
                outline.gameObject.AddComponent<MeshFilter>().sharedMesh = SmoothOutlineMesh(source);
                var ink = outline.gameObject.AddComponent<MeshRenderer>();
                ink.sharedMaterial = source.name == "CurvedHerbLeaf" || filter.transform.parent.name.StartsWith("Bound spellbook") ? fine : mat;
                ink.shadowCastingMode = ShadowCastingMode.Off;
                ink.receiveShadows = false;
            }
        }

        static Mesh SmoothOutlineMesh(Mesh source)
        {
            string key = source.name + "_Ink";
            if (Meshes.TryGetValue(key, out var existing)) return existing;
            // Weld only the expansion normals: keep the visible mesh's intentional flat shading.
            var positions = source.vertices;
            var normals = source.normals;
            var sums = new Dictionary<Vector3Int, Vector3>();
            var keys = new Vector3Int[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                keys[i] = Vector3Int.RoundToInt(positions[i] * 10000);
                sums.TryGetValue(keys[i], out var sum);
                sums[keys[i]] = sum + normals[i];
            }
            var mesh = SaveMesh(key, positions.ToList(), source.triangles.ToList());
            mesh.normals = keys.Select(k => sums[k].normalized).ToArray();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static Transform AtRim(Transform parent, string name, float degrees, float radius)
        {
            var t = Group(parent, name, Polar(degrees, radius, 0));
            t.rotation = Quaternion.LookRotation(-new Vector3(t.position.x, 0, t.position.z));
            return t;
        }

        static Vector3 Polar(float degree, float radius, float y) => new Vector3(Mathf.Cos(degree * Mathf.Deg2Rad) * radius, y, Mathf.Sin(degree * Mathf.Deg2Rad) * radius);

        static GameObject Model(Transform parent, string name, Mesh mesh, string material, Vector3 pos, Vector3 scale)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = Mats[material]; r.shadowCastingMode = ShadowCastingMode.On;
            return go;
        }

        static Mesh SaveMesh(string key, List<Vector3> vertices, List<int> indices)
        {
            if (Meshes.TryGetValue(key, out var cached)) return cached;
            var mesh = new Mesh { name = key };
            mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var path = Root + "/Meshes/" + key + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; }
            else AssetDatabase.CreateAsset(mesh, path);
            EditorUtility.SetDirty(mesh);
            Meshes[key] = mesh; return mesh;
        }

        static void Quad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int n = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d); t.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
        }

        static Mesh Lathe(string name, params Vector2[] profile)
        {
            if (Meshes.TryGetValue(name, out var found)) return found;
            var v = new List<Vector3>(); var t = new List<int>(); const int sides = 24;
            for (int j = 0; j < profile.Length - 1; j++)
                for (int i = 0; i < sides; i++)
                {
                    float a = 360f * i / sides, b = 360f * (i + 1) / sides;
                    Quad(v, t, Polar(a, profile[j].x, profile[j].y), Polar(a, profile[j + 1].x, profile[j + 1].y),
                        Polar(b, profile[j + 1].x, profile[j + 1].y), Polar(b, profile[j].x, profile[j].y));
                }
            return SaveMesh(name, v, t);
        }

        static GameObject Round(Transform p, string name, Vector3 pos, Vector3 scale, string mat)
            => Model(p, name, Lathe("Round", new Vector2(0, 0), new Vector2(.45f, 0), new Vector2(.5f, .06f), new Vector2(.5f, .94f), new Vector2(.45f, 1), new Vector2(0, 1)), mat, pos, scale);

        static GameObject Ball(Transform p, string name, Vector3 pos, Vector3 scale, string mat)
        {
            var profile = new List<Vector2>();
            for (int i = 0; i <= 12; i++) { float a = Mathf.PI * i / 12; profile.Add(new Vector2(Mathf.Sin(a) * .5f, -Mathf.Cos(a) * .5f)); }
            return Model(p, name, Lathe("Orb", profile.ToArray()), mat, pos, scale);
        }

        static GameObject Box(Transform p, string name, Vector3 pos, Vector3 size, string mat)
        {
            string key = "BevelBox";
            if (!Meshes.TryGetValue(key, out var mesh))
            {
                var v = new List<Vector3>(); var t = new List<int>(); const float b = .07f;
                var ring = new[] { new Vector2(-.5f + b, -.5f), new Vector2(.5f-b,-.5f), new Vector2(.5f,-.5f+b), new Vector2(.5f,.5f-b), new Vector2(.5f-b,.5f), new Vector2(-.5f+b,.5f), new Vector2(-.5f,.5f-b), new Vector2(-.5f,-.5f+b) };
                for (int layer = 0; layer < 3; layer++)
                {
                    float y0 = layer == 0 ? -.5f : layer == 1 ? -.5f+b : .5f-b;
                    float y1 = layer == 0 ? -.5f+b : layer == 1 ? .5f-b : .5f;
                    float s0 = layer == 0 ? .87f : 1, s1 = layer == 2 ? .87f : 1;
                    for (int i = 0; i < 8; i++)
                    {
                        var a = ring[i]; var c = ring[(i + 1) % 8];
                        Quad(v,t,new Vector3(a.x*s0,y0,a.y*s0),new Vector3(a.x*s1,y1,a.y*s1),new Vector3(c.x*s1,y1,c.y*s1),new Vector3(c.x*s0,y0,c.y*s0));
                    }
                }
                for (int i = 0; i < 8; i++)
                {
                    var a = ring[i]*.87f; var c = ring[(i+1)%8]*.87f;
                    int n=v.Count; v.Add(new Vector3(0,.5f,0));v.Add(new Vector3(c.x,.5f,c.y));v.Add(new Vector3(a.x,.5f,a.y)); t.AddRange(new[]{n,n+1,n+2});
                    n=v.Count;v.Add(new Vector3(0,-.5f,0));v.Add(new Vector3(a.x,-.5f,a.y));v.Add(new Vector3(c.x,-.5f,c.y));t.AddRange(new[]{n,n+1,n+2});
                }
                mesh=SaveMesh(key,v,t);
            }
            return Model(p,name,mesh,mat,pos,size);
        }

        static void Arc(Transform p, string name, float inner, float outer, float from, float to, float bottom, float height, string mat)
        {
            string key = FormattableString.Invariant($"Arc_{inner:F2}_{outer:F2}_{from:F2}_{to:F2}_{height:F2}");
            if (!Meshes.TryGetValue(key, out var mesh))
            {
                var v=new List<Vector3>(); var t=new List<int>(); int count=Mathf.Max(1, Mathf.CeilToInt((to-from)/5));
                for(int i=0;i<count;i++)
                {
                    float a=Mathf.Lerp(from,to,(float)i/count),b=Mathf.Lerp(from,to,(float)(i+1)/count);
                    var ai=Polar(a,inner,0);var bi=Polar(b,inner,0);var ao=Polar(a,outer,0);var bo=Polar(b,outer,0);var up=Vector3.up*height;
                    Quad(v,t,ai,bi,bi+up,ai+up);Quad(v,t,bo,ao,ao+up,bo+up);
                    Quad(v,t,ai+up,bi+up,bo+up,ao+up);Quad(v,t,ao,bo,bi,ai);
                    if(i==0) Quad(v,t,ao,ai,ai+up,ao+up);
                    if(i==count-1) Quad(v,t,bi,bo,bo+up,bi+up);
                }
                mesh=SaveMesh(key,v,t);
            }
            Model(p,name,mesh,mat,new Vector3(0,bottom,0),Vector3.one);
        }

        static void BuildFloor(Transform root)
        {
            var floor=Group(root,"01 — quiet floor below gameplay");
            Round(floor,"Circular stone foundation",new Vector3(0,-.65f,0),new Vector3(47,.42f,47),"Mortar");
            Round(floor,"Unpatterned combat floor",new Vector3(0,-.24f,0),new Vector3(34,.08f,34),"Floor");
            Arc(floor,"Brass perimeter outside zone art",16.65f,16.76f,0,360,-.17f,.025f,"TrimShade");
            Arc(floor,"Continuous outer walkway",17,23.25f,0,360,-.24f,.075f,"FloorOuter");
            Arc(floor,"Outer floor lip",23.25f,23.6f,0,360,-.25f,.22f,"TrimShade");
            // Small central medallion only; no wedges or colored rugs competing with enemy ownership.
            Arc(floor,"Central brass inlay",1.12f,1.16f,0,360,-.15f,.012f,"TrimShade");
        }

        static void BuildWalls(Transform root)
        {
            var walls=Group(root,"02 — curved tower wall");
            Arc(walls,"Continuous shadow mortar",22.55f,23.35f,-12,192,-.16f,8.8f,"WallMortar");
            for(int row=0;row<7;row++)
                for(int i=0;i<26;i++)
                {
                    float a=-12+i*8+(row%2)*4,b=Mathf.Min(a+7.8f,192);
                    if(a>=192)continue;
                    Arc(walls,$"Masonry {row}-{i}",22.35f,23.38f,a+.1f,b,row*1.21f-.1f,1.16f,"Stone"+rng.Next(3));
                }
            Arc(walls,"Skirting cream moulding",21.97f,22.5f,-12,192,-.12f,.48f,"TrimShade");
            Arc(walls,"Upper cornice",22.05f,23.55f,-12,192,8.37f,.48f,"Trim");
            foreach(float angle in new[]{8f,65f,115f,172f})
            {
                var c=AtRim(walls,"Soft stone pillar "+angle,angle,22.05f);
                Box(c,"Foot",new Vector3(0,.3f,0),new Vector3(1.5f,.7f,1.35f),"TrimShade");
                Box(c,"Foot cap",new Vector3(0,.77f,0),new Vector3(1.35f,.25f,1.18f),"Trim");
                Round(c,"Column",new Vector3(0,.85f,0),new Vector3(.94f,6.65f,.94f),"Trim");
                Round(c,"Capital collar",new Vector3(0,7.35f,0),new Vector3(1.25f,.33f,1.25f),"TrimShade");
                Box(c,"Capital",new Vector3(0,7.88f,0),new Vector3(1.62f,.7f,1.42f),"Trim");
            }
            foreach(float angle in new[]{43f,90f,137f}) Window(AtRim(walls,"Moonlit arch "+angle,angle,21.5f));
            foreach(float angle in new[]{69f,111f})
            {
                var banner=AtRim(walls,"Hanging star pennant "+angle,angle,21.75f);
                var cloth = Group(banner,"Pennant top pivot",new Vector3(0,7.4f,.18f));
                Box(cloth,"Velvet pennant",new Vector3(0,-1.7f,0),new Vector3(1.1f,3.4f,.12f),"Lavender");
                Box(banner,"Top rod",new Vector3(0,7.42f,.21f),new Vector3(1.45f,.12f,.16f),"Gold");
                Star(cloth,new Vector3(0,-1.5f,.12f),.43f,"Pages");
                Idle(cloth,6.8f,new Vector3(1.6f,0,.6f));
            }
        }

        static Mesh ArchMesh()
        {
            const string key="ArchWindow";if(Meshes.TryGetValue(key,out var m))return m;
            var outline=new List<Vector3>{new Vector3(-1,0,0),new Vector3(1,0,0),new Vector3(1,2.1f,0)};
            for(int i=1;i<=20;i++){float a=Mathf.PI*i/20;outline.Add(new Vector3(Mathf.Cos(a),2.1f+Mathf.Sin(a),0));}
            var v=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<outline.Count;i++){int n=v.Count;v.Add(new Vector3(0,1.5f,0));v.Add(outline[i]);v.Add(outline[(i+1)%outline.Count]);t.AddRange(new[]{n,n+1,n+2});}
            return SaveMesh(key,v,t);
        }

        static void Window(Transform p)
        {
            const float spring=5.25f, bottom=2.62f, radius=1.72f;
            var opening=new List<Vector2>{new Vector2(-1.84f,2.49f),new Vector2(1.84f,2.49f)};
            for(int i=0;i<=24;i++)
            {
                float a=Mathf.PI*i/24;
                opening.Add(new Vector2(Mathf.Cos(a)*1.84f,spring+Mathf.Sin(a)*1.84f));
            }
            WindowPane(p,"Shadow inside the arch",opening,"Ink",.025f);

            // Real stone voussoirs and jambs: the glass sits behind their front faces.
            var arch=Group(p,"Cut stone arch",new Vector3(0,spring,.61f));
            arch.localRotation=Quaternion.Euler(-90,0,0);
            for(int i=0;i<9;i++)
                Arc(arch,"Arch stone "+i,1.83f,i==4?2.36f:2.25f,i*20+.55f,(i+1)*20-.55f,0,.4f,i%3==1?"WindowStone":"Trim");
            foreach(float side in new[]{-1f,1f})
                for(int row=0;row<4;row++)
                    Box(p,"Stone jamb block",new Vector3(side*2.035f,2.58f+(row+.5f)*.6675f,.39f),
                        new Vector3(.43f,.638f,.42f),row%2==0?"Trim":"WindowStone");

            var innerArch=Group(p,"Recessed curved timber frame",new Vector3(0,spring,.39f));
            innerArch.localRotation=Quaternion.Euler(-90,0,0);
            Arc(innerArch,"Curved inner reveal",1.72f,1.83f,0,180,0,.27f,"WindowFrame");
            foreach(float side in new[]{-1f,1f})
                Box(p,"Deep timber jamb",new Vector3(side*1.775f,(spring+bottom)*.5f,.255f),new Vector3(.11f,spring-bottom,.27f),"WindowFrame");
            Box(p,"Lower timber rail",new Vector3(0,bottom,.255f),new Vector3(3.55f,.12f,.27f),"WindowFrame");

            // Four quiet rectangular panes below a four-part fanlight.
            for(int row=0;row<2;row++)
                for(int col=0;col<2;col++)
                {
                    float x0=col==0?-radius:.055f, x1=col==0?-.055f:radius;
                    float y0=bottom+.06f+row*1.28f, y1=y0+1.19f;
                    WindowPane(p,$"Lower glass {row}-{col}",new[]{new Vector2(x0,y0),new Vector2(x1,y0),new Vector2(x1,y1),new Vector2(x0,y1)},
                        new[]{"GlassBlue","GlassTeal","GlassLilac","GlassBlue"}[row*2+col],.12f);
                    float cx=(x0+x1)*.5f,cy=(y0+y1)*.5f;
                    WindowPane(p,$"Diamond glass inlay {row}-{col}",new[]{new Vector2(cx,cy-.23f),new Vector2(cx+.15f,cy),new Vector2(cx,cy+.23f),new Vector2(cx-.15f,cy)},"GlassPale",.135f);
                }
            for(int sector=0;sector<4;sector++)
            {
                var fan=new List<Vector2>{new Vector2(0,spring)};
                for(int j=0;j<=8;j++)
                {
                    float a=(sector*45+j*45f/8)*Mathf.Deg2Rad;
                    fan.Add(new Vector2(Mathf.Cos(a)*radius,spring+Mathf.Sin(a)*radius));
                }
                WindowPane(p,"Fanlight glass "+sector,fan,sector%2==0?"GlassIndigo":"GlassLilac",.12f);
            }
            Box(p,"Central lead mullion",new Vector3(0,4.8f,.245f),new Vector3(.095f,4.35f,.15f),"WindowFrame");
            foreach(float y in new[]{3.91f,spring})
                Box(p,"Horizontal lead came",new Vector3(0,y,.245f),new Vector3(3.47f,.085f,.15f),"WindowFrame");
            foreach(float angle in new[]{45f,135f})
            {
                float a=angle*Mathf.Deg2Rad;
                Rod(p,"Fanlight radial lead",new Vector3(0,spring,.245f),new Vector3(Mathf.Cos(a)*radius,spring+Mathf.Sin(a)*radius,.245f),.075f,"WindowFrame");
            }

            var moon=Group(p,"Crescent moon medallion",new Vector3(0,5.75f,.36f));
            Ball(moon,"Brass medallion rim",Vector3.zero,new Vector3(.95f,.95f,.10f),"Gold");
            Ball(moon,"Indigo medallion glass",new Vector3(0,0,.061f),new Vector3(.82f,.82f,.035f),"GlassIndigo");
            var v=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<24;i++)
            {
                Vector3 Outer(float u){float a=(60+240*u)*Mathf.Deg2Rad;return new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.30f;}
                Vector3 Inner(float u){float a=(90+180*u)*Mathf.Deg2Rad;return new Vector3(.5f+.8660254f*Mathf.Cos(a),.8660254f*Mathf.Sin(a),0)*.30f;}
                float a=i/24f,b=(i+1)/24f;
                Quad(v,t,Outer(b),Inner(b),Inner(a),Outer(a));
            }
            Model(moon,"Ivory crescent",SaveMesh("Window_Crescent",v,t),"WindowMoon",new Vector3(.025f,0,.09f),Vector3.one);

            Box(p,"Projecting stone sill",new Vector3(0,2.45f,.47f),new Vector3(4.75f,.25f,1.08f),"Trim");
            Box(p,"Sill underside moulding",new Vector3(0,2.25f,.35f),new Vector3(4.32f,.17f,.7f),"WindowStone");
            foreach(float side in new[]{-1f,1f})
                Box(p,"Small sill corbel",new Vector3(side*1.55f,2.02f,.28f),new Vector3(.34f,.35f,.48f),"WindowStone");
        }

        static void WindowPane(Transform parent,string name,IEnumerable<Vector2> outline,string material,float z)
        {
            var points=outline.ToArray();
            var center=points.Aggregate(Vector2.zero,(sum,point)=>sum+point)/points.Length;
            var v=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<points.Length;i++)
            {
                int n=v.Count;
                v.Add(new Vector3(center.x,center.y,0));
                v.Add(new Vector3(points[i].x,points[i].y,0));
                var next=points[(i+1)%points.Length];v.Add(new Vector3(next.x,next.y,0));
                t.AddRange(new[]{n,n+1,n+2});
            }
            var pane=Model(parent,name,SaveMesh("Window_"+name.Replace(' ','_'),v,t),material,new Vector3(0,0,z),Vector3.one);
            pane.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        }

        static void Star(Transform p,Vector3 pos,float radius,string mat)
        {
            var s=Box(p,"Star",pos,new Vector3(radius*.38f,radius*2,.055f),mat);
            Box(s.transform,"Cross",Vector3.zero,new Vector3(3.4f,.19f,1.05f),mat);
        }

        static void BuildFurniture(Transform root)
        {
            var furniture=Group(root,"03 — props outside combat silhouette");
            Shelf(AtRim(furniture,"Apothecary cabinet",156,20.6f),false);
            Shelf(AtRim(furniture,"Spellbook cabinet",24,20.7f),true);
            var desk=AtRim(furniture,"Writing desk",114,19.6f);Table(desk,5.4f,2,2.05f);
            Book(desk,new Vector3(-.9f,2.25f,.1f),1.25f,"Rose",-12);
            Book(desk,new Vector3(-.7f,2.5f,.1f),1.1f,"Blue",7);
            Candle(desk,new Vector3(1.8f,2.2f,0),.8f);
            Bottle(desk,new Vector3(.9f,2.2f,-.3f),.8f,"LiquidMint");
            var altar=AtRim(furniture,"Three familiar bottles",90,20.4f);Table(altar,5.3f,1.7f,1.4f);
            Bottle(altar,new Vector3(-1.55f,1.56f,0),1.65f,"LiquidRose");
            Bottle(altar,new Vector3(0,1.56f,0),1.8f,"LiquidMint");
            Bottle(altar,new Vector3(1.55f,1.56f,0),1.65f,"LiquidPurple");
            var crystal=AtRim(furniture,"Crystal reading nook",187,20.5f);Table(crystal,2.5f,2.2f,1.7f);
            Round(crystal,"Orb plinth",new Vector3(0,1.85f,0),new Vector3(1.65f,.28f,1.65f),"Gold");
            var orb=Group(crystal,"Quietly levitating crystal",new Vector3(0,2.9f,0));
            Ball(orb,"Lavender crystal ball",Vector3.zero,new Vector3(1.65f,1.65f,1.65f),"Crystal");
            Ball(orb,"Orb highlight",new Vector3(-.37f,.44f,.61f),new Vector3(.35f,.35f,.06f),"Pages");
            Idle(orb,5.7f,new Vector3(0,5,0),new Vector3(0,.1f,0));
            var pot=AtRim(furniture,"Round potion cauldron",-9,20.4f);
            Model(pot,"Cauldron",Lathe("Cauldron",new Vector2(0,0),new Vector2(.7f,0),new Vector2(1.15f,.4f),new Vector2(1.35f,1.2f),new Vector2(1.2f,2),new Vector2(1.4f,2.15f),new Vector2(1.4f,2.3f),new Vector2(1.15f,2.3f),new Vector2(1.15f,1.9f)),"Metal",new Vector3(0,.25f,0),Vector3.one);
            var brew=Round(pot,"Mint brew",new Vector3(0,2.32f,0),new Vector3(2.35f,.035f,2.35f),"LiquidMint");
            Idle(brew.transform,3.8f,position:new Vector3(0,.025f,0),scale:new Vector3(.015f,0,.015f));
            for(int i=0;i<3;i++)Round(pot,"Pot foot",Polar(120*i,1,.02f),new Vector3(.42f,.5f,.42f),"Gold");
            for(int i=0;i<5;i++)
            {
                var bubble=Ball(pot,"Rising brew bubble",new Vector3(-.6f+(i%3)*.5f,2.36f,-.2f+(i%2)*.35f),Vector3.one*(.32f-(i%3)*.045f),"LiquidMint");
                Idle(bubble.transform,3.5f+i*.37f,position:new Vector3(.13f,1.6f,.07f),motion:WitchTowerIdleMotion.Motion.RisingBubble);
            }
            foreach(float a in new[]{174f,7f,55f})Plant(AtRim(furniture,"Potted herbs "+a,a,19.7f));
            var foreground=AtRim(furniture,"Low foreground book stack",229,20.9f);
            Book(foreground,new Vector3(0,.18f,0),1.8f,"Lavender",-8);Book(foreground,new Vector3(.12f,.6f,.05f),1.5f,"Rose",9);
            Candle(foreground,new Vector3(-1.8f,0,.2f),.9f);
            var cushions=AtRim(furniture,"Round velvet cushions",308,21.5f);
            Ball(cushions,"Plum cushion",new Vector3(0,.3f,0),new Vector3(2.9f,.7f,2.1f),"Lavender");
            Ball(cushions,"Rose cushion",new Vector3(.35f,.73f,0),new Vector3(2.3f,.65f,1.7f),"Rose");
            Idle(cushions,7.1f,scale:new Vector3(.006f,.018f,.006f));

            Broom(AtRim(furniture,"Leaning besom by the apothecary",144,21.1f));
            Broom(AtRim(furniture,"Spare broom by the spellbooks",34,21.1f));
            Scroll(desk,new Vector3(.65f,2.25f,.55f),.65f,18);
            var scrolls=AtRim(furniture,"Parchment basket",206,20.2f);
            ScrollBasket(scrolls);
            MushroomPot(AtRim(furniture,"Mushroom planter by the window",47,18.9f));
            MushroomPot(AtRim(furniture,"Small foreground mushroom planter",335,19.9f));
            var grinding=AtRim(furniture,"Herbalist grinding table",73,19.2f);
            Table(grinding,2.35f,1.6f,1.65f);
            MortarAndPestle(grinding,new Vector3(0,1.84f,0));
        }

        static void Table(Transform p,float width,float depth,float height)
        {
            Box(p,"Rounded tabletop",new Vector3(0,height,0),new Vector3(width,.32f,depth),"WoodLight");
            Box(p,"Apron",new Vector3(0,height-.38f,0),new Vector3(width-.3f,.55f,depth-.25f),"Wood");
            foreach(float x in new[]{-1f,1f})foreach(float z in new[]{-1f,1f})
                Box(p,"Little turned leg",new Vector3(x*(width*.5f-.3f),height*.45f,z*(depth*.5f-.28f)),new Vector3(.28f,height*.9f,.28f),"WoodDark");
        }

        static void Shelf(Transform p,bool books)
        {
            Box(p,"Cabinet back",new Vector3(0,3,-.58f),new Vector3(4.9f,6,.24f),"WoodDark");
            foreach(float x in new[]{-2.45f,2.45f})Box(p,"Carved side",new Vector3(x,3.1f,0),new Vector3(.3f,6.3f,1.55f),"Wood");
            Box(p,"Overhanging crown",new Vector3(0,6.25f,0),new Vector3(5.35f,.38f,1.85f),"WoodLight");
            Box(p,"Bottom cupboard",new Vector3(0,.9f,.05f),new Vector3(4.65f,1.8f,1.4f),"Wood");
            foreach(float x in new[]{-1.18f,1.18f})
            {
                Box(p,"Cupboard door",new Vector3(x,.92f,.8f),new Vector3(2.15f,1.5f,.12f),"WoodLight");
                Ball(p,"Brass knob",new Vector3(x*.25f,.96f,.94f),Vector3.one*.15f,"Gold");
            }
            for(int row=0;row<3;row++)
            {
                float y=1.85f+row*1.4f;
                Box(p,"Shelf "+row,new Vector3(0,y,0),new Vector3(4.8f,.19f,1.55f),"WoodLight");
                for(int i=0;i<(books?6:4);i++)
                {
                    float x=-1.65f+i*1.08f;
                    // The 1.4-unit shelf pitch leaves 1.21 units for a bottle including its cork.
                    if(!books)Bottle(p,new Vector3(x,y+.1f,.05f),.62f+(float)rng.NextDouble()*.08f,new[]{"LiquidRose","LiquidMint","LiquidPurple"}[(i+row)%3]);
                    else
                    {
                        float width=new[]{.48f,.56f,.42f,.53f,.46f,.57f}[(i+row)%6];
                        float height=new[]{1.06f,.9f,1.13f,.97f,1.02f,.84f}[(i+row*2)%6];
                        float lean=i==0?-4:i==5?4:(i%2==0?1.5f:-1.5f);
                        var b=Group(p,"Bound spellbook "+row+"-"+i,new Vector3(-1.9f+i*.75f,y+.12f+Mathf.Abs(Mathf.Sin(lean*Mathf.Deg2Rad))*width*.5f,.12f));
                        b.localRotation=Quaternion.Euler(0,(i%3-1)*3,lean);
                        BoundBook(b,width,height,.87f+(i%3)*.07f,new[]{"Rose","Blue","Mint","Lavender"}[(i+row)%4],i+row);
                        // A tiny forward breathing movement, with enough clearance between neighboring books.
                        Idle(b,6.5f+i*.7f+row*.4f,position:new Vector3(0,0,.025f+(i%3)*.012f));
                    }
                }
            }
            Candle(p,new Vector3(-1.8f,6.45f,0),.8f);
        }

        static void Bottle(Transform p,Vector3 pos,float size,string liquid)
        {
            var b=Group(p,"Corked potion — "+liquid,pos);b.localScale=Vector3.one*size;
            Model(b,"Potbelly bottle",Lathe("PotionBottle",new Vector2(0,0),new Vector2(.31f,0),new Vector2(.49f,.18f),new Vector2(.53f,.55f),new Vector2(.44f,.85f),new Vector2(.22f,1.08f),new Vector2(.2f,1.36f),new Vector2(.26f,1.4f),new Vector2(.26f,1.52f),new Vector2(0,1.52f)),liquid,Vector3.zero,Vector3.one);
            Round(b,"Bottle collar",new Vector3(0,1.39f,0),new Vector3(.56f,.13f,.56f),liquid);
            Round(b,"Cork",new Vector3(0,1.49f,0),new Vector3(.4f,.23f,.4f),"Cork");
            Idle(b,5.2f+(float)rng.NextDouble()*2,scale:new Vector3(.009f,.014f,.009f));
        }

        static void Candle(Transform p,Vector3 pos,float size)
        {
            var c=Group(p,"Warm candle",pos);c.localScale=Vector3.one*size;
            Round(c,"Brass saucer",Vector3.zero,new Vector3(.92f,.12f,.92f),"Gold");
            Round(c,"Wax",new Vector3(0,.1f,0),new Vector3(.52f,1.02f,.52f),"Wax");
            Ball(c,"Wax drip",new Vector3(.14f,.78f,.2f),new Vector3(.18f,.65f,.14f),"Wax");
            var flamePivot=Group(c,"Flame wick pivot",new Vector3(0,1.1f,0));
            Ball(flamePivot,"Flame",new Vector3(0,.28f,0),new Vector3(.26f,.56f,.26f),"Flame");
            var light=Group(c,"Candle glow",new Vector3(0,1.7f,0)).gameObject.AddComponent<Light>();light.type=LightType.Point;
            light.color=new Color(1,.72f,.42f);light.intensity=1.1f;light.range=5.5f;light.shadows=LightShadows.None;
            Idle(flamePivot,.83f,new Vector3(5,0,7),scale:new Vector3(.09f,.17f,.09f),motion:WitchTowerIdleMotion.Motion.Flame).glow=light;
        }

        static void BoundBook(Transform b,float width,float height,float depth,string mat,int variant)
        {
            Box(b,"Ivory page block",new Vector3(0,height*.5f,-.025f),new Vector3(width-.105f,height-.13f,depth-.12f),"Pages");
            foreach(float side in new[]{-1f,1f})
            {
                Box(b,"Overhanging leather cover",new Vector3(side*(width*.5f-.03f),height*.5f,0),new Vector3(.06f,height,depth),mat);
                Box(b,"Cover hinge",new Vector3(side*(width*.5f-.064f),height*.5f,depth*.5f-.065f),new Vector3(.023f,height-.08f,.035f),"BookLeather");
            }
            // An oval spine joins the two covers; pages remain visible at the head and foot.
            Round(b,"Rounded leather spine",new Vector3(0,.025f,depth*.5f-.045f),new Vector3(width,.95f*height,.18f),mat);
            foreach(float fraction in new[]{.19f,.79f})
            {
                Box(b,"Raised binding rib",new Vector3(0,height*fraction,depth*.5f+.036f),new Vector3(width*.9f,.055f,.05f),"BookLeather");
                Box(b,"Narrow gilt tooling",new Vector3(0,height*fraction+.035f,depth*.5f+.043f),new Vector3(width*.78f,.014f,.024f),"Gold");
            }
            for(int page=1;page<=4;page++)
            {
                float x=-width*.5f+.075f+(width-.15f)*page/5;
                Box(b,"Page folds at head",new Vector3(x,height-.062f,-.035f),new Vector3(.009f,.009f,depth-.18f),"PageEdge");
                Box(b,"Page folds at fore edge",new Vector3(x,height*.5f,-depth*.5f+.032f),new Vector3(.009f,height-.18f,.009f),"PageEdge");
            }
            if(variant%3==0)
            {
                Box(b,"Ribbon over page head",new Vector3(.04f,height-.05f,-.02f),new Vector3(.075f,.014f,depth-.16f),"Rose");
                var ribbon=Group(b,"Hanging bookmark pivot",new Vector3(.04f,height-.05f,-depth*.5f+.035f));
                Box(ribbon,"Bookmark tail",new Vector3(0,-.16f,-.012f),new Vector3(.075f,.32f,.018f),"Rose");
                Idle(ribbon,4.8f+variant*.12f,new Vector3(5,0,3));
            }
        }

        static void Book(Transform p,Vector3 pos,float size,string mat,float angle)
        {
            var b=Group(p,"Stacked spellbook",pos);b.localScale=Vector3.one*size;b.localRotation=Quaternion.Euler(0,angle,0);
            Box(b,"Pages",Vector3.zero,new Vector3(1.6f,.22f,1.08f),"Pages");
            foreach(float y in new[]{-.15f,.15f})Box(b,"Cover",new Vector3(0,y,0),new Vector3(1.8f,.08f,1.2f),mat);
            Box(b,"Spine",new Vector3(-.85f,0,0),new Vector3(.13f,.34f,1.2f),mat);
            Box(b,"Clasp",new Vector3(.2f,.205f,0),new Vector3(.2f,.04f,1.25f),"Gold");
            foreach(float y in new[]{-.065f,0,.065f})
            {
                Box(b,"Layered page fore edge",new Vector3(.803f,y,0),new Vector3(.012f,.012f,.99f),"PageEdge");
                Box(b,"Layered page side",new Vector3(.02f,y,.542f),new Vector3(1.48f,.012f,.012f),"PageEdge");
            }
            foreach(float z in new[]{-.36f,.36f})Box(b,"Raised spine binding",new Vector3(-.92f,0,z),new Vector3(.035f,.31f,.055f),"BookLeather");
            var bookmark=Group(b,"Stacked book ribbon pivot",new Vector3(.42f,.12f,.51f));
            Box(bookmark,"Silk bookmark",new Vector3(0,-.045f,.16f),new Vector3(.11f,.025f,.37f),"Rose");
            Idle(bookmark,5.3f,new Vector3(4,0,2));
        }

        static void Plant(Transform p)
        {
            Planter(p);
            for(int i=0;i<5;i++)
            {
                float angle=i*137.5f;
                var stemEnd=Polar(angle,.25f,1.18f+(i%2)*.14f);
                Rod(p,"Connected herb stem",new Vector3(0,.94f,0),stemEnd,.055f,"Herb");
                var leaf=Model(p,"Curved pointed leaf",LeafMesh(),i%2==0?"Leaf":"Herb",stemEnd,
                    new Vector3(.85f, .65f+(i%2)*.18f, .85f+(i%3)*.13f));
                leaf.transform.localRotation=Quaternion.Euler(-12,angle,0);
                Idle(leaf.transform,3.9f+i*.47f,new Vector3(3.8f,1,2));
            }
        }

        static void Planter(Transform p)
        {
            Model(p,"Plain hollow ceramic planter",Lathe("HollowPlanter",
                new Vector2(0,0),new Vector2(.47f,0),new Vector2(.54f,.12f),
                new Vector2(.68f,.88f),new Vector2(.76f,.94f),new Vector2(.76f,1.1f),
                new Vector2(.63f,1.1f),new Vector2(.61f,.96f),new Vector2(.46f,.23f),new Vector2(0,.23f)),
                "Pot",Vector3.zero,Vector3.one);
            Round(p,"Recessed soil",new Vector3(0,.91f,0),new Vector3(1.19f,.04f,1.19f),"WoodDark");
        }

        static Mesh LeafMesh()
        {
            const string key="CurvedHerbLeaf";
            if(Meshes.TryGetValue(key,out var mesh))return mesh;
            var v=new List<Vector3>();var t=new List<int>();
            // Two closed halves around a curved central vein; pointed tip, no floating oval decals.
            Vector3 Point(float u,int side,bool lower)
            {
                float width=Mathf.Sin(Mathf.PI*u)*.32f;
                float ridge=Mathf.Sin(Mathf.PI*u)*.09f;
                return new Vector3(side*width,Mathf.Sin(u*Mathf.PI*.8f)*.55f+(side==0?ridge:0)-(lower?.035f:0),u*1.15f);
            }
            for(int j=0;j<8;j++)
            {
                float a=j/8f,b=(j+1)/8f;
                foreach(int side in new[]{-1,1})
                {
                    var a0=Point(a,0,false);var a1=Point(a,side,false);
                    var b0=Point(b,0,false);var b1=Point(b,side,false);
                    // Clockwise winding from outside: the ridge is the upper face (+Y).
                    if(side<0)Quad(v,t,a1,b1,b0,a0);else Quad(v,t,a0,b0,b1,a1);
                    var al0=Point(a,0,true);var al1=Point(a,side,true);
                    var bl0=Point(b,0,true);var bl1=Point(b,side,true);
                    if(side<0)Quad(v,t,al0,bl0,bl1,al1);else Quad(v,t,al1,bl1,bl0,al0);
                    if(side<0)Quad(v,t,al1,bl1,b1,a1);else Quad(v,t,a1,b1,bl1,al1);
                }
            }
            return SaveMesh(key,v,t);
        }

        static GameObject Rod(Transform p,string name,Vector3 from,Vector3 to,float diameter,string material)
        {
            var go=Round(p,name,from,new Vector3(diameter,Vector3.Distance(from,to),diameter),material);
            go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,(to-from).normalized);
            return go;
        }

        static void Broom(Transform p)
        {
            var broom=Group(p,"Leaning broom",new Vector3(0,.02f,0));
            broom.localRotation=Quaternion.Euler(-6,0,-12);
            Model(broom,"Tapered straw bundle",Lathe("BesomStraw",new Vector2(0,0),new Vector2(.62f,0),
                new Vector2(.65f,.12f),new Vector2(.48f,.62f),new Vector2(.2f,1.32f),new Vector2(0,1.4f)),
                "Straw",Vector3.zero,new Vector3(1,1,.55f));
            for(int i=0;i<7;i++)
                Rod(broom,"Individual straw strand",new Vector3((i-3)*.17f,.07f,.26f),
                    new Vector3((i-3)*.045f,1.16f,.12f),.035f,"StrawShade");
            Round(broom,"Binding",new Vector3(0,1.12f,0),new Vector3(.57f,.2f,.36f),"WoodLight");
            Rod(broom,"Wooden handle",new Vector3(0,1.25f,0),new Vector3(.14f,4.4f,0),.15f,"WoodLight");
            Ball(broom,"Rounded handle end",new Vector3(.14f,4.4f,0),Vector3.one*.16f,"WoodLight");
            Idle(broom,6.6f,new Vector3(.35f,0,.85f));
        }

        static Mesh ScrollRollMesh() => Lathe("HollowParchmentRoll",new Vector2(.11f,0),
            new Vector2(.18f,0),new Vector2(.19f,.04f),new Vector2(.19f,1.06f),
            new Vector2(.18f,1.1f),new Vector2(.11f,1.1f),new Vector2(.11f,0));

        static void Scroll(Transform p,Vector3 pos,float size,float angle)
        {
            var scroll=Group(p,"Unmarked open parchment",pos);scroll.localScale=Vector3.one*size;
            scroll.localRotation=Quaternion.Euler(0,angle,0);
            Box(scroll,"Blank parchment sheet",Vector3.zero,new Vector3(1.5f,.035f,1.05f),"Pages");
            foreach(float x in new[]{-.74f,.74f})
            {
                var roll=Model(scroll,"Curled parchment edge",ScrollRollMesh(),"Pages",new Vector3(x,.1f,-.55f),Vector3.one);
                roll.transform.localRotation=Quaternion.Euler(90,0,0);
                Idle(roll.transform,5.1f+Mathf.Abs(x),new Vector3(0,3,0));
            }
        }

        static void ScrollBasket(Transform p)
        {
            Box(p,"Wooden basket bottom",new Vector3(0,.12f,0),new Vector3(1.9f,.23f,1.45f),"Wood");
            foreach(float x in new[]{-.92f,.92f})Box(p,"Basket side",new Vector3(x,.66f,0),new Vector3(.15f,1.1f,1.45f),"WoodLight");
            foreach(float z in new[]{-.66f,.66f})
                for(int row=0;row<3;row++)Box(p,"Basket slat",new Vector3(0,.3f+row*.33f,z),new Vector3(1.8f,.24f,.13f),"WoodLight");
            for(int i=0;i<3;i++)
            {
                var roll=Group(p,"Stored parchment roll",new Vector3(-.58f+i*.57f,.27f,0));
                roll.localRotation=Quaternion.Euler(0,0,(i-1)*8);
                Model(roll,"Hollow rolled paper",ScrollRollMesh(),"Pages",Vector3.zero,new Vector3(1,1.45f+i*.17f,1));
                Round(roll,"Plain ribbon tie",new Vector3(0,.8f,0),new Vector3(.41f,.12f,.41f),"Rose");
                Idle(roll,7.2f+i*.6f,new Vector3(.6f,0,.7f));
            }
        }

        static void MushroomPot(Transform p)
        {
            Planter(p);
            var positions=new[]{new Vector3(-.22f,.95f,.08f),new Vector3(.26f,.95f,.1f),new Vector3(.05f,.95f,-.27f)};
            for(int i=0;i<positions.Length;i++)
            {
                float height=.65f+i*.18f;
                var mushroom=Group(p,"Mushroom rooted pivot",positions[i]);
                Rod(mushroom,"Cream mushroom stem",Vector3.zero,Vector3.up*height,.17f,"Pages");
                Model(mushroom,"Plain rounded mushroom cap",Lathe("MushroomCap",new Vector2(0,0),new Vector2(.5f,0),
                    new Vector2(.52f,.08f),new Vector2(.46f,.24f),new Vector2(.32f,.42f),new Vector2(0,.5f)),
                    i==1?"Lavender":"Mushroom",Vector3.up*(height-.08f),Vector3.one*(.73f+i*.13f));
                Idle(mushroom,4.6f+i*.8f,new Vector3(1.1f,0,1.6f),scale:new Vector3(.008f,.018f,.008f));
            }
        }

        static void MortarAndPestle(Transform p,Vector3 pos)
        {
            var mortar=Group(p,"Small mortar and pestle",pos);
            Model(mortar,"Hollow stone mortar",Lathe("HerbalMortar",new Vector2(0,0),new Vector2(.36f,0),
                new Vector2(.48f,.1f),new Vector2(.63f,.65f),new Vector2(.63f,.76f),new Vector2(.5f,.76f),
                new Vector2(.36f,.2f),new Vector2(0,.2f)),"TrimShade",Vector3.zero,Vector3.one);
            var pestle=Group(mortar,"Pestle grinding pivot",new Vector3(-.18f,.28f,0));
            Rod(pestle,"Leaning pestle",Vector3.zero,new Vector3(.79f,.96f,.1f),.19f,"Trim");
            Ball(pestle,"Rounded pestle foot",Vector3.zero,Vector3.one*.33f,"Trim");
            Idle(pestle,5.2f,new Vector3(3,8,2));
            Model(mortar,"Herb on the worktop",LeafMesh(),"Herb",new Vector3(-.84f,-.06f,.22f),new Vector3(.45f,.13f,.5f));
        }

        static void ConfigureLighting()
        {
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.65f,.63f,.77f);
            RenderSettings.ambientEquatorColor=new Color(.43f,.38f,.5f);
            RenderSettings.ambientGroundColor=new Color(.27f,.24f,.32f);
            RenderSettings.fog=false;
            foreach(var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if(l.type==LightType.Directional){l.color=new Color(1,.88f,.78f);l.intensity=1.1f;l.transform.rotation=Quaternion.Euler(48,-35,0);l.shadows=LightShadows.Soft;}
            var camera=Camera.main;
            if(camera!=null){camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.14f,.22f);}
        }
    }
}
