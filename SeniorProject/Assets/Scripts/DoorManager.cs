using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class DoorManager : MonoBehaviour
{
    public TMP_Text doorText;
    public float range = 5f;
    public Transform playerTransform;
    // Start is called before the first frame update
    void Start()
    {
        doorText.fontSize = 0f;
        if (playerTransform == null)
        {
            playerTransform = GameObject.FindGameObjectWithTag("Player").transform;
        }
    }
private void FixedUpdate() {
    CheckTextDistance();
}
    // Update is called once per frame
    void Update()
    {

        
        // T tuşuna basıldığında kontrol et
        if (Input.GetKeyDown(KeyCode.T))
        {
            CheckPlayerDistance();
        }
    }
    void CheckTextDistance()
    {
    float distance = Vector3.Distance(playerTransform.position, transform.position);
    if (distance <= range)
    {
        doorText.fontSize = 36f;
    }
    else
    {
        doorText.fontSize = 0f;
    }
   }
    void CheckPlayerDistance()
    {
        // Oyuncu ile kapı arasındaki mesafeyi hesapla
        float distance = Vector3.Distance(playerTransform.position, transform.position);
        

        if (distance <= range)
        {
            
            Vector3 tempRot = new Vector3(0, -90, 0f);
            
            transform.rotation = Quaternion.Euler(tempRot);
            Debug.Log("Kapı Açıldı");
            
            // Sahne değişmeden önce otomatik save
            AutoSaveBeforeSceneChange();
            
            UnityEngine.SceneManagement.SceneManager.LoadScene("ShopScene");
        }
        else
        {
            
            Debug.Log("Kapıya çok uzaksın!");
        }
    }
    
    void AutoSaveBeforeSceneChange()
    {
        // Gelişmiş save sistemini kullan
        GameSaveManager saveManager = FindObjectOfType<GameSaveManager>();
        if (saveManager != null)
        {
            saveManager.SaveGame();
            Debug.Log("Otomatik save tamamlandı - sahne değişimi öncesi!");
        }
        else
        {
            // Eski save sistemine fallback
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            string saveTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // Oyuncunun pozisyonunu kaydet
            if (playerTransform != null)
            {
                PlayerPrefs.SetFloat("PlayerPosX", playerTransform.position.x);
                PlayerPrefs.SetFloat("PlayerPosY", playerTransform.position.y);
                PlayerPrefs.SetFloat("PlayerPosZ", playerTransform.position.z);
                Debug.Log("Oyuncu pozisyonu kaydedildi (eski sistem): " + playerTransform.position);
            }

            PlayerPrefs.SetString("SavedScene_" + saveTime, currentScene);
            
            // Save times listesini güncelle
            string times = PlayerPrefs.GetString("SaveTimes", "");
            List<string> saveTimes = new List<string>(times.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
            saveTimes.Add(saveTime);
            if (saveTimes.Count > 3) saveTimes.RemoveAt(0); // Maksimum 3 save
            
            PlayerPrefs.SetString("SaveTimes", string.Join(",", saveTimes.ToArray()));
            PlayerPrefs.Save();
            Debug.Log("Otomatik save tamamlandı (eski sistem): " + saveTime);
        }
    }
    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range);   
    }


}