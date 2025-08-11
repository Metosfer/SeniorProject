using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class WellManager : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("Max distance to detect a carried bucket/player for filling and prompt.")]
    public float detectRange = 2.5f;
    [Header("Prompt")]
    [Tooltip("UI prompt to show when a carried bucket is in range and not filled.")]
    public GameObject fillPromptUI;

    [Header("Well")]
    [Tooltip("Optional: Visual or particle effect to play when a bucket is filled.")]
    public GameObject fillVFX;

    [Tooltip("Invoked when a bucket gets filled from this well.")]
    public UnityEvent onBucketFilled;

    private static readonly HashSet<WellManager> s_wells = new HashSet<WellManager>();
    
    // Throttling for UI updates to prevent flicker
    private const float UI_UPDATE_INTERVAL = 0.1f; // Update UI every 100ms instead of every frame
    private const float RANGE_HYSTERESIS = 0.2f;   // Extra margin to prevent prompt flicker
    private float _lastUIUpdateTime;
    private bool _lastPromptState;
    private bool _inRangeSticky;

    private void OnEnable()
    {
        s_wells.Add(this);
        if (fillPromptUI != null) fillPromptUI.SetActive(false);
    }

    private void OnDisable()
    {
        s_wells.Remove(this);
        if (fillPromptUI != null) fillPromptUI.SetActive(false);
    }

    private void Update()
    {
        // Throttled UI update to prevent flicker
        UpdateFillPromptThrottled();
    }
    
    private void UpdateFillPromptThrottled()
    {
        if (fillPromptUI == null) return;
        
        float currentTime = Time.unscaledTime;
        bool shouldUpdate = currentTime - _lastUIUpdateTime >= UI_UPDATE_INTERVAL;
        
        if (!shouldUpdate)
        {
            // Use cached state
            if (fillPromptUI.activeSelf != _lastPromptState)
                fillPromptUI.SetActive(_lastPromptState);
            return;
        }
        
        _lastUIUpdateTime = currentTime;
        
        var bucket = BucketManager.CurrentCarried;
        bool shouldShow = false;

        if (bucket != null && bucket.IsCarried && !bucket.IsFilled)
        {
            Vector3 center = bucket.player != null ? bucket.player.position : bucket.transform.position;
            float baseRange = Mathf.Max(0.1f, Mathf.Min(bucket.fillRange, detectRange));
            float enterRange = baseRange + RANGE_HYSTERESIS;
            float exitRange  = baseRange - RANGE_HYSTERESIS;
            float sqrDistance = (transform.position - center).sqrMagnitude;

            if (!_inRangeSticky && sqrDistance <= (enterRange * enterRange))
                _inRangeSticky = true;
            else if (_inRangeSticky && sqrDistance > (exitRange * exitRange))
                _inRangeSticky = false;

            shouldShow = _inRangeSticky;
        }
        else
        {
            _inRangeSticky = false;
        }

        _lastPromptState = shouldShow;
        if (fillPromptUI.activeSelf != shouldShow)
            fillPromptUI.SetActive(shouldShow);
    }

    public static bool AnyWellInRange(Vector3 position, float range)
    {
        foreach (var well in s_wells)
        {
            if (well == null) continue;
            float r = Mathf.Min(range, well.detectRange);
            if (Vector3.Distance(position, well.transform.position) <= r)
                return true;
        }
        return false;
    }

    public static bool TryGetNearestInRange(Vector3 position, float range, out WellManager nearest)
    {
        nearest = null;
        float best = float.MaxValue;
        foreach (var well in s_wells)
        {
            if (well == null) continue;
            float r = Mathf.Min(range, well.detectRange);
            float d = Vector3.Distance(position, well.transform.position);
            if (d <= r && d < best)
            {
                best = d;
                nearest = well;
            }
        }
        return nearest != null;
    }

    public bool FillBucket(BucketManager bucket)
    {
        if (bucket == null) return false;
        if (bucket.IsFilled) return false; // Already filled, ignore

        // Validate range using sticky in-range OR direct distance check
    Vector3 center = bucket.player != null ? bucket.player.position : bucket.transform.position;
    float baseRange = Mathf.Max(0.1f, Mathf.Min(bucket.fillRange, detectRange));
        float sqrDistance = (transform.position - center).sqrMagnitude;
        if (!_inRangeSticky && sqrDistance > baseRange * baseRange)
            return false;

        // Additional checks can go here (cooldowns, water remaining, etc.)
        if (fillVFX != null)
        {
            fillVFX.SetActive(false);
            fillVFX.SetActive(true);
        }
        onBucketFilled?.Invoke();
        return true;
    }
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(WellManager))]
public class WellManagerEditor : UnityEditor.Editor
{
    private void OnSceneGUI()
    {
        var well = (WellManager)target;
        UnityEditor.Handles.color = new Color(0f, 0.5f, 1f, 0.25f);
        UnityEditor.Handles.DrawSolidDisc(well.transform.position, Vector3.up, well.detectRange);
        UnityEditor.Handles.color = new Color(0f, 0.5f, 1f, 0.9f);
        UnityEditor.Handles.DrawWireDisc(well.transform.position, Vector3.up, well.detectRange);
    }
}
#endif
