using DiceOrbit.Core;
using DiceOrbit.Core.Zones;
using DiceOrbit.UI.Skin;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 구역 플레이트 — 사분면 조각 스프라이트(UiSkin.ZonePlate)를 타일 아래 바닥에 눕혀 주인 몬스터 색으로 틴트한다
    /// (스펙 2026-09-25-monster-zone-intent-reskin §2). 구 ZoneFloorRenderer(V자·ㄱ자 브래킷)를 대체.
    ///
    /// 스프라이트: pivot (0,0) = 궤도 중심, PPU 128 → 16×16 유닛, 조각은 +x·+y(위) 사분면.
    /// 배치: 위치 (0, plateY, 0), 회전 Euler(90, −startDeg, 0) — 눕힌 뒤 구역 시작각만큼 반시계로 돌린다.
    /// 타일(불투명 메시, y 0~0.03) 아래에 있어 타일이 그 위에 올라앉고, 사이·안팎으로 플레이트가 드러난다.
    /// 정렬: 유닛 레이어에서 order −50 — 방 배경 스프라이트(−100)와 발밑 마커(−1) 사이.
    ///
    /// 등장: 주인 몬스터가 인트로 팝인으로 드러난 뒤 페이드인, 중립 구역은 주인 있는 몬스터가 전부 드러난 뒤.
    /// </summary>
    public class ZonePlateRenderer : MonoBehaviour
    {
        public static ZonePlateRenderer Instance { get; private set; }

        [Tooltip("플레이트 높이. 타일 바닥(0)보다 아래여야 타일이 위에 올라앉는다")]
        [SerializeField] private float plateY = -0.02f;
        [Tooltip("선택 사항: 씬 배경에 맞춘 구역 재질. 비어 있으면 기존 스프라이트 재질을 사용한다. 생성 전에 설정한다.")]
        public Material PlateMaterial;
        [Tooltip("영역 스프라이트 크기. 기본값 1은 기존 씬의 크기를 유지한다.")]
        [Min(0.01f)] public float PlateScale = 1f;
        [Header("틴트")]
        [SerializeField, Range(0f, 1f)] private float ownerSaturation = 0.32f;
        [SerializeField, Range(0f, 1f)] private float ownerAlpha = 0.85f;
        [SerializeField] private Color neutralTint = new Color(0.80f, 0.80f, 0.86f, 0.5f);
        [SerializeField] private float fadeDuration = 0.25f;
        [Tooltip("유닛 스프라이트(0) 대비 렌더 순서. 발밑 마커(−1)보다 뒤, 방 배경 스프라이트(−100)보다는 앞이어야 한다 — −200은 배경에 가려 안 보였다 (2026-09-25)")]
        [SerializeField] private int sortingOrderOffset = -50;

        private SpriteRenderer[] _plates = new SpriteRenderer[0];
        private int[] _lastOwnerIds;      // 파괴된 몬스터는 == null이라 InstanceID로 추적
        private bool[] _lastVisible;
        private Color[] _targetTint;
        private float[] _fadeT;
        private bool _sortingSynced;

        // ── 순수 헬퍼 (ZonePlateSelfTests) ─────────────────────

        /// <summary>눕힌 뒤(X 90°) 구역 시작각만큼 반시계로 돌린 회전. 스프라이트 +x → 각도 startDeg 방향.</summary>
        public static Quaternion PlateRotation(float startDeg) => Quaternion.Euler(90f, -startDeg, 0f);

        /// <summary>정체성 색의 색상(H)은 두고 채도를 낮춰 종이 위에 얹기 좋은 틴트로 만든다.</summary>
        public static Color OwnerTint(Color identity, float saturation, float alpha)
        {
            Color.RGBToHSV(identity, out float h, out _, out _);
            var c = Color.HSVToRGB(h, saturation, 1f);
            c.a = alpha;
            return c;
        }

        // ── 수명 ─────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static ZonePlateRenderer EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<ZonePlateRenderer>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }
            return new GameObject("[ZonePlateRenderer]").AddComponent<ZonePlateRenderer>();
        }

        /// <summary>
        /// 구역의 플레이트 렌더러 — 지금 보이고 정렬까지 끝난 상태일 때만 true.
        /// 구역 위에 겹쳐 그리는 표시(패시브 구역 테두리·밝힘)가 스프라이트·자세·정렬을 맞추는 기준이다.
        /// </summary>
        public bool TryGetPlate(int zone, out SpriteRenderer plate)
        {
            plate = zone >= 0 && zone < _plates.Length ? _plates[zone] : null;
            return plate != null && plate.enabled && _sortingSynced;
        }

        private void LateUpdate()
        {
            var zones = CombatZoneManager.Instance;
            if (zones == null || !zones.IsGeometryReady) return;   // 준비 전 각도로 놓으면 반 칸 밀기가 빠진다

            if (_plates.Length != zones.ZoneCount) Build(zones);
            SyncSortingBehindUnits();
            Refresh(zones);
            Fade();
        }

        // ── 생성 ─────────────────────────────────────────────

        private void Build(CombatZoneManager zones)
        {
            foreach (var p in _plates) if (p != null) Destroy(p.gameObject);

            var sprite = UiSkin.Current.GetZonePlate();   // 비면 예외 — 브래킷·사각형 폴백 없음
            int n = zones.ZoneCount;
            _plates = new SpriteRenderer[n];
            _lastOwnerIds = new int[n];
            _lastVisible = new bool[n];
            _targetTint = new Color[n];
            _fadeT = new float[n];

            for (int zone = 0; zone < n; zone++)
            {
                zones.GetZoneAngularRangeDeg(zone, out float startDeg, out _);
                var go = new GameObject($"_ZonePlate{zone}");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(0f, plateY, 0f);
                go.transform.rotation = PlateRotation(startDeg);
                go.transform.localScale = Vector3.one * PlateScale;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                if (PlateMaterial != null) sr.sharedMaterial = PlateMaterial;
                sr.color = new Color(1f, 1f, 1f, 0f);
                sr.enabled = false;
                _plates[zone] = sr;
                _lastOwnerIds[zone] = int.MinValue;   // 첫 프레임에 반드시 칠하도록
            }
            _sortingSynced = false;
        }

        // ── 색·표시 갱신 ─────────────────────────────────────

        private void Refresh(CombatZoneManager zones)
        {
            var identity = MonsterIdentityManager.Instance;
            bool allOwnersRevealed = AreAllOwnersRevealed(zones);

            for (int zone = 0; zone < zones.ZoneCount; zone++)
            {
                var owner = zones.GetOwner(zone);
                bool visible = owner != null ? IsMonsterRevealed(owner) : allOwnersRevealed;
                int ownerId = owner != null ? owner.GetInstanceID() : 0;
                if (_lastOwnerIds[zone] == ownerId && _lastVisible[zone] == visible) continue;

                bool wasVisible = _lastVisible[zone];
                _lastOwnerIds[zone] = ownerId;
                _lastVisible[zone] = visible;

                _targetTint[zone] = owner != null && identity != null
                    ? OwnerTint(identity.GetColor(owner), ownerSaturation, ownerAlpha)
                    : neutralTint;

                var sr = _plates[zone];
                if (sr == null) continue;
                sr.enabled = visible;
                if (visible && !wasVisible)
                {
                    _fadeT[zone] = 0f;                                   // 새로 나타남 → 알파 0에서 페이드인
                    var c = _targetTint[zone]; c.a = 0f; sr.color = c;
                }
                else if (visible)
                {
                    _fadeT[zone] = fadeDuration;                         // 주인만 바뀜 → 즉시 색 교체
                    sr.color = _targetTint[zone];
                }
            }
        }

        private void Fade()
        {
            for (int zone = 0; zone < _plates.Length; zone++)
            {
                var sr = _plates[zone];
                if (sr == null || !sr.enabled || _fadeT[zone] >= fadeDuration) continue;
                _fadeT[zone] += Time.deltaTime;
                float k = fadeDuration <= 0f ? 1f : Mathf.Clamp01(_fadeT[zone] / fadeDuration);
                var c = _targetTint[zone];
                c.a = _targetTint[zone].a * k;
                sr.color = c;
            }
        }

        /// <summary>몬스터가 인트로 팝인으로 충분히 드러났는지 — 발밑 마커(MonsterIdentityManager)와 같은 판정.</summary>
        private static bool IsMonsterRevealed(Monster monster)
        {
            float baseX = monster.IntroBaseScale.x;
            float threshold = Mathf.Max(0.02f, baseX * 0.15f);
            return monster.transform.localScale.x > threshold;
        }

        private static bool AreAllOwnersRevealed(CombatZoneManager zones)
        {
            for (int zone = 0; zone < zones.ZoneCount; zone++)
            {
                var owner = zones.GetOwner(zone);
                if (owner != null && !IsMonsterRevealed(owner)) return false;
            }
            return true;
        }

        // ── 정렬 ─────────────────────────────────────────────

        /// <summary>유닛 스프라이트와 같은 정렬 레이어에서 그보다 뒤로. 레이어가 다르면 order를 낮춰도 유닛 위로 튀어나온다.</summary>
        private void SyncSortingBehindUnits()
        {
            if (_sortingSynced || _plates.Length == 0) return;
            var reference = FindAnyUnitSprite();
            if (reference == null) return;   // 유닛이 아직 없다 — 다음 프레임에 다시 시도
            foreach (var sr in _plates)
            {
                if (sr == null) continue;
                sr.sortingLayerID = reference.sortingLayerID;
                sr.sortingOrder = reference.sortingOrder + sortingOrderOffset;
            }
            _sortingSynced = true;
        }

        private static SpriteRenderer FindAnyUnitSprite()
        {
            var combat = CombatManager.Instance;
            if (combat != null && combat.ActiveMonsters != null)
                foreach (var monster in combat.ActiveMonsters)
                    if (monster != null)
                    {
                        var sprite = MonsterIdentityManager.FindVisibleSprite(monster.transform);
                        if (sprite != null) return sprite;
                    }

            var party = PartyManager.Instance != null ? PartyManager.Instance.Party : null;
            if (party != null)
                foreach (var character in party)
                    if (character != null)
                    {
                        var sprite = MonsterIdentityManager.FindVisibleSprite(character.transform);
                        if (sprite != null) return sprite;
                    }
            return null;
        }
    }
}
