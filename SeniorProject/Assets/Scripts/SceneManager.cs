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
        UnityEngine.SceneManagement.SceneManager.LoadScene("ShopScene");
    }
    public void ChangeToFarmScene()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("FarmScene");
    }
}