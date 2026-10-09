using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DiceOrbit.Core.Run;
using DiceOrbit.Core.Combo;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Data.MonsterPresets.Wave0.Shared;
using DiceOrbit.Data.Skills;
using DiceOrbit.UI;
using DiceOrbit.UI.Tutorial;

namespace DiceOrbit.Core.Tutorial
{
    /// <summary>
    /// 튜토리얼 고정 데모 셋업. 실제 런 상태(RunManager/세이브/영구덱)를 건드리지 않고
    /// 데모 파티(전사·도적 인접)·약체 몬스터만 스폰하고, 끝나면 ClearAll로 정리한다.
    /// </summary>
    public class TutorialScenario : MonoBehaviour
    {
        public static TutorialScenario Instance { get; private set; }

        [Header("데모 데이터 (Resources/TutorialScenario 프리팹에 배선)")]
        [SerializeField] private CharacterPreset warriorPreset;
        [SerializeField] private CharacterPreset roguePreset;
        [SerializeField] private MonsterPreset blueDemoMonster; // 첫 박치기로 방진 체험
        [SerializeField] private MonsterPreset demoMonster; // 나머지 세 구역의 GreenSlime
        [SerializeField] private Sprite tutorialBackground; // 튜토리얼 전투 배경(배경 수정 1차)

        public Character Warrior { get; private set; }
        public Character Rogue { get; private set; }
        public Monster DemoMonster { get; private set; }
        public IReadOnlyList<Monster> DemoMonsters => demoMonsters;
        public bool HasConfiguredPartyPresets => warriorPreset != null && roguePreset != null;
        public bool HasConfiguredMonsterPresets => blueDemoMonster != null && demoMonster != null;

        private readonly List<Monster> demoMonsters = new List<Monster>();

        public IReadOnlyList<MonsterPreset> BuildDemoMonsterPresets()
            => new[] { blueDemoMonster, demoMonster, demoMonster, demoMonster };

        public static TutorialScenario EnsureInstance()
        {
            if (Instance != null) return Instance;
            Instance = FindAnyObjectByType<TutorialScenario>(FindObjectsInactive.Include);
            if (Instance == null)
            {
                var prefab = Resources.Load<TutorialScenario>("TutorialScenario");
                Instance = prefab != null ? Instantiate(prefab)
                                          : new GameObject("TutorialScenario").AddComponent<TutorialScenario>();
            }
            return Instance;
        }

        private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>데모 파티(전사·도적 인접) 스폰 후 약체 몬스터로 전투 개시.</summary>
        public void SpawnDemo()
        {
            var spawner = UnityEngine.Object.FindAnyObjectByType<CharacterSpawner>();
            int tileCount = UnityEngine.Object.FindAnyObjectByType<OrbitManager>()?.TileCount ?? 12;

            // slotCount = 타일수 → gap 1칸 → slot 0/1 = 인접 타일. 전사 옆에 도적 → 전사 패시브(+50%) 조건.
            Warrior = spawner?.Spawn(warriorPreset, 0, tileCount);
            Rogue   = spawner?.Spawn(roguePreset,   1, tileCount);

            // 캐릭터 지연 배치(Character.InitializeAfterDelay)가 끝난 뒤 전투 개시.
            // 인트로의 타일 이동과 레이스가 나면 캐릭터가 엉뚱한(공중) 위치에 고정된다.
            StartCoroutine(StartCombatAfterSettle());
        }

        private System.Collections.IEnumerator StartCombatAfterSettle()
        {
            float t = 0f;
            while ((Warrior == null || Warrior.CurrentTile == null) && t < 1f) { t += Time.deltaTime; yield return null; }
            yield return null; // 여유 한 프레임

            // 튜토리얼: 데모 캐릭터는 죽지 않게(HP 1 미만 방지) — 몬스터 공격에 튜토리얼이 실패하지 않도록
            if (Warrior != null && Warrior.Stats != null) Warrior.Stats.Invulnerable = true;
            if (Rogue != null && Rogue.Stats != null) Rogue.Stats.Invulnerable = true;

            if (HasConfiguredMonsterPresets && CombatManager.Instance != null)
            {
                var enc = new EncounterDefinition { MonsterPresets = BuildDemoMonsterPresets().ToList() };
                if (tutorialBackground != null) enc.BackgroundSprite = tutorialBackground; // 튜토리얼 전용 배경
                CombatManager.Instance.StartEncounter(enc, 1); // 몬스터 스폰 + 인트로 + 전투 개시
                demoMonsters.Clear();
                demoMonsters.AddRange(CombatManager.Instance.ActiveMonsters.Where(m => m != null));
                DemoMonster = demoMonsters.FirstOrDefault();
                TuneDemoMonsterSurvival();
                // 턴1 고정 주사위: 전사 눈4 → 타일4, 도적 눈2 → 타일3. 같은 구역에서 협공/방진을 체험한다.
                // 인트로 후 StartPlayerTurn의 자동 굴림이 이 값을 소모(1회).
                DiceManager.Instance?.SetScriptedRoll(new[] { 4, 2, 5, 3 });
            }
            else
            {
                Debug.LogError("[TutorialScenario] demoMonster/CombatManager 없음 — 데모 전투 개시 불가.");
            }
        }

        private void TuneDemoMonsterSurvival()
        {
            var combo = Warrior?.Stats?.ActiveAbilities?
                .Select(slot => slot?.RuntimeInstance)
                .OfType<ComboActiveSkill>()
                .FirstOrDefault();
            int finaleDamage = combo != null ? combo.CalculateStageDamage(Warrior, 2) : 1;
            finaleDamage = Mathf.Max(1, finaleDamage);

            foreach (var monster in demoMonsters)
            {
                if (monster?.Stats == null) continue;
                monster.Stats.MaxHP = finaleDamage;
                monster.Stats.CurrentHP = finaleDamage;
                monster.Stats.Invulnerable = true;
            }
        }

        private void ReleaseFinaleTargets()
        {
            foreach (var monster in demoMonsters)
                if (monster?.Stats != null) monster.Stats.Invulnerable = false;
        }

        private void PrepareFormationAttack()
        {
            if (DemoMonster == null) return;

            // 최초 의도는 캐릭터가 움직이기 전에 잡힌다. 두 캐릭터가 모인 현재 위치를 기준으로
            // 박치기 범위를 다시 만들되, 실행은 파란 슬라임의 실제 AI/스킬 경로를 그대로 쓴다.
            for (int i = 0; i < 3; i++)
            {
                DemoMonster.SelectNextIntent();
                if (DemoMonster.NextSkill?.skillData is SlimeBodySlamSkill) break;
            }
        }

        // 진행조건 이벤트 핸들러 (Cleanup에서 해제)
        private Action<Character, int, ComboOutcome> _onComboChanged;
        private TutorialCombatProgress combatProgress;

        /// <summary>전투 튜토리얼 시퀀스. 대상/조건은 런타임 상태를 참조하므로 델리게이트로 지연 평가.
        /// 정보 단계(Confirm)만 입력잠금, 조작 단계는 하이라이트+조건 대기(비잠금 — 다중 클릭 허용).</summary>
        public List<TutorialStep> BuildCombatSteps()
        {
            var cm = CombatManager.Instance;
            var combo = ComboSystem.EnsureInstance();
            if (_onComboChanged != null) combo.OnComboChanged -= _onComboChanged;
            combatProgress = new TutorialCombatProgress(Warrior, Rogue);
            _onComboChanged = combatProgress.Observe;
            combo.OnComboChanged += _onComboChanged;

            Func<RectTransform> Action = () => CharacterActionUI.Instance != null ? CharacterActionUI.Instance.PanelRoot : null;
            Func<RectTransform> Dice   = () => DiceUI.Instance != null ? DiceUI.Instance.PanelRect : null;
            Func<RectTransform> EndTurn = () => cm != null ? cm.EndTurnRect : null;
            Func<RectTransform> W = () => ScreenBoxProvider.ForWorld(Warrior != null ? Warrior.transform : null);
            Func<RectTransform> R = () => ScreenBoxProvider.ForWorld(Rogue != null ? Rogue.transform : null);
            Func<RectTransform> M = () => ScreenBoxProvider.ForWorld(DemoMonster != null ? DemoMonster.transform : null);
            Func<RectTransform> Field = () =>
            {
                var om = UnityEngine.Object.FindAnyObjectByType<OrbitManager>();
                return om != null ? ScreenBoxProvider.ForWorld(om.transform, new Vector2(1050, 900)) : null;   // 위아래로 더 넓게
            };
            Func<RectTransform> Info = () => BattleInfoPanelUI.Instance != null ? BattleInfoPanelUI.Instance.PanelRect : null;

            return new List<TutorialStep>
            {
                new TutorialStep("캐릭터를 움직여 네 구역의 몬스터를 모두 처치하는 것이 전투의 목표입니다. 먼저 화면부터 살펴볼게요.")
                    { GateInput = true },
                new TutorialStep("몬스터 머리 위의 아이콘은 다음 턴에 사용할 행동을 보여줍니다.")
                    { Target = M, GateInput = true, HighlightOffset = new Vector2(0, 60) },
                new TutorialStep("오른쪽 정보 패널에서 몬스터의 체력과 다음 행동을 자세히 확인할 수 있습니다.")
                    { Target = Info, GateInput = true, CenterBubble = true, OnEnter = () => BattleInfoPanelUI.Instance?.ShowUnitExternal(DemoMonster) },
                new TutorialStep("전장은 <b>4개 구역</b>으로 나뉘고, 각 구역에는 담당 몬스터가 한 마리씩 있습니다. 가장자리 색을 따라가면 어느 몬스터의 구역인지 알 수 있습니다.")
                    { Target = Field, GateInput = true, OnEnter = () => BattleInfoPanelUI.Instance?.ClearUnitExternal(DemoMonster) },
                new TutorialStep("캐릭터는 이동을 마친 구역의 몬스터를 공격합니다. 몬스터가 쓰러진 빈 구역에서는 공격할 대상이 없습니다.")
                    { Target = Field, GateInput = true },
                new TutorialStep("색이 칠해진 타일은 몬스터가 다음 턴에 공격할 범위입니다. 의도를 확인하고 맞을지 피할지 결정하세요.")
                    { Target = Field, GateInput = true },
                new TutorialStep("플레이어 턴이 시작되면 주사위가 자동으로 굴러갑니다. 나온 주사위를 캐릭터에게 하나씩 배정할 수 있습니다.")
                    { Target = Dice, GateInput = true },
                new TutorialStep("캐릭터는 한 턴에 한 번 움직입니다. 주사위 눈만큼 이동한 뒤 도착한 구역의 몬스터를 자동으로 공격하므로, 따로 공격 버튼을 누를 필요는 없습니다.")
                    { GateInput = true },
                new TutorialStep("전사를 선택하세요.")
                    { Target = W, Advance = TutorialAdvance.Custom,
                      Done = () => CharacterActionUI.Instance != null && CharacterActionUI.Instance.IsShowingCharacter(Warrior) },
                new TutorialStep("주사위 눈은 이동 거리이면서 강화 공격 조건입니다. 전사는 <b>눈 4 이상</b>을 연속으로 사용하면 대검 공격이 3단계까지 이어집니다.")
                    { Target = Action, GateInput = true, HighlightPad = new Vector4(0, 0, 0, 120) },
                new TutorialStep("<b>눈 4</b>를 전사에게 배정하고 이동하세요. 도착한 구역의 몬스터에게 1단계 <b>대검 베기</b>가 자동으로 발동합니다.")
                    { NoSpotlight = true, OnlyDieValue = 4, ActionLock = TutorialActionLock.MoveOnly, Advance = TutorialAdvance.Custom,
                      Done = () => combatProgress != null && combatProgress.WarriorStage1Done },
                new TutorialStep("강화 공격이 실제로 적중하면 콤보 표시가 다음 단계로 올라갑니다. 같은 조건의 주사위를 다음 턴에도 사용해야 이어집니다.")
                    { Target = W, GateInput = true },
                new TutorialStep("이번에는 도적을 선택하세요. 도적은 <b>눈 3 이하</b>를 연속으로 사용해 비수 콤보를 이어갑니다.")
                    { Target = R, Advance = TutorialAdvance.Custom,
                      Done = () => CharacterActionUI.Instance != null && CharacterActionUI.Instance.IsShowingCharacter(Rogue) },
                new TutorialStep("<b>눈 2</b>를 도적에게 배정해 전사와 같은 구역으로 이동하세요. 1단계 <b>쌍비수</b>가 두 번 공격하고 <b>협공 +20%</b>도 적용됩니다.")
                    { NoSpotlight = true, OnlyDieValue = 2, ActionLock = TutorialActionLock.MoveOnly, Advance = TutorialAdvance.Custom,
                      Done = () => combatProgress != null && combatProgress.RogueStage1Done },
                new TutorialStep("같은 구역의 다른 아군 한 명마다 도적의 <b>협공</b> 피해는 20% 증가합니다. 전사의 <b>방진</b>은 같은 구역 아군이 받는 공격 피해를 한 명마다 10% 줄입니다.")
                    { GateInput = true, OnEnter = PrepareFormationAttack },
                new TutorialStep("두 캐릭터가 같은 구역에 모였습니다. [턴 종료]를 눌러 몬스터 공격을 받고 방진을 확인하세요.")
                    { Target = EndTurn, Advance = TutorialAdvance.Custom, OnEnter = () => DiceManager.Instance?.SetScriptedRoll(new[] { 4, 1, 5, 6 }),
                      Done = () => cm == null || !cm.InCombat || !cm.PlayerTurnActive },
                new TutorialStep("몬스터가 예고한 타일을 차례로 공격합니다. 파란 슬라임의 박치기가 두 캐릭터를 덮칠 때 <b>방진 -10%</b>가 적용됩니다.")
                    { NoSpotlight = true, Advance = TutorialAdvance.Custom,
                      Done = () => cm == null || !cm.InCombat || cm.PlayerTurnActive },
                new TutorialStep("두 번째 턴입니다. 전사를 선택하세요. 이번 눈 4로 콤보 2단계를 발동해 보겠습니다.")
                    { Target = W, Advance = TutorialAdvance.Custom,
                      Done = () => CharacterActionUI.Instance != null && CharacterActionUI.Instance.IsShowingCharacter(Warrior) },
                new TutorialStep("전사에게 <b>눈 4</b>를 배정하고 이동하세요. 2단계 <b>균열 베기</b>가 현재 구역과 양옆 구역을 함께 공격합니다.")
                    { NoSpotlight = true, OnlyDieValue = 4, ActionLock = TutorialActionLock.MoveOnly, Advance = TutorialAdvance.Custom,
                      Done = () => combatProgress != null && combatProgress.WarriorStage2Done },
                new TutorialStep("균열 베기처럼 공격 범위가 넓어지면 어느 구역으로 이동하느냐에 따라 맞는 몬스터가 달라집니다.")
                    { Target = Field, GateInput = true },
                new TutorialStep("도적은 이번 턴에 움직이지 않고 넘어가겠습니다. [턴 종료]를 누르세요.")
                    { Target = EndTurn, Advance = TutorialAdvance.Custom, OnEnter = () => DiceManager.Instance?.SetScriptedRoll(new[] { 4, 2, 3, 6 }),
                      Done = () => cm == null || !cm.InCombat || !cm.PlayerTurnActive },
                new TutorialStep("몬스터의 두 번째 행동이 끝날 때까지 기다리세요.")
                    { NoSpotlight = true, Advance = TutorialAdvance.Custom,
                      Done = () => cm == null || !cm.InCombat || cm.PlayerTurnActive },
                new TutorialStep("도적처럼 한 턴 동안 움직이지 않으면 진행 중이던 콤보는 끊깁니다. 이제 전사의 마지막 단계를 확인해 봅시다.")
                    { GateInput = true },
                new TutorialStep("전사를 선택하세요.")
                    { Target = W, Advance = TutorialAdvance.Custom,
                      Done = () => CharacterActionUI.Instance != null && CharacterActionUI.Instance.IsShowingCharacter(Warrior) },
                new TutorialStep("전사에게 마지막 <b>눈 4</b>를 배정하고 이동하세요. 3단계 <b>대지 가르기</b>가 전장의 모든 몬스터를 공격합니다.")
                    { NoSpotlight = true, OnlyDieValue = 4, ActionLock = TutorialActionLock.MoveOnly, Advance = TutorialAdvance.Custom,
                      OnEnter = ReleaseFinaleTargets,
                      Done = () => (combatProgress != null && combatProgress.WarriorFinished) || (cm != null && !cm.InCombat) },
                new TutorialStep("대지 가르기는 모든 구역을 공격하고 모든 아군에게 방어도를 줍니다. 콤보를 끝까지 이었을 때 얻는 최종 보상입니다.")
                    { NoSpotlight = true, GateInput = true },
                new TutorialStep("마지막으로, 조건에 맞지 않는 주사위를 쓰거나 공격할 대상이 없는 구역으로 이동하거나 한 턴 동안 움직이지 않으면 진행 중인 콤보가 끊깁니다.")
                    { NoSpotlight = true, GateInput = true },
            };
        }

        /// <summary>데모 정리 — 실제 런 시작 전에 데모 파티/몬스터 제거(파티 0으로 → Recruit가 2명 선택으로 시작).</summary>
        public void Cleanup()
        {
            var combo = ComboSystem.Instance;
            if (combo != null && _onComboChanged != null) combo.OnComboChanged -= _onComboChanged;
            _onComboChanged = null;
            combatProgress = null;
            ScreenBoxProvider.ClearAll();
            HoverTooltipUI.Instance?.HidePinned();   // 데모 몬스터 위에 떠 있던 커서 툴팁이 얼어붙어 남지 않게
            DiceManager.Instance?.SetScriptedRoll(null);   // 잔여 통제 주사위가 실제 런으로 새지 않게
            DiceUI.Instance?.SetTutorialDiceLock(null);    // 주사위 잠금 해제(실제 런 무영향)
            CharacterActionUI.Instance?.SetTutorialActionLock(TutorialActionLock.None);

            if (Warrior?.Stats != null) Warrior.Stats.Invulnerable = false;   // 방어적 해제(스탯이 공유일 경우 대비)
            if (Rogue?.Stats != null) Rogue.Stats.Invulnerable = false;
            foreach (var monster in demoMonsters)
                if (monster?.Stats != null) monster.Stats.Invulnerable = false;
            PartyManager.Instance?.ClearAll();   // 데모 파티 제거 + 오브젝트 파괴
            foreach (var m in UnityEngine.Object.FindObjectsByType<Monster>(FindObjectsSortMode.None))
                if (m != null) Destroy(m.gameObject);
            demoMonsters.Clear();
            Warrior = null; Rogue = null; DemoMonster = null;
        }
    }
}
