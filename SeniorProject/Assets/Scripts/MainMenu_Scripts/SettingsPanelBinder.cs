using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Binds Settings UI controls to SettingsManager and applies live.
public class SettingsPanelBinder : MonoBehaviour
{
    [Header("Audio")]
    public Slider volumeSlider;

    [Header("Graphics")]
    public TMP_Dropdown graphicsDropdown;

    [Header("Camera/Input")]
    public Toggle rmbRotateToggle;
    public Toggle invertYToggle;
    public Slider sensitivitySlider;
    public Toggle showRotateCursorToggle;
    public TMP_InputField rotateCursorSizeInput;
    public Toggle dpiAwareCursorToggle;

    private bool _initialized;

    private void OnEnable()
    {
        InitIfNeeded();
        RefreshFromSettings();
    }

    private void Awake()
    {
        InitIfNeeded();
    }

    private void InitIfNeeded()
    {
        if (_initialized) return;
        _initialized = true;

        var sm = SettingsManager.Instance;
        if (sm == null) return;

        // Hook listeners
        if (volumeSlider != null)
            volumeSlider.onValueChanged.AddListener(v => sm.SetMasterVolume(v));
        if (graphicsDropdown != null)
            graphicsDropdown.onValueChanged.AddListener(i => sm.SetGraphicsQuality(i));
        if (rmbRotateToggle != null)
            rmbRotateToggle.onValueChanged.AddListener(b => sm.SetCameraRotateOnlyWithRMB(b));
        if (invertYToggle != null)
            invertYToggle.onValueChanged.AddListener(b => sm.SetCameraInvertY(b));
        if (sensitivitySlider != null)
            sensitivitySlider.onValueChanged.AddListener(v => sm.SetCameraSensitivity(v));
        if (showRotateCursorToggle != null)
            showRotateCursorToggle.onValueChanged.AddListener(b => sm.SetCameraShowRotateCursor(b));
        if (dpiAwareCursorToggle != null)
            dpiAwareCursorToggle.onValueChanged.AddListener(b => sm.SetDPIAwareCursor(b));
        if (rotateCursorSizeInput != null)
            rotateCursorSizeInput.onEndEdit.AddListener(OnRotateCursorSizeEdited);

        sm.OnSettingsChanged += _ => RefreshFromSettings();
    }

    private void OnDestroy()
    {
        var sm = SettingsManager.Instance;
        if (sm != null) sm.OnSettingsChanged -= _ => RefreshFromSettings();
    }

    private void RefreshFromSettings()
    {
        var sm = SettingsManager.Instance;
        if (sm == null) return;
        var s = sm.Current;

        if (volumeSlider != null && volumeSlider.value != s.masterVolume)
            volumeSlider.value = s.masterVolume;
        if (graphicsDropdown != null && graphicsDropdown.value != s.graphicsQuality)
            graphicsDropdown.value = s.graphicsQuality;
        if (rmbRotateToggle != null && rmbRotateToggle.isOn != s.cameraRotateOnlyWithRMB)
            rmbRotateToggle.isOn = s.cameraRotateOnlyWithRMB;
        if (invertYToggle != null && invertYToggle.isOn != s.cameraInvertY)
            invertYToggle.isOn = s.cameraInvertY;
        if (sensitivitySlider != null && Mathf.Abs(sensitivitySlider.value - s.cameraMouseSensitivity) > 0.0001f)
            sensitivitySlider.value = s.cameraMouseSensitivity;
        if (showRotateCursorToggle != null && showRotateCursorToggle.isOn != s.cameraShowRotateCursor)
            showRotateCursorToggle.isOn = s.cameraShowRotateCursor;
        if (dpiAwareCursorToggle != null && dpiAwareCursorToggle.isOn != s.dpiAwareCursor)
            dpiAwareCursorToggle.isOn = s.dpiAwareCursor;
        if (rotateCursorSizeInput != null)
            rotateCursorSizeInput.text = s.rotateCursorSize.ToString();
    }

    private void OnRotateCursorSizeEdited(string text)
    {
        if (int.TryParse(text, out int px))
        {
            px = Mathf.Clamp(px, 8, 256);
            SettingsManager.Instance?.SetRotateCursorSize(px);
            // normalize text
            if (rotateCursorSizeInput != null)
                rotateCursorSizeInput.text = px.ToString();
        }
    }
}
