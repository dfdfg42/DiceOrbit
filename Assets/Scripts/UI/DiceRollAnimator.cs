using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.UI
{
    /// <summary>Rounded 3D dice: center tumble, readable result hold, then delivery to the real UI slots.</summary>
    public class DiceRollAnimator : MonoBehaviour
    {
        [SerializeField] float centerYOffset;
        [Header("3D Roll")]
        [SerializeField, Min(.2f)] float tumbleDuration = 1.05f;
        [SerializeField, Min(0)] float resultHoldDuration = .48f;
        [SerializeField, Min(.1f)] float slotTravelDuration = .42f;
        [SerializeField, Min(40)] float presentationSize = 112f;
        public System.Action OnAnimationComplete;
        public bool IsPlaying { get; private set; }

        sealed class Slot
        {
            internal DiceElement element;
            internal RectTransform rect;
            internal CanvasGroup group;
            internal float alpha;
            internal bool interactable, raycasts;
            internal Vector3 scale;
        }

        readonly List<Slot> slots = new List<Slot>();
        Coroutine sequence;
        DiceRollStage stage;
        RectTransform canvasRect;

        public void PlayRollAnimation(List<DiceElement> elements, Transform diceContainer)
        {
            CancelAnimation();
            if (elements == null || elements.Count == 0)
            { OnAnimationComplete?.Invoke(); return; }
            Canvas canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (!isActiveAndEnabled || canvas == null || diceContainer == null)
            { OnAnimationComplete?.Invoke(); return; }
            canvasRect = (RectTransform)canvas.transform;
            // Keep real elements in their layout: no reparenting, no temporary numbers, no gameplay RNG.
            foreach (var element in elements)
            {
                if (element == null || element.Data == null) continue;
                var group = element.GetComponent<CanvasGroup>() ?? element.gameObject.AddComponent<CanvasGroup>();
                slots.Add(new Slot { element = element, rect = (RectTransform)element.transform, group = group,
                    alpha = group.alpha, interactable = group.interactable, raycasts = group.blocksRaycasts,
                    scale = element.transform.localScale });
                group.alpha = 0; group.interactable = false; group.blocksRaycasts = false;
            }
            if (slots.Count == 0) { OnAnimationComplete?.Invoke(); return; }
            IsPlaying = true;
            sequence = StartCoroutine(RollSequence(canvas, diceContainer));
        }

        IEnumerator RollSequence(Canvas canvas, Transform container)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (container is RectTransform layout) LayoutRebuilder.ForceRebuildLayoutImmediate(layout);
            if (!AllSlotsAlive()) { CancelAnimation(); yield break; }
            stage = new DiceRollStage(canvas);
            foreach (var slot in slots) stage.Add(slot.element);
            float rollDuration = Mathf.Max(.2f, tumbleDuration);
            float travelDuration = Mathf.Max(.1f, slotTravelDuration);
            float rollEnd = rollDuration + (slots.Count - 1) * .055f;
            float flightStart = rollEnd + Mathf.Max(0, resultHoldDuration);
            float end = flightStart + (slots.Count - 1) * .055f + travelDuration + .16f;
            float elapsed = 0;
            while (elapsed < end)
            {
                if (!AllSlotsAlive()) { CancelAnimation(); yield break; }
                stage.Resize();
                int columns = Mathf.Min(6, slots.Count);
                int rows = Mathf.CeilToInt((float)slots.Count / columns);
                float size = Mathf.Min(presentationSize, canvasRect.rect.height * .115f,
                    canvasRect.rect.width * .76f / (columns * 1.7f + 1), canvasRect.rect.height / (rows * 2.1f + 2));
                for (int i = 0; i < slots.Count; i++)
                {
                    Slot slot = slots[i];
                    int row = i / columns;
                    int rowCount = Mathf.Min(columns, slots.Count - row * columns);
                    Vector2 center = new Vector2(-canvasRect.rect.width * .11f + (i % columns - (rowCount - 1) * .5f) * size * 1.65f,
                        centerYOffset + 35 + ((rows - 1) * .5f - row) * size * 1.75f);
                    float start = flightStart + i * .055f;
                    if (elapsed < start)
                    {
                        float t = Mathf.Clamp01((elapsed - i * .055f) / rollDuration);
                        float remaining = Mathf.Pow(1 - t, 2.6f);
                        // All axes settle to zero so the result face looks straight at the camera.
                        Quaternion rotation = Quaternion.Euler(remaining * (640 + i * 29), remaining * (490 + i * 47), remaining * 155);
                        float height = BounceHeight(t) * size;
                        Vector2 position = center + new Vector2((i % 2 == 0 ? -1 : 1) * size * .8f * remaining, 0);
                        float appear = Mathf.SmoothStep(0, 1, t / .13f);
                        float settle = t > .86f ? 1 + Mathf.Sin((t - .86f) / .14f * Mathf.PI) * .035f : 1;
                        stage.Pose(i, position, size * appear * settle, rotation, height);
                        stage.RefreshResult(i, slot.element.Data.Value);
                    }
                    else
                    {
                        float t = Mathf.Clamp01((elapsed - start) / travelDuration);
                        float eased = t * t * (3 - 2 * t);
                        Vector2 target = (Vector2)canvasRect.InverseTransformPoint(slot.rect.TransformPoint(slot.rect.rect.center)) - canvasRect.rect.center;
                        Vector3 slotWidth = canvasRect.InverseTransformVector(slot.rect.TransformVector(Vector3.right * slot.rect.rect.width));
                        float finalSize = slotWidth.magnitude;
                        Vector2 position = Vector2.Lerp(center, target, eased) + Vector2.up * (Mathf.Sin(t * Mathf.PI) * size * .35f);
                        stage.Pose(i, position, Mathf.Lerp(size, finalSize, eased), Quaternion.identity, 0, t < 1 ? 1 : 0);
                        if (t >= 1)
                        {
                            slot.group.alpha = slot.alpha;
                            float arrival = Mathf.Clamp01((elapsed - start - travelDuration) / .16f);
                            slot.rect.localScale = slot.scale * (1 + Mathf.Sin(arrival * Mathf.PI) * .10f);
                        }
                    }
                }
                yield return null;
                elapsed += Time.deltaTime;
            }
            sequence = null;
            RestoreAndDispose();
            OnAnimationComplete?.Invoke();
        }

        static float BounceHeight(float t)
        {
            if (t < .44f) return 1.05f * (1 - Mathf.Pow(t / .44f, 2));
            if (t < .76f) return .34f * Mathf.Sin((t - .44f) / .32f * Mathf.PI);
            if (t < .94f) return .10f * Mathf.Sin((t - .76f) / .18f * Mathf.PI);
            return 0;
        }

        bool AllSlotsAlive()
        {
            foreach (var slot in slots) if (slot.element == null || slot.group == null) return false;
            return true;
        }

        public void CancelAnimation()
        {
            if (sequence != null) { StopCoroutine(sequence); sequence = null; }
            RestoreAndDispose();
        }

        void RestoreAndDispose()
        {
            stage?.Dispose(); stage = null;
            foreach (var slot in slots)
            {
                if (slot.element == null || slot.group == null) continue;
                slot.group.alpha = slot.alpha;
                slot.group.interactable = slot.interactable;
                slot.group.blocksRaycasts = slot.raycasts;
                slot.rect.localScale = slot.scale;
                slot.element.RefreshDisplayFromData();
            }
            slots.Clear();
            IsPlaying = false;
        }

        void OnDisable() => CancelAnimation();
        void OnDestroy() => CancelAnimation();
    }
}
