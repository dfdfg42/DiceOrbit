using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace DiceOrbit.Data
{
    /// <summary>
    /// 스킬 타겟 타입
    /// </summary>
    public enum SkillTargetType
    {
        SingleEnemy,
        AllEnemies,
        Self,
        Ally,
        AllAllies,
        Tiles,
        None
    }

    /// <summary>
    /// 스킬 데이터 (다형성 지원 - [SerializeReference]로 사용)
    /// </summary>
    [System.Serializable]
    public abstract class SkillData
    {
        [SerializeField] protected string skillName = "";
        [SerializeField] protected string description = "";

        [Tooltip("몬스터 의도 말풍선 옆에 띄우는 짧은 설명 — 한두 줄. 첫 줄 = 무슨 일이 일어나나, 둘째 줄 = 조건·부연. {피해}는 지금 상황의 예상 피해로 바뀐다. 예: {피해} 피해 · 대상 좌우 2칸")]
        [SerializeField, TextArea(1, 2)] protected string intentSummary = "";

        [Tooltip("이 스킬의 큐 태그. 비우면 루트(cast/impact) 폴백")]
        [SerializeField] protected string castCue = "";
        [SerializeField] protected string impactCue = "";

        public virtual string SkillName => skillName;
        public virtual string Description => description;
        /// <summary>의도 말풍선 옆 짧은 설명의 원문 ({피해} 치환 전). 보여 줄 때는 UI.MonsterIntentText를 거친다.</summary>
        public virtual string IntentSummary => intentSummary;

        /// <summary>
        /// 의도 미리보기용 기본 피해. 0이면 피해 스킬이 아니므로 예상 피해를 표시하지 않는다.
        /// SimulateCalculation의 시드값으로 쓰여 패시브/모디파이어 보너스가 합산된 최종 예상 피해가 산출된다.
        /// </summary>
        public virtual int GetPreviewDamage() => 0;

        /// <summary>
        /// TargetSelectionStrategy가 Custom일 때 호출되어 타겟 유닛들을 가져옵니다.
        /// 파생 클래스에서 오버라이드하여 독자적인 타겟팅을 구현하세요.
        /// </summary>
        public virtual List<Core.Unit> GetCustomTargets(MonsterSkill skill, Core.Monster owner)
        {
            return new List<Core.Unit>();
        }

        /// <summary>
        /// TargetSelectionStrategy가 Custom일 때 호출되어 타겟 타일들을 가져옵니다.
        /// 파생 클래스에서 오버라이드하여 독자적인 타겟팅을 구현하세요.
        /// </summary>
        public virtual List<TileData> GetCustomTiles(MonsterSkill skill, Core.Monster owner)
        {
            return new List<TileData>();
        }

        /// <summary>
        /// true면 이 타일 스킬은 대상 1명을 무작위로 고정해 두고, 그 대상의 현재 위치를 따라
        /// 매 RefreshTargets마다 타일을 재계산한다(AttackIntent가 GetFollowTiles 호출). 기본 false.
        /// </summary>
        public virtual bool FollowsTarget => false;

        /// <summary>FollowsTarget=true일 때, 고정된 대상(target)의 현재 위치를 기준으로 공격 타일을 계산해 반환.</summary>
        public virtual List<TileData> GetFollowTiles(Core.Character target)
        {
            return new List<TileData>();
        }

        public void ExecuteSkillWithIntent(Core.Unit source, AttackIntent intent)
        {
            if (source == null || intent == null)
            {
                Debug.LogWarning("[SkillData] Execute called with null source or intent");
                return;
            }

            // Intent에서 타겟 정보 추출
            var targetUnits = new List<Core.Unit>();
            if (intent.Targets != null)
            {
                foreach (var character in intent.Targets)
                {
                    if (character != null && character.IsAlive)
                        targetUnits.Add(character);
                }
            }

            var targetTiles = intent.TargetTiles ?? new List<TileData>();

            // 시전 VFX는 스킬 실행 직전 1회 (큐 없으면 루트 cast) — 모든 몬스터 스킬 공통
            DiceOrbit.Visuals.VfxService.PlayOn(
                string.IsNullOrEmpty(castCue) ? DiceOrbit.Visuals.VfxTags.Cast : castCue, source);

            Execute(source, targetUnits, targetTiles, 0);
        }

        /// <summary>
        /// 스킬 실행
        /// </summary>
        public virtual void Execute(Core.Unit source, List<Core.Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            
        }

        public virtual void AttackUnits(Unit source, List<Core.Unit> targetUnits, int damage)
        {
            if (source == null || !source.IsAlive) return;
            foreach (var target in targetUnits)
            {
                if (target == null || !target.IsAlive) continue;

                var context = new AttackContext(
                    source,
                    target,
                    SkillName, damage
                );
                context.VfxCue = impactCue;   // 비면 파이프라인이 루트 impact 사용
                CombatPipeline.Instance?.Process(context);

                Debug.Log($"[{SkillName}] {source.name} attacks {target.name} for {damage} damage");
            }
        }

        public virtual void AttackTiles(Unit source, List<TileData> targetTiles, int damage)
        {
            if (source == null || !source.IsAlive) return;

            // 조준된 모든 타일(빈 칸 포함)에 착탄 연출 — 유닛 히트는 아래 파이프라인이 별도 처리
            if (targetTiles != null)
            {
                string tileCue = string.IsNullOrEmpty(impactCue) ? DiceOrbit.Visuals.VfxTags.TileImpact : impactCue;
                foreach (var tile in targetTiles)
                {
                    if (tile == null) continue;
                    DiceOrbit.Visuals.VfxService.PlayOn(tileCue, tile);
                }
            }

            List<Core.Unit> targets = new();
            foreach (var tile in targetTiles)
            {
                if (tile == null) continue;
                tile.GetCharactersOnTile()?.ForEach(c =>
                {
                    if (c != null && c.IsAlive)
                        targets.Add(c);
                });
            }
            // 타겟에 있는 유닛들 중복 제거
            targets = new List<Core.Unit>(new HashSet<Core.Unit>(targets));
            foreach (var character in targets)
            {
                if (character == null || !character.IsAlive) continue;
                var context = new AttackContext(
                    source,
                    character,
                    SkillName, damage
                );
                context.VfxCue = impactCue;   // 비면 파이프라인이 루트 impact 사용
                CombatPipeline.Instance?.Process(context);
                Debug.Log($"[{SkillName}] {source.name} attacks {character.name} on tile for {damage} damage");
            }
        }
    }
}
