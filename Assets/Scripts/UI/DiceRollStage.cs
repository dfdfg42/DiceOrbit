using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DiceOrbit.UI
{
    /// <summary>Temporary, isolated 3D render stage. All positions/sizes are in root Canvas units.</summary>
    internal sealed class DiceRollStage : IDisposable
    {
        const float Units = 100f;
        const int RenderLayer = 31;
        readonly RectTransform canvasRect;
        readonly GameObject worldRoot, overlay;
        readonly Camera camera;
        readonly RawImage output;
        readonly Mesh mesh;
        readonly Texture2D shadowTexture;
        readonly List<Material> materials = new List<Material>();
        readonly List<DieView> dice = new List<DieView>();
        RenderTexture target;
        Vector2 canvasSize;
        bool disposed;

        internal sealed class DieView
        {
            internal Transform body;
            internal RawImage shadow;
            internal TMP_Text result;
        }

        public DiceRollStage(Canvas canvas)
        {
            canvasRect = (RectTransform)canvas.transform;
            overlay = new GameObject("Dice Roll Overlay", typeof(RectTransform), typeof(Canvas));
            Stretch((RectTransform)overlay.transform, canvas.transform);
            var overlayCanvas = overlay.GetComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = 30000;
            worldRoot = new GameObject("Dice Roll 3D Stage") { hideFlags = HideFlags.DontSave };
            // No lights, colliders, or gameplay cameras are modified by this stage.
            worldRoot.transform.position = new Vector3(10000, 10000, -10000);
            camera = new GameObject("Dice Roll Camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(worldRoot.transform, false);
            camera.transform.localPosition = new Vector3(0, 0, -30);
            camera.orthographic = true;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 60;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << RenderLayer;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            camera.useOcclusionCulling = false;
            camera.depth = -100;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            // The scene renderer includes SSAO, which consumes UnityEngine.Random every frame.
            // Use a feature-free renderer for this additional camera, without changing scene rendering.
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var diceRenderer = Resources.Load<UniversalRendererData>("DiceRoll/DiceRollRenderer");
            if (pipeline != null)
                for (int i = 0; i < pipeline.rendererDataList.Length; i++)
                    if (pipeline.rendererDataList[i] == diceRenderer) { cameraData.SetRenderer(i); break; }
            cameraData.renderPostProcessing = false;
            cameraData.renderShadows = false;
            cameraData.requiresColorOption = CameraOverrideOption.Off;
            cameraData.requiresDepthOption = CameraOverrideOption.Off;
            output = new GameObject("Rendered Dice", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            Stretch(output.rectTransform, overlay.transform);
            output.raycastTarget = false;
            mesh = BuildRoundedCube();
            shadowTexture = BuildShadow();
            Resize();
        }

        public void Resize()
        {
            Vector2 size = canvasRect.rect.size;
            float aspect = size.x / Mathf.Max(1, size.y);
            int longEdge = Mathf.Clamp(Mathf.Max(Screen.width, Screen.height) * 2, 1280, 1920);
            int width = aspect >= 1 ? longEdge : Mathf.Max(1, Mathf.RoundToInt(longEdge * aspect));
            int height = aspect >= 1 ? Mathf.Max(1, Mathf.RoundToInt(longEdge / aspect)) : longEdge;
            if (size == canvasSize && target != null && target.width == width && target.height == height) return;
            canvasSize = size;
            camera.orthographicSize = size.y / (2 * Units);
            camera.aspect = aspect;
            if (target != null) { camera.targetTexture = null; target.Release(); Object.Destroy(target); }
            target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            { name = "Dice Roll Texture", antiAliasing = 2 };
            target.Create();
            camera.targetTexture = target;
            output.texture = target;
        }

        public void Add(DiceElement element)
        {
            var body = new GameObject("Rounded Die", typeof(MeshFilter), typeof(MeshRenderer));
            body.layer = RenderLayer;
            body.transform.SetParent(worldRoot.transform, false);
            body.GetComponent<MeshFilter>().sharedMesh = mesh;
            var surface = new Material(Resources.Load<Shader>("DiceRoll/DiceSurface")) { name = "Dice Roll Surface" };
            materials.Add(surface);
            var art = element.GetComponent<Image>()?.sprite;
            if (art != null)
            {
                surface.mainTexture = art.texture;
                var uv = UnityEngine.Sprites.DataUtility.GetOuterUV(art);
                surface.SetVector("_FaceRect", new Vector4(uv.x, uv.y, uv.z - uv.x, uv.w - uv.y));
            }
            body.GetComponent<MeshRenderer>().sharedMaterial = surface;
            var view = new DieView { body = body.transform };
            TMP_Text sourceText = element.GetComponentInChildren<TMP_Text>();
            var values = element.Data.Source?.Faces;
            // Put the rolled face first; retain the other faces, including duplicates and overrides.
            var ordered = new List<int>();
            ordered.Add(element.Data.Value);
            bool skippedResult = false;
            if (values != null)
                foreach (int value in values)
                {
                    if (!skippedResult && value == element.Data.Value) { skippedResult = true; continue; }
                    ordered.Add(value);
                }
            Vector3[] normals = { Vector3.back, Vector3.right, Vector3.up, Vector3.left, Vector3.down, Vector3.forward };
            for (int face = 0; face < 6; face++)
            {
                int value = values == null ? (face == 0 ? element.Data.Value : face) : ordered[face % ordered.Count];
                var label = new GameObject(face == 0 ? "Result Face" : "Face " + face, typeof(TextMeshPro)).GetComponent<TextMeshPro>();
                label.gameObject.layer = RenderLayer;
                label.transform.SetParent(body.transform, false);
                label.transform.localPosition = normals[face] * .503f;
                label.transform.localRotation = Quaternion.FromToRotation(Vector3.back, normals[face]);
                label.rectTransform.sizeDelta = new Vector2(.67f, .67f);
                label.font = sourceText != null ? sourceText.font : TMP_Settings.defaultFontAsset;
                label.color = element.Data.Source?.BaseDie != null ? element.Data.Source.BaseDie.NumberColor : new Color(.22f,.18f,.30f);
                label.alignment = TextAlignmentOptions.Center;
                label.fontStyle = FontStyles.Bold;
                label.enableAutoSizing = true;
                label.fontSize = 5.3f;
                label.fontSizeMin = 1.4f;
                label.fontSizeMax = 5.3f;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.text = value.ToString();
                label.ForceMeshUpdate();
                if (face == 0) view.result = label;
            }
            view.shadow = new GameObject("Die Shadow", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            view.shadow.transform.SetParent(overlay.transform, false);
            view.shadow.rectTransform.anchorMin = view.shadow.rectTransform.anchorMax = new Vector2(.5f, .5f);
            view.shadow.texture = shadowTexture;
            view.shadow.raycastTarget = false;
            output.transform.SetAsLastSibling();
            dice.Add(view);
        }

        public void Pose(int index, Vector2 position, float size, Quaternion rotation, float height, float opacity = 1)
        {
            var die = dice[index];
            die.body.gameObject.SetActive(opacity > 0 && size > 0);
            die.body.localPosition = new Vector3(position.x / Units, (position.y + height) / Units, 0);
            die.body.localScale = Vector3.one * (size / Units);
            die.body.localRotation = rotation;
            die.shadow.rectTransform.anchoredPosition = position + Vector2.down * size * .47f;
            float spread = 1 + height / 220;
            die.shadow.rectTransform.sizeDelta = new Vector2(size * 1.65f, size * .42f) * spread;
            die.shadow.color = new Color(.10f, .07f, .18f, .36f * opacity / spread);
        }

        public void RefreshResult(int index, int value) => dice[index].result.text = value.ToString();

        static void Stretch(RectTransform rect, Transform parent)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static Texture2D BuildShadow()
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "Dice Soft Shadow", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[64 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float radius = new Vector2((x - 31.5f) / 31.5f, (y - 31.5f) / 31.5f).sqrMagnitude;
                    pixels[y * 64 + x] = new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - radius), 2));
                }
            texture.SetPixels(pixels); texture.Apply(false, true);
            return texture;
        }

        static Mesh BuildRoundedCube()
        {
            const int steps = 12;
            const float radius = .115f;
            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            var uvs = new List<Vector2>(); var triangles = new List<int>();
            Vector3[] directions = { Vector3.back, Vector3.right, Vector3.up, Vector3.left, Vector3.down, Vector3.forward };
            foreach (Vector3 direction in directions)
            {
                Quaternion rotation = Quaternion.FromToRotation(Vector3.back, direction);
                int start = vertices.Count;
                for (int y = 0; y <= steps; y++)
                    for (int x = 0; x <= steps; x++)
                    {
                        Vector3 p = rotation * new Vector3((float)x / steps - .5f, (float)y / steps - .5f, -.5f);
                        Vector3 inner = new Vector3(Mathf.Clamp(p.x, -.5f + radius, .5f - radius), Mathf.Clamp(p.y, -.5f + radius, .5f - radius), Mathf.Clamp(p.z, -.5f + radius, .5f - radius));
                        Vector3 normal = (p - inner).normalized;
                        vertices.Add(inner + normal * radius); normals.Add(normal);
                        uvs.Add(new Vector2((float)x / steps, (float)y / steps));
                        if (x == steps || y == steps) continue;
                        int a = start + y * (steps + 1) + x;
                        triangles.AddRange(new[] { a, a + steps + 1, a + 1, a + 1, a + steps + 1, a + steps + 2 });
                    }
            }
            var result = new Mesh { name = "Dice Rounded Cube" };
            result.SetVertices(vertices); result.SetNormals(normals); result.SetUVs(0, uvs); result.SetTriangles(triangles, 0);
            result.RecalculateBounds();
            return result;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            // Scene teardown may destroy these independent hierarchies before the animator.
            if (camera != null) { camera.enabled = false; camera.targetTexture = null; }
            if (worldRoot != null) worldRoot.SetActive(false);
            if (overlay != null) overlay.SetActive(false);
            Object.Destroy(worldRoot); Object.Destroy(overlay);
            foreach (var material in materials) Object.Destroy(material);
            Object.Destroy(mesh); Object.Destroy(shadowTexture);
            if (target != null) { target.Release(); Object.Destroy(target); }
        }
    }
}
