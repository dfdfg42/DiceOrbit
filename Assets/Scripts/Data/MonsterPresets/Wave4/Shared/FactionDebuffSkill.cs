using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.MonsterPresets.Wave4.Shared
{
    /// <summary>
    /// 진영 디버프 스킬 (흑점=취약 / 만월=쇠약 공용). 공격 대신 무작위 적 캐릭터에게 상태이상 부여.
    /// MonsterSkill 설정 권장: TargetStrategy=RandomCharacter, TargetType=Characters, count 1, IntentType=Special.
    /// 취약/쇠약은 기존 VulnerableStatus/WeakStatus 를 재사용한다.
    /// </summary>
    [System.Serializable]
    public class FactionDebuffSkill : SkillData
    {
        public enum DebuffKind { Vulnerable, Weak }

        [Header("Debuff Settings")]
        [Tooltip("스킬 표시 이름 (예: 흑점, 만월)")]
        [SerializeField] private string skillLabel = "디버프";
        [Tooltip("Vulnerable=대상이 받는 피해 증가, Weak=대상이 가하는 피해 감소")]
        [SerializeField] private DebuffKind kind = DebuffKind.Vulnerable;
        [Tooltip("증가/감소 퍼센트")]
        [SerializeField] private int percent = 20;
        [Tooltip("지속 턴 (다음 몬스터 턴을 덮으려면 2 권장)")]
        [SerializeField] private int duration = 2;

        public override string SkillName => string.IsNullOrEmpty(skillLabel) ? "디버프" : skillLabel;
        // 말풍선 옆 칩에 그대로 뜨는 글 — 한두 줄 (첫 줄 = 무슨 일, 둘째 줄 = 부연). 수치는 설정값에서 온다.
        public override string Description => kind == DebuffKind.Vulnerable
            ? $"취약 {duration}턴 · 1명\n받는 피해 {percent}% 증가"
            : $"쇠약 {duration}턴 · 1명\n주는 피해 {percent}% 감소";

        public override int GetPreviewDamage() => 0;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            if (targetUnits == null) return;
            foreach (var u in targetUnits)
            {
                if (u == null || !u.IsAlive || u.StatusEffects == null) continue;
                if (kind == DebuffKind.Vulnerable)
                    u.StatusEffects.AddEffect(new VulnerableStatus(percent, duration));
                else
                    u.StatusEffects.AddEffect(new WeakStatus(percent, duration));
            }
            Debug.Log($"[{SkillName}] {(kind == DebuffKind.Vulnerable ? "취약" : "쇠약")} +{percent}% ({duration}턴) → {targetUnits.Count}명");
        }
    }
}
