using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>Small local-space idle motions for the tower props. No Animator or material instances required.</summary>
    [DisallowMultipleComponent]
    public sealed class WitchTowerIdleMotion : MonoBehaviour
    {
        public enum Motion { Gentle, Flame, RisingBubble }

        public Motion motion;
        [Min(.1f)] public float period = 5f;
        public float phase;
        public Vector3 positionAmplitude;
        public Vector3 rotationAmplitude;
        [Tooltip("Relative scale change; .02 means two percent.")]
        public Vector3 scaleAmplitude;
        public Light glow;
        [Range(0, .5f)] public float glowAmplitude = .12f;

        Vector3 restPosition, restScale;
        Quaternion restRotation;
        float restIntensity, elapsed;

        void OnEnable()
        {
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            restScale = transform.localScale;
            restIntensity = glow != null ? glow.intensity : 0;
            elapsed = 0;
        }

        void Update()
        {
            // Scaled time makes the room stop with the game. Never accumulate transform offsets.
            elapsed += Time.deltaTime;
            float cycle = elapsed / Mathf.Max(.1f, period) + phase;
            float wave = Mathf.Sin(cycle * Mathf.PI * 2);
            if (motion == Motion.Flame)
                wave = wave * .65f + Mathf.Sin(cycle * Mathf.PI * 5.4f + phase) * .35f;

            if (motion == Motion.RisingBubble)
            {
                float progress = Mathf.Repeat(cycle, 1);
                transform.localPosition = restPosition + new Vector3(
                    positionAmplitude.x * wave, positionAmplitude.y * progress,
                    positionAmplitude.z * Mathf.Sin(cycle * Mathf.PI * 4));
                // Fully shrink at the loop seam, so a bubble never visibly teleports downward.
                float envelope = Mathf.SmoothStep(0, 1, Mathf.Min(progress / .16f, (1 - progress) / .22f));
                transform.localScale = restScale * envelope;
            }
            else
            {
                transform.localPosition = restPosition + positionAmplitude * wave;
                transform.localScale = Vector3.Scale(restScale, Vector3.one + scaleAmplitude * wave);
            }
            transform.localRotation = restRotation * Quaternion.Euler(rotationAmplitude * wave);
            if (glow != null) glow.intensity = restIntensity * (1 + glowAmplitude * wave);
        }

        void OnDisable()
        {
            transform.localPosition = restPosition;
            transform.localRotation = restRotation;
            transform.localScale = restScale;
            if (glow != null) glow.intensity = restIntensity;
        }
    }
}
