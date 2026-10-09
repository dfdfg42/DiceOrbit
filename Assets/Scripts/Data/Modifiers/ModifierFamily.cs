namespace DiceOrbit.Data.Modifiers
{
    /// <summary>
    /// 모디파이어 계열 — 보상 카드의 계열 아이콘·라벨을 정한다 (보상 리워크 2026-10-03).
    /// 공용 17종 = 위치 5 / 주사위 4 / 콤보 4 / 생존 4 (2026-10-09 B). 계열이 없는 모디파이어(None)는 보상 카드로 제시할 수 없다.
    /// </summary>
    public enum ModifierFamily
    {
        None = 0,
        Position = 1,   // 위치
        Dice = 2,       // 주사위
        Combo = 3,      // 콤보
        Survival = 4,   // 생존
    }

    public static class ModifierFamilyExtensions
    {
        /// <summary>계열 한국어 표기 (카드 상단 라벨).</summary>
        public static string Label(this ModifierFamily family) => family switch
        {
            ModifierFamily.Position => "위치",
            ModifierFamily.Dice     => "주사위",
            ModifierFamily.Combo    => "콤보",
            ModifierFamily.Survival => "생존",
            _ => throw new System.ArgumentOutOfRangeException(nameof(family), family, "계열이 없는 모디파이어에는 라벨이 없다"),
        };
    }
}
