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
    [Header("Prompt Stability")]
    public bool usePlanarDistance = true;
    public float promptUpdateInterval = 0.1f;
    public float rangeHysteresis = 0.3f;
    public float promptMinOnDuration = 0.25f;
    public float promptMinOffDuration = 0.2f;
    public float inputGraceWindow = 0.25f;

    private float _lastUIUpdateTime;
    private bool _inRangeSticky;
    private bool _lastPromptShown;
    private float _lastPromptChangeTime;
    private float _lastInRangeTime;
    // Start is called before the first frame update
    void Start()
    {
        if (doorText != null) doorText.fontSize = 0f;
        if (playerTransform == null)
        {
            playerTransform = GameObject.FindGameObjectWithTag("Player").transform;
        }
    }
    // Remove per-physics flicker; throttle in Update instead
    // Update is called once per frame
    void Update()
    {
        UpdateDoorPromptThrottled();
        // T tuşuna basıldığında kontrol et
        if (Input.GetKeyDown(KeyCode.T))
        {
            CheckPlayerDistance();
        }
    }
    void UpdateDoorPromptThrottled()
    {
        if (playerTransform == null || doorText == null) return;
        float now = Time.unscaledTime;
        bool timeToUpdate = now - _lastUIUpdateTime >= promptUpdateInterval;

        // Distance with optional planar mode
        Vector3 a = transform.position;
        Vector3 b = playerTransform.position;
        if (usePlanarDistance)
        {
            a.y = 0f; b.y = 0f;
        }
        float baseRange = Mathf.Max(0.1f, range);
        float enter = baseRange + Mathf.Max(0f, rangeHysteresis);
        float exit = baseRange - Mathf.Max(0f, rangeHysteresis);
        float dist = Vector3.Distance(a, b);

        if (!_inRangeSticky && dist <= enter) _inRangeSticky = true;
        else if (_inRangeSticky && dist > exit) _inRangeSticky = false;

        if (_inRangeSticky)
        {
            _lastInRangeTime = now;
        }

        if (timeToUpdate)
        {
            _lastUIUpdateTime = now;
            bool desired = _inRangeSticky;
            bool target = desired;
            float since = now - _lastPromptChangeTime;
            if (_lastPromptShown && !desired && since < promptMinOnDuration)
                target = true; // keep on briefly
            else if (!_lastPromptShown && desired && since < promptMinOffDuration)
                target = false; // keep off briefly to avoid blink

            float size = target ? 36f : 0f;
            if ((_lastPromptShown && !target) || (!_lastPromptShown && target))
            {
                _lastPromptChangeTime = now;
            }
            _lastPromptShown = target;
            if (!Mathf.Approximately(doorText.fontSize, size))
                doorText.fontSize = size;
        }
        else
        {
            // hold current visual state; no action
        }
    }
    void CheckPlayerDistance()
    {
        if (playerTransform == null) return;
        // Allow interaction if currently in range or very recently in range
        bool canInteract = _inRangeSticky || (Time.unscaledTime - _lastInRangeTime <= inputGraceWindow);
        if (canInteract)
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
    private void OnDisable()
    {
        if (doorText != null) doorText.fontSize = 0f;
        _inRangeSticky = false;
        _lastPromptShown = false;
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
                PlayerPrefs.SetFloat("PlayerPosY", playerTransform.position.y + 0.5f);
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