using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 캐릭터 선택 카드
    /// </summary>
    public class CharacterCard : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image portraitImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private RectTransform bottleLabel;
        [SerializeField] private Button selectButton;

        [Header("Portrait Sprite")]
        [SerializeField] private Sprite defaultPortraitOverride;
        [SerializeField] private List<PortraitSpriteOption> portraitSprites = new List<PortraitSpriteOption>();

        [Header("Hover Settings")]
        [SerializeField] private float normalAlpha = 0.5f;
        [SerializeField] private float hoverAlpha = 1f;
        [SerializeField] private float hoverScale = 1.08f;
        [SerializeField] private float hoverScaleDuration = 0.12f;
        [SerializeField] private List<HoverPortraitSpriteOption> hoverPortraitSprites = new List<HoverPortraitSpriteOption>();

        [Header("Select Button Sprite")]
        [SerializeField] private Image selectButtonImage;
        [SerializeField] private Sprite defaultSelectButtonSprite;
        [SerializeField] private List<SelectButtonSpriteOption> selectButtonSprites = new List<SelectButtonSpriteOption>();

        [Header("Bottle Positions (Left / Center / Right)")]
        [SerializeField] private BottleAppearance[] bottleAppearances = new BottleAppearance[0];

        [System.Serializable]
        private class BottleAppearance
        {
            public Sprite sprite;
            public Vector2 namePosition;
            public float nameAngle;
        }

        [Header("Intro Animation")]
        [SerializeField] private float introOffsetY = 750f; //350
        [SerializeField] private float introDuration = 0.35f;
        [SerializeField] private AnimationCurve introCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Selection Animation")]
        [SerializeField] private float selectRiseOffsetY = 40f;
        [SerializeField] private float selectRiseDuration = 0.12f;
        [SerializeField] private float selectExitOffsetY = 900f;
        [SerializeField] private float selectExitDuration = 0.32f;
        [SerializeField] private AnimationCurve selectRiseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve selectExitCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Detail Slide Animation")]
        [SerializeField] private float detailSlideDuration = 0.32f;
        [SerializeField] private AnimationCurve detailSlideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Dim Settings")]
        [SerializeField] private float dimmedAlpha = 0.25f;
        [Tooltip("호버 전 카드 전체 투명도. 구 0.3은 병이 배경에 묻혀 안 보였다 (2026-09-26)")]
        [SerializeField, Range(0f, 1f)] private float restingGroupAlpha = 0.85f;
        [SerializeField, Range(0f, 1f)] private float hoverGroupAlpha = 1f;

        [Header("Tilt & Fall Animation")]
        [SerializeField] private float fallTiltAngle = 90f;
        [SerializeField] private float fallDropOffsetX = -260f;
        [SerializeField] private float fallDropOffsetY = 1100f;
        [SerializeField] private float fallDuration = 0.7f;
        [Range(0f, 1f)]
        [SerializeField] private float tiltCompleteAt = 0.55f;
        [Range(0f, 1f)]
        [SerializeField] private float dropStartAt = 0.15f;
        [SerializeField] private AnimationCurve fallTiltCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve fallDropCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2.5f, 2.5f));

        private Core.CharacterPreset character;
        private System.Action<CharacterCard, Core.CharacterPreset> onSelected;
        private Sprite defaultPortraitSprite;
        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private Vector2 introTargetPosition;
        private bool hasCapturedIntroTarget;
        private bool isDimmed;
        private bool hoverSuppressed;
        private Vector3 baseScale = Vector3.one;
        private Coroutine hoverScaleRoutine;

        [System.Serializable]
        private class SelectButtonSpriteOption
        {
            public string characterName;
            public Sprite sprite;
        }

        [System.Serializable]
        private class PortraitSpriteOption
        {
            public string characterName;
            public Sprite sprite;
        }

        [System.Serializable]
        private class HoverPortraitSpriteOption
        {
            public string characterName;
            public Sprite sprite;
        }
        
        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (rectTransform != null)
            {
                baseScale = rectTransform.localScale;
            }

            if (selectButton != null)
            {
                selectButton.onClick.AddListener(OnSelectClicked);
            }

            // Ensure initial alpha and register pointer events so hover works
            ApplyAlphaToUI(normalAlpha);
            RegisterPointerEvents();
        }

        public void CaptureIntroTargetPosition()
        {
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            if (rectTransform == null)
            {
                return;
            }

            introTargetPosition = rectTransform.anchoredPosition;
            hasCapturedIntroTarget = true;
        }

        public Coroutine PlayIntro(float startDelay)
        {
            return StartCoroutine(PlayIntroRoutine(startDelay));
        }

        private IEnumerator PlayIntroRoutine(float startDelay)
        {
            if (!hasCapturedIntroTarget)
            {
                CaptureIntroTargetPosition();
            }

            if (rectTransform == null)
            {
                yield break;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            yield return new WaitForSecondsRealtime(startDelay);

            var startPosition = introTargetPosition + new Vector2(0f, -introOffsetY);
            rectTransform.anchoredPosition = startPosition;

            var elapsed = 0f;
            while (elapsed < introDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / introDuration);
                var easedT = introCurve != null ? introCurve.Evaluate(t) : t;
                rectTransform.anchoredPosition = Vector2.LerpUnclamped(startPosition, introTargetPosition, easedT);

                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Lerp(0f, restingGroupAlpha, easedT);
                }

                yield return null;
            }

            rectTransform.anchoredPosition = introTargetPosition;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = restingGroupAlpha;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }
        
        /// <summary>
        /// 카드 설정
        /// </summary>
        public void Setup(Core.CharacterPreset preset, System.Action<CharacterCard, Core.CharacterPreset> callback, int displayIndex = 1)
        {
            character = preset;
            //Debug.Log($"[CharacterCard] preset name = '{preset?.CharacterName}'");
            onSelected = callback;

            ApplySelectButtonSprite(preset);
            ApplyBottleAppearance(displayIndex);
            
            // UI 업데이트
            var resolvedPortrait = ResolvePortraitSprite(preset);
            if (portraitImage != null && resolvedPortrait != null)
            {
                portraitImage.sprite = resolvedPortrait;
                defaultPortraitSprite = resolvedPortrait;
            }
            
            if (nameText != null)
            {
                nameText.text = preset.CharacterName;
            }
            
            if (descriptionText != null)
            {
                descriptionText.text = preset.Description;
            }
        }

        private void ApplyBottleAppearance(int displayIndex)
        {
            if (bottleAppearances == null || bottleAppearances.Length == 0) return;
            var appearance = bottleAppearances[Mathf.Clamp(displayIndex, 0, bottleAppearances.Length - 1)];
            if (appearance == null || appearance.sprite == null) return;
            var image = selectButtonImage != null ? selectButtonImage : selectButton != null ? selectButton.image : null;
            if (image != null) image.sprite = appearance.sprite;
            if (bottleLabel != null)
            {
                bottleLabel.anchoredPosition = appearance.namePosition;
                bottleLabel.localRotation = Quaternion.Euler(0f, 0f, appearance.nameAngle);
            }
        }

        private void ApplySelectButtonSprite(Core.CharacterPreset preset)
        {
            var buttonImage = selectButtonImage != null ? selectButtonImage : selectButton != null ? selectButton.image : null;
            if (buttonImage == null || preset == null)
            {
                return;
            }

            Sprite resolvedSprite = defaultSelectButtonSprite;
            var presetName = preset.CharacterName;

            for (int i = 0; i < selectButtonSprites.Count; i++)
            {
                var option = selectButtonSprites[i];
                if (option != null && !string.IsNullOrEmpty(option.characterName) && option.characterName == presetName)
                {
                    resolvedSprite = option.sprite;
                    break;
                }
            }

            if (resolvedSprite != null)
            {
                buttonImage.sprite = resolvedSprite;
            }
        }

        private void ApplyAlphaToUI(float alpha)
        {
            if (portraitImage != null)
            {
                var c = portraitImage.color;
                portraitImage.color = new Color(c.r, c.g, c.b, alpha);
                // ensure it can receive pointer events
                portraitImage.raycastTarget = true;
            }

            var btnImage = selectButtonImage != null ? selectButtonImage : selectButton != null ? selectButton.image : null;
            if (btnImage != null)
            {
                var c2 = btnImage.color;
                btnImage.color = new Color(c2.r, c2.g, c2.b, alpha);
            }
        }

        private void SetHover(bool hover)
        {
            if (hoverSuppressed)
            {
                return;
            }

            ApplyAlphaToUI(hover ? hoverAlpha : normalAlpha);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = hover ? hoverGroupAlpha : restingGroupAlpha;
            }

            if (portraitImage != null)
            {
                if (hover)
                {
                    var hoverSprite = ResolveHoverPortraitSprite();
                    if (hoverSprite != null)
                    {
                        portraitImage.sprite = hoverSprite;
                    }
                }
                else if (!hover && defaultPortraitSprite != null)
                {
                    portraitImage.sprite = defaultPortraitSprite;
                }
            }

            PlayHoverScale(hover ? hoverScale : 1f);
        }

        private void PlayHoverScale(float multiplier)
        {
            if (rectTransform == null)
            {
                return;
            }

            if (hoverScaleRoutine != null)
            {
                StopCoroutine(hoverScaleRoutine);
            }

            if (!gameObject.activeInHierarchy)
            {
                rectTransform.localScale = baseScale * multiplier;
                return;
            }

            hoverScaleRoutine = StartCoroutine(HoverScaleRoutine(baseScale * multiplier));
        }

        private IEnumerator HoverScaleRoutine(Vector3 target)
        {
            var start = rectTransform.localScale;
            var elapsed = 0f;
            var duration = Mathf.Max(0.0001f, hoverScaleDuration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                rectTransform.localScale = Vector3.LerpUnclamped(start, target, t);
                yield return null;
            }

            rectTransform.localScale = target;
            hoverScaleRoutine = null;
        }

        public void SetDimmed(bool dimmed)
        {
            isDimmed = dimmed;
            hoverSuppressed = dimmed;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = dimmed ? dimmedAlpha : restingGroupAlpha;
                canvasGroup.interactable = !dimmed;
                canvasGroup.blocksRaycasts = !dimmed;
            }

            ApplyAlphaToUI(dimmed ? dimmedAlpha : normalAlpha);

            if (dimmed)
            {
                PlayHoverScale(1f);
            }
        }

        public IEnumerator PlayDetailEntryRoutine(Vector2 targetAnchoredPosition)
        {
            if (!hasCapturedIntroTarget)
            {
                CaptureIntroTargetPosition();
            }

            hoverSuppressed = true;
            PlayHoverScale(1f);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
            ApplyAlphaToUI(hoverAlpha);

            if (rectTransform == null)
            {
                yield break;
            }

            var start = rectTransform.anchoredPosition;
            var elapsed = 0f;
            var duration = Mathf.Max(0.0001f, detailSlideDuration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = detailSlideCurve != null ? detailSlideCurve.Evaluate(t) : t;
                rectTransform.anchoredPosition = Vector2.LerpUnclamped(start, targetAnchoredPosition, eased);
                yield return null;
            }

            rectTransform.anchoredPosition = targetAnchoredPosition;
        }

        public IEnumerator PlayDetailReturnRoutine()
        {
            if (!hasCapturedIntroTarget)
            {
                yield break;
            }

            if (rectTransform == null)
            {
                yield break;
            }

            var start = rectTransform.anchoredPosition;
            var elapsed = 0f;
            var duration = Mathf.Max(0.0001f, detailSlideDuration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = detailSlideCurve != null ? detailSlideCurve.Evaluate(t) : t;
                rectTransform.anchoredPosition = Vector2.LerpUnclamped(start, introTargetPosition, eased);
                yield return null;
            }

            rectTransform.anchoredPosition = introTargetPosition;
            hoverSuppressed = false;
            ApplyAlphaToUI(normalAlpha);
            if (canvasGroup != null) canvasGroup.alpha = restingGroupAlpha;
        }

        public IEnumerator PlayConfirmFallRoutine()
        {
            hoverSuppressed = true;
            PlayHoverScale(1f);

            if (rectTransform == null)
            {
                yield break;
            }

            var startRotation = rectTransform.localEulerAngles;
            var targetRotation = startRotation + new Vector3(0f, 0f, fallTiltAngle);
            var startPos = rectTransform.anchoredPosition;
            var targetPos = startPos + new Vector2(fallDropOffsetX, -fallDropOffsetY);

            var elapsed = 0f;
            var duration = Mathf.Max(0.0001f, fallDuration);
            var tiltCutoff = Mathf.Clamp01(tiltCompleteAt);
            var dropBegin = Mathf.Clamp01(dropStartAt);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);

                // Tilt: completes early so the bottle is fully on its side while still falling.
                var tiltT = tiltCutoff > 0f ? Mathf.Clamp01(t / tiltCutoff) : 1f;
                var tiltEased = fallTiltCurve != null ? fallTiltCurve.Evaluate(tiltT) : tiltT;
                rectTransform.localEulerAngles = Vector3.LerpUnclamped(startRotation, targetRotation, tiltEased);

                // Drop: starts slightly after the tilt begins, then accelerates (gravity-ish).
                var dropSpan = 1f - dropBegin;
                var dropT = dropSpan > 0f ? Mathf.Clamp01((t - dropBegin) / dropSpan) : 1f;
                var dropEased = fallDropCurve != null ? fallDropCurve.Evaluate(dropT) : dropT;
                rectTransform.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, dropEased);

                if (canvasGroup != null)
                {
                    // Hold opacity until the bottle has cleared, then fade out quickly.
                    var fadeT = Mathf.Clamp01((t - 0.6f) / 0.4f);
                    canvasGroup.alpha = Mathf.Lerp(1f, 0f, fadeT);
                }
                yield return null;
            }

            rectTransform.anchoredPosition = targetPos;
            rectTransform.localEulerAngles = targetRotation;
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }

        private Sprite ResolveHoverPortraitSprite()
        {
            if (character == null)
            {
                return null;
            }

            var presetName = character.CharacterName;
            for (int i = 0; i < hoverPortraitSprites.Count; i++)
            {
                var option = hoverPortraitSprites[i];
                if (option != null && !string.IsNullOrEmpty(option.characterName) && option.characterName == presetName)
                {
                    return option.sprite;
                }
            }

            return defaultPortraitSprite;
        }

        private Sprite ResolvePortraitSprite(Core.CharacterPreset preset)
        {
            if (preset == null)
            {
                return defaultPortraitOverride;
            }

            var presetName = preset.CharacterName;
            for (int i = 0; i < portraitSprites.Count; i++)
            {
                var option = portraitSprites[i];
                if (option != null && !string.IsNullOrEmpty(option.characterName) && option.characterName == presetName)
                {
                    return option.sprite;
                }
            }

            if (defaultPortraitOverride != null)
            {
                return defaultPortraitOverride;
            }

            return preset.Portrait;
        }

        private void RegisterPointerEvents()
        {
            // Add EventTrigger to selectButton and portrait so pointer enter/exit work
            if (selectButton != null)
            {
                AddEventTrigger(selectButton.gameObject);
            }

            if (portraitImage != null)
            {
                portraitImage.raycastTarget = true;
                AddEventTrigger(portraitImage.gameObject);
                // 병 전체(초상화 영역)를 클릭해도 선택되도록 클릭 핸들러 추가
                AddClickTrigger(portraitImage.gameObject);
            }
        }

        private void AddEventTrigger(GameObject go)
        {
            if (go == null) return;

            var ev = go.GetComponent<EventTrigger>();
            if (ev == null) ev = go.AddComponent<EventTrigger>();

            // Pointer Enter
            var entryEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entryEnter.callback.AddListener((data) => { SetHover(true); });
            ev.triggers.Add(entryEnter);

            // Pointer Exit
            var entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            entryExit.callback.AddListener((data) => { SetHover(false); });
            ev.triggers.Add(entryExit);
        }

        private void AddClickTrigger(GameObject go)
        {
            if (go == null) return;

            var ev = go.GetComponent<EventTrigger>();
            if (ev == null) ev = go.AddComponent<EventTrigger>();

            var entryClick = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            entryClick.callback.AddListener((data) => { OnSelectClicked(); });
            ev.triggers.Add(entryClick);
        }
        
        /// <summary>
        /// 선택 버튼 클릭
        /// </summary>
        private void OnSelectClicked()
        {
            Debug.Log($"[CharacterCard] Select button clicked for {character?.CharacterName}");

            onSelected?.Invoke(this, character);
        }

        public void SetSelectionLocked(bool locked)
        {
            if (selectButton != null)
            {
                selectButton.interactable = !locked;
            }

            if (canvasGroup != null)
            {
                canvasGroup.interactable = !locked;
                canvasGroup.blocksRaycasts = !locked;
            }
        }

        public IEnumerator PlaySelectionExitRoutine(bool riseFirst)
        {
            if (!hasCapturedIntroTarget)
            {
                CaptureIntroTargetPosition();
            }

            if (rectTransform == null)
            {
                onSelected?.Invoke(this, character);
                yield break;
            }

            var basePosition = rectTransform.anchoredPosition;

            if (riseFirst)
            {
                var riseTarget = basePosition + new Vector2(0f, selectRiseOffsetY);
                var riseElapsed = 0f;

                while (riseElapsed < selectRiseDuration)
                {
                    riseElapsed += Time.unscaledDeltaTime;
                    var t = Mathf.Clamp01(riseElapsed / selectRiseDuration);
                    var easedT = selectRiseCurve != null ? selectRiseCurve.Evaluate(t) : t;
                    rectTransform.anchoredPosition = Vector2.LerpUnclamped(basePosition, riseTarget, easedT);
                    yield return null;
                }

                rectTransform.anchoredPosition = riseTarget;
                basePosition = riseTarget;
            }

            var exitTarget = basePosition + new Vector2(0f, -selectExitOffsetY);
            var exitElapsed = 0f;

            while (exitElapsed < selectExitDuration)
            {
                exitElapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(exitElapsed / selectExitDuration);
                var easedT = selectExitCurve != null ? selectExitCurve.Evaluate(t) : t;
                rectTransform.anchoredPosition = Vector2.LerpUnclamped(basePosition, exitTarget, easedT);

                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Lerp(restingGroupAlpha, 0f, easedT);
                }

                yield return null;
            }

            rectTransform.anchoredPosition = exitTarget;
        }
    }
}
