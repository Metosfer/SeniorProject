using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Global cursor manager that replaces the Player Settings default cursor with a custom one,
// but allows temporary overrides (e.g., inventory hovers, rotate cursors). When no override
// is active, it applies the assigned default cursor instead of Unity's default.
public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    [Header("Assigned Default Cursor (replaces Player Settings)")]
    [Tooltip("Global default cursor texture to use when no other cursor is active")] public Texture2D defaultCursor;
    [Tooltip("Default cursor hotspot (in pixels)")] public Vector2 defaultHotspot = Vector2.zero;
    [Tooltip("Scale default cursor to this size (px). (0,0) = texture size")] public Vector2 defaultSize = Vector2.zero;
    [Tooltip("CursorMode for default cursor")] public CursorMode defaultMode = CursorMode.Auto;
    [Tooltip("Auto adjust size using screen DPI (baseline 96 dpi)")] public bool autoAdjustForDPI = true;

    private Texture2D _scaledDefaultCursor;
    private Vector2 _scaledDefaultHotspot;
    private float _lastAppliedDPI;

    // Simple override stack (top-most wins)
    private readonly List<OverrideEntry> _overrides = new List<OverrideEntry>();
    private int _nextToken = 1;
    private float _suppressUntil;

    private struct OverrideEntry
    {
        public int token;
        public Texture2D tex;
        public Vector2 hotspot;
        public Vector2 size;
        public CursorMode mode;
        public Texture2D scaled;
        public Vector2 scaledHotspot;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // A scene provided its own CursorManager with scene-specific defaults.
            // Transfer those defaults to the persistent instance so scene switch uses assigned cursor.
            Instance.ApplyDefaultsFrom(this, applyNow: true);
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_scaledDefaultCursor != null)
        {
            Destroy(_scaledDefaultCursor);
            _scaledDefaultCursor = null;
        }
        // Clean scaled override textures
        for (int i = 0; i < _overrides.Count; i++)
        {
            if (_overrides[i].scaled != null) Destroy(_overrides[i].scaled);
        }
        _overrides.Clear();
    }

    private void Start()
    {
        BuildScaledDefaultIfNeeded(true);
        ApplyDefaultIfNoOverride(true);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            // Re-apply on focus regain to combat OS cursor resets
            ApplyDefaultIfNoOverride(true);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // After scene load, re-apply default when no override is active
        ApplyDefaultIfNoOverride(true);
    }

    // Copy defaults from another CursorManager (e.g., scene-local instance) and optionally apply now
    public void ApplyDefaultsFrom(CursorManager src, bool applyNow = true)
    {
        if (src == null) return;
        defaultCursor = src.defaultCursor;
        defaultHotspot = src.defaultHotspot;
        defaultSize = src.defaultSize;
        defaultMode = src.defaultMode;
        autoAdjustForDPI = src.autoAdjustForDPI;
        // Force rebuild of scaled default and optionally apply
        BuildScaledDefaultIfNeeded(true);
        if (applyNow)
        {
            ApplyDefaultIfNoOverride(true);
        }
    }

    // Public API
    public int PushOverride(Texture2D tex, Vector2 hotspot, Vector2 size, CursorMode mode = CursorMode.Auto)
    {
        var entry = new OverrideEntry
        {
            token = _nextToken++,
            tex = tex,
            hotspot = hotspot,
            size = size,
            mode = mode
        };
        BuildScaled(ref entry);
        _overrides.Add(entry);
        ApplyOverride(entry);
        return entry.token;
    }

    public void PopOverride(int token)
    {
        int idx = _overrides.FindIndex(o => o.token == token);
        if (idx >= 0)
        {
            var e = _overrides[idx];
            if (e.scaled != null) Destroy(e.scaled);
            _overrides.RemoveAt(idx);
        }
        // Apply next top or default
        if (_overrides.Count > 0)
        {
            ApplyOverride(_overrides[_overrides.Count - 1]);
        }
        else
        {
            ApplyDefaultIfNoOverride(true);
        }
    }

    // Call this when some logic resets to default (null) and you want the assigned default instead
    public void UseDefaultNow()
    {
        ApplyDefaultIfNoOverride(true);
    }

    // Temporarily avoid re-applying the default for scenarios that quickly toggle cursors
    public void SuppressDefault(float seconds)
    {
        _suppressUntil = Mathf.Max(_suppressUntil, Time.unscaledTime + Mathf.Max(0f, seconds));
    }

    // Internal application logic
    private void ApplyDefaultIfNoOverride(bool force)
    {
        if (_overrides.Count > 0) return;
        if (Time.unscaledTime < _suppressUntil && !force) return;
        BuildScaledDefaultIfNeeded(false);
        if (_scaledDefaultCursor != null)
        {
            Cursor.SetCursor(_scaledDefaultCursor, _scaledDefaultHotspot, defaultMode);
        }
        else
        {
            Cursor.SetCursor(defaultCursor, defaultHotspot, defaultMode);
        }
    }

    private void ApplyOverride(OverrideEntry e)
    {
        if (e.scaled != null)
        {
            Cursor.SetCursor(e.scaled, e.scaledHotspot, e.mode);
        }
        else
        {
            Cursor.SetCursor(e.tex, e.hotspot, e.mode);
        }
    }

    private void BuildScaledDefaultIfNeeded(bool force)
    {
        if (defaultCursor == null) return;
        float dpi = Screen.dpi;
        bool dpiChanged = Mathf.Abs(dpi - _lastAppliedDPI) > 1f;
        bool needScale = defaultSize != Vector2.zero || (autoAdjustForDPI && dpi > 1f);
        if (!force && !needScale) return;
        if (!force && !dpiChanged && _scaledDefaultCursor != null) return;

        if (_scaledDefaultCursor != null)
        {
            Destroy(_scaledDefaultCursor);
            _scaledDefaultCursor = null;
        }

        if (needScale)
        {
            Vector2 targetSize = defaultSize;
            if (targetSize == Vector2.zero)
            {
                targetSize = new Vector2(defaultCursor.width, defaultCursor.height);
            }
            if (autoAdjustForDPI && dpi > 1f)
            {
                float scale = dpi / 96f; // baseline 96 dpi
                targetSize *= scale;
            }
            int w = Mathf.Max(8, Mathf.RoundToInt(targetSize.x));
            int h = Mathf.Max(8, Mathf.RoundToInt(targetSize.y));
            _scaledDefaultCursor = ResizeTexture(defaultCursor, w, h);
            float sx = (float)w / Mathf.Max(1, defaultCursor.width);
            float sy = (float)h / Mathf.Max(1, defaultCursor.height);
            _scaledDefaultHotspot = new Vector2(defaultHotspot.x * sx, defaultHotspot.y * sy);
        }
        else
        {
            _scaledDefaultCursor = null;
            _scaledDefaultHotspot = defaultHotspot;
        }
        _lastAppliedDPI = dpi;
    }

    private void BuildScaled(ref OverrideEntry e)
    {
        if (e.tex == null) { e.scaled = null; e.scaledHotspot = e.hotspot; return; }
        bool needScale = e.size != Vector2.zero;
        if (!needScale) { e.scaled = null; e.scaledHotspot = e.hotspot; return; }
        int w = Mathf.Max(8, Mathf.RoundToInt(e.size.x));
        int h = Mathf.Max(8, Mathf.RoundToInt(e.size.y));
        e.scaled = ResizeTexture(e.tex, w, h);
        float sx = (float)w / Mathf.Max(1, e.tex.width);
        float sy = (float)h / Mathf.Max(1, e.tex.height);
        e.scaledHotspot = new Vector2(e.hotspot.x * sx, e.hotspot.y * sy);
    }

    // GPU-based resize (does not require Read/Write enabled on source texture)
    private Texture2D ResizeTexture(Texture2D source, int width, int height)
    {
        var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = RenderTexture.active;
        Graphics.Blit(source, rt);
        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        tex.filterMode = FilterMode.Bilinear;
        tex.name = source.name + "_scaled";
        return tex;
    }
}
