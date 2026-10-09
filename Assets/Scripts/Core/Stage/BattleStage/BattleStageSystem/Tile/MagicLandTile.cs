using DiceOrbit.Core;
using DiceOrbit.Data.Modifiers.Job;
using UnityEngine;

namespace DiceOrbit.Data.Tile
{
    /// <summary>
    /// 마법의 땅 (2026-10-09 C) — 마법사 [마법의 땅] 모디파이어가 전투 시작 시 무작위 구역 하나의 모든 타일에 설치한다.
    /// 피해 보정은 MagicLandModifier.OnAttack이 주인의 CurrentTile을 보고 낸다 — 이 속성은 표식·툴팁·버블만 맡고 반응하지 않는다.
    /// 다른 타일 속성과 공존(타입별 1개)하고, 공격 범위·아군 점유는 바꾸지 않는다.
    /// 전투 종료 시 CombatManager.EndCombat이 ClearAll로 지운다. 시각 항목은 예리함 아이콘을 보라색으로 임시 사용(전용 아트는 후속).
    /// </summary>
    public class MagicLandTile : TileAttribute
    {
        /// <summary>설치한 모디파이어 — 다음 전투 시작 때 자기 타일만 지우는 데 쓴다 (null 허용).</summary>
        public readonly MagicLandModifier Source;

        public MagicLandTile(MagicLandModifier source)
            : base(TileAttributeType.MagicLand, Mathf.RoundToInt(MagicLandModifier.Percent), -1, false)
        {
            Source = source;
        }

        public override string GetDescription()
            => $"마법사가 이 타일에서 공격하면 주는 피해가 {Value}% 증가합니다. 전투가 끝나면 사라집니다.";

        /// <summary>궤도의 모든 마법의 땅을 지운다 (전투 종료).</summary>
        public static void ClearAll()
        {
            var orbit = GameManager.Instance != null ? GameManager.Instance.GetOrbitManager() : null;
            if (orbit == null || orbit.Tiles == null) return;

            foreach (var tile in orbit.Tiles)
                if (tile != null) tile.RemoveAttributeType(TileAttributeType.MagicLand);
        }

        /// <summary>source가 설치한 마법의 땅만 지운다 (설치 직전 정리 — 다른 마법사의 것은 남긴다).</summary>
        public static void ClearFrom(MagicLandModifier source)
        {
            var orbit = GameManager.Instance != null ? GameManager.Instance.GetOrbitManager() : null;
            if (orbit == null || orbit.Tiles == null) return;

            foreach (var tile in orbit.Tiles)
            {
                if (tile == null) continue;
                foreach (var attribute in tile.GetAttributes())   // 스냅샷 — 순회 중 제거 안전
                {
                    if (attribute is MagicLandTile land && land.Source == source)
                    {
                        tile.RemoveAttribute(attribute);
                        break;
                    }
                }
            }
        }
    }
}
