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

    [Header("Prompt Facing")]
    [Tooltip("Prompt UI oyuncu range içindeyken yalnızca Y ekseninde oyuncuya dönsün (billboard).")]
    public bool facePlayerWhileInRange = true;
    [Tooltip("Dönüş yumuşatma hızı (lerp). 0 = anlık, 10 = hızlı.")]
    [Range(0f, 15f)] public float faceLerpSpeed = 8f;
    [Tooltip("Minimum mesafe (XZ) - daha yakına gelirse dönme çok ufak jitter'i engellemek için yavaşlatılır.")]
    public float faceMinDistance = 0.15f;
    [Tooltip("Yüksek performans için bu kadar saniyede bir güncelle (0 = her frame)")]
    [Range(0f, 0.2f)] public float faceUpdateInterval = 0.02f;

    private float _lastFaceUpdate;
    private Transform _playerCached;

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

    // Stabilized interact state to prevent flicker
    private const float INTERACT_CHECK_INTERVAL = 0.05f; // how often to evaluate interactability
    private const float ENTER_STABLE_TIME = 0.18f; // must remain in range this long to turn ON
    private const float EXIT_STABLE_TIME = 0.28f;  // must remain out of range this long to turn OFF
    private float _lastInteractEvalTime;
    private bool _desiredInRange;
    private bool _stableInRange;
    private float _lastDesireChangeTime;

    [Header("Right-Click Fill (Context Menu)")]
    [Tooltip("Allow right-click to open a small menu with actions when in range and carrying an empty bucket.")]
    public bool enableRightClickMenu = true;
    [Tooltip("Label for the primary action in the menu.")]
    public string rightClickPrimaryLabel = "Fill Bucket";
    [Tooltip("Label for the secondary/cancel action in the menu.")]
    public string rightClickSecondaryLabel = "Cancel";
    private bool _showContextMenu;
    private Vector2 _menuScreenPos;
    private Rect _lastMenuRect;
    // Global context menu tracker
    private static int s_contextMenuOpenCount = 0;
    public static bool AnyContextMenuOpen => s_contextMenuOpenCount > 0;

    [Header("Context Menu Layout")]
    [Tooltip("Context menu total width in pixels.")]
    public float contextMenuWidth = 160f;
    [Tooltip("Context menu total height in pixels. Leave 0 to auto-calc from button height.")]
    public float contextMenuHeight = 0f;
    [Tooltip("Button height in pixels.")]
    public float contextMenuButtonHeight = 24f;
    [Tooltip("Content padding inside the menu box in pixels.")]
    public float contextMenuPadding = 8f;
    [Tooltip("Button label font size. Set 0 to use default skin size.")]
    public int contextMenuFontSize = 0;

    private void OnEnable()
    {
        s_wells.Add(this);
    if (fillPromptUI != null) fillPromptUI.SetActive(false);
    if (fillVFX != null && fillVFX.activeSelf) fillVFX.SetActive(false);
    }

    private void OnDisable()
    {
        s_wells.Remove(this);
        if (fillPromptUI != null) fillPromptUI.SetActive(false);
        if (fillVFX != null && fillVFX.activeSelf) fillVFX.SetActive(false);
        // Ensure menu closed accounting if this gets disabled while open
        if (_showContextMenu)
        {
            _showContextMenu = false;
            if (s_contextMenuOpenCount > 0) s_contextMenuOpenCount--;
        }
    }

    private void Update()
    {
        // Throttled UI update to prevent flicker
        UpdateFillPromptThrottled();
    // Handle right-click context menu
    UpdateRightClickMenu();
        UpdatePromptFacing();
    }

    private void UpdatePromptFacing()
    {
        if (!facePlayerWhileInRange) return;
        if (fillPromptUI == null || !fillPromptUI.activeSelf) return;
        if (!_stableInRange) return; // sadece stabilized in-range iken

        // Player referansı cache
        if (_playerCached == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _playerCached = player.transform;
        }
        if (_playerCached == null) return;

        // Güncelleme frekansı kontrolü
        if (faceUpdateInterval > 0f && (Time.unscaledTime - _lastFaceUpdate) < faceUpdateInterval)
            return;
        _lastFaceUpdate = Time.unscaledTime;

        Transform t = fillPromptUI.transform;
        Vector3 from = t.position; from.y = 0f;
        Vector3 to = _playerCached.position; to.y = 0f;
        Vector3 dir = to - from;
        float planarDist = dir.magnitude;
        if (planarDist < 0.0001f) return;
        dir /= planarDist;

        // Hedef rotasyon (sadece Y ekseni)
    Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, 180f, 0f); // 180° çevir
        if (faceLerpSpeed <= 0f || planarDist <= faceMinDistance)
        {
            t.rotation = targetRot;
        }
        else
        {
            t.rotation = Quaternion.Slerp(t.rotation, targetRot, 1f - Mathf.Exp(-faceLerpSpeed * Time.unscaledDeltaTime));
        }
    }
    
    private void UpdateFillPromptThrottled()
    {
        if (fillPromptUI == null) return;
        
        float currentTime = Time.unscaledTime;
        bool shouldUpdate = currentTime - _lastUIUpdateTime >= UI_UPDATE_INTERVAL;
        
        if (!shouldUpdate)
        {
            // Use cached state
            if (!_showContextMenu && fillPromptUI.activeSelf != _lastPromptState)
                fillPromptUI.SetActive(_lastPromptState);
            return;
        }
        
        _lastUIUpdateTime = currentTime;
        
    bool shouldShow = GetStableCanInteract();
    _inRangeSticky = shouldShow; // keep for legacy checks

        _lastPromptState = shouldShow;
    if (!_showContextMenu && fillPromptUI.activeSelf != shouldShow)
            fillPromptUI.SetActive(shouldShow);
    }

    private void UpdateRightClickMenu()
    {
        if (!enableRightClickMenu)
        {
            _showContextMenu = false;
            return;
        }

    bool canInteract = GetStableCanInteract();

        // Close automatically if conditions no longer valid
        if (!canInteract && _showContextMenu)
        {
            CloseContextMenu();
        }

        // Open on right click
        if (canInteract && Input.GetMouseButtonDown(1))
        {
            OpenContextMenu(Input.mousePosition);
            // Hide prompt while menu is visible
            if (fillPromptUI != null && fillPromptUI.activeSelf) fillPromptUI.SetActive(false);
        }

        // Close via Escape
    if (_showContextMenu && InputHelper.GetKeyDown(KeyCode.Escape))
        {
            CloseContextMenu();
        }
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

        // Prefer stabilized interact state, but allow immediate fill if click is within enter range in planar distance
        bool canInteractStable = GetStableCanInteract();
        if (!canInteractStable)
        {
            // Immediate planar range check to reduce rare multi-click requirement
            Vector3 a = transform.position; a.y = 0f;
            Vector3 b = bucket.transform.position; b.y = 0f;
            float baseRange = Mathf.Max(0.1f, Mathf.Min(bucket.fillRange, detectRange));
            float enterRange = baseRange + RANGE_HYSTERESIS;
            if (Vector3.Distance(a, b) > enterRange)
                return false;
        }

        // Additional checks can go here (cooldowns, water remaining, etc.)
        if (fillVFX != null)
        {
            fillVFX.SetActive(false);
            fillVFX.SetActive(true);
        }
    onBucketFilled?.Invoke();
    // Debounce prompt immediately after fill to avoid brief flicker while state propagates
    _stableInRange = false;
    _desiredInRange = false;
    _lastInteractEvalTime = Time.unscaledTime;
    _lastUIUpdateTime = Time.unscaledTime;
    _lastPromptState = false;
    if (fillPromptUI != null && fillPromptUI.activeSelf) fillPromptUI.SetActive(false);
    // Close any open context menu
    if (_showContextMenu) CloseContextMenu();
        return true;
    }

    // Unified, stabilized can-interact evaluation (2D distance + hysteresis + time)
    private bool GetStableCanInteract()
    {
        float now = Time.unscaledTime;
        if (now - _lastInteractEvalTime < INTERACT_CHECK_INTERVAL)
        {
            return _stableInRange;
        }
        _lastInteractEvalTime = now;

        var bucket = BucketManager.CurrentCarried;
        bool valid = (bucket != null && bucket.IsCarried && !bucket.IsFilled);
        if (!valid)
        {
            _desiredInRange = false;
        }
        else
        {
            // Use bucket transform to avoid player animation jitter; compute planar (XZ) distance only
            Vector3 a = transform.position; a.y = 0f;
            // Prefer player's position (more stable than carried hand) to reduce jitter near threshold
            Vector3 b = (bucket.player != null ? bucket.player.position : bucket.transform.position); b.y = 0f;
            float baseRange = Mathf.Max(0.1f, Mathf.Min(bucket.fillRange, detectRange));
            float enterRange = baseRange + RANGE_HYSTERESIS;
            float exitRange  = baseRange - RANGE_HYSTERESIS;
            float d = Vector3.Distance(a, b);

            bool desire = _stableInRange ? (d <= exitRange) : (d <= enterRange);
            if (desire != _desiredInRange)
            {
                _desiredInRange = desire;
                _lastDesireChangeTime = now;
            }

            float required = _desiredInRange ? ENTER_STABLE_TIME : EXIT_STABLE_TIME;
            if (_stableInRange != _desiredInRange && (now - _lastDesireChangeTime) >= required)
            {
                _stableInRange = _desiredInRange;
            }
        }

        if (!valid)
        {
            _stableInRange = false;
        }
        return _stableInRange;
    }

    private void OnGUI()
    {
        if (!_showContextMenu) return;
        // Convert mouse position to GUI space (invert Y)
        Vector2 guiPos = _menuScreenPos;
        guiPos.y = Screen.height - guiPos.y;

        // Simple menu rect
    float width = Mathf.Max(100f, contextMenuWidth);
    float autoHeight = (contextMenuButtonHeight * 2f) + (contextMenuPadding * 2f) + 22f; // header approx 22
    float height = contextMenuHeight > 0f ? contextMenuHeight : autoHeight;
    Rect rect = new Rect(guiPos.x, guiPos.y, width, height);
    rect.x = Mathf.Clamp(rect.x, 0, Screen.width - width);
    rect.y = Mathf.Clamp(rect.y, 0, Screen.height - height);
        _lastMenuRect = rect;

        GUI.Box(rect, "Well");
    float pad = Mathf.Max(0f, contextMenuPadding);
    GUILayout.BeginArea(new Rect(rect.x + pad, rect.y + 22, rect.width - (pad * 2f), rect.height - (22f + pad)));
    GUIStyle btnStyle = new GUIStyle(GUI.skin.button);
    if (contextMenuFontSize > 0) btnStyle.fontSize = contextMenuFontSize;
        if (GUILayout.Button(rightClickPrimaryLabel, btnStyle, GUILayout.Height(contextMenuButtonHeight)))
        {
            var bucket = BucketManager.CurrentCarried;
            if (bucket != null && bucket.IsCarried && !bucket.IsFilled)
            {
                if (FillBucket(bucket))
                {
                    bucket.SendMessage("Fill", SendMessageOptions.DontRequireReceiver);
                    CloseContextMenu();
                }
            }
        }
    if (GUILayout.Button(rightClickSecondaryLabel, btnStyle, GUILayout.Height(contextMenuButtonHeight)))
        {
            CloseContextMenu();
        }
        GUILayout.EndArea();

        // Close if left-click outside the menu
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            if (!_lastMenuRect.Contains(Event.current.mousePosition))
            {
                CloseContextMenu();
            }
        }
    }

    private void OpenContextMenu(Vector2 screenPos)
    {
        if (_showContextMenu) return;
        _showContextMenu = true;
        _menuScreenPos = screenPos;
        s_contextMenuOpenCount++;
    }

    private void CloseContextMenu()
    {
        if (!_showContextMenu) return;
        _showContextMenu = false;
        if (s_contextMenuOpenCount > 0) s_contextMenuOpenCount--;
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
