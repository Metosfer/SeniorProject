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
        // Save current scene state before changing (if save system is available)
        var saveManager = GameSaveManager.Instance ?? FindObjectOfType<GameSaveManager>();
        if (saveManager != null)
        {
            saveManager.SaveGame();
        }
        UnityEngine.SceneManagement.SceneManager.LoadScene("ShopScene");
    }
    public void ChangeToFarmScene()
    {
        // Save current scene state before changing (if save system is available)
        var saveManager = GameSaveManager.Instance ?? FindObjectOfType<GameSaveManager>();
        if (saveManager != null)
        {
            saveManager.SaveGame();
        }
        UnityEngine.SceneManagement.SceneManager.LoadScene("FarmScene");
    }
}