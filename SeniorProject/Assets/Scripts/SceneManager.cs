using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;

public class SceneManager : MonoBehaviour
{
    public void ChangeToShopScene()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("ShopScene");
    }
    public void ChangeToFarmScene()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("FarmScene");
    }
}