using System.Collections;
using System.Collections.Generic;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Core.Zones;
using DiceOrbit.Visuals;
using UnityEngine;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 자동 공격 — 이동해야만 공격이다. 주사위를 배정해 이동을 마친 캐릭터가 도착 구역의 몬스터를
    /// 자동으로 한 번 때린다. 제자리로 턴을 넘긴 캐릭터는 공격하지 않는다(2026-08-23 결정) —
    /// 이동 포기 자체가 선택이고 그 대가가 이번 턴의 딜(그리고 콤보)이다.
    ///
    /// 배정한 주사위가 게이트(전사 4+/도적 3↓/마법 홀/연금 짝)에 맞으면 현재 콤보 단계(1→2→3)의
    /// 강화 공격이 기본공격을 '대체'하고, 조건 실패면 콤보가 끊기며 기본공격이 나간다(2026-08-28 콤보 개편).
    /// 발동 지점은 이동 직후(MoveThenAttackRoutine) 한 곳이며, 중복 발동은 턴별 기록으로 막는다.
    /// 모든 공격(기본/강화)은 AttackActionScope로 감싼 하나의 '공격 행동'이다.
    /// </summary>
    public class AutoAttackSystem : MonoBehaviour
    {
        public static AutoAttackSystem Instance { get; private set; }

        [Header("연출")]
        [Tooltip("캐릭터의 공격이 끝난 뒤 다음 캐릭터로 넘어가기까지의 간격(초).")]
        [SerializeField] private float attackInterval = 0.25f;
        [Tooltip("한 대상을 때린 뒤 다음 대상까지의 간격(초). 발사체 비행 시간에 이 값이 더해진다 — 피해 팝업이 겹치지 않도록.")]
        [SerializeField] private float hitGap = 0.15f;
        [Tooltip("피해 팝업·로그에 표시할 이름")]
        [SerializeField] private string attackName = "기본 공격";

        private readonly HashSet<Character> _resolvedThisTurn = new HashSet<Character>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static AutoAttackSystem EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<AutoAttackSystem>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }
            return new GameObject("[AutoAttackSystem]").AddComponent<AutoAttackSystem>();
        }

        /// <summary>플레이어 턴 시작 시 호출 — 이번 턴 발동 기록을 비운다.</summary>
        public void ResetTurn() => _resolvedThisTurn.Clear();

        /// <summary>이번 턴에 이 캐릭터의 공격이 이미 처리됐는가 (턴 종료 시 미이동 콤보 끊기 판정용).</summary>
        public bool HasResolved(Character character) => character != null && _resolvedThisTurn.Contains(character);

        /// <summary>이동을 끝까지 수행한 뒤 도착 구역의 몬스터를 자동 공격한다.</summary>
        public IEnumerator MoveThenAttackRoutine(Character character, int diceValue)
        {
            var orbit = GameManager.Instance != null ? GameManager.Instance.GetOrbitManager() : null;
            if (orbit == null)
            {
                Debug.LogError("[AutoAttack] OrbitManager가 없어 이동을 처리할 수 없다.");
                yield break;
            }

            // 지나갈 구역은 이동 전에 기록한다 — 경로형 강화 공격(전사 돌파)이 읽는다.
            var passedZones = CollectPassedZones(character, diceValue);

            yield return orbit.MoveRoutine(character, diceValue);
            yield return ResolveRoutine(character, diceValue, passedZones);
        }

        /// <summary>
        /// 이번 이동에서 지나갈 구역 번호들(출발 구역 포함, 중복 없이 순서대로). 이동 '전' 위치에서 불러야 한다.
        /// 경로는 이동 루틴과 같은 계산(OrbitManager.BuildMovePath)을 쓴다 — 버프/디버프로 실제 이동이 달라지므로.
        /// </summary>
        public static List<int> CollectPassedZones(Character character, int diceValue)
        {
            var result = new List<int>();
            var zones = Zones.CombatZoneManager.Instance;
            if (zones == null || character == null || character.CurrentTile == null) return result;

            int startZone = zones.GetZoneOfTile(character.CurrentTile);
            if (startZone >= 0) result.Add(startZone);

            var path = new List<Data.TileData>();
            OrbitManager.BuildMovePath(character, diceValue, path);
            foreach (var tile in path)
            {
                int zone = zones.GetZoneOfTile(tile);
                if (zone >= 0 && !result.Contains(zone)) result.Add(zone);
            }
            return result;
        }

        /// <summary>
        /// 이 캐릭터가 '지금 선 자리'에서 diceValue로 공격하면 무엇이 나가고 누구를 때리는가 (상태를 바꾸지 않는다).
        /// 실행 루틴(ResolveRoutine)과 행동 예고(ActionForecaster)가 같이 쓴다 — 판정이 두 벌로 갈라지지 않게.
        /// diceValue가 게이트를 만족하면 현재 콤보 단계의 강화 공격, 아니면 기본공격.
        /// </summary>
        public AttackPlan PlanAttack(Character character, int diceValue, IReadOnlyList<int> passedZones)
        {
            var plan = new AttackPlan { Source = character };

            // 이동 시작 구역 vs 도착 구역 (AttackActionInfo.CrossedZone — 현재 읽는 모디파이어 없음, 이동 구역 변화는 IMoveFinishedListener로 통지)
            var zoneMgr = CombatZoneManager.Instance;
            int endZone = zoneMgr != null ? zoneMgr.GetZoneOf(character) : -1;
            int startZone = passedZones != null && passedZones.Count > 0 ? passedZones[0] : endZone;
            plan.CrossedZone = startZone >= 0 && endZone >= 0 && startZone != endZone;

            int currentStage = Combo.ComboSystem.Instance != null ? Combo.ComboSystem.Instance.PeekStage(character) : 0;
            var empowered = FindEmpoweredAttack(character, diceValue);

            if (empowered == null)
            {
                // 게이트 실패 — 기본공격. 쌓인 콤보가 있었다면 끊긴다 (스펙 §1 규칙 5)
                plan.Kind = AttackPlanKind.Basic;
                plan.HadCombo = currentStage > 0;
                plan.Targets.AddRange(CollectTargets(character));
                return plan;
            }

            if (!(empowered.RuntimeInstance is Data.Skills.ComboActiveSkill comboSkill))
            {
                // 콤보형이 아닌 액티브가 남아 있으면 설계 위반 — 기본공격으로 처리 (실행 루틴이 에러로 드러낸다)
                plan.Kind = AttackPlanKind.Basic;
                plan.EmpoweredIsNotCombo = true;
                plan.Targets.AddRange(CollectTargets(character));
                return plan;
            }

            plan.ComboSkill = comboSkill;
            plan.Stage = currentStage;

            var targets = comboSkill.ResolveStageTargets(character, currentStage, passedZones);
            if (targets == null || targets.Count == 0)
            {
                // 게이트는 맞았지만 강화공격이 실제로 발생하지 않음 → 콤보 초기화 (스펙 §1 규칙 7)
                plan.Kind = AttackPlanKind.ComboNoTarget;
                return plan;
            }

            plan.Kind = AttackPlanKind.Combo;
            plan.Targets.AddRange(targets);
            return plan;
        }

        /// <summary>기본공격의 표시 이름 (실행과 예고가 공유).</summary>
        public string BasicAttackName => attackName;

        /// <summary>
        /// 한 캐릭터의 이동 후 처리 1회. 이번 턴에 이미 처리됐으면 아무 일도 하지 않는다.
        /// 순서: 주사위 사용 통지 → 이동 완료 통지 → 공격(강화/기본) → 행동 종료 통지(공격이 없어도).
        /// diceValue가 게이트를 만족하면 현재 콤보 단계의 강화 공격이, 아니면 콤보가 끊기며 기본공격이 나간다.
        /// (2026-08-28 콤보 개편 — 단계 관리: ComboSystem / 단계 정의: ComboActiveSkill / 판정: PlanAttack)
        /// </summary>
        public IEnumerator ResolveRoutine(Character character, int diceValue, IReadOnlyList<int> passedZones)
        {
            if (character == null || !character.IsAlive) yield break;
            if (_resolvedThisTurn.Contains(character)) yield break;
            _resolvedThisTurn.Add(character);

            // 주사위 '사용' 사건 — 공격 성사 여부와 무관하게 통지 (저속 방호 등 공용 모디파이어).
            NotifyDiceUsed(character, diceValue);

            // 이동 완료 사건 — 출발 구역은 이동 전에 기록한 경로의 첫 구역, 도착 구역은 지금 선 구역 (기동 장갑).
            var zoneMgr = CombatZoneManager.Instance;
            int toZone = zoneMgr != null ? zoneMgr.GetZoneOf(character) : -1;
            int fromZone = passedZones != null && passedZones.Count > 0 ? passedZones[0] : toZone;
            NotifyMoveFinished(character, fromZone, toZone);

            yield return AttackRoutine(character, diceValue, passedZones);

            // 이동+공격 처리 전체 종료 — 공격이 없었어도 통지 (구조대).
            NotifyActionFinished(character);
        }

        /// <summary>공격 처리 — 계획(PlanAttack)대로 강화 공격 또는 기본공격을 실행한다.</summary>
        private IEnumerator AttackRoutine(Character character, int diceValue, IReadOnlyList<int> passedZones)
        {
            var combo = Combo.ComboSystem.EnsureInstance();
            var plan = PlanAttack(character, diceValue, passedZones);

            if (plan.Kind == AttackPlanKind.Basic)
            {
                if (plan.EmpoweredIsNotCombo)
                {
                    Debug.LogError($"[AutoAttack] '{character.Stats?.CharacterName}'의 강화 공격이 ComboActiveSkill이 아니다 — 기본공격으로 대체. 프리셋을 확인할 것.");
                }
                else
                {
                    // 강화 조건 실패 판정 직후 — 기본공격이 실제로 나가는지와 무관 (예비 충전).
                    NotifyComboGateFailed(character);
                    combo.ResetCombo(character, Combo.ComboOutcome.BrokenByDice);   // 게이트 실패 — 콤보 즉시 초기화
                }

                yield return BasicAttackRoutine(character, diceValue, plan);
                yield break;
            }

            if (plan.Kind == AttackPlanKind.ComboNoTarget)
            {
                combo.ResetCombo(character, Combo.ComboOutcome.BrokenByNoTarget);
                yield break;
            }

            var comboSkill = plan.ComboSkill;
            int stage = plan.Stage;
            var targets = plan.Targets;
            var attacked = new List<Unit>();

            character.OnSkillExecutionStarted();
            comboSkill.PlayCast(character);

            // 한 단계의 모든 타격(다단×다중 대상)은 하나의 '공격 행동' — 촉매/고양/감전/충전이 전체에 적용된다.
            Systems.Effects.AttackActionScope.Begin(character, plan.BuildActionInfo(diceValue));

            float perHit = ResolveHitDelay(comboSkill);
            int hits = plan.Hits;
            for (int h = 0; h < hits; h++)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    var target = targets[i];
                    if (target == null || !target.IsAlive) continue;

                    if (!attacked.Contains(target)) attacked.Add(target);
                    comboSkill.ApplyStageHit(character, target, stage, h);
                    if (perHit > 0f) yield return new WaitForSeconds(perHit);
                }
            }

            Systems.Effects.AttackActionScope.End();

            // 처치 수는 마무리 효과(중독 2배 등) 전에 센다 — 디버프 피해 처치는 사냥에 들어가지 않는다.
            int kills = CountKills(attacked);

            // 마무리 효과(방어도 부여·중독 2배·고양 살포)는 행동 스코프 밖 — 다음 행동을 위한 상태다.
            comboSkill.OnStageCompleted(character, targets, stage);

            // 강화 공격을 마친 뒤 충전 1 소비 — 직업 고유(마나흡수, C)의 "소비 후 획득"이 이 뒤 통지에서 이어진다.
            Systems.Effects.ChargeStatus.ConsumeOne(character);

            NotifyAttackActionFinished(character, new Data.Modifiers.Common.AttackActionResult(stage, attacked, kills));
            combo.ReportExecuted(character);

            if (attackInterval > 0f) yield return new WaitForSeconds(attackInterval);
        }

        /// <summary>
        /// 기본공격 1회 (단일 대상). 기본공격도 하나의 공격 행동이라 촉매가 여기에도 적용·소모된다.
        /// </summary>
        private IEnumerator BasicAttackRoutine(Character character, int diceValue, AttackPlan plan)
        {
            var targets = plan.Targets;
            if (targets.Count == 0) yield break;

            character.OnSkillExecutionStarted();

            var weapon = FindProjectileSource(character);
            var attacked = new List<Unit>();
            Systems.Effects.AttackActionScope.Begin(character, plan.BuildActionInfo(diceValue));

            float perHit = ResolveHitDelay(weapon);
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null || !target.IsAlive) continue;

                if (!attacked.Contains(target)) attacked.Add(target);
                LaunchBasicHit(character, target, weapon);
                if (perHit > 0f) yield return new WaitForSeconds(perHit);
            }

            Systems.Effects.AttackActionScope.End();

            int kills = CountKills(attacked);
            NotifyAttackActionFinished(character, new Data.Modifiers.Common.AttackActionResult(-1, attacked, kills));

            if (attackInterval > 0f) yield return new WaitForSeconds(attackInterval);
        }

        /// <summary>
        /// 행동 중 타격을 시도한 대상 중 지금 죽어 있는(또는 이미 파괴된) 수 — 시도 시점엔 모두 살아 있었다.
        /// 스코프 종료 직후에 불러야 마무리 효과·턴 사건 피해 처치가 섞이지 않는다.
        /// </summary>
        private static int CountKills(List<Unit> attacked)
        {
            int kills = 0;
            foreach (var target in attacked)
                if (target == null || !target.IsAlive) kills++;
            return kills;
        }

        /// <summary>기본공격 1타. 발사체가 있으면 날아가 도착할 때 피해가 들어간다.</summary>
        private void LaunchBasicHit(Character character, Unit target, Data.Skills.CharacterActiveSkill weapon)
        {
            var context = new AttackContext(character, target, attackName, character.Stats.EffectiveAttack);
            HitDirector.ReportAttackLaunched(character, target.transform.position);   // 발사 반동 + 휘두르는 소리

            if (weapon != null && weapon.ProjectilePrefab != null)
            {
                Vector3 from = character.transform.position + Vector3.up * 0.5f;
                Vector3 to   = target.transform.position + Vector3.up * 0.5f;
                ProjectileService.Launch(weapon.ProjectilePrefab, from, to,
                    weapon.ProjectileDuration, weapon.ProjectileArcHeight,
                    () => CombatPipeline.Instance?.Process(context));
            }
            else
            {
                CombatPipeline.Instance?.Process(context);
            }
        }

        /// <summary>한 타 사이의 대기 시간. 발사체가 도착한 뒤 간격이 생기도록 비행 시간을 더한다.</summary>
        private float ResolveHitDelay(Data.Skills.CharacterActiveSkill weapon)
        {
            float flight = weapon != null && weapon.ProjectilePrefab != null ? weapon.ProjectileDuration : 0f;
            return flight + Mathf.Max(0f, hitGap);
        }

        /// <summary>이 캐릭터의 무기 연출(발사체)을 들고 있는 액티브. 없으면 null(즉시 피해).</summary>
        private static Data.Skills.CharacterActiveSkill FindProjectileSource(Character character)
        {
            var abilities = character.Stats != null ? character.Stats.ActiveAbilities : null;
            if (abilities == null) return null;

            foreach (var slot in abilities)
            {
                var skill = slot != null ? (slot.RuntimeInstance ?? slot.BaseSkill) : null;
                if (skill != null && skill.ProjectilePrefab != null) return skill;
            }
            return null;
        }

        /// <summary>배정한 주사위가 조건을 만족하는 액티브. 없으면 null(기본공격으로 나간다).</summary>
        private static Data.Skills.ActiveSkillSlot FindEmpoweredAttack(Character character, int diceValue)
        {
            if (diceValue <= 0) return null;
            var abilities = character.Stats != null ? character.Stats.ActiveAbilities : null;
            if (abilities == null) return null;

            foreach (var slot in abilities)
                if (slot != null && slot.RuntimeInstance != null && slot.CanUse(diceValue)) return slot;

            return null;
        }

        /// <summary>
        /// 이 캐릭터가 이번 공격으로 때릴 대상들.
        /// 기본은 자기가 선 구역의 주인 하나. 집합형 패시브(IAttackZoneProvider — 마력 회로)가 있으면
        /// 그 구역들 중 가장 가까운 주인을, 거리형(IZoneReachProvider)이 있으면 그 거리 안에서 찾는다.
        /// 탐색 범위만 넓어지고 표적 수는 여전히 하나다.
        /// </summary>
        private List<Unit> CollectTargets(Character character)
        {
            var result = new List<Unit>();

            var zones = CombatZoneManager.Instance;
            if (zones == null)
            {
                Debug.LogError("[AutoAttack] CombatZoneManager가 없어 공격 대상을 정할 수 없다.");
                return result;
            }

            int zone = zones.GetZoneOf(character);
            if (zone < 0) return result;   // 아직 타일에 배치되지 않음

            // 집합형(마력 회로) 우선 — 연결된 구역들 중 자기 구역에서 원형 거리 최소인 주인.
            var zoneProvider = FindAttackZoneProvider(character);
            if (zoneProvider != null)
            {
                var nearest = FindNearestOwnerInZones(zones, zone, zoneProvider.GetAttackableZones(character));
                if (nearest != null) result.Add(nearest);
                return result;
            }

            var owner = zones.FindNearestOwner(zone, ResolveZoneReach(character));
            if (owner != null) result.Add(owner);   // 사거리 안에 주인이 없으면 이번 턴 공격 없음

            return result;
        }

        /// <summary>구역 집합에서 기준 구역과의 원형 거리가 가장 짧은 주인 하나.</summary>
        private static Unit FindNearestOwnerInZones(CombatZoneManager zones, int fromZone, List<int> candidateZones)
        {
            if (candidateZones == null || candidateZones.Count == 0) return null;

            Unit best = null;
            int bestDistance = int.MaxValue;
            int n = zones.ZoneCount;

            foreach (var zone in candidateZones)
            {
                var owner = zones.GetOwner(zone);
                if (owner == null) continue;

                int diff = Mathf.Abs(((zone - fromZone) % n + n) % n);
                int distance = Mathf.Min(diff, n - diff);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = owner;
                }
            }
            return best;
        }

        /// <summary>
        /// 캐릭터의 장착 모디파이어 중 리스너 T를 구현한 것에 통지한다.
        /// 통지 중 장착 목록이 바뀌어도 안전하도록 복사본을 돈다.
        /// </summary>
        private static void NotifyModifiers<T>(Character character, System.Action<T> invoke) where T : class
        {
            var mods = character != null && character.Stats != null && character.Stats.Modifiers != null
                ? character.Stats.Modifiers.Modifiers
                : null;
            if (mods == null || mods.Count == 0) return;

            var snapshot = new List<Data.Modifiers.CharacterModifier>(mods);
            foreach (var mod in snapshot)
                if (mod is T listener) invoke(listener);
        }

        /// <summary>주사위 사용 사건 (저속 방호 등).</summary>
        private static void NotifyDiceUsed(Character character, int diceValue)
            => NotifyModifiers<Data.Modifiers.Common.IDiceUseListener>(character, l => l.OnDiceUsed(character, diceValue));

        /// <summary>이동 완료 사건 (기동 장갑).</summary>
        private static void NotifyMoveFinished(Character character, int fromZone, int toZone)
            => NotifyModifiers<Data.Modifiers.Common.IMoveFinishedListener>(character, l => l.OnMoveFinished(character, fromZone, toZone));

        /// <summary>강화 조건 실패 사건 (예비 충전).</summary>
        private static void NotifyComboGateFailed(Character character)
            => NotifyModifiers<Data.Modifiers.Common.IComboGateFailedListener>(character, l => l.OnComboGateFailed(character));

        /// <summary>공격 행동 종료 사건 (대단원·독 바른 무기·공방 일체·사냥).</summary>
        private static void NotifyAttackActionFinished(Character character, Data.Modifiers.Common.AttackActionResult result)
            => NotifyModifiers<Data.Modifiers.Common.IAttackActionFinishedListener>(character, l => l.OnAttackActionFinished(character, result));

        /// <summary>행동(이동+공격) 종료 사건 (구조대).</summary>
        private static void NotifyActionFinished(Character character)
            => NotifyModifiers<Data.Modifiers.Common.IActionFinishedListener>(character, l => l.OnActionFinished(character));

        /// <summary>캐릭터의 집합형 공격 구역 패시브 (마력 회로). 없으면 null.</summary>
        private static Data.Passives.IAttackZoneProvider FindAttackZoneProvider(Character character)
        {
            var passives = character?.Stats?.PassiveInstances;
            if (passives == null) return null;

            foreach (var passive in passives)
                if (passive is Data.Passives.IAttackZoneProvider provider)
                    return provider;
            return null;
        }

        /// <summary>캐릭터의 패시브가 제공하는 구역 사거리 중 가장 큰 값. 없으면 0(자기 구역만).</summary>
        private static int ResolveZoneReach(Character character)
        {
            var passives = character?.Stats?.PassiveInstances;
            if (passives == null) return 0;

            int reach = 0;
            foreach (var passive in passives)
                if (passive is Data.Passives.IZoneReachProvider provider)
                    reach = Mathf.Max(reach, provider.ExtraZoneReach);

            return reach;
        }
    }
}
