using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SkyManager - Skybox rotation efekti ile hareketli gökyüzü yaratır
/// </summary>
public class SkyManager : MonoBehaviour
{
    [Header("Skybox Settings")]
    [Tooltip("Rotate edilecek skybox materyali")]
    public Material skyboxMaterial;
    [Tooltip("Rotation hızı (derece/saniye)")]
    [Range(-10f, 10f)]
    public float rotationSpeed = 1f;
    [Tooltip("Başlangıç rotation değeri")]
    [Range(0f, 360f)]
    public float initialRotation = 0f;
    
    [Header("Advanced Settings")]
    [Tooltip("Otomatik olarak mevcut skybox materyalini bul")]
    public bool autoFindSkyboxMaterial = true;
    [Tooltip("Rotation değerinin property name'i (genellikle '_Rotation')")]
    public string rotationPropertyName = "_Rotation";
    [Tooltip("Pause esnasında durdur")]
    public bool pauseWhenGamePaused = true;
    
    [Header("Debug")]
    [Tooltip("Console'a rotation bilgilerini yazdır")]
    public bool debugRotation = false;
    [Tooltip("Manual rotation test (Inspector'da test için)")]
    [Range(0f, 360f)]
    public float manualRotationTest = 0f;
    [Tooltip("Manual test aktif")]
    public bool useManualTest = false;
    
    // Private variables
    private float currentRotation = 0f;
    private bool isRotating = true;
    
    void Start()
    {
        InitializeSkybox();
        currentRotation = initialRotation;
        
        // Başlangıç rotation değerini uygula
        ApplySkyboxRotation(currentRotation);
        
        Debug.Log($"🌌 SkyManager initialized - Speed: {rotationSpeed}°/s, Initial: {initialRotation}°");
    }

    void Update()
    {
        // Manual test modu
        if (useManualTest)
        {
            ApplySkyboxRotation(manualRotationTest);
            return;
        }
        
        // Pause kontrolü
        if (pauseWhenGamePaused && Time.timeScale == 0f)
        {
            return;
        }
        
        // Rotation efekti
        if (isRotating && skyboxMaterial != null)
        {
            UpdateSkyboxRotation();
        }
    }
    
    /// <summary>
    /// Skybox materyalini initialize et
    /// </summary>
    private void InitializeSkybox()
    {
        // Otomatik olarak skybox materyalini bul
        if (autoFindSkyboxMaterial && skyboxMaterial == null)
        {
            skyboxMaterial = RenderSettings.skybox;
            if (skyboxMaterial != null)
            {
                Debug.Log($"🌌 Auto-found skybox material: {skyboxMaterial.name}");
            }
            else
            {
                Debug.LogWarning("🌌 No skybox material found in RenderSettings!");
            }
        }
        
        // Skybox materyalin rotation property'si var mı kontrol et
        if (skyboxMaterial != null && !skyboxMaterial.HasProperty(rotationPropertyName))
        {
            Debug.LogWarning($"🌌 Skybox material '{skyboxMaterial.name}' does not have property '{rotationPropertyName}'!");
        }
    }
    
    /// <summary>
    /// Skybox rotation'ı güncelle
    /// </summary>
    private void UpdateSkyboxRotation()
    {
        // Rotation değerini arttır
        currentRotation += rotationSpeed * Time.deltaTime;
        
        // 0-360 aralığında tut
        currentRotation = currentRotation % 360f;
        if (currentRotation < 0f)
        {
            currentRotation += 360f;
        }
        
        // Rotation'ı uygula
        ApplySkyboxRotation(currentRotation);
        
        // Debug bilgisi
        if (debugRotation && Time.frameCount % 60 == 0) // Her saniye bir yazdır
        {
            Debug.Log($"🌌 Skybox rotation: {currentRotation:F1}°");
        }
    }
    
    /// <summary>
    /// Skybox rotation değerini materyale uygula
    /// </summary>
    private void ApplySkyboxRotation(float rotation)
    {
        if (skyboxMaterial != null && skyboxMaterial.HasProperty(rotationPropertyName))
        {
            skyboxMaterial.SetFloat(rotationPropertyName, rotation);
        }
    }
    
    /// <summary>
    /// Rotation'ı başlat/durdur
    /// </summary>
    public void SetRotationEnabled(bool enabled)
    {
        isRotating = enabled;
        Debug.Log($"🌌 Skybox rotation {(enabled ? "enabled" : "disabled")}");
    }
    
    /// <summary>
    /// Rotation hızını değiştir
    /// </summary>
    public void SetRotationSpeed(float speed)
    {
        rotationSpeed = speed;
        Debug.Log($"🌌 Skybox rotation speed set to: {speed}°/s");
    }
    
    /// <summary>
    /// Rotation değerini direkt set et
    /// </summary>
    public void SetRotation(float rotation)
    {
        currentRotation = rotation % 360f;
        if (currentRotation < 0f)
        {
            currentRotation += 360f;
        }
        
        ApplySkyboxRotation(currentRotation);
        Debug.Log($"🌌 Skybox rotation set to: {currentRotation:F1}°");
    }
    
    /// <summary>
    /// Mevcut rotation değerini al
    /// </summary>
    public float GetCurrentRotation()
    {
        return currentRotation;
    }
    
    /// <summary>
    /// Skybox materyalini runtime'da değiştir
    /// </summary>
    public void SetSkyboxMaterial(Material newMaterial)
    {
        skyboxMaterial = newMaterial;
        RenderSettings.skybox = newMaterial;
        
        // Yeni materyale mevcut rotation'ı uygula
        ApplySkyboxRotation(currentRotation);
        
        Debug.Log($"🌌 Skybox material changed to: {(newMaterial != null ? newMaterial.name : "null")}");
    }
}
