using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Core.Zones;
using DiceOrbit.Data;
using DiceOrbit.Visuals;

namespace DiceOrbit.Data.Skills
{
    public enum CharacterSkillTargetType
    {
        None,        // 타겟 없음, 즉시 실행
        OneEnemy,    // 단일 적
        AllEnemies,  // 모든 적
        OneAlly,     // 단일 아군
        AllAllies,   // 모든 아군
        OneTile,     // 단일 타일
        AllTiles,    // 모든 타일
        MultiEnemy,  // N명 적 순차 선택
        MultiAlly,   // N명 아군 순차 선택
        MultiTile,   // N개 타일 순차 선택
    }

    /// <summary>
    /// 캐릭터의 '강화 공격'. 매 턴 나가는 공격 1회가, 배정한 주사위가 requirement를 만족하면
    /// 기본공격 대신 이 형태로 나간다(2026-08-21 개편). 클릭도 타게팅도 없다 —
    /// 대상은 캐릭터가 선 구역과 지나온 경로에서 유도된다(ResolveTargets).
    /// 피해는 주사위 눈이 아니라 캐릭터 공격력을 기준으로 한다.
    /// </summary>
    [System.Serializable]
    public abstract class CharacterActiveSkill
    {
        [Header("Info")]
        [SerializeField] protected string skillName = "";
        [SerializeField, TextArea(2, 4)] protected string description = "";
        [SerializeField] public Sprite icon;

        [Header("Requirement")]
        [SerializeField] public DiceRequirement requirement = new DiceRequirement();

        [Header("Targeting")]
        [SerializeField] public CharacterSkillTargetType targetType = CharacterSkillTargetType.OneEnemy;
        [SerializeField] public TilePreviewStyle previewStyle = TilePreviewStyle.Neutral;
        [Tooltip("MultiEnemy/MultiAlly/MultiTile 타입일 때 선택할 개수")]
        [SerializeField] public int targetCount = 1;

        [Header("VFX")]
        [Tooltip("큐 태그. 비우면 루트(cast/impact) 폴백")]
        [SerializeField] protected string castCue = "";
        [SerializeField] protected string impactCue = "";

        [Header("Projectile (선택 — 지정 시 공격이 포물선 발사체로 날아가 도착 시 피해)")]
        [Tooltip("비행 발사체 프리팹. 비우면 즉시 피해(기존 동작)")]
        [SerializeField] protected GameObject projectilePrefab;
        [Tooltip("발사체 비행 시간(초)")]
        [SerializeField] protected float projectileDuration = 0.35f;
        [Tooltip("포물선 최고 높이")]
        [SerializeField] protected float projectileArcHeight = 1.2f;

        public string SkillName   => skillName;
        public string Description => description;

        public CharacterSkillTargetType TargetType => targetType;
        public TilePreviewStyle PreviewStyle       => previewStyle;
        public int              TargetCount        => Mathf.Max(1, targetCount);

        public bool CanUse(int diceValue) => requirement.CanUse(diceValue);

        // 기본공격도 같은 무기 연출로 나가도록 발사체 설정을 공개한다 —
        // 주사위 눈에 따라 총알이 나갔다 안 나갔다 하면 같은 캐릭터로 보이지 않는다.
        public GameObject ProjectilePrefab   => projectilePrefab;
        public float      ProjectileDuration => projectileDuration;
        public float      ProjectileArcHeight => projectileArcHeight;

        /// <summary>
        /// 표시용 동적 설명. 현재 유효 수치(배율/조건)에서 매번 생성하므로
        /// 모디파이어 등이 값을 바꾸면 자동으로 반영된다. 기본은 정적 Description.
        /// </summary>
        public virtual string GetDynamicDescription() => Description;

        /// <summary>
        /// 소유자 문맥이 있는 동적 설명 (전투 중 정보 패널용). 콤보 스킬은 '현재 단계만' 보여주도록
        /// 오버라이드한다. 기본은 문맥 없는 버전과 동일 — 모집 화면 등 전투 밖에서는 그쪽을 쓴다.
        /// </summary>
        public virtual string GetDynamicDescription(Character source) => GetDynamicDescription();

        /// <summary>캐릭터 선택창에서 사용하는 짧은 설명. 기본값은 일반 동적 설명과 같다.</summary>
        public virtual string GetSelectionSummary() => GetDynamicDescription();

        /// <summary>주사위 요구 조건을 한국어 문구로 변환. (정보 패널 등 UI 표시용으로도 사용)</summary>
        public string FormatDiceCondition()
        {
            if (requirement == null) return string.Empty;

            if (requirement.ExactDiceValue.HasValue)
                return $"주사위 눈 {requirement.ExactDiceValue.Value}";

            switch (requirement.Pattern)
            {
                case DicePattern.Odd:  return "홀수 눈";
                case DicePattern.Even: return "짝수 눈";
                case DicePattern.High: return "주사위 눈 4 이상";
                case DicePattern.Low:  return "주사위 눈 3 이하";
            }

            int min = requirement.MinDiceValue;
            var max = requirement.MaxDiceValue;
            if (max.HasValue && min > 1) return $"주사위 눈 {min}~{max.Value}";
            if (max.HasValue)            return $"주사위 눈 {max.Value} 이하";
            if (min > 1)                 return $"주사위 눈 {min} 이상";
            return "모든 주사위";
        }

        public virtual CharacterActiveSkill Clone() => (CharacterActiveSkill)MemberwiseClone();

        /// <summary>
        /// 이 스킬에 맞는 캐싱용 컨텍스트를 생성하여 반환합니다.
        /// 파생 클래스에서 오버라이드하여 전용 컨텍스트를 생성할 수 있습니다.
        /// </summary>
        public virtual CharacterModfierContext GenerateContext(Character source, ActiveSkillSlot ability)
        {
            return new CharacterModfierContext(source, this);
        }

        public abstract int    CalculateRawDamage(Character source, ActiveSkillSlot ability, int diceValue);
        public abstract string BuildPreview(Character source, ActiveSkillSlot ability, int diceValue);

        /// <summary>
        /// 이 공격이 누구를 때리는지 한국어로. 타게팅이 위치에서 유도되므로 옛 대상 타입 enum이 아니라
        /// 각 공격이 스스로 설명한다 — 정보 패널의 '유효 대상' 자리에 쓰인다.
        /// </summary>
        public virtual string GetTargetLabel() => "자기 구역";

        /// <summary>
        /// 이 공격이 때릴 대상을 위치에서 유도한다. 기본은 자기가 선 구역의 주인 하나.
        /// passedZones는 이번 이동에서 지나온 구역 번호들(출발 구역 포함, 순서대로) —
        /// 경로형 강화 공격이 이것을 읽는다.
        /// </summary>
        public virtual List<Unit> ResolveTargets(Character source, IReadOnlyList<int> passedZones)
        {
            var result = new List<Unit>();
            var zones = CombatZoneManager.Instance;
            if (zones == null || source == null) return result;

            var owner = zones.GetOwner(zones.GetZoneOf(source));
            if (owner != null) result.Add(owner);
            return result;
        }

        /// <summary>시전 연출 1회. 대상 수와 무관하게 공격 시작에 한 번만 재생한다.</summary>
        public void PlayCast(Character source)
        {
            if (source == null) return;
            VfxService.PlayOn(string.IsNullOrEmpty(castCue) ? VfxTags.Cast : castCue, source);
        }

        /// <summary>
        /// 대상 하나에게 이 공격을 적용한다. 발사체가 지정돼 있으면 포물선으로 날아가
        /// '도착 시' 피해와 피격 VFX가 들어간다.
        /// 여러 대상을 순서대로 때리려면 호출자가 이 메서드를 한 번씩 부르며 사이에 간격을 둔다
        /// (팝업이 겹치지 않게 — AutoAttackSystem이 그 역할을 한다).
        /// </summary>
        public virtual void ApplyToTarget(Character source, ActiveSkillSlot ability, Unit target, int diceValue)
        {
            if (source == null || target == null || !target.IsAlive) return;

            int rawDamage = CalculateRawDamage(source, ability, diceValue);
            if (rawDamage <= 0) return;

            var context = new AttackContext(source, target, skillName, rawDamage);
            context.VfxCue = impactCue;   // 비면 파이프라인이 루트 impact 사용
            HitDirector.ReportAttackLaunched(source, target.transform.position);   // 발사 반동 + 휘두르는 소리

            if (projectilePrefab != null)
            {
                Vector3 from = source.transform.position + Vector3.up * 0.5f;
                Vector3 to   = target.transform.position + Vector3.up * 0.5f;
                var ctx = context;   // 클로저 캡처(루프 변수 방지)
                ProjectileService.Launch(projectilePrefab, from, to, projectileDuration, projectileArcHeight,
                    () => CombatPipeline.Instance?.Process(ctx));
            }
            else
            {
                CombatPipeline.Instance?.Process(context);
            }
        }

        public virtual bool Execute(
            Character source, ActiveSkillSlot ability,
            List<Unit> targets, List<TileData> targetTiles, int diceValue)
        {
            if (source == null || ability == null) return false;

            if (targets != null && targets.Count > 0)
            {
                PlayCast(source);
                foreach (var target in targets)
                    ApplyToTarget(source, ability, target, diceValue);
            }

            OnAfterResolved(source, ability);
            return true;
        }

        public virtual void OnAfterResolved(Character source, ActiveSkillSlot ability) { }
    }
}
