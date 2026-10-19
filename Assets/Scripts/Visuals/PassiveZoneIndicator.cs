using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Zones;
using DiceOrbit.Data.Passives;
using DiceOrbit.UI.Skin;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 패시브 구역 표시 — "구역 테" (2026-10-04, 타일 모서리 ㄱ자 브래킷 PassiveRangeIndicator를 대체).
    /// 조회 중인 캐릭터의 구역 패시브(<see cref="IPassiveZoneProvider"/>)마다:
    ///   · 구역 부채꼴을 통째로 감싸는 둥근 테두리 하나 (짙은 잉크 밑선 + 하늘색 심선, 타일 위·유닛 아래)
    ///   · 그 구역 플레이트를 살짝 밝힘 (색상은 그대로 — 주인 몬스터 색 의미 유지)
    /// 효과가 꺼져 있으면(같은 구역 아군 없음 등) 테두리를 흐리게, 밝힘은 끈다.
    /// "지금 몇 %인가"는 월드에 글자를 띄우지 않고 정보 패널의 패시브 제목 옆에 적는다 (UnitInfoBuilder) —
    /// 구역 안쪽에는 몬스터 이름·HP 바가 있어 글자를 얹을 자리가 없다.
    ///
    /// ── 표시 규칙 ─────────────────────────────────────────────
    /// - 조회 시에만: 정보 패널이 캐릭터를 표시할 때 Show, 아니면 Hide (상시 표시 없음)
    /// - 타게팅 모드 중엔 숨김 (위험/조준 > 정보 우선순위)
    /// - 매 프레임 패시브에 다시 묻는다 — 조회 중 아군이 움직이거나 죽으면 바로 따라간다
    /// - 궤도·플레이트가 준비되기 전에는 그리지 않고 기다린다 (ZonePlateRenderer와 같은 규칙)
    /// </summary>
    public class PassiveZoneIndicator : MonoBehaviour
    {
        public static PassiveZoneIndicator Instance { get; private set; }

        [Header("테두리 기하")]
        [Tooltip("테두리 안쪽 호의 반지름 — 플레이트 안쪽 가장자리(≈2.5) 바로 바깥")]
        [SerializeField] private float innerRadius = 2.95f;
        [Tooltip("구역 타일의 가장 먼 모서리에서 바깥으로 더 나가는 거리")]
        [SerializeField] private float outerMargin = 0.4f;
        [Tooltip("구역 경계에서 안쪽으로 들이는 거리 — 이웃 구역 테두리와 겹치지 않게")]
        [SerializeField] private float edgeInset = 0.3f;
        [SerializeField] private float cornerRadius = 0.9f;
        [SerializeField] private float arcStepDeg = 3f;
        [Tooltip("타일 윗면에서 띄우는 높이")]
        [SerializeField] private float elevation = 0.06f;

        [Header("테두리 선")]
        [SerializeField] private float coreWidth = 0.2f;
        [SerializeField] private float inkWidth = 0.46f;
        [SerializeField, Range(0f, 1f)] private float inkAlpha = 0.8f;
        [Tooltip("효과가 꺼진 구역의 테두리 알파 배수")]
        [SerializeField, Range(0f, 1f)] private float dormantAlpha = 0.4f;

        [Header("구역 밝힘")]
        [Tooltip("플레이트 위에 얹는 흰색의 알파 (효과가 켜진 구역만)")]
        [SerializeField, Range(0f, 1f)] private float plateLighten = 0.22f;

        [Header("공통")]
        [SerializeField] private float fadeDuration = 0.15f;
        [Tooltip("플레이트 대비 렌더 순서. 플레이트(유닛 −50)보다 앞, 발밑 마커(유닛 −1)보다 뒤")]
        [SerializeField] private int sortingOrderAbovePlate = 10;

        private sealed class ZoneVisual
        {
            public GameObject Root;
            public LineRenderer Ink, Core;
            public SpriteRenderer Lighten;
            public bool GeometryBuilt;
            public float Fade;          // 0~1
            public bool Active;         // 마지막으로 그린 상태 (페이드아웃 중에도 유지)
        }

        private Character _current;
        private ZoneVisual[] _visuals = new ZoneVisual[0];
        private Material _lineMaterial;   // 모든 테두리가 공유 (누수 방지)

        private readonly Dictionary<int, bool> _wanted = new Dictionary<int, bool>();   // 구역 → 효과 켜짐
        private readonly List<int> _scratchZones = new List<int>();
        private readonly List<Vector3> _scratchPoints = new List<Vector3>();

        // ── 수명 ─────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _lineMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_lineMaterial != null) Destroy(_lineMaterial);
        }

        public static void EnsureInstance()
        {
            if (Instance != null) return;
            new GameObject("[PassiveZoneIndicator]").AddComponent<PassiveZoneIndicator>();
        }

        // ── 공개 API ──────────────────────────────────────────

        /// <summary>이 캐릭터의 구역 패시브를 표시한다. 같은 캐릭터를 다시 넘겨도 된다 (매 렌더 호출).</summary>
        public void Show(Character character)
        {
            if (character == _current) return;
            _current = character;
            foreach (var visual in _visuals)
                if (visual != null) visual.GeometryBuilt = false;   // 조회가 바뀔 때마다 기하를 다시 잰다 (궤도 재생성 대비)
        }

        public void Hide() => _current = null;

        // ── 갱신 ─────────────────────────────────────────────

        private void LateUpdate()
        {
            var zones = CombatZoneManager.Instance;
            CollectWanted(zones);

            int zoneCount = zones != null ? zones.ZoneCount : 0;
            if (_visuals.Length != zoneCount) Rebuild(zoneCount);

            float step = fadeDuration > 0f ? Time.unscaledDeltaTime / fadeDuration : 1f;
            for (int zone = 0; zone < _visuals.Length; zone++)
            {
                bool show = _wanted.TryGetValue(zone, out bool active) && Prepare(zones, zone, active);
                var visual = _visuals[zone];
                if (visual == null) continue;

                visual.Fade = Mathf.Clamp01(visual.Fade + (show ? step : -step));
                Paint(visual);
            }
        }

        /// <summary>조회 캐릭터의 구역 패시브에 물어, 표시할 구역(→ 효과 켜짐 여부)을 모은다.</summary>
        private void CollectWanted(CombatZoneManager zones)
        {
            _wanted.Clear();

            if (_current == null || !_current.IsAlive) return;
            if (zones == null || !zones.IsGeometryReady) return;
            if (SkillTargetSelector.Instance != null && SkillTargetSelector.Instance.IsSelectingTarget) return;
            var passives = _current.Passives != null ? _current.Passives.ActivePassives : null;
            if (passives == null) return;

            foreach (var passive in passives)
            {
                if (!(passive is IPassiveZoneProvider provider)) continue;
                _scratchZones.Clear();
                provider.CollectPassiveZones(_scratchZones);
                if (_scratchZones.Count == 0) continue;

                // 같은 구역에 패시브가 둘 걸리면 하나라도 켜져 있을 때 켜진 것으로 그린다
                bool active = provider.GetPassiveZoneStatus().Active;
                foreach (int zone in _scratchZones)
                    _wanted[zone] = active || (_wanted.TryGetValue(zone, out bool already) && already);
            }
        }

        // ── 구역 하나 ─────────────────────────────────────────

        private void Rebuild(int zoneCount)
        {
            foreach (var visual in _visuals)
                if (visual != null && visual.Root != null) Destroy(visual.Root);
            _visuals = new ZoneVisual[zoneCount];
        }

        /// <summary>
        /// 구역 표시물을 그릴 수 있게 준비한다 (생성·기하·정렬·밝힘 자세). 플레이트나 타일이 아직 없으면 false — 다음 프레임에 다시.
        /// </summary>
        private bool Prepare(CombatZoneManager zones, int zone, bool active)
        {
            var plates = ZonePlateRenderer.Instance;
            if (plates == null || !plates.TryGetPlate(zone, out var plate)) return false;

            var visual = _visuals[zone];
            if (visual == null) visual = _visuals[zone] = Create(zone);

            if (!visual.GeometryBuilt)
            {
                if (!BuildOutline(zones, zone, visual)) return false;
                visual.GeometryBuilt = true;
            }

            visual.Active = active;

            // 정렬·밝힘 자세는 플레이트를 따른다 (플레이트가 다시 만들어져도 어긋나지 않게 매번 맞춘다)
            int order = plate.sortingOrder + sortingOrderAbovePlate;
            visual.Ink.sortingLayerID = plate.sortingLayerID;
            visual.Ink.sortingOrder = order;
            visual.Core.sortingLayerID = plate.sortingLayerID;
            visual.Core.sortingOrder = order + 1;

            visual.Lighten.sprite = plate.sprite;
            // Keep a scene's floor treatment when highlighting; do not bring back the original painted border.
            visual.Lighten.sharedMaterial = plate.sharedMaterial;
            visual.Lighten.sortingLayerID = plate.sortingLayerID;
            visual.Lighten.sortingOrder = plate.sortingOrder + 1;
            visual.Lighten.transform.SetPositionAndRotation(plate.transform.position + Vector3.up * 0.002f, plate.transform.rotation);
            visual.Lighten.transform.localScale = plate.transform.lossyScale;
            return true;
        }

        private ZoneVisual Create(int zone)
        {
            var root = new GameObject($"_PassiveZone{zone}");
            root.transform.SetParent(transform, false);

            var visual = new ZoneVisual
            {
                Root = root,
                Ink = CreateLine(root.transform, "_Ink", inkWidth),
                Core = CreateLine(root.transform, "_Core", coreWidth),
            };

            var lightenGo = new GameObject("_Lighten");
            lightenGo.transform.SetParent(root.transform, false);
            visual.Lighten = lightenGo.AddComponent<SpriteRenderer>();
            visual.Lighten.enabled = false;
            return visual;
        }

        private LineRenderer CreateLine(Transform parent, string name, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.startWidth = width;
            line.endWidth = width;
            line.sharedMaterial = _lineMaterial;   // 공유 머티리얼 (인스턴스 생성 안 함)
            line.numCornerVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        /// <summary>구역 테두리 점열을 다시 잰다. 구역에 타일이 아직 없으면 false.</summary>
        private bool BuildOutline(CombatZoneManager zones, int zone, ZoneVisual visual)
        {
            var tiles = zones.GetTilesInZone(zone);
            if (tiles.Count == 0) return false;

            float farthest = 0f, topY = float.MinValue;
            foreach (var tile in tiles)
                foreach (var corner in TileCornerResolver.ResolveTopCorners(tile, 0f))
                {
                    farthest = Mathf.Max(farthest, new Vector2(corner.x, corner.z).magnitude);
                    topY = Mathf.Max(topY, corner.y);
                }

            zones.GetZoneAngularRangeDeg(zone, out float startDeg, out float endDeg);
            ZoneSectorShape.Build(startDeg, endDeg, innerRadius, farthest + outerMargin,
                edgeInset, cornerRadius, arcStepDeg, topY + elevation, _scratchPoints);

            visual.Ink.positionCount = _scratchPoints.Count;
            visual.Ink.SetPositions(_scratchPoints.ToArray());
            visual.Core.positionCount = _scratchPoints.Count;
            visual.Core.SetPositions(_scratchPoints.ToArray());
            return true;
        }

        /// <summary>페이드·켜짐 상태를 색에 반영한다. 다 사라졌으면 렌더러를 끈다.</summary>
        private void Paint(ZoneVisual visual)
        {
            bool visible = visual.Fade > 0f && visual.GeometryBuilt;
            visual.Ink.enabled = visible;
            visual.Core.enabled = visible;
            visual.Lighten.enabled = visible && visual.Active && visual.Lighten.sprite != null;
            if (!visible) return;

            var skin = UiSkin.Current;
            float strength = visual.Fade * (visual.Active ? 1f : dormantAlpha);

            Color ink = skin.Ink; ink.a = inkAlpha * strength;
            Color core = skin.Secondary; core.a = strength;
            visual.Ink.startColor = visual.Ink.endColor = ink;
            visual.Core.startColor = visual.Core.endColor = core;
            visual.Lighten.color = new Color(1f, 1f, 1f, plateLighten * visual.Fade);
        }
    }
}
