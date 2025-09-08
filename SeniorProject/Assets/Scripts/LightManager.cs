using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class LightManager : MonoBehaviour
{
    // Singleton to persist across scenes
    private static LightManager _instance;

    [Header("Pipeline & Quality")]
    [Tooltip("URP Pipeline Asset to enforce at runtime (assign the one you use in Project Settings > Graphics).")]
    public UniversalRenderPipelineAsset pipelineAsset;

    [Tooltip("Force Unity Quality shadows on.")]
    public bool enforceQualityShadows = true;

    [Tooltip("Global shadow distance to enforce (0 keeps current).")]
    public float enforceShadowDistance = 50f;

    [Header("Lights & Cameras")]
    [Tooltip("Optional: Explicit sun directional light. If null, will use RenderSettings.sun or find first directional light in scene.")]
    public Light sunLight;

    [Tooltip("Set main light to cast soft shadows.")]
    public bool forceMainLightShadows = true;

    [Tooltip("Also enable additional lights shadows in URP.")]
    public bool enableAdditionalLightShadows = true;

    [Tooltip("Apply settings every time a scene loads.")]
    public bool applyOnSceneLoaded = true;

    [Tooltip("Log debug info in Console.")]
    public bool logDebug = true;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        // Apply immediately on first scene load
        StartCoroutine(ApplyEndOfFrame());
    }

    private void OnDisable() { }

    private string _lastSceneName = null;
    private float _nextPollTime = 0f;

    private void Update()
    {
        // Poll scene change every 0.25s (only if requested)
        if (!applyOnSceneLoaded) return;

        if (Time.unscaledTime >= _nextPollTime)
        {
            _nextPollTime = Time.unscaledTime + 0.25f;
            string current = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (!string.Equals(_lastSceneName, current))
            {
                if (logDebug && _lastSceneName != null)
                    Debug.Log($"[LightManager] Scene changed: {_lastSceneName} -> {current}. Applying lighting & pipeline settings...");
                _lastSceneName = current;
                StartCoroutine(ApplyEndOfFrame());
            }
        }
    }

    private IEnumerator ApplyEndOfFrame()
    {
        // Wait one frame so all scene objects/cameras are present
        yield return null;
        ApplySettings();
    }

    public void ApplySettings()
    {
        // 1) Ensure the correct URP pipeline asset is active
        TryApplyPipelineAsset();

        // 2) Ensure Unity Quality shadow settings allow shadows
        if (enforceQualityShadows)
        {
            if (QualitySettings.shadows == UnityEngine.ShadowQuality.Disable)
                QualitySettings.shadows = UnityEngine.ShadowQuality.All;

            if (enforceShadowDistance > 0f && Mathf.Abs(QualitySettings.shadowDistance - enforceShadowDistance) > 0.01f)
                QualitySettings.shadowDistance = enforceShadowDistance;
        }

        // 3) Ensure a sun light exists and casts shadows
        EnsureSunLight();

        // 4) Ensure all cameras render shadows under URP
        EnsureCameraShadows();

        if (logDebug) Debug.Log("[LightManager] Lighting & shadow settings applied.");
    }

    private void TryApplyPipelineAsset()
    {
        if (pipelineAsset == null)
            return;

        // Assign pipeline both on Graphics and Quality to be safe
        if (GraphicsSettings.renderPipelineAsset != pipelineAsset)
        {
            GraphicsSettings.renderPipelineAsset = pipelineAsset;
            if (logDebug) Debug.Log("[LightManager] Assigned URP pipeline asset to GraphicsSettings.renderPipelineAsset.");
        }
        if (QualitySettings.renderPipeline != pipelineAsset)
        {
            QualitySettings.renderPipeline = pipelineAsset;
            if (logDebug) Debug.Log("[LightManager] Assigned URP pipeline asset to QualitySettings.renderPipeline.");
        }

        // URP runtime toggles for shadow features are read-only; we can only adjust distances
        var urp = pipelineAsset;
        if (urp != null)
        {
            if (enforceShadowDistance > 0f && Mathf.Abs(urp.shadowDistance - enforceShadowDistance) > 0.01f)
                urp.shadowDistance = enforceShadowDistance;

            // Warn if pipeline disables shadows we rely on
            bool mainOk = urp.supportsMainLightShadows; // read-only
            bool addOk = urp.supportsAdditionalLightShadows; // read-only
            if (logDebug && forceMainLightShadows && !mainOk)
                Debug.LogWarning("[LightManager] URP asset has Main Light Shadows disabled. Enable it in the URP asset to see shadows.");
            if (logDebug && enableAdditionalLightShadows && !addOk)
                Debug.LogWarning("[LightManager] URP asset has Additional Light Shadows disabled. Enable it in the URP asset to see additional light shadows.");
        }
    }

    private void EnsureSunLight()
    {
        // Pick sun if not assigned
        if (sunLight == null)
        {
            if (RenderSettings.sun != null)
            {
                sunLight = RenderSettings.sun;
            }
            else
            {
                // Try find any directional light in scene
                var lights = GameObject.FindObjectsOfType<Light>(true);
                foreach (var l in lights)
                {
                    if (l.type == LightType.Directional)
                    {
                        sunLight = l;
                        break;
                    }
                }
            }
        }

        // Ensure RenderSettings.sun is set for consistency (if we have a directional)
        if (sunLight != null && RenderSettings.sun == null)
        {
            RenderSettings.sun = sunLight;
        }

        // Make sure it casts shadows
        if (sunLight != null && forceMainLightShadows)
        {
            if (sunLight.shadows == LightShadows.None)
                sunLight.shadows = LightShadows.Soft; // Soft includes Hard capability
        }
    }

    private void EnsureCameraShadows()
    {
        var cams = Camera.allCameras;
        for (int i = 0; i < cams.Length; i++)
        {
            var cam = cams[i];
            UniversalAdditionalCameraData data = null;
            // Try extension method (URP) first
            try { data = cam.GetUniversalAdditionalCameraData(); } catch { /* ignore */ }
            if (data == null)
                data = cam.GetComponent<UniversalAdditionalCameraData>();

            if (data != null && !data.renderShadows)
            {
                data.renderShadows = true;
            }
        }
    }
}
