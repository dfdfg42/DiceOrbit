using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.Core;
using DiceOrbit.Core.Combo;
using DiceOrbit.UI.Skin;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 캐릭터 체력바 위 행동 표시 — 이동 토큰 + 콤보 보석 세 칸 (2026-10-05, 그 전에는 흐린 아이콘 하나와 회색 네모 셋이었다).
    ///
    ///   받침판 [ 장화 | ◆ ◆ ★ ]
    ///
    /// - 이동 토큰: 이번 턴에 아직 움직일 수 있으면 밝은 날개 장화, 이동을 썼으면 회색 장화(조금 작게).
    /// - 콤보: 채운 단계만큼 보석이 켜진다. 마지막 칸은 별 — 두 칸을 채우면 별 자리가 두근거린다(다음 공격이 마무리).
    ///   마무리 발동 = 셋 다 켜지며 팡, 끊김 = 붉게 물들며 흔들리고 꺼진다.
    ///
    /// 평상시 모습은 매 프레임 ComboSystem.PeekStage / CombatManager.CanSpendMove를 그대로 그린다 — 내부 상태와 어긋날 수 없다.
    /// 이벤트(OnComboChanged)는 마무리·끊김의 짧은 연출에만 쓴다. 그림은 UiSkin의 「전투 머리 위 표시」 칸에서 온다.
    /// </summary>
    public class CharacterActionStrip : MonoBehaviour
    {
        // 배치 (월드 캔버스 단위 — 캐릭터 체력바 폭이 약 100)
        private const float PlateHeight = 22f;
        private const float PlatePadding = 5f;
        private const float MoveSize = 20f;
        private const float GemSize = 16f;
        private const float FinisherSize = 19f;
        private const float GroupGap = 5f;     // 이동 토큰과 콤보 사이
        private const float GemGap = 2f;
        private const float PlateBorderScale = 4.8f;   // 받침판 9-slice 모서리가 판 높이에 맞게

        private const float PopDuration = 0.22f;
        private const float PopScale = 1.45f;
        private const float FxDuration = 0.45f;
        private static readonly Color BreakTint = new Color(1f, 0.45f, 0.42f, 1f);

        private Character _character;
        private Image _move;
        private Image[] _gems;          // 마지막 칸 = 마무리(별)
        private float[] _popUntil;      // 칸마다 '팡' 연출이 끝나는 시각
        private int _shownStage = -1;
        private bool _shownCanMove;
        private bool _moveInitialized;
        private bool _fxActive;

        /// <summary>worldCanvas 아래에 표시줄을 만든다. leftCenter = 판의 왼쪽 끝 가운데 (캔버스 중심 기준).</summary>
        public static CharacterActionStrip Attach(Canvas worldCanvas, Character character, Vector2 leftCenter)
        {
            if (worldCanvas == null) throw new System.ArgumentNullException(nameof(worldCanvas));
            if (character == null) throw new System.ArgumentNullException(nameof(character));

            var go = new GameObject("ActionStrip", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(worldCanvas.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = leftCenter;

            var strip = go.AddComponent<CharacterActionStrip>();
            strip._character = character;
            strip.Build(rect);
            return strip;
        }

        private void Build(RectTransform root)
        {
            var skin = UiSkin.Current;
            Require(skin.MoveToken, nameof(skin.MoveToken));
            Require(skin.MoveTokenSpent, nameof(skin.MoveTokenSpent));
            Require(skin.ComboGemEmpty, nameof(skin.ComboGemEmpty));
            Require(skin.ComboGemLit, nameof(skin.ComboGemLit));
            Require(skin.ComboFinisherEmpty, nameof(skin.ComboFinisherEmpty));
            Require(skin.ComboFinisherLit, nameof(skin.ComboFinisherLit));
            Require(skin.TokenPlate, nameof(skin.TokenPlate));

            int stages = ComboTracker.StageCount;
            float width = PlatePadding + MoveSize + GroupGap + (stages - 1) * (GemSize + GemGap) + FinisherSize + PlatePadding;
            root.sizeDelta = new Vector2(width, PlateHeight);

            var plate = MakeImage(root, "Plate", skin.TokenPlate, Vector2.zero, Vector2.zero);
            plate.rectTransform.anchorMin = Vector2.zero;
            plate.rectTransform.anchorMax = Vector2.one;
            plate.rectTransform.offsetMin = plate.rectTransform.offsetMax = Vector2.zero;
            plate.type = Image.Type.Sliced;
            plate.pixelsPerUnitMultiplier = PlateBorderScale;
            plate.preserveAspect = false;

            float x = PlatePadding + MoveSize * 0.5f;
            _move = MakeImage(root, "MoveToken", skin.MoveToken, new Vector2(x, 0f), new Vector2(MoveSize, MoveSize));
            x += MoveSize * 0.5f + GroupGap;

            _gems = new Image[stages];
            _popUntil = new float[stages];
            for (int i = 0; i < stages; i++)
            {
                bool finisher = i == stages - 1;
                float size = finisher ? FinisherSize : GemSize;
                x += size * 0.5f;
                _gems[i] = MakeImage(root, finisher ? "ComboFinisher" : $"ComboGem{i + 1}",
                    finisher ? skin.ComboFinisherEmpty : skin.ComboGemEmpty, new Vector2(x, 0f), new Vector2(size, size));
                x += size * 0.5f + GemGap;
            }

            ComboSystem.EnsureInstance().OnComboChanged += HandleComboChanged;
        }

        private void OnDestroy()
        {
            if (ComboSystem.Instance != null) ComboSystem.Instance.OnComboChanged -= HandleComboChanged;
        }

        private static Image MakeImage(RectTransform parent, string imageName, Sprite sprite, Vector2 leftCenterOffset, Vector2 size)
        {
            var go = new GameObject(imageName, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);   // 판의 왼쪽 끝 가운데 기준
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = leftCenterOffset;
            rect.sizeDelta = size;

            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static void Require(Sprite sprite, string field)
        {
            if (sprite == null)
                throw new System.InvalidOperationException($"[CharacterActionStrip] UiSkin.{field}가 비어 있습니다 — 「도구/Dice Orbit/UI 스킨 점검」으로 확인하세요.");
        }

        private void Update()
        {
            if (_character == null || _gems == null) return;
            DrawMove();
            if (!_fxActive) DrawCombo();
            AnimateGems();
        }

        private void DrawMove()
        {
            var combat = CombatManager.Instance;
            bool canMove = combat != null && combat.CanSpendMove(_character);
            if (_moveInitialized && canMove == _shownCanMove) return;
            _moveInitialized = true;
            _shownCanMove = canMove;

            var skin = UiSkin.Current;
            _move.sprite = canMove ? skin.MoveToken : skin.MoveTokenSpent;
            _move.rectTransform.localScale = Vector3.one * (canMove ? 1f : 0.86f);
        }

        private void DrawCombo()
        {
            int stage = ComboSystem.Instance != null ? ComboSystem.Instance.PeekStage(_character) : 0;
            if (stage == _shownStage) return;

            var skin = UiSkin.Current;
            for (int i = 0; i < _gems.Length; i++)
            {
                bool finisher = i == _gems.Length - 1;
                bool lit = i < stage;
                _gems[i].sprite = lit ? (finisher ? skin.ComboFinisherLit : skin.ComboGemLit)
                                      : (finisher ? skin.ComboFinisherEmpty : skin.ComboGemEmpty);
                _gems[i].color = Color.white;
                if (lit && i >= _shownStage && _shownStage >= 0) _popUntil[i] = Time.unscaledTime + PopDuration;   // 새로 켜진 칸은 팡
            }
            _shownStage = stage;
        }

        /// <summary>칸 크기 연출 — 새로 켜진 칸의 팡, 마무리 직전 별 자리의 두근거림.</summary>
        private void AnimateGems()
        {
            float now = Time.unscaledTime;
            int last = _gems.Length - 1;
            for (int i = 0; i < _gems.Length; i++)
            {
                float scale = 1f;
                if (now < _popUntil[i])
                {
                    float k = 1f - (_popUntil[i] - now) / PopDuration;
                    scale = Mathf.Lerp(PopScale, 1f, 1f - (1f - k) * (1f - k));
                }
                else if (i == last && !_fxActive && _shownStage == last)
                {
                    scale = 1f + 0.12f * (0.5f + 0.5f * Mathf.Sin(now * 7f));   // 다음 공격이 마무리다
                }
                _gems[i].rectTransform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private void HandleComboChanged(Character changed, int stage, ComboOutcome outcome)
        {
            if (changed != _character || _gems == null || !isActiveAndEnabled) return;

            if (outcome == ComboOutcome.Finished)
                StartCoroutine(FinishFx());
            else if (outcome == ComboOutcome.BrokenByDice || outcome == ComboOutcome.BrokenByNoTarget || outcome == ComboOutcome.BrokenByNoMove)
                StartCoroutine(BreakFx());
        }

        /// <summary>마무리 발동 — 세 칸이 모두 켜지며 팡, 잠깐 뒤 평상시(0단계)로.</summary>
        private IEnumerator FinishFx()
        {
            _fxActive = true;
            var skin = UiSkin.Current;
            for (int i = 0; i < _gems.Length; i++)
            {
                _gems[i].sprite = i == _gems.Length - 1 ? skin.ComboFinisherLit : skin.ComboGemLit;
                _gems[i].color = Color.white;
                _popUntil[i] = Time.unscaledTime + PopDuration + i * 0.05f;
            }
            yield return new WaitForSecondsRealtime(FxDuration);
            EndFx();
        }

        /// <summary>끊김 — 붉게 물들며 좌우로 떨리고 꺼진다.</summary>
        private IEnumerator BreakFx()
        {
            _fxActive = true;
            float until = Time.unscaledTime + FxDuration;
            var root = (RectTransform)transform;
            Vector2 home = root.anchoredPosition;
            foreach (var gem in _gems) gem.color = BreakTint;
            while (Time.unscaledTime < until)
            {
                float left = (until - Time.unscaledTime) / FxDuration;
                root.anchoredPosition = home + new Vector2(Mathf.Sin(Time.unscaledTime * 60f) * 1.6f * left, 0f);
                yield return null;
            }
            root.anchoredPosition = home;
            EndFx();
        }

        private void EndFx()
        {
            _fxActive = false;
            _shownStage = -1;   // 다음 프레임에 실제 단계로 다시 그린다
        }
    }
}
