using DiceOrbit.Core.Pipeline;
using DiceOrbit.UI;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.Core
{
    public enum CombatStatus
    {
        StartCombat,
        StartPlayerTurn,
        ExecutePlayerTurn,
        EndPlayerTurn,
        StartMonsterTurn,
        ExecuteMonsterTurn,
        EndMonsterTurn,
        EndCombat
    }

    /// <summary>
    /// 전투 관리자 (싱글톤)
    /// 전투 실행, 턴 관리, UI 제어 통합
    /// </summary>
    public class CombatManager : MonoBehaviour
    {
        public static CombatManager Instance { get; private set; }

        [Header("Combat State")]
        [SerializeField] private bool inCombat = false;
        [SerializeField] private List<Monster> activeMonsters = new List<Monster>();

        [Header("Turn Management")]
        [SerializeField] private int turnCount = 0;
        private bool playerTurnActive = false;
        // 캐릭터별 턴 예산(이동/행동)을 중앙에서 강제 관리합니다.
        private readonly Dictionary<Character, CharacterTurnBudget> playerTurnBudgets = new Dictionary<Character, CharacterTurnBudget>();

        [Header("UI References")]
        [SerializeField] private Button rollDiceButton;
        [SerializeField] private Button endTurnButton;
        [SerializeField] private TextMeshProUGUI turnCountText;
        [Tooltip("턴 카운터 칩(텍스트의 부모). 전투 중에만 표시한다. 비어 있으면 텍스트 오브젝트 자체를 토글.")]
        [SerializeField] private GameObject turnCounterRoot;

        /// <summary>튜토리얼 하이라이트용 — 턴 종료 버튼 Rect.</summary>
        public RectTransform EndTurnRect => endTurnButton != null ? endTurnButton.transform as RectTransform : null;

        // Events
        public System.Action OnCombatStart;
        public System.Action OnCombatEnd;
        public System.Action<Monster> OnMonsterDeath;
        public event System.Action OnMonsterTurnStart; // Legacy event support or internal use
        public System.Action<Character> OnPlayerMoved;      // TrySpendMove 성공 시 (튜토리얼 진행조건)
        public System.Action<Character> OnPlayerSkillUsed;  // TrySpendAction 성공 시 (튜토리얼 진행조건)

        // Properties
        public bool InCombat => inCombat;
        public List<Monster> ActiveMonsters => activeMonsters;
        public int TurnCount => turnCount;
        public bool PlayerTurnActive => playerTurnActive;
        public Run.EncounterDefinition CurrentEncounter { get; private set; }   // 배경 등 조회용
        public int CurrentFloorNumber { get; private set; }                     // 표시용 (층+1)

        private sealed class CharacterTurnBudget
        {
            public int RemainingMove = 1;
            public int RemainingAction = 1;
            public bool HasAny => RemainingMove > 0 || RemainingAction > 0;
        }

        public CombatStatus combatStatus { get; private set; }
        private void Awake()
        {
            // 싱글톤 패턴
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Debug.LogWarning("Multiple CombatManagers detected! Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            // 버튼 자동 연결 (Scene에 할당 안 된 경우 대비)
            EnsureButtons();

            // 버튼 이벤트 연결
            if (rollDiceButton != null)
            {
                rollDiceButton.onClick.AddListener(OnRollDiceClicked);
            }

            if (endTurnButton != null)
            {
                endTurnButton.onClick.AddListener(OnEndTurnClicked);
                endTurnButton.interactable = false; // 처음엔 비활성
            }

            combatStatus = CombatStatus.StartCombat;
        }

        private void Start()
        {
            EnsureButtons();
            UpdateUI();
        }

        private void OnDestroy()
        {
            // Cleanup listeners if needed
            if (rollDiceButton != null) rollDiceButton.onClick.RemoveListener(OnRollDiceClicked);
            if (endTurnButton != null) endTurnButton.onClick.RemoveListener(OnEndTurnClicked);
        }

        private void EnsureButtons()
        {
            if (rollDiceButton == null)
            {
                rollDiceButton = FindButtonByName("Roll Dice Button", "RollDiceButton", "Roll Dice");
            }

            if (endTurnButton == null)
            {
                endTurnButton = FindButtonByName("End Turn Button", "EndTurnButton", "End Turn");
            }
        }

        private Button FindButtonByName(params string[] names)
        {
            foreach (var name in names)
            {
                var obj = GameObject.Find(name);
                if (obj != null)
                {
                    var button = obj.GetComponent<Button>();
                    if (button != null) return button;
                }
            }

            var buttons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            foreach (var button in buttons)
            {
                var text = button.GetComponentInChildren<TextMeshProUGUI>();
                if (text == null) continue;

                foreach (var name in names)
                {
                    if (!string.IsNullOrEmpty(text.text) && text.text.Contains(name))
                    {
                        return button;
                    }
                }
            }

            return null;
        }

        /// <summary>전투 진입점 (노드맵 흐름). 몬스터 숨겨 스폰 → 시작 연출 → CombatStart 방송 → 턴 시퀀스.</summary>
        public void StartEncounter(Run.EncounterDefinition encounter, int floorNumber)
        {
            if (inCombat) return;

            DestroyActiveMonsters();
            CurrentEncounter = encounter;
            CurrentFloorNumber = floorNumber;
            SetTurnCounterVisible(true);

            // 배경은 연출 전에 미리 세팅 (OnCombatStart는 연출 후 발화되므로 배경만 앞당김)
            var bg = FindFirstObjectByType<BackgroundManager>(FindObjectsInactive.Include);
            if (bg != null) bg.ApplyEncounterBackground();

            // 숨긴 채 전량 스폰 (전멸 판정/인텐트 로직은 즉시 유효, 연출이 순차로 드러냄)
            var spawned = EncounterSpawner.EnsureInstance().Spawn(encounter, startHidden: true);
            foreach (var m in spawned)
                RegisterMonster(m);
            // 사망 구독 불필요 — Monster가 사망 시 OnMonsterDefeated를 직접 호출한다 (이중 호출 금지).

            StartCoroutine(IntroThenStart(spawned));
        }

        /// <summary>시작 연출(타일→캐릭터→몬스터) 후 전투 개시.</summary>
        private System.Collections.IEnumerator IntroThenStart(System.Collections.Generic.List<Monster> spawned)
        {
            yield return Visuals.CombatIntroDirector.EnsureInstance().Play(spawned);
            BroadcastCombatStart();
            StartCombat();
        }

        /// <summary>이전 전투 잔여 몬스터 파괴 + 장부 초기화 (파괴 책임 = 장부 권위).</summary>
        private void DestroyActiveMonsters()
        {
            foreach (var m in activeMonsters)
            {
                if (m == null) continue;
                Destroy(m.gameObject);
            }
            activeMonsters.Clear();
        }

        /// <summary>전투 시작 사건을 파이프라인에 방송 — 유물 등 리액터가 반응 (개별 효과는 모름).</summary>
        private void BroadcastCombatStart()
        {
            // 이전 전투의 충전은 남기지 않는다 — 방송(사전 준비의 전투 시작 충전)보다 반드시 먼저 (2026-10-09 B).
            ClearPartyCharge();

            if (Pipeline.CombatPipeline.Instance == null || PartyManager.Instance == null) return;
            foreach (var ch in PartyManager.Instance.Party)
            {
                if (ch == null || !ch.IsAlive) continue;
                var ctx = new Pipeline.TurnEventContext(ch, ch, Pipeline.EventPhase.CombatStart);
                Pipeline.CombatPipeline.Instance.Process(ctx);
            }
        }

        /// <summary>
        /// 전투 시작
        /// </summary>
        public void StartCombat()
        {
            combatStatus = CombatStatus.StartCombat;
            if (inCombat) return;

            inCombat = true;
            turnCount = 0;
            Debug.Log($"Combat started! {activeMonsters.Count} monster(s)");

            // 콤보/행동 스코프는 전투 단위 상태 — 시작 시 깨끗하게 (스펙 2026-08-28 §1 규칙 9)
            Combo.ComboSystem.EnsureInstance().ResetAll();
            Systems.Effects.AttackActionScope.ResetAll();
            ExpireRoundStatuses();

            OnCombatStart?.Invoke();
            DiceOrbit.Visuals.VfxService.Play(DiceOrbit.Visuals.VfxTags.CombatStart,
                Camera.main != null ? Camera.main.transform.position + Camera.main.transform.forward * 6f : Vector3.zero);

            // 첫 플레이어 턴은 안내 띄우고 시작
            StartCoroutine(AnnounceAndStartPlayerTurn());
        }

        // (턴 안내의 이미지/텍스트·색 결정은 TurnAnnouncementUI 소유로 이동)

        private System.Collections.IEnumerator AnnounceAndStartPlayerTurn()
        {
            UI.TurnAnnouncementUI.EnsureInstance();
            var ui = UI.TurnAnnouncementUI.Instance;
            if (ui != null)
                yield return ui.ShowPlayerTurn();
            StartPlayerTurn();
        }

        private System.Collections.IEnumerator AnnounceAndProgressMonsterTurn()
        {
            UI.TurnAnnouncementUI.EnsureInstance();
            var ui = UI.TurnAnnouncementUI.Instance;
            if (ui != null)
                yield return ui.ShowMonsterTurn();
            ProgressMonsterTurn();
        }

        public bool IsCombatFinished()
        {
            if (!inCombat) return true;
            if (activeMonsters.All(m => m == null || !m.IsAlive))
            {
                Debug.Log("All monsters defeated! Ending combat.");
                EndCombat(true);
                return true;
            }
            if (PartyManager.Instance != null && PartyManager.Instance.IsPartyWiped())
            {
                Debug.Log("Party wiped out! Ending combat.");
                EndCombat(false);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 전투 종료
        /// </summary>
        public void EndCombat(bool victory)
        {
            if (!inCombat) return;

            combatStatus = CombatStatus.EndCombat;
            inCombat = false;
            SetTurnCounterVisible(false);
            // 전투가 끝나면 턴 예산도 초기화합니다.
            playerTurnBudgets.Clear();
            HideMonsterIntents(); // Clean up visuals

            // 콤보/행동 스코프 정리 — 전투를 넘어 이월되지 않는다 (스펙 2026-08-28 §1 규칙 9)
            Combo.ComboSystem.EnsureInstance().ResetAll();
            Systems.Effects.AttackActionScope.ResetAll();
            ExpireRoundStatuses(); // 턴 도중 승리·패배여도 라운드형 상태가 남지 않게
            ClearPartyCharge();    // 충전은 전투를 넘어 이월되지 않는다 (방송 없는 디버그 시작 경로 포함)

            if (victory)
            {
                Debug.Log("Victory! All monsters defeated!");
            }
            else
            {
                Debug.Log("Defeat! Party wiped out!");
            }

            var screenAt = Camera.main != null ? Camera.main.transform.position + Camera.main.transform.forward * 6f : Vector3.zero;
            DiceOrbit.Visuals.VfxService.Play(
                victory ? DiceOrbit.Visuals.VfxTags.Victory : DiceOrbit.Visuals.VfxTags.Defeat, screenAt);

            OnCombatEnd?.Invoke();

            // 전투 결과 통지 (승/패 대칭 — GameFlow 직접 호출)
            if (victory)
            {
                if (GameFlowManager.Instance != null)
                {
                    GameFlowManager.Instance.OnEncounterCleared();
                }
            }
            else
            {
                if (GameFlowManager.Instance != null)
                {
                    GameFlowManager.Instance.OnCombatDefeat();
                }
            }
        }

        // ===========================================
        // Turn Management Logic (Merged from TurnManager)
        // ===========================================

        /// <summary>
        /// 플레이어 턴 시작
        /// </summary>
        public void StartPlayerTurn()
        {
            if (IsCombatFinished()) return;

            combatStatus = CombatStatus.StartPlayerTurn;
            playerTurnActive = true;
            turnCount++;

            Debug.Log($"=== Turn {turnCount} - Player Turn ===");

            // 캐릭터 턴 시작 처리 (패시브/상태효과)
            var partyManager = PartyManager.Instance;
            if (partyManager != null)
            {
                partyManager.ResetTeamFirstAction();
                foreach (var character in partyManager.Party)
                {
                    if (character != null && character.IsAlive)
                    {
                        character.OnStartTurn();
                        if (IsCombatFinished()) return;
                    }
                }
            }

            if (IsCombatFinished()) return;
            // 플레이어 턴 시작 시 캐릭터별 이동/행동 횟수를 1로 초기화합니다.
            InitializePlayerTurnBudgets();

            // 이번 턴 자동 기본공격 발동 기록을 비웁니다 (캐릭터당 1회 보장).
            AutoAttackSystem.EnsureInstance().ResetTurn();

            // 주사위 자동 굴리기
            var diceManager = DiceManager.Instance;
            if (diceManager != null)
            {
                diceManager.RollDice();
            }

            // UI Update
            if (rollDiceButton != null) rollDiceButton.interactable = false; // Auto rolled
            if (endTurnButton != null) endTurnButton.interactable = true;

            // 몬스터 공격 의도 미리보기 표시
            ShowMonsterIntents();
            UpdateUI();
            combatStatus = CombatStatus.ExecutePlayerTurn;
        }

        /// <summary>
        /// 플레이어 턴 종료 (코루틴)
        /// </summary>
        private System.Collections.IEnumerator EndPlayerTurnRoutine()
        {
            if (IsCombatFinished() || !playerTurnActive)
            {
                yield break; // 큐에 들어왔지만 이미 조건이 안맞으면 즉시 종료
            }
            combatStatus = CombatStatus.EndPlayerTurn;

            // 이동해야만 공격이다 — 제자리로 턴을 넘긴 캐릭터는 공격하지 않는다 (2026-08-23 결정).
            // 이동을 포기하는 것 자체가 선택이고, 그 대가가 이번 턴의 딜이다.
            // 그리고 그 대가에 콤보도 포함된다 — 이번 턴 공격하지 않은(미이동/사망) 캐릭터는 콤보가 끊긴다 (2026-08-28 §1 규칙 6·10).
            {
                var combo = Combo.ComboSystem.EnsureInstance();
                var auto = AutoAttackSystem.Instance;
                var party = PartyManager.Instance;
                if (party != null)
                {
                    foreach (var ch in party.Party)
                    {
                        if (ch == null) continue;
                        if (!ch.IsAlive || auto == null || !auto.HasResolved(ch))
                            combo.ResetCombo(ch, Combo.ComboOutcome.BrokenByNoMove);
                    }
                }
            }

            var partyManager = PartyManager.Instance;
            if (partyManager != null)
            {
                partyManager.ResetTeamFirstAction();
                foreach (var character in partyManager.Party)
                {
                    if (character != null && character.IsAlive)
                    {
                        character.OnEndTurn();
                        if (IsCombatFinished()) yield break;
                    }
                }
            }

            if (IsCombatFinished()) yield break;
            playerTurnActive = false;
            // 턴 종료 시 예산 정보를 비워 다음 턴에 새로 구성합니다.
            playerTurnBudgets.Clear();

            Debug.Log("=== Player Turn End ===");

            // UI Lock (이미 OnClick에서 잠갔지만, 여기서 한번 더 확인)
            if (endTurnButton != null) endTurnButton.interactable = false;

            // 공격 의도 미리보기 숨기기 (몬스터 턴 시작 전)
            HideMonsterIntents();

            // 플레이어 턴의 팝업 연출(피해 숫자·패시브 버블)이 전부 사라진 뒤에 몬스터 턴으로 —
            // 몬스터 행동이 연출 위로 덮치지 않게 한다.
            yield return new WaitUntil(() => UI.FloatingPopupQueue.AllPopupsFinished);

            // "몬스터 턴" 안내 → ProgressMonsterTurn
            yield return AnnounceAndProgressMonsterTurn();
        }

        /// <summary>
        /// 현재 캐릭터가 이동을 수행할 수 있는지 확인합니다.
        /// </summary>
        public bool CanSpendMove(Character character)
        {
            if (!IsCharacterTurnActionValid(character)) return false;
            if (!playerTurnBudgets.TryGetValue(character, out var budget)) return false;
            return budget.RemainingMove > 0;
        }

        /// <summary>
        /// 현재 캐릭터가 행동(스킬)을 수행할 수 있는지 확인합니다.
        /// </summary>
        public bool CanSpendAction(Character character)
        {
            if (!IsCharacterTurnActionValid(character)) return false;
            if (!playerTurnBudgets.TryGetValue(character, out var budget)) return false;
            return budget.RemainingAction > 0;
        }

        /// <summary>
        /// 이동 예산 1회를 실제로 소모합니다.
        /// </summary>
        public bool TrySpendMove(Character character)
        {
            if (!CanSpendMove(character)) return false;
            playerTurnBudgets[character].RemainingMove--;
            OnPlayerMoved?.Invoke(character);
            return true;
        }

        /// <summary>
        /// 행동(스킬) 예산 1회를 실제로 소모합니다.
        /// </summary>
        public bool TrySpendAction(Character character)
        {
            if (!CanSpendAction(character)) return false;
            playerTurnBudgets[character].RemainingAction--;
            OnPlayerSkillUsed?.Invoke(character);
            return true;
        }

        /// <summary>
        /// 해당 캐릭터가 이번 턴에 남은 행동권이 하나라도 있는지 확인합니다.
        /// </summary>
        public bool HasAnyTurnActionRemaining(Character character)
        {
            if (!IsCharacterTurnActionValid(character)) return false;
            if (!playerTurnBudgets.TryGetValue(character, out var budget)) return false;
            return budget.HasAny;
        }

        private bool IsCharacterTurnActionValid(Character character)
        {
            if (character == null || !character.IsAlive) return false;
            if (!inCombat || !playerTurnActive) return false;
            return true;
        }

        private void InitializePlayerTurnBudgets()
        {
            playerTurnBudgets.Clear();

            var partyManager = PartyManager.Instance;
            if (partyManager == null) return;

            foreach (var character in partyManager.Party)
            {
                if (character == null || !character.IsAlive) continue;
                playerTurnBudgets[character] = new CharacterTurnBudget();
            }
        }

        /// <summary>
        /// Roll Dice 버튼 클릭
        /// </summary>
        private void OnRollDiceClicked()
        {
            var diceManager = DiceManager.Instance;
            if (diceManager != null)
            {
                diceManager.RollDice();

                if (rollDiceButton != null) rollDiceButton.interactable = false;
                if (endTurnButton != null) endTurnButton.interactable = true;
            }
        }

        /// <summary>
        /// 턴 종료 버튼 클릭
        /// </summary>
        private void OnEndTurnClicked()
        {
            // 즉시 버튼 비활성화하여 중복 클릭 방지
            if (endTurnButton != null) endTurnButton.interactable = false;
            if (rollDiceButton != null) rollDiceButton.interactable = false;

            // 턴 종료 로직을 큐에 삽입
            ActionQueueManager.Instance.EnqueueAction(EndPlayerTurnRoutine());
        }

        /// <summary>턴 카운터 칩은 전투 중에만 보인다 (맵·상점·보상에서 "턴 0"이 남지 않게, 2026-09-25).</summary>
        private void SetTurnCounterVisible(bool visible)
        {
            var target = turnCounterRoot != null ? turnCounterRoot : turnCountText != null ? turnCountText.gameObject : null;
            if (target != null) target.SetActive(visible);
        }

        private void UpdateUI()
        {
            if (turnCountText != null)
            {
                turnCountText.text = $"턴 {turnCount}";
            }
        }

        // ===========================================
        // Monster Logic
        // ===========================================


        /// <summary>
        /// 몬스터 턴 실행
        /// </summary>
        [Header("Monster Turn Pacing")]
        [Tooltip("몬스터 한 마리 행동 후 다음 마리까지의 지연(초)")]
        [SerializeField] private float monsterActionDelay = 0.8f;
        [Tooltip("스킬명 말풍선을 띄운 뒤 실제 실행까지의 짧은 대기(초)")]
        [SerializeField] private float monsterActionLeadIn = 0.25f;

        public void ProgressMonsterTurn()
        {
            if (IsCombatFinished()) return;
            StartCoroutine(ProgressMonsterTurnRoutine());
        }

        private System.Collections.IEnumerator ProgressMonsterTurnRoutine()
        {
            OnMonsterTurnStart?.Invoke();
            StartMonsterTurn();
            if (IsCombatFinished()) yield break;

            // 몬스터 한 마리씩 순차 실행(표시 → 실행 → 지연)
            yield return ExecuteMonsterTurnRoutine();
            if (IsCombatFinished()) yield break;

            yield return EndMonsterTurnRoutine();
        }

        private void StartMonsterTurn()
        {
            Debug.Log("=== Monster Turn Start ===");
            if (IsCombatFinished()) return;

            combatStatus = CombatStatus.StartMonsterTurn;
            var sortedMonster = activeMonsters.OrderByDescending(m => m.Stats.Speed).ToList();
            // 실제 행동
            foreach (var monster in sortedMonster)
            {
                if (monster != null && monster.IsAlive)
                {
                    monster.OnStartTurn();
                    if (IsCombatFinished()) return;
                }
            }
        }

        private System.Collections.IEnumerator ExecuteMonsterTurnRoutine()
        {
            Debug.Log("=== Monster Turn Execute ===");
            if (IsCombatFinished()) yield break;

            combatStatus = CombatStatus.ExecuteMonsterTurn;
            UI.MonsterActionLabel.EnsureInstance();
            var sortedMonster = activeMonsters.OrderByDescending(m => m.Stats.Speed).ToList();

            // 속도 순으로 한 마리씩: 무엇을 하는지 말풍선 표시 → 실행 → 지연
            foreach (var monster in sortedMonster)
            {
                if (monster == null || !monster.IsAlive) continue;

                // ExecuteIntent가 '다음' 의도를 새로 뽑으므로, 이번 행동 이름/아이콘은 실행 전에 읽는다.
                string actionName = monster.NextSkill != null && monster.NextSkill.skillData != null
                    ? monster.NextSkill.skillData.SkillName : null;
                Sprite actionIcon = monster.NextSkill != null ? monster.NextSkill.IntentIcon : null;

                if (!string.IsNullOrEmpty(actionName))
                {
                    monster.SetActingHighlight(true);

                    // 이번 공격의 대상 타일 둘레 외곽선(정체성 색)을 실행 동안 강제 표시.
                    // ExecuteIntent가 '다음' 의도를 뽑으므로 타일은 실행 전에 캡처한다(actionName과 동일 타이밍).
                    var threatTiles = Visuals.IntentTileLiftEffect.CollectIntentTiles(monster);
                    Color threatColor = Visuals.MonsterIdentityManager.Instance != null
                        ? Visuals.MonsterIdentityManager.Instance.GetColor(monster)
                        : new Color(1f, 0.45f, 0.3f, 1f);
                    Visuals.MonsterThreatOutline.EnsureInstance();
                    Visuals.MonsterThreatOutline.Instance?.ShowForced(threatTiles, threatColor);

                    var lbl = UI.MonsterActionLabel.Instance;
                    if (lbl != null) yield return lbl.Show(monster, actionName, actionIcon);
                    if (monsterActionLeadIn > 0f) yield return new WaitForSeconds(monsterActionLeadIn);

                    // 공격(대상 타일이 있는 행동)이면 움츠렸다가 대상 쪽으로 내리찍는다 — 피해는 내리찍는 순간 들어간다 (타격감 리워크)
                    if (threatTiles != null && threatTiles.Count > 0)
                    {
                        Vector3 aim = Vector3.zero;
                        foreach (var tile in threatTiles) aim += tile.Position;
                        aim /= threatTiles.Count;
                        yield return Visuals.HitDirector.MonsterWindup(monster, aim);
                        Visuals.HitDirector.MonsterStrike(monster, aim);
                    }

                    monster.ExecuteIntent();

                    if (monsterActionDelay > 0f) yield return new WaitForSeconds(monsterActionDelay);
                    if (lbl != null) yield return lbl.Hide();
                    if (monster != null) monster.SetActingHighlight(false);

                    Visuals.MonsterThreatOutline.Instance?.ClearForced();   // 강제 표시 해제 → 호버/패널 채널로 복귀

                    if (IsCombatFinished()) yield break;
                }
                else
                {
                    // 기절/유휴 — 말풍선 없이 조용히 처리.
                    monster.ExecuteIntent();
                    if (IsCombatFinished()) yield break;
                }
            }
        }

        private System.Collections.IEnumerator EndMonsterTurnRoutine()
        {
            yield return new WaitForSeconds(1.0f); // Default monster turn duration
            Debug.Log("=== Monster Turn End ===");
            if (IsCombatFinished()) yield break;

            combatStatus = CombatStatus.EndMonsterTurn;
            foreach (var monster in activeMonsters)
            {
                if (monster != null && monster.IsAlive)
                {
                    monster.OnEndTurn();
                }
            }

            Debug.Log("=== Monster Turn End ===");

            if (IsCombatFinished()) yield break;
            {
                TileTurnEnd(); // Loop back to player
                ExpireRoundStatuses();
            }

            if (IsCombatFinished()) yield break;
            if (inCombat)
            {
                // "플레이어 턴" 안내 → StartPlayerTurn
                yield return AnnounceAndStartPlayerTurn();
            }
        }

        /// <summary>
        /// 라운드형 상태(감전·고양·약화) 일괄 제거 — 시트 "이번 턴 동안" = 플레이어 턴 + 몬스터 턴.
        /// 캐릭터는 전투 사이에 살아남으므로 전투 시작·전투 종료에도 호출해 이전 전투의 고양이 새지 않게 한다.
        /// </summary>
        private void ExpireRoundStatuses()
        {
            var party = PartyManager.Instance;
            if (party != null && party.Party != null)
                foreach (var character in party.Party)
                    if (character != null && character.StatusEffects != null)
                        character.StatusEffects.RemoveRoundStatuses();

            foreach (var monster in activeMonsters.ToList())
                if (monster != null && monster.StatusEffects != null)
                    monster.StatusEffects.RemoveRoundStatuses();
        }

        /// <summary>파티 전원의 충전 제거 — 전투 경계에서 호출 (2026-10-09 B).</summary>
        private void ClearPartyCharge()
        {
            var party = PartyManager.Instance;
            if (party == null || party.Party == null) return;

            foreach (var character in party.Party)
                if (character != null) Systems.Effects.ChargeStatus.Clear(character);
        }

        private void TileTurnEnd()
        {
            // 매 라운드 종료 시 모든 타일 속성의 지속시간을 1 감소시키고, 만료된 속성을 제거한다.
            // (파이프라인 IsTiling 컨텍스트가 실제로 발사되지 않아 동작하지 않던 것을 직접 틱으로 대체)
            var orbit = GameManager.Instance?.GetOrbitManager();
            if (orbit?.Tiles == null) return;

            foreach (var tile in orbit.Tiles)
                if (tile != null) tile.TickTurnEnd();
        }

        /// <summary>
        /// 몬스터들의 공격 의도 미리보기 표시
        /// </summary>
        private void ShowMonsterIntents()
        {
            // AttackIndicator가 등록된 모든 Intent를 시각화
            UI.MonsterAttackIntentManager.Instance?.Show();
            Debug.Log("[CombatManager] Showing monster attack previews");
        }

        /// <summary>
        /// 몬스터들의 공격 의도 미리보기 숨기기
        /// </summary>
        private void HideMonsterIntents()
        {
            UI.MonsterAttackIntentManager.Instance?.Hide();
            Debug.Log("[CombatManager] Hiding monster attack previews");
        }

        // ===========================================
        // Management Methods
        // ===========================================

        /// <summary>
        /// 몬스터 등록 (Wave 스폰 시 사용)
        /// </summary>
        public void RegisterMonster(Monster monster)
        {
            if (monster == null) return;
            if (!activeMonsters.Contains(monster))
            {
                activeMonsters.Add(monster);
            }
        }

        /// <summary>
        /// 현재 몬스터 목록 초기화
        /// </summary>
        public void ClearMonsters()
        {
            activeMonsters.Clear();
        }

        /// <summary>
        /// 몬스터 격파 처리
        /// </summary>
        public void OnMonsterDefeated(Monster monster)
        {
            activeMonsters.Remove(monster);
            OnMonsterDeath?.Invoke(monster);

            if (IsCombatFinished()) return;
        }

        /// <summary>
        /// 캐릭터 격파 처리
        /// </summary>
        public void OnCharacterDefeated(Character character)
        {
            IsCombatFinished();
        }

        /// <summary>
        /// 캐릭터가 몬스터 공격
        /// </summary>
        public void AttackMonster(Monster target, int damage, bool ignoreDefense = false)
        {
            if (target == null || !target.IsAlive) return;

            // System/Direct Attack via Pipeline
            var context = new Pipeline.AttackContext(null, target, "Direct Attack", damage); // Source is null (System)

            if (Pipeline.CombatPipeline.Instance != null)
            {
                Pipeline.CombatPipeline.Instance.Process(context);
            }
        }

        /// <summary>
        /// 모든 몬스터 공격 (범위 공격, Pipeline 사용)
        /// </summary>
        public void AttackAllMonsters(int damage, bool ignoreDefense = false)
        {
            // 리스트 복사하여 순회 중 변경 대비
            var targets = new List<Monster>(activeMonsters);

            foreach (var monster in targets)
            {
                if (monster.IsAlive)
                {
                    var context = new Pipeline.AttackContext(null, monster, "Global Attack", damage);

                    if (Pipeline.CombatPipeline.Instance != null)
                    {
                        Pipeline.CombatPipeline.Instance.Process(context);
                    }
                }
            }
        }

        /// <summary>
        /// 생존한 몬스터 목록
        /// </summary>
        public List<Monster> GetAliveMonsters()
        {
            return activeMonsters.Where(m => m.IsAlive).ToList();
        }
    }
}

