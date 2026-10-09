using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.Core;
using DiceOrbit.Core.Run;
using DiceOrbit.Data.Modifiers;
using DiceOrbit.Data.Modifiers.Common;
using DiceOrbit.UI.Skin;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 전투 클리어 보상 화면 — "전리품 시트 3박자" (보상 리워크 2026-10-03).
    /// 스펙: Docs/superpowers/specs/2026-10-03-reward-screen-rework-design.md
    ///
    ///   ① 전리품 (자동)  골드·유물·포션이 칩으로 붙는다. 클릭 없음.
    ///   ② 강화 (결정)    공용 모디파이어 카드 3장 + 파티 초상 → [○○에게 장착]
    ///   ③ 주사위 (결정)  새 주사위 카드 ↔ 내 덱 카드 → [교체] / [받지 않기]
    ///   마지막 결정이 끝나면 도장 연출 뒤 자동으로 다음 상태. 못 받은 포션이 남으면 [계속]을 거친다.
    ///
    /// 에디터 소유 패턴: 시트·박자 몸통·템플릿은 씬에 있고(메뉴 [DiceOrbit/Rebuild Reward UI Layout]으로 재생성),
    /// 이 코드는 내용만 채운다. 슬롯이 비면 에러 — 런타임 생성 폴백은 없다.
    /// 보상은 진입 시 <see cref="RewardRoller"/>가 한 번만 굴린다 (재굴림 불가). 박자 순서는 <see cref="RewardFlow"/>.
    /// </summary>
    public class RewardUI : MonoBehaviour
    {
        [Header("Tuning")]
        [SerializeField] private int goldPerReward = 50;
        [SerializeField] private int modifierChoiceCount = 3;
        [Range(0f, 1f)]
        [SerializeField] private float potionDropChance = 0.35f;   // 전투 보상 포션 드랍 (스펙 §6, 2026-08-28 20%→35% 상향)

        [Header("씬 배선 — 시트 (비어 있으면 에러, 코드 생성 폴백 없음)")]
        [SerializeField] private Canvas rewardCanvas;
        [SerializeField] private RectTransform sheet;
        [SerializeField] private CanvasGroup sheetGroup;
        [SerializeField] private RectTransform stepRow;
        [SerializeField] private RectTransform lootRow;
        [SerializeField] private GameObject discardRow;            // "버릴 포션 고르기" 줄 (가방 가득일 때만)
        [SerializeField] private RectTransform discardChipRow;

        [Header("씬 배선 — 강화 박자")]
        [SerializeField] private CanvasGroup upgradeBody;
        [SerializeField] private TextMeshProUGUI upgradePrompt;
        [SerializeField] private RectTransform cardRow;
        [SerializeField] private RectTransform partyRow;

        [Header("씬 배선 — 주사위 박자")]
        [SerializeField] private CanvasGroup diceBody;
        [SerializeField] private TextMeshProUGUI dicePrompt;
        [SerializeField] private RewardDieCard newDieCard;
        [SerializeField] private RectTransform deckRow;

        [Header("씬 배선 — 마무리 · 풋터 · 도장")]
        [SerializeField] private CanvasGroup wrapBody;
        [SerializeField] private TextMeshProUGUI wrapMessage;
        [SerializeField] private Button primaryButton;
        [SerializeField] private TextMeshProUGUI primaryLabel;
        [SerializeField] private Button secondaryButton;
        [SerializeField] private TextMeshProUGUI secondaryLabel;
        [SerializeField] private TextMeshProUGUI hintText;
        [SerializeField] private RectTransform stamp;
        [SerializeField] private CanvasGroup stampGroup;
        [SerializeField] private TextMeshProUGUI stampLabel;

        [Header("씬 배선 — 템플릿 (비활성 오브젝트, 런타임에 복제)")]
        [SerializeField] private RewardLootChip lootChipTemplate;
        [SerializeField] private RewardStepPip stepPipTemplate;
        [SerializeField] private RewardModifierCard modifierCardTemplate;
        [SerializeField] private RewardPartyPortrait portraitTemplate;
        [SerializeField] private RewardDieCard dieCardTemplate;

        private const float SkipConfirmSeconds = 2f;
        private const float BodyFadeSeconds = 0.15f;

        private static UiSkin Skin => UiSkin.Current;

        // ── 런타임 상태 ────────────────────────────────────────
        private RewardBundle _bundle;
        private RewardFlow _flow;
        private readonly List<Character> _party = new List<Character>();

        private readonly List<RewardLootChip> _lootChips = new List<RewardLootChip>();
        private RewardLootChip _goldChip;
        private RewardLootChip _potionChip;
        private bool _potionClaimed;
        private bool _claimingPotion;
        private bool _lootShown;        // 인트로(칩 등장)가 끝났다

        private readonly List<RewardStepPip> _stepPips = new List<RewardStepPip>();
        private readonly List<RewardModifierCard> _cards = new List<RewardModifierCard>();
        private readonly List<RewardPartyPortrait> _portraits = new List<RewardPartyPortrait>();
        private readonly List<RewardDieCard> _deckCards = new List<RewardDieCard>();

        private int _selectedOffer = -1;
        private Character _selectedCharacter;
        private int _selectedDeckIndex = -1;

        private bool _busy;             // 연출 중 — 입력을 받지 않는다
        private bool _skipArmed;        // [건너뛰기] 1회 눌림 (2초 안에 다시 눌러야 확정)
        private Coroutine _skipRoutine;
        private bool _wired;

        // ═══════════════════════════════════════════════════════
        // 공개 API (GameFlowManager가 부른다)
        // ═══════════════════════════════════════════════════════

        public void Show()
        {
            gameObject.SetActive(true);
            if (!EnsureSceneRefs()) return;
            rewardCanvas.gameObject.SetActive(true);
            BattleInfoPanelUI.SetVisible(false);   // 보상 화면 동안 정보 패널 숨김

            StopAllCoroutines();
            ResetView();

            _bundle = RewardRoller.Roll(new RewardRollSettings
            {
                GoldPerReward = goldPerReward,
                ModifierChoiceCount = modifierChoiceCount,
                PotionDropChance = potionDropChance,
            });

            CollectAliveParty();
            var deck = DiceDeckManager.Instance != null ? DiceDeckManager.Instance.Deck : null;
            bool hasUpgrade = _bundle.ModifierOffers.Count > 0 && _party.Count > 0;
            bool hasDice = _bundle.NewDie != null && deck != null && deck.Count > 0;
            _flow = new RewardFlow(hasUpgrade, hasDice);

            ClaimLoot();
            BuildLootChips();
            BuildSteps();

            var potions = PotionManager.Instance;
            if (potions != null) { potions.OnChanged -= OnPotionsChanged; potions.OnChanged += OnPotionsChanged; }

            StartCoroutine(Intro());
        }

        public void Hide()
        {
            StopAllCoroutines();
            _busy = false;
            if (PotionManager.Instance != null) PotionManager.Instance.OnChanged -= OnPotionsChanged;
            HoverTooltipUI.Instance?.HidePinned();

            if (rewardCanvas != null) rewardCanvas.gameObject.SetActive(false);
            gameObject.SetActive(false);
            BattleInfoPanelUI.SetVisible(true);    // 전투 복귀 시 정보 패널 복원
        }

        // ═══════════════════════════════════════════════════════
        // 준비
        // ═══════════════════════════════════════════════════════

        /// <summary>씬 배선 검증 + 버튼 1회 연결. 폴백 생성은 없다 — 빠졌으면 배선 문제를 드러낸다.</summary>
        private bool EnsureSceneRefs()
        {
            bool complete =
                rewardCanvas != null && sheet != null && sheetGroup != null && stepRow != null && lootRow != null
                && discardRow != null && discardChipRow != null
                && upgradeBody != null && upgradePrompt != null && cardRow != null && partyRow != null
                && diceBody != null && dicePrompt != null && newDieCard != null && deckRow != null
                && wrapBody != null && wrapMessage != null
                && primaryButton != null && primaryLabel != null && secondaryButton != null && secondaryLabel != null && hintText != null
                && stamp != null && stampGroup != null && stampLabel != null
                && lootChipTemplate != null && stepPipTemplate != null && modifierCardTemplate != null
                && portraitTemplate != null && dieCardTemplate != null;

            if (!complete)
            {
                Debug.LogError("[RewardUI] 씬 배선이 비어 있다 — 메뉴 [DiceOrbit/Rebuild Reward UI Layout]으로 RewardCanvas를 다시 만들 것 (코드 생성 폴백은 없다).");
                return false;
            }

            if (!_wired)
            {
                _wired = true;
                primaryButton.onClick.AddListener(OnPrimaryClicked);
                secondaryButton.onClick.AddListener(OnSecondaryClicked);
            }
            return true;
        }

        private void ResetView()
        {
            _busy = false;
            _skipArmed = false;
            _skipRoutine = null;
            _selectedOffer = -1;
            _selectedCharacter = null;
            _selectedDeckIndex = -1;
            _goldChip = null;
            _potionChip = null;
            _lootShown = false;

            ClearChildren(stepRow);      _stepPips.Clear();
            ClearChildren(lootRow);      _lootChips.Clear();
            ClearChildren(discardChipRow);
            ClearChildren(cardRow);      _cards.Clear();
            ClearChildren(partyRow);     _portraits.Clear();
            ClearChildren(deckRow);      _deckCards.Clear();

            discardRow.SetActive(false);
            SetBody(upgradeBody, false);
            SetBody(diceBody, false);
            SetBody(wrapBody, false);

            sheetGroup.alpha = 0f;
            stampGroup.alpha = 0f;
            hintText.text = string.Empty;
            primaryButton.gameObject.SetActive(false);
            secondaryButton.gameObject.SetActive(false);
        }

        private void CollectAliveParty()
        {
            _party.Clear();
            var party = PartyManager.Instance != null ? PartyManager.Instance.Party : null;
            if (party == null) return;
            foreach (var ch in party)
                if (ch != null && ch.IsAlive) _party.Add(ch);
        }

        // ═══════════════════════════════════════════════════════
        // ① 전리품 — 자동 수령
        // ═══════════════════════════════════════════════════════

        private void ClaimLoot()
        {
            GoldManager.EnsureInstance().AddGold(_bundle.Gold);

            if (_bundle.Artifact != null)
                ArtifactManager.EnsureInstance().Grant(_bundle.Artifact);

            _potionClaimed = _bundle.Potion == null || PotionManager.EnsureInstance().TryAdd(_bundle.Potion);
            _flow.HasBlockedLoot = !_potionClaimed;
        }

        private void BuildLootChips()
        {
            _goldChip = AddLootChip(lootRow);
            _goldChip.Bind(Skin.Coin, "+0", $"골드 {_bundle.Gold} 획득");

            if (_bundle.Artifact != null)
            {
                var chip = AddLootChip(lootRow);
                chip.Bind(_bundle.Artifact.artifactIcon, _bundle.Artifact.artifactName, _bundle.Artifact.artifactTooltip);
            }

            if (_bundle.Potion != null)
            {
                _potionChip = AddLootChip(lootRow);
                RefreshPotionChip();
            }

            foreach (var chip in _lootChips) chip.Group.alpha = 0f;   // 인트로에서 차례로 나타난다
        }

        private RewardLootChip AddLootChip(RectTransform row)
        {
            var chip = Instantiate(lootChipTemplate, row);
            chip.gameObject.SetActive(true);
            if (row == lootRow) _lootChips.Add(chip);
            return chip;
        }

        private void RefreshPotionChip()
        {
            var potion = _bundle.Potion;
            if (_potionClaimed)
            {
                _potionChip.Bind(potion.Icon, potion.PotionName, potion.Description);
                _potionChip.SetBlocked(false);
                discardRow.SetActive(false);
            }
            else
            {
                _potionChip.Bind(potion.Icon, $"{potion.PotionName} · 가득", $"가방이 가득 차 받지 못했습니다\n\n{potion.Description}");
                _potionChip.SetBlocked(true);
                if (_lootShown) BuildDiscardRow();   // 인트로가 끝나 칩이 다 나온 뒤에 "버릴 포션" 줄을 연다
            }
            RefreshPrompts();
        }

        /// <summary>가방이 가득 찼을 때: 가진 포션을 누르면 그것을 버리고 새 포션을 받는다.</summary>
        private void BuildDiscardRow()
        {
            ClearChildren(discardChipRow);
            var slots = PotionManager.Instance.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                int index = i;
                var held = slots[i];
                var chip = AddLootChip(discardChipRow);
                chip.Bind(held.Icon, held.PotionName, $"버리고 {_bundle.Potion.PotionName}을(를) 받습니다\n\n{held.Description}",
                    () => OnDiscardPicked(index));
            }
            discardRow.SetActive(true);
        }

        private void OnDiscardPicked(int index)
        {
            if (_busy || _potionClaimed) return;
            _claimingPotion = true;   // Discard·TryAdd가 OnChanged를 쏜다 — 재진입으로 두 번 받지 않게
            PotionManager.Instance.Discard(index);
            bool added = PotionManager.Instance.TryAdd(_bundle.Potion);
            _claimingPotion = false;
            if (added) OnPotionClaimedLate();
        }

        /// <summary>HUD 등 다른 곳에서 포션 슬롯이 비면 막혔던 포션을 바로 받는다.</summary>
        private void OnPotionsChanged()
        {
            if (_potionClaimed || _claimingPotion || _bundle == null || _bundle.Potion == null) return;
            if (!PotionManager.Instance.HasFreeSlot) { BuildDiscardRow(); return; }

            _claimingPotion = true;
            bool added = PotionManager.Instance.TryAdd(_bundle.Potion);
            _claimingPotion = false;
            if (added) OnPotionClaimedLate();
        }

        private void OnPotionClaimedLate()
        {
            _potionClaimed = true;
            _flow.HasBlockedLoot = false;
            RefreshPotionChip();

            if (_busy) return;
            if (_flow.IsFinished)
            {
                // 막힌 포션 때문에 머물던 마무리 박자였다 — 받았으니 도장으로 알리고 넘어간다
                StartCoroutine(FinishWithStamp("획득!"));
                return;
            }
            if (_flow.Current == RewardBeat.Wrap) BuildWrap();
            RefreshFooter();
        }

        private IEnumerator FinishWithStamp(string stampText)
        {
            _busy = true;
            primaryButton.interactable = false;
            SetBody(wrapBody, false);
            stampLabel.text = stampText;
            yield return UiMotion.Stamp(stamp, stampGroup);
            Complete();
        }

        // ═══════════════════════════════════════════════════════
        // 진행 표시
        // ═══════════════════════════════════════════════════════

        private void BuildSteps()
        {
            AddStep("전리품");
            foreach (var beat in _flow.ChoiceBeats)
                AddStep(beat == RewardBeat.Upgrade ? "강화" : "주사위");
            RefreshSteps(lootDone: false);
        }

        private void AddStep(string text)
        {
            var pip = Instantiate(stepPipTemplate, stepRow);
            pip.gameObject.SetActive(true);
            pip.Bind(text);
            _stepPips.Add(pip);
        }

        private void RefreshSteps(bool lootDone)
        {
            _stepPips[0].SetState(lootDone ? RewardStepPip.State.Done : RewardStepPip.State.Current);

            var beats = _flow.ChoiceBeats;
            int currentIndex = -1;
            for (int i = 0; i < beats.Count; i++)
                if (lootDone && beats[i] == _flow.Current) currentIndex = i;
            bool pastChoices = lootDone && currentIndex < 0;   // Wrap/Finished — 결정 박자는 전부 끝났다

            for (int i = 0; i < beats.Count; i++)
            {
                var state = pastChoices || (currentIndex >= 0 && i < currentIndex) ? RewardStepPip.State.Done
                          : i == currentIndex ? RewardStepPip.State.Current
                          : RewardStepPip.State.Upcoming;
                _stepPips[i + 1].SetState(state);
            }
        }

        // ═══════════════════════════════════════════════════════
        // 박자 전환
        // ═══════════════════════════════════════════════════════

        private IEnumerator Intro()
        {
            _busy = true;
            yield return UiMotion.PopIn(sheet, sheetGroup);

            foreach (var chip in _lootChips)
            {
                yield return UiMotion.PopIn(chip.Rect, chip.Group, 0.12f, 0.8f);
                if (chip == _potionChip && !_potionClaimed) chip.SetBlocked(true);   // 팝인이 알파를 1로 올린 뒤 다시 흐리게
                if (chip == _goldChip)
                    yield return UiMotion.CountUp(chip.Label, 0, _bundle.Gold, 0.4f);
            }

            _lootShown = true;
            if (_potionChip != null && !_potionClaimed) { BuildDiscardRow(); RefreshPrompts(); }

            _busy = false;
            EnterBeat();
        }

        private void EnterBeat()
        {
            RefreshSteps(lootDone: true);

            switch (_flow.Current)
            {
                // 몸통을 먼저 켠 뒤 채운다 — 켜지는 순간 UiSkinButton.Awake가 스프라이트를 보통 상태로 되돌리므로
                case RewardBeat.Upgrade:
                    OpenBody(upgradeBody);
                    BuildUpgrade();
                    break;
                case RewardBeat.Dice:
                    OpenBody(diceBody);
                    BuildDice();
                    break;
                case RewardBeat.Wrap:
                    OpenBody(wrapBody);
                    BuildWrap();
                    break;
                default:
                    Complete();
                    return;
            }
            RefreshPrompts();
            RefreshFooter();
        }

        private void OpenBody(CanvasGroup body)
        {
            SetBody(body, true);
            body.alpha = 0f;
            StartCoroutine(UiMotion.Fade(body, 1f, BodyFadeSeconds));
        }

        /// <summary>현재 박자를 끝내고 다음으로. stampText가 있으면 도장을 찍고 넘어간다.</summary>
        private IEnumerator ResolveBeat(string stampText)
        {
            _busy = true;
            DisarmSkip();
            RefreshFooter();

            if (!string.IsNullOrEmpty(stampText))
            {
                stampLabel.text = stampText;
                yield return UiMotion.Stamp(stamp, stampGroup);
            }

            var body = BodyOf(_flow.Current);
            _flow.Resolve();
            if (body != null)
            {
                yield return UiMotion.Fade(body, 0f, BodyFadeSeconds);
                SetBody(body, false);
            }

            _busy = false;
            EnterBeat();
        }

        private void Complete()
        {
            _busy = true;   // 다음 상태로 넘어가는 중 — 추가 입력 무시 (Hide가 풀어 준다)
            GameFlowManager.Instance?.OnRewardComplete();
        }

        private CanvasGroup BodyOf(RewardBeat beat)
        {
            switch (beat)
            {
                case RewardBeat.Upgrade: return upgradeBody;
                case RewardBeat.Dice: return diceBody;
                case RewardBeat.Wrap: return wrapBody;
                default: return null;
            }
        }

        // ═══════════════════════════════════════════════════════
        // ② 강화 박자
        // ═══════════════════════════════════════════════════════

        private void BuildUpgrade()
        {
            ClearChildren(cardRow); _cards.Clear();
            ClearChildren(partyRow); _portraits.Clear();

            var offers = _bundle.ModifierOffers;
            for (int i = 0; i < offers.Count; i++)
            {
                int index = i;
                var card = Instantiate(modifierCardTemplate, cardRow);
                card.gameObject.SetActive(true);
                card.Bind(offers[i], () => OnCardClicked(index));
                _cards.Add(card);
            }

            foreach (var ch in _party)
            {
                var captured = ch;
                var portrait = Instantiate(portraitTemplate, partyRow);
                portrait.gameObject.SetActive(true);
                portrait.Bind(ch, () => OnPortraitClicked(captured));
                portrait.SetTooltip(DescribeModifiers(ch));
                _portraits.Add(portrait);
            }

            _selectedOffer = -1;
            _selectedCharacter = FirstWithAnyEligibleOffer() ?? _party[0];
            RefreshUpgrade();
        }

        /// <summary>기본 대상 — 제시된 카드 중 하나라도 받을 수 있는 첫 캐릭터 (전부 장착 불가인 캐릭터로 시작하지 않게).</summary>
        private Character FirstWithAnyEligibleOffer()
        {
            foreach (var ch in _party)
                foreach (var offer in _bundle.ModifierOffers)
                    if (offer.CanApplyTo(ch)) return ch;
            return null;
        }

        private void OnCardClicked(int index)
        {
            if (_busy) return;
            DisarmSkip();

            if (_selectedOffer == index)
            {
                _selectedOffer = -1;   // 다시 누르면 선택 해제
            }
            else
            {
                var offer = _bundle.ModifierOffers[index];
                if (!offer.CanApplyTo(_selectedCharacter))
                {
                    // 지금 고른 캐릭터가 못 받는 카드 — 받을 수 있는 첫 캐릭터로 대상을 옮긴다
                    var other = FirstEligible(offer);
                    if (other == null) return;
                    _selectedCharacter = other;
                }
                _selectedOffer = index;
            }

            RefreshUpgrade();
            RefreshFooter();
        }

        private void OnPortraitClicked(Character character)
        {
            if (_busy) return;
            DisarmSkip();
            _selectedCharacter = character;
            RefreshUpgrade();
            RefreshFooter();
        }

        private void RefreshUpgrade()
        {
            var offers = _bundle.ModifierOffers;
            string who = RewardPartyPortrait.DisplayName(_selectedCharacter);

            for (int i = 0; i < _cards.Count; i++)
            {
                var offer = offers[i];
                bool owned = ModifierRegistry.CountOn(_selectedCharacter, offer) > 0;
                bool can = offer.CanApplyTo(_selectedCharacter);
                string status = owned ? $"{who} · 보유 중"
                              : !can ? $"{who} · 장착 불가"
                              : $"{who} · 신규";
                _cards[i].SetStatus(status, can);
                _cards[i].SetSelected(i == _selectedOffer);
                _cards[i].SetDimmed(_selectedOffer >= 0 && i != _selectedOffer);
            }

            var picked = _selectedOffer >= 0 ? offers[_selectedOffer] : null;
            foreach (var portrait in _portraits)
            {
                var ch = portrait.Character;
                portrait.SetEligible(picked == null || picked.CanApplyTo(ch));
                portrait.SetSelected(ch == _selectedCharacter);
                portrait.SetPips(picked != null
                    ? StackPips(ModifierRegistry.CountOn(ch, picked))
                    : $"모디파이어 {ModifierCount(ch)}");
            }
        }

        private void ConfirmUpgrade()
        {
            var offer = _bundle.ModifierOffers[_selectedOffer];
            if (!offer.CanApplyTo(_selectedCharacter)) return;

            _selectedCharacter.Stats.Modifiers.Add(offer);
            Debug.Log($"[Reward] {RewardPartyPortrait.DisplayName(_selectedCharacter)} ← '{offer.ModifierName}' 장착");
            StartCoroutine(ResolveBeat("장착!"));
        }

        private Character FirstEligible(CharacterModifier offer)
        {
            foreach (var ch in _party)
                if (offer.CanApplyTo(ch)) return ch;
            return null;
        }

        private static string StackPips(int stacks)
        {
            const int max = 1;   // 같은 종류 중복 장착 불가 (2026-10-09 B) — 보유 ●, 미보유 ○
            stacks = Mathf.Clamp(stacks, 0, max);
            return new string('●', stacks) + new string('○', max - stacks);
        }

        private static int ModifierCount(Character character)
        {
            var mods = character.Stats != null && character.Stats.Modifiers != null ? character.Stats.Modifiers.Modifiers : null;
            return mods != null ? mods.Count : 0;
        }

        /// <summary>초상 호버 툴팁 — 장착한 모디파이어를 종류별로 묶어 "이름 ×N".</summary>
        private static string DescribeModifiers(Character character)
        {
            var mods = character.Stats != null && character.Stats.Modifiers != null ? character.Stats.Modifiers.Modifiers : null;
            string who = RewardPartyPortrait.DisplayName(character);
            if (mods == null || mods.Count == 0) return $"{who}\n장착한 모디파이어 없음";

            var names = new List<string>();
            var counts = new List<int>();
            foreach (var mod in mods)
            {
                if (mod == null) continue;
                int at = names.IndexOf(mod.ModifierName);
                if (at < 0) { names.Add(mod.ModifierName); counts.Add(1); }
                else counts[at]++;
            }

            var sb = new StringBuilder(who);
            for (int i = 0; i < names.Count; i++)
                sb.Append('\n').Append(names[i]).Append(counts[i] > 1 ? $" ×{counts[i]}" : string.Empty);
            return sb.ToString();
        }

        // ═══════════════════════════════════════════════════════
        // ③ 주사위 박자
        // ═══════════════════════════════════════════════════════

        private void BuildDice()
        {
            ClearChildren(deckRow); _deckCards.Clear();

            newDieCard.BindDefinition(_bundle.NewDie);
            newDieCard.SetSelected(true);   // 새 주사위는 강조 테두리로 고정 표시

            var deck = DiceDeckManager.Instance.Deck;
            for (int i = 0; i < deck.Count; i++)
            {
                int index = i;
                var card = Instantiate(dieCardTemplate, deckRow);
                card.gameObject.SetActive(true);
                card.BindInstance(deck[i], () => OnDeckCardClicked(index));
                _deckCards.Add(card);
            }

            _selectedDeckIndex = -1;
            RefreshDice();
        }

        private void OnDeckCardClicked(int index)
        {
            if (_busy) return;
            _selectedDeckIndex = _selectedDeckIndex == index ? -1 : index;
            RefreshDice();
            RefreshFooter();
        }

        private void RefreshDice()
        {
            for (int i = 0; i < _deckCards.Count; i++)
            {
                _deckCards[i].SetSelected(i == _selectedDeckIndex);
                _deckCards[i].SetDimmed(_selectedDeckIndex >= 0 && i != _selectedDeckIndex);
            }
        }

        private void ConfirmDice()
        {
            DiceDeckManager.Instance.Replace(_selectedDeckIndex, _bundle.NewDie);
            Debug.Log($"[Reward] 덱 {_selectedDeckIndex}번을 '{_bundle.NewDie.Name}'로 교체");
            StartCoroutine(ResolveBeat("교체!"));
        }

        // ═══════════════════════════════════════════════════════
        // 마무리 박자
        // ═══════════════════════════════════════════════════════

        private void BuildWrap()
        {
            wrapMessage.text = _flow.HasBlockedLoot
                ? $"가방이 가득 차 {_bundle.Potion.PotionName}을(를) 두고 갑니다.\n위에서 버릴 포션을 고르면 받을 수 있어요."
                : "전리품을 챙겼습니다.";
        }

        // ═══════════════════════════════════════════════════════
        // 풋터 · 안내
        // ═══════════════════════════════════════════════════════

        private void OnPrimaryClicked()
        {
            if (_busy) return;
            switch (_flow.Current)
            {
                case RewardBeat.Upgrade:
                    if (_selectedOffer >= 0) ConfirmUpgrade();
                    break;
                case RewardBeat.Dice:
                    if (_selectedDeckIndex >= 0) ConfirmDice();
                    break;
                case RewardBeat.Wrap:
                    StartCoroutine(ResolveBeat(null));
                    break;
            }
        }

        private void OnSecondaryClicked()
        {
            if (_busy) return;
            switch (_flow.Current)
            {
                case RewardBeat.Upgrade:
                    // 강화를 버리는 일은 드물다 — 오클릭으로 잃지 않게 두 번 눌러야 확정
                    if (!_skipArmed)
                    {
                        _skipArmed = true;
                        _skipRoutine = StartCoroutine(SkipConfirmWindow());
                        RefreshFooter();
                        return;
                    }
                    StartCoroutine(ResolveBeat(null));
                    break;
                case RewardBeat.Dice:
                    StartCoroutine(ResolveBeat(null));   // 주사위를 안 받는 것은 흔한 선택 — 한 번에
                    break;
            }
        }

        private IEnumerator SkipConfirmWindow()
        {
            float t = 0f;
            while (t < SkipConfirmSeconds) { t += Time.unscaledDeltaTime; yield return null; }
            _skipArmed = false;
            _skipRoutine = null;
            RefreshFooter();
        }

        private void DisarmSkip()
        {
            if (_skipRoutine != null) StopCoroutine(_skipRoutine);
            _skipRoutine = null;
            _skipArmed = false;
        }

        private void RefreshFooter()
        {
            var beat = _flow.Current;
            bool interactive = !_busy;

            primaryButton.gameObject.SetActive(beat != RewardBeat.Finished);
            secondaryButton.gameObject.SetActive(beat == RewardBeat.Upgrade || beat == RewardBeat.Dice);
            hintText.text = string.Empty;

            switch (beat)
            {
                case RewardBeat.Upgrade:
                    bool picked = _selectedOffer >= 0;
                    primaryLabel.text = picked ? $"{RewardPartyPortrait.DisplayName(_selectedCharacter)}에게 장착" : "카드를 고르세요";
                    primaryButton.interactable = interactive && picked;
                    secondaryLabel.text = _skipArmed ? "정말 건너뛸까요?" : "건너뛰기";
                    secondaryButton.interactable = interactive;
                    if (_skipArmed) hintText.text = "한 번 더 누르면 강화를 받지 않고 넘어갑니다.";
                    break;

                case RewardBeat.Dice:
                    bool chosen = _selectedDeckIndex >= 0;
                    primaryLabel.text = chosen ? "교체" : "바꿀 주사위를 고르세요";
                    primaryButton.interactable = interactive && chosen;
                    secondaryLabel.text = "받지 않기";
                    secondaryButton.interactable = interactive;
                    break;

                case RewardBeat.Wrap:
                    primaryLabel.text = "계속";
                    primaryButton.interactable = interactive;
                    break;
            }
        }

        /// <summary>"버릴 포션 고르기" 줄이 박자 안내문과 같은 자리를 쓴다 — 줄이 나와 있으면 안내문을 숨긴다.</summary>
        private void RefreshPrompts()
        {
            bool showPrompt = !discardRow.activeSelf;
            upgradePrompt.enabled = showPrompt;
            dicePrompt.enabled = showPrompt;
        }

        // ═══════════════════════════════════════════════════════
        // 도우미
        // ═══════════════════════════════════════════════════════

        private static void SetBody(CanvasGroup body, bool visible)
        {
            body.gameObject.SetActive(visible);
            body.alpha = visible ? 1f : 0f;
        }

        private static void ClearChildren(RectTransform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
                Destroy(root.GetChild(i).gameObject);
        }
    }
}
