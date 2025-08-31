using UnityEngine;
using System.Collections;

// Attach to any spawned prefab to give it a lively pop-in and gentle idle sway.
public class SpawnAnimator : MonoBehaviour
{
    [Header("Pop-in")]
    public bool playOnEnable = true;
    [Tooltip("Total duration of the pop-in effect")] public float popDuration = 0.35f;
    [Tooltip("Initial scale multiplier")] public float startScale = 0.7f;
    [Tooltip("Overshoot scale multiplier at mid animation")] public float overshootScale = 1.08f;
    [Tooltip("Final scale multiplier")] public float endScale = 1.0f;
    [Tooltip("Max wiggle angle during pop (deg), decays to 0")] public float wiggleAngle = 8f;

    [Header("Idle Sway")]
    public bool enableIdleSway = true;
    [Tooltip("Axis to sway around (local)")] public Vector3 swayAxis = Vector3.up;
    [Tooltip("Peak sway angle (deg)")] public float swayAngle = 2.5f;
    [Tooltip("Sway speed (Hz)")] public float swaySpeed = 0.7f;
    [Tooltip("Randomize initial phase for variation")] public bool swayRandomizePhase = true;

    private Vector3 _baseScale;
    private Quaternion _baseRotation;
    private Coroutine _popRoutine;
    private Coroutine _swayRoutine;
    private float _phase;

    private void OnEnable()
    {
        _baseScale = transform.localScale;
        _baseRotation = transform.localRotation;
        if (swayRandomizePhase) _phase = Random.Range(0f, Mathf.PI * 2f);

        if (playOnEnable)
        {
            if (_popRoutine != null) StopCoroutine(_popRoutine);
            _popRoutine = StartCoroutine(PopIn());
        }
        else if (enableIdleSway)
        {
            if (_swayRoutine != null) StopCoroutine(_swayRoutine);
            _swayRoutine = StartCoroutine(IdleSway());
        }
    }

    private void OnDisable()
    {
        if (_popRoutine != null) StopCoroutine(_popRoutine);
        if (_swayRoutine != null) StopCoroutine(_swayRoutine);
        transform.localScale = _baseScale;
        transform.localRotation = _baseRotation;
    }

    private IEnumerator PopIn()
    {
        // Pop: start -> overshoot -> settle
        float t = 0f;
        float half = Mathf.Max(0.01f, popDuration * 0.55f);
        float tail = Mathf.Max(0.01f, popDuration - half);

        // Phase 1: grow to overshoot
        while (t < half)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / half);
            float s = Mathf.Lerp(startScale, overshootScale, EaseOutCubic(u));
            float ang = Mathf.Lerp(wiggleAngle, wiggleAngle * 0.35f, u);
            ApplyScaleAndWiggle(s, ang, u);
            yield return null;
        }

        // Phase 2: settle to end
        t = 0f;
        while (t < tail)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / tail);
            float s = Mathf.Lerp(overshootScale, endScale, EaseOutCubic(u));
            float ang = Mathf.Lerp(wiggleAngle * 0.35f, 0f, u);
            ApplyScaleAndWiggle(s, ang, 1f - u);
            yield return null;
        }

        transform.localScale = _baseScale * endScale;
        transform.localRotation = _baseRotation;

        if (enableIdleSway)
        {
            if (_swayRoutine != null) StopCoroutine(_swayRoutine);
            _swayRoutine = StartCoroutine(IdleSway());
        }
    }

    private void ApplyScaleAndWiggle(float mul, float angle, float decay)
    {
        transform.localScale = _baseScale * Mathf.Max(0.01f, mul);
        // light wiggle around swayAxis
        if (angle > 0.0001f)
        {
            float w = Mathf.Sin((Time.time + _phase) * 16f) * angle * decay;
            transform.localRotation = _baseRotation * Quaternion.AngleAxis(w, swayAxis.normalized);
        }
        else
        {
            transform.localRotation = _baseRotation;
        }
    }

    private IEnumerator IdleSway()
    {
        while (true)
        {
            float w = Mathf.Sin((Time.time + _phase) * (Mathf.PI * 2f) * Mathf.Max(0.01f, swaySpeed)) * swayAngle;
            transform.localRotation = _baseRotation * Quaternion.AngleAxis(w, swayAxis.normalized);
            yield return null;
        }
    }

    private static float EaseOutCubic(float x)
    {
        x = Mathf.Clamp01(x);
        return 1f - Mathf.Pow(1f - x, 3f);
    }
}
