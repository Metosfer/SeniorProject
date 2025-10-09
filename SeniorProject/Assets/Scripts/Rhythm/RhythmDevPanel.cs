using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Developer-only in-game panel to start/stop the rhythm mini-game without gameplay gating.
// Shown only in Editor or Development Build. Toggle visibility with F10.
public class RhythmDevPanel : MonoBehaviour
{
    public RythmGameManager rhythm; // Assign in Inspector or auto-find
    [Tooltip("Start hidden; toggle with F10")] public bool startHidden = true;
    [Tooltip("Remember window position across play sessions (Editor only)")] public bool rememberWindow = true;
    [Tooltip("Key to toggle the dev panel visibility (Editor/Dev builds only)")]
    public KeyCode toggleKey = KeyCode.F10;
    [Tooltip("Panel size in pixels (ignored if rememberSize is enabled and a saved size exists)")]
    public Vector2 windowSize = new Vector2(320, 240);
    [Tooltip("Remember window size across play sessions (Editor only)")]
    public bool rememberSize = true;
    [Header("Content Scaling")]
    [Tooltip("Automatically scale inner UI based on the panel width")] public bool autoScaleContents = true;
    [Tooltip("Reference width for scale = 1.0")] public float contentBaseWidth = 320f;
    [Tooltip("Clamp minimum scale")] public float contentMinScale = 0.75f;
    [Tooltip("Clamp maximum scale")] public float contentMaxScale = 2.0f;

    private bool _visible;
    private Rect _win = new Rect(20, 20, 320, 220);
    private string _seekText = "";

    private void Awake()
    {
        if (rhythm == null)
        {
            rhythm = FindObjectOfType<RythmGameManager>(includeInactive: true);
        }
        _visible = !startHidden;
#if UNITY_EDITOR
        if (rememberWindow)
        {
            float x = EditorPrefs.GetFloat("RhythmDevPanel_x", _win.x);
            float y = EditorPrefs.GetFloat("RhythmDevPanel_y", _win.y);
            _win.position = new Vector2(x, y);
        }
        if (rememberSize)
        {
            float w = EditorPrefs.GetFloat("RhythmDevPanel_w", _win.width);
            float h = EditorPrefs.GetFloat("RhythmDevPanel_h", _win.height);
            _win.size = new Vector2(Mathf.Max(220f, w), Mathf.Max(160f, h));
        }
        else
        {
            _win.size = new Vector2(Mathf.Max(220f, windowSize.x), Mathf.Max(160f, windowSize.y));
        }
#endif
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    if (InputHelper.GetKeyDown(toggleKey)) _visible = !_visible;
#else
        // Hide in release builds
        _visible = false;
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void OnGUI()
    {
        if (!_visible) return;
    // Apply Inspector size when not remembering size (so live tweaks reflect)
#if UNITY_EDITOR
    if (!rememberSize)
    {
        _win.size = new Vector2(Mathf.Max(220f, windowSize.x), Mathf.Max(160f, windowSize.y));
    }
#endif
        _win = GUI.Window(8192, _win, DrawWindow, "Rhythm Dev Panel");
#if UNITY_EDITOR
        if (rememberWindow)
        {
            EditorPrefs.SetFloat("RhythmDevPanel_x", _win.x);
            EditorPrefs.SetFloat("RhythmDevPanel_y", _win.y);
        }
    if (rememberSize)
    {
        EditorPrefs.SetFloat("RhythmDevPanel_w", _win.width);
        EditorPrefs.SetFloat("RhythmDevPanel_h", _win.height);
    }
#endif
    }

    private void DrawWindow(int id)
    {
        // Optional scaling for all inner controls
        Matrix4x4 prevMatrix = GUI.matrix;
        float scale = 1f;
        if (autoScaleContents && contentBaseWidth > 0.01f)
        {
            scale = Mathf.Clamp(_win.width / contentBaseWidth, contentMinScale, contentMaxScale);
            GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), Vector2.zero);
        }
        GUILayout.BeginVertical();
        if (rhythm == null)
        {
            GUILayout.Label("RythmGameManager not found.");
            if (GUILayout.Button("Find in scene"))
            {
                rhythm = FindObjectOfType<RythmGameManager>(includeInactive: true);
            }
            GUILayout.EndVertical();
            GUI.matrix = prevMatrix;
            GUI.DragWindow();
            return;
        }

        // Status
        GUILayout.Label($"Score: {rhythm.Score}    MaxCombo: {rhythm.MaxCombo}");
        GUILayout.Space(6);

        // Controls
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Start Game")) rhythm.StartGame();
        if (GUILayout.Button("Stop Game")) rhythm.StopGame();
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Seek (s):", GUILayout.Width(60));
        _seekText = GUILayout.TextField(_seekText, GUILayout.Width(80));
        if (GUILayout.Button("Go", GUILayout.Width(50)))
        {
            if (rhythm.musicSource != null && float.TryParse(_seekText, out float t))
            {
                t = Mathf.Clamp(t, 0f, rhythm.musicSource.clip != null ? rhythm.musicSource.clip.length - 0.01f : 600f);
                rhythm.musicSource.time = t;
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        GUILayout.Label("Travel Time");
        float tr = GUILayout.HorizontalSlider(rhythm.travelTime, 0.3f, 1.6f);
        if (Mathf.Abs(tr - rhythm.travelTime) > 0.0001f) rhythm.travelTime = tr;

        GUILayout.Space(4);
        GUILayout.Label("Hit Window (px)");
        float hw = GUILayout.HorizontalSlider(rhythm.hitWindowPixels, 20f, 100f);
        if (Mathf.Abs(hw - rhythm.hitWindowPixels) > 0.0001f) rhythm.hitWindowPixels = hw;

        GUILayout.Space(4);
        GUILayout.Label("Combo Strictness");
        float cs = GUILayout.HorizontalSlider(rhythm.comboStrictnessAtHighSpeed, 0.5f, 3f);
        if (Mathf.Abs(cs - rhythm.comboStrictnessAtHighSpeed) > 0.0001f) rhythm.comboStrictnessAtHighSpeed = cs;

        GUILayout.FlexibleSpace();
    GUILayout.Label($"{toggleKey}: toggle panel  •  Editor/Dev build only", EditorStyles.miniLabel);
        GUILayout.EndVertical();
    GUI.matrix = prevMatrix;
        GUI.DragWindow();
    }
#endif
}
