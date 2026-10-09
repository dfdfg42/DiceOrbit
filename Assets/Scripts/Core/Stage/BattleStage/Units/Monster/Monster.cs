using UnityEngine;
using DiceOrbit.Data;
using DiceOrbit.Core.Pipeline;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DiceOrbit.UI;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 몬스터 (중앙 구역)
    /// AI + Skills + Managers 통합 구현
    /// </summary>
    public class Monster : Unit<MonsterStats>, UI.IBattleInfoProvider
    {
        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string attackTrigger = "Attack";
        [SerializeField] private string hitTrigger = "Hit";
        [SerializeField] private string deathTrigger = "Death";
        [SerializeField] private string deadBool = "IsDead";
        [SerializeField] private float destroyDelayAfterDeath = 0.35f;

        [Header("Sprite Swap Feedback")]
        [Tooltip("공격 스프라이트를 보여주는 시간(초)")]
        [SerializeField] private float attackSpriteDuration = 0.25f;
        [Tooltip("피격 스프라이트를 보여주는 시간(초)")]
        [SerializeField] private float damageSpriteDuration = 0.2f;
        private Coroutine spriteSwapRoutine;

        [Header("Preset")]
        [SerializeField] private Data.Monsters.MonsterPreset preset;

        [Header("AI")]
        [SerializeField] private Data.MonsterAI.MonsterAI aiPattern; // Inspector 설정 전용 (원본 참조)
        private Data.MonsterAI.MonsterAI runtimeAiPattern; // 실제 실행되는 런타임 인스턴스
        private MonsterSkill nextSkill;
        private AttackIntent nextIntent; // 다음 턴에 사용할 AttackIntent
        public AttackIntent CurrentIntent => nextIntent; // AttackIntent 타입으로 반환
        public MonsterSkill NextSkill => nextSkill;      // 정보 패널 "다음 행동" 표시용

        // 사망 이벤트 (외부 구독용 — 장부 제거는 Die()의 CombatManager.OnMonsterDefeated 직접 호출)
        public event System.Action<Monster> OnDeath;

        // 전투 시작 연출용: 숨기기 직전의 원래 루트 스케일 (CombatIntroDirector가 복원)
        [System.NonSerialized] public Vector3 IntroBaseScale = Vector3.one;

        // MonsterStats 타입으로 반환 (기존 코드 호환성 유지)
        public new MonsterStats Stats => stat;

        // 정체성 색상: 웨이브 시작 시 1회 배정되어 웨이브 내내 고정. 바닥 마커/타일 색 오버레이가 사용.
        [System.NonSerialized] public Color IdentityColor = Color.white;
        [System.NonSerialized] public bool HasIdentityColor = false;

        // 세트(진영) — 프리셋에서 배정. 세트 지원 패시브/스킬이 같은 진영 대상 필터에 사용.
        public Data.Monsters.MonsterFaction Faction { get; private set; } = Data.Monsters.MonsterFaction.None;
        
        protected override void Awake()
        {
            if (stat == null)
            {
                stat = new MonsterStats();
            }
            
            base.Awake();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
            EnsureSpriteScalingIsolation();
            
            EnsureHoverCollider();

            // Systems 초기화
            passives = GetComponent<Systems.Passives.PassiveManager>();
            if (passives == null) passives = gameObject.AddComponent<Systems.Passives.PassiveManager>();
            passives.Initialize(this);

            statusEffects = GetComponent<Systems.Effects.StatusEffectManager>();
            if (statusEffects == null) statusEffects = gameObject.AddComponent<Systems.Effects.StatusEffectManager>();
            statusEffects.Initialize(this);
        }

        private void Start()
        {
            // Preset이 Inspector에 할당되어 있다면 바로 초기화
            //if (preset != null)
            //{
            //    InitializeFromPreset(preset);
            //}
            
            // 첫 턴 의도 선택
            SelectNextIntent();
        }
        
        /// <summary>
        /// 프리셋으로부터 초기화
        /// </summary>
        public void InitializeFromPreset(Data.Monsters.MonsterPreset monsterPreset)
        {
            if (monsterPreset == null)
            {
                Debug.LogError($"[Monster] InitializeFromPreset called with null preset.");
                return;
            }
            preset = monsterPreset;
            Faction = monsterPreset.Faction;

            InitializeStats();
            InitializeVisuals();
            InitializeAI();
            InitializePassives();

            Debug.Log($"Monster '{stat.MonsterName}' initialized from preset.");
        }

        /// <summary>
        /// Stats 초기화
        /// </summary>
        private void InitializeStats()
        {
            if (preset == null) return;
            stat = preset.CreateStats();
        }

        /// <summary>
        /// Visual 초기화 (Sprite, Color)
        /// </summary>
        private void InitializeVisuals()
        {
            if (stat == null) return;

            if (spriteRenderer != null)
            {
                if (stat.MonsterSprite != null)
                {
                    spriteRenderer.sprite = stat.MonsterSprite;
                }
                spriteRenderer.color = stat.SpriteColor;

                // Visual Scale 적용
                if (preset != null)
                {
                    spriteRenderer.transform.localScale = new Vector3(preset.VisualScale, preset.VisualScale, 1f);
                }
            }
        }

        /// <summary>
        /// </summary>
        private void InitializeAI()
        {
            // Preset에서 AI 패턴 가져오기
            if (preset != null && preset.AIPattern != null)
            {
                runtimeAiPattern = preset.AIPattern;
            }
            else
            {
                runtimeAiPattern = null;
            }

            // 런타임 AI 초기화
            if (runtimeAiPattern != null)
            {
                runtimeAiPattern.Initialize(this);
            }
        }

        /// <summary>
        /// Passive 초기화
        /// </summary>
        private void InitializePassives()
        {
            if (preset == null) return;
            foreach (var passive in preset.GetStartingPassives())
            {
                if (passive == null) continue;

                var clonedPassive = passive.Clone();
                // 패시브 초기화
                clonedPassive.Initialize(this);

                // 매니저에 등록
                passives.AddPassive(clonedPassive);
            }
        }

        private void OnDestroy()
        {
            // AttackIndicator에서 Intent 제거
            UI.MonsterAttackIntentManager.Instance?.RemoveAttackIntent(this);
        }
        
        /// <summary>
        /// 다음 턴 행동 결정
        /// </summary>
        public void SelectNextIntent()
        {
            if (runtimeAiPattern != null)
            {
                nextSkill = runtimeAiPattern.GetNextSkill();

                if (nextSkill != null)
                {
                    // MonsterSkill이 타겟 선정 및 Intent 생성
                    nextIntent = nextSkill.GenerateIntent(this);

                    if (nextIntent != null)
                    {
                        Debug.Log($"[Monster] Next Intent Selected: {nextIntent}");

                        // AttackIndicator에 Intent 등록 (시각화는 Battle 시스템에서 Show() 호출)
                        UI.MonsterAttackIntentManager.Instance?.AddAttackIntent(this, nextIntent);
                    }
                    else
                    {
                        Debug.LogWarning($"[Monster] Failed to generate intent.");
                    }
                }
                else
                {
                    Debug.LogWarning($"[Monster] No skill selected by AI.");
                }
            }
        }
        
        /// <summary>
        /// 의도 실행 (Monster Turn)
        /// </summary>
        public void ExecuteIntent()
        {
            if (!IsAlive) return;
            if (nextIntent != null && nextSkill != null)
            {
                // AttackIntent의 유효성 확인 (죽은 타겟 제거)
                nextIntent.RefreshTargets();

                // 몬스터의 의도 실행 1회 = 공격 행동 1회 — 1회성 상태가 다단 공격 전체에 적용된다 (약화는 라운드형이라 별개로 라운드 내내 적용).
                Systems.Effects.AttackActionScope.Begin(this);
                try
                {
                    // 스킬 실행 (Intent에 저장된 타겟 사용)
                    ExecuteSkillWithIntent(nextSkill.skillData, nextIntent);
                }
                finally
                {
                    Systems.Effects.AttackActionScope.End();
                }
            }
            else
            {
                Debug.Log($"[Monster] Idling (No Skill)");
            }

            // 다음 의도 준비
            SelectNextIntent();
        }

        /// <summary>
        /// 이번 턴에 예약된 행동을 취소한다. 의도가 없으면 ExecuteIntent가 자동으로 아무것도 하지 않는다.
        /// 기절 등 "행동 불가" 효과가 소유자 턴 시작 시 호출한다(효과별 하드코딩 대신 상태이상이 주도).
        /// </summary>
        public void CancelIntent()
        {
            nextSkill = null;
            nextIntent = null;
            UI.MonsterAttackIntentManager.Instance?.RemoveAttackIntent(this);
        }

        // === 행동 연출(순차 실행 연동) ===

        private bool actingHighlighted;
        private Vector3 actingBaseScale;

        /// <summary>머리 위(월드 좌표) — 행동 말풍선 앵커. 스프라이트 상단 + 여백.</summary>
        public Vector3 GetHeadTopWorld()
        {
            if (spriteRenderer != null && spriteRenderer.sprite != null)
                return new Vector3(transform.position.x, spriteRenderer.bounds.max.y + 0.6f, transform.position.z);
            return transform.position + Vector3.up * 1.8f;
        }

        /// <summary>행동 중 강조(살짝 확대). on=false면 원래 스케일로 복원.</summary>
        public void SetActingHighlight(bool on)
        {
            if (spriteRenderer == null) return;
            var t = spriteRenderer.transform;
            if (on)
            {
                if (!actingHighlighted)
                {
                    actingBaseScale = t.localScale;
                    actingHighlighted = true;
                }
                t.localScale = actingBaseScale * 1.12f;
            }
            else if (actingHighlighted)
            {
                t.localScale = actingBaseScale;
                actingHighlighted = false;
            }
        }

        /// <summary>
        /// 턴 시작 (Pipeline TurnStart)
        /// </summary>
        public override void OnStartTurn()
        {
            Debug.Log($"[Monster] {stat?.MonsterName} Start Turn");
            base.OnStartTurn();
        }
        
        private void ExecuteSkillWithIntent(SkillData skill, AttackIntent intent)
        {
            Debug.Log($"[Monster] Executing Skill: {skill.SkillName}");
            PlayAttackVisual();

            // SkillData의 Execute 메서드에 위임
            skill.ExecuteSkillWithIntent(this, intent);
        }

        // === Damage & Death ===

        /// <summary>
        /// 데미지 처리
        /// </summary>
        public override int TakeDamage(int damage)
        {
            if (!IsAlive) return 0;
            int result=base.TakeDamage(damage);
            if (!IsAlive)
            {
                HandleDeath();
            }
            else if (result > 0)
            {
                PlayDamageVisual();
            }
            return result;
        }

        /// <summary>직접 체력 손실(중독 등)도 일반 피해와 같은 사망/피격 후처리를 거친다.</summary>
        public override int TakeDirectDamage(int damage)
        {
            if (!IsAlive) return 0;
            int result = base.TakeDirectDamage(damage);
            if (!IsAlive)
            {
                HandleDeath();
            }
            else if (result > 0)
            {
                PlayDamageVisual();
            }
            return result;
        }

        protected override void HandleDeath()
        {
            base.HandleDeath();
            PlayDeathVisual();

            // 사망 효과 실행 (OnDeathEffects)
            if (preset != null && preset.OnDeathEffects != null)
            {
                foreach (var deathEffect in preset.OnDeathEffects)
                {
                    if (deathEffect != null)
                    {
                        Debug.Log($"[Monster] Executing death effect: {deathEffect.EffectName}");
                        deathEffect.Execute(this);
                    }
                }
            }

            // 사망 이벤트 발생 (외부 구독자 통지)
            OnDeath?.Invoke(this);

            // AttackIndicator에서 Intent 제거
            UI.MonsterAttackIntentManager.Instance?.RemoveAttackIntent(this);

            var combatManager = CombatManager.Instance;
            if (combatManager != null) combatManager.OnMonsterDefeated(this);
            DiceOrbit.Visuals.HitDirector.MonsterDeathSlice(this);   // 참격과 함께 두 조각으로 갈라진다 (연출은 이 오브젝트와 따로 산다)
            StartCoroutine(CoDestroyAfterDeath());
        }

        /// <summary>정보 패널용 구조화 데이터 (빌드 로직은 UnitInfoBuilder로 단일화).</summary>
        public UI.UnitInfoData GetBattleInfo() => UI.UnitInfoBuilder.Build(this);

        private void EnsureHoverCollider()
        {
            var existing = GetComponent<Collider>();
            if (existing != null) return;

            var box = gameObject.AddComponent<BoxCollider>();
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                var bounds = spriteRenderer.sprite.bounds;
                box.size = new Vector3(Mathf.Max(0.5f, bounds.size.x), Mathf.Max(0.5f, bounds.size.y), 1.5f);
                box.center = new Vector3(bounds.center.x, bounds.center.y, 0f);
            }
            else
            {
                box.size = new Vector3(1.2f, 1.2f, 1.5f);
            }
        }

        private void PlayAttackVisual()
        {
            SetTriggerSafe(attackTrigger);
            SwapSpriteTemporarily(stat?.AttackSprite, attackSpriteDuration);
        }

        private void PlayDamageVisual()
        {
            SetTriggerSafe(hitTrigger);
            SwapSpriteTemporarily(stat?.DamageSprite, damageSpriteDuration);
        }

        private void PlayDeathVisual()
        {
            SetBoolSafe(deadBool, true);
            SetTriggerSafe(deathTrigger);

            // 사망 시 진행 중인 스프라이트 스왑 중단 (idle로 되돌리지 않음)
            if (spriteSwapRoutine != null)
            {
                StopCoroutine(spriteSwapRoutine);
                spriteSwapRoutine = null;
            }
        }

        /// <summary>
        /// 공격/피격 스프라이트로 잠시 바꿨다가 기본(MonsterSprite)으로 복귀한다.
        /// AttackSprite/DamageSprite가 없으면 아무것도 하지 않는다.
        /// </summary>
        private void SwapSpriteTemporarily(Sprite temp, float duration)
        {
            if (spriteRenderer == null || temp == null || duration <= 0f) return;

            if (spriteSwapRoutine != null) StopCoroutine(spriteSwapRoutine);
            spriteSwapRoutine = StartCoroutine(CoSwapSprite(temp, duration));
        }

        private System.Collections.IEnumerator CoSwapSprite(Sprite temp, float duration)
        {
            spriteRenderer.sprite = temp;
            yield return new WaitForSeconds(duration);

            // 살아있으면 기본 스프라이트로 복귀
            if (spriteRenderer != null && stat != null && stat.MonsterSprite != null)
                spriteRenderer.sprite = stat.MonsterSprite;

            spriteSwapRoutine = null;
        }

        private void SetTriggerSafe(string trigger)
        {
            if (animator == null || string.IsNullOrWhiteSpace(trigger)) return;
            animator.SetTrigger(trigger);
        }

        private void SetBoolSafe(string param, bool value)
        {
            if (animator == null || string.IsNullOrWhiteSpace(param)) return;
            animator.SetBool(param, value);
        }

        private System.Collections.IEnumerator CoDestroyAfterDeath()
        {
            yield return new WaitForSeconds(Mathf.Max(0f, destroyDelayAfterDeath));
            Destroy(gameObject);
        }

        private void EnsureSpriteScalingIsolation()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
                if (spriteRenderer == null) return;
            }

            // If SpriteRenderer is on root, scaling it also scales child UI.
            // Clone renderer to a child visual root so only sprite scales.
            if (spriteRenderer.transform != transform) return;

            var original = spriteRenderer;
            var visualRoot = new GameObject("MonsterVisualRoot").transform;
            visualRoot.SetParent(transform, false);
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = Vector3.one;

            var cloned = visualRoot.gameObject.AddComponent<SpriteRenderer>();
            cloned.sprite = original.sprite;
            cloned.color = original.color;
            cloned.material = original.sharedMaterial;
            cloned.sortingLayerID = original.sortingLayerID;
            cloned.sortingOrder = original.sortingOrder;
            cloned.flipX = original.flipX;
            cloned.flipY = original.flipY;

            original.enabled = false;
            spriteRenderer = cloned;
        }
    }
}
