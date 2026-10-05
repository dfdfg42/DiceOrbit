using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Data.Passives
{
    /// <summary>
    /// [시약 준비] 전투 시작 시 서로 다른 무작위 타일에 영구 시약 타일을 설치한다 (2026-08-28 개편).
    /// 시약 타일은 소모되지 않으며, 연금술사를 포함한 어떤 살아 있는 아군이든 지나가거나 도착하면
    /// 촉매(다음 자동공격 행동 피해 +catalystPercent%)를 얻는다 — 파티 전체가 쓰는 경로 자원.
    /// (구 '본인 전용 딜 스택'은 촉매 공유 체계로 대체됐다.)
    /// </summary>
    [System.Serializable]
    public class ReagentPrepPassive : CharacterPassiveSkill
    {
        [Header("Designer Tuning")]
        [Tooltip("웨이브 시작 시 설치할 시약 타일 개수")]
        [SerializeField] private int reagentTileCount = 3;
        [Tooltip("시약 타일이 부여하는 촉매의 피해 증가율(%). 다음 자동공격 행동 1회에 적용 후 소멸.")]
        [SerializeField] private int catalystPercent = 25;

        /// <summary>시약 타일이 밟힐 때 부여할 촉매 수치(%). ReagentTile이 읽는다.</summary>
        public int CatalystPercent => Mathf.Max(0, catalystPercent);

        public override int Priority => 98;

        public override string GetDynamicDescription()
        {
            return $"전투 시작 시 시약 타일 {reagentTileCount}개 설치 (소모 안 됨)\n"
                 + $"아군이 밟으면 촉매 (다음 자동공격 +{CatalystPercent}%)";
        }

        public override void Initialize(Unit ownerUnit)
        {
            base.Initialize(ownerUnit);
            SubscribeCombatStart();

            // 전투 진행 중에 합류한 경우 즉시 설치
            if (CombatManager.Instance != null && CombatManager.Instance.InCombat)
                PlaceReagentTiles();
        }

        private void SubscribeCombatStart()
        {
            if (CombatManager.Instance == null) return;
            CombatManager.Instance.OnCombatStart -= HandleCombatStart;
            CombatManager.Instance.OnCombatStart += HandleCombatStart;
        }

        private void HandleCombatStart()
        {
            if (owner == null) return;
            if (!(owner is Character ch) || !ch.IsAlive) return;

            ClearReagentTiles();
            PlaceReagentTiles();
        }

        private void PlaceReagentTiles()
        {
            var orbitManager = GameManager.Instance != null ? GameManager.Instance.GetOrbitManager() : null;
            if (orbitManager == null) return;

            var tiles = orbitManager.Tiles;
            if (tiles == null || tiles.Count == 0) return;

            int count = Mathf.Clamp(reagentTileCount, 0, tiles.Count);

            var pool = tiles.Where(t => t != null && !t.HasAttribute(TileAttributeType.Reagent)).ToList();
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int idx = Random.Range(0, pool.Count);
                var tile = pool[idx];
                pool.RemoveAt(idx);
                tile.AddAttribute(new ReagentTile(this));
            }
        }

        private void ClearReagentTiles()
        {
            var orbitManager = GameManager.Instance != null ? GameManager.Instance.GetOrbitManager() : null;
            if (orbitManager == null) return;

            var tiles = orbitManager.Tiles;
            if (tiles == null) return;

            foreach (var tile in tiles)
            {
                if (tile != null) tile.RemoveAttributeType(TileAttributeType.Reagent);
            }
        }

        // (구 시그니처 모디파이어의 시약 타일 추가 연동은 2026-08-28 공용 모디파이어 전면 교체로 폐기됐다)
    }
}
