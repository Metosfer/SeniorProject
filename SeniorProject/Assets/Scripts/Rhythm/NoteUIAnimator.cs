using UnityEngine;

// Animates UI notes while falling: subtle hover, rotation wiggle, and scale pulse.
// Intensity increases with combo.
public class NoteUIAnimator : MonoBehaviour
{
    [Header("Offsets (px)")]
    public float hoverAmpX = 6f;
    public float hoverAmpY = 3f;
    public float hoverFreq = 3.2f; // Hz

    [Header("Rotation (deg)")]
    public float rotAmplitude = 8f;
    public float rotFreq = 2.4f; // Hz

    [Header("Scale")]
    public float scalePulseAmp = 0.08f; // +/-
    public float scalePulseFreq = 4.2f; // Hz

    [Header("Combo Scaling")]
    [Tooltip("At this combo or higher, use max visual intensity.")]
    public int comboMaxForIntensity = 50;
    public float intensityAtZero = 0.4f; // 0..1

    private RectTransform _rt;
    private Vector2 _baseAnchored;
    private Vector3 _baseScale;
    private Quaternion _baseRot;

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
        if (_rt != null)
        {
            _baseAnchored = _rt.anchoredPosition;
        }
        _baseScale = transform.localScale;
        _baseRot = transform.localRotation;
    }

    // Called by manager when updating position. Returns an additional offset to apply.
    public Vector2 GetPositionOffset(float elapsed, int combo)
    {
        float k = ComputeIntensity(combo);
        float t = elapsed;
        float x = Mathf.Sin(t * Mathf.PI * 2f * hoverFreq) * hoverAmpX * k;
        float y = Mathf.Cos((t + 0.31f) * Mathf.PI * 2f * (hoverFreq * 0.7f)) * hoverAmpY * k;
        return new Vector2(x, y);
    }

    // Apply rotation and scale visuals each frame. Manager calls this after position is set.
    public void ApplyVisuals(float elapsed, int combo)
    {
        float k = ComputeIntensity(combo);
        float t = elapsed;
        // Rotation wiggle
        float ang = Mathf.Sin(t * Mathf.PI * 2f * rotFreq) * rotAmplitude * k;
        transform.localRotation = _baseRot * Quaternion.Euler(0f, 0f, ang);
        // Scale pulse
        float s = 1f + Mathf.Sin((t + 0.17f) * Mathf.PI * 2f * scalePulseFreq) * (scalePulseAmp * k);
        transform.localScale = _baseScale * s;
    }

    private float ComputeIntensity(int combo)
    {
        if (comboMaxForIntensity <= 0) return 1f;
        float u = Mathf.Clamp01(combo / (float)comboMaxForIntensity);
        return Mathf.Lerp(intensityAtZero, 1f, u);
    }
}
