using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;

public class SceneManager : MonoBehaviour
{
    private Camera mainCamera;
    private void Start() {
        mainCamera = FindAnyObjectByType<Camera>();
    }
    public void ChangeToShopScene()
    {
        Debug.Log("[SceneManager] Changing to ShopScene, saving game data including Flask...");
        // Save current scene state before changing (if save system is available)
        var saveManager = GameSaveManager.Instance ?? FindObjectOfType<GameSaveManager>();
        if (saveManager != null)
        {
            saveManager.SaveGame();
            Debug.Log("[SceneManager] Game saved successfully before scene change");
        }
        else
        {
            Debug.LogWarning("[SceneManager] No GameSaveManager found, data not saved");
        }
        UnityEngine.SceneManagement.SceneManager.LoadScene("ShopScene");
    }
    public void ChangeToFarmScene()
    {
        Debug.Log("[SceneManager] Changing to FarmScene, saving game data including Flask...");
        // Save current scene state before changing (if save system is available)
        var saveManager = GameSaveManager.Instance ?? FindObjectOfType<GameSaveManager>();
        if (saveManager != null)
        {
            saveManager.SaveGame();
            Debug.Log("[SceneManager] Game saved successfully before scene change");
        }
        else
        {
            Debug.LogWarning("[SceneManager] No GameSaveManager found, data not saved");
        }
        UnityEngine.SceneManagement.SceneManager.LoadScene("FarmScene");
    }
}