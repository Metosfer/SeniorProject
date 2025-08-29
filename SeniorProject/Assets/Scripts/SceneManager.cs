using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using System.Collections;

public class SceneManager : MonoBehaviour
{
    private Camera mainCamera;
    private void Start() {
        mainCamera = FindAnyObjectByType<Camera>();
        // Wire up onClick in code (no need to set in Editor)
        WireButtonOnClicks();
        // Setup hover scale handlers
        SetupChangeFarmButtonHoverScale();
    }

    [Header("Change Scene Buttons")]
    [Tooltip("Farm sahnesine geçiş butonu")] public Button changeFarmButton;

    [Header("Hover Scale (Farm Button)")]
    [Tooltip("ChangeFarm butonuna hover scale efekti uygula")] public bool enableHoverScaleForChangeFarmButton = true;
    [Tooltip("Hedef ölçek çarpanı (1 = orijinal)")] public float changeFarmButtonHoverScale = 1.08f;
    [Tooltip("Hover'a geçiş süresi (sn)")] public float changeFarmButtonScaleInDuration = 0.12f;
    [Tooltip("Hover'dan çıkış süresi (sn)")] public float changeFarmButtonScaleOutDuration = 0.12f;
    private Vector3 _changeFarmBtnInitialScale;
    private Coroutine _changeFarmBtnScaleCo;

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

    private void OnDisable()
    {
        if (changeFarmButton != null && enableHoverScaleForChangeFarmButton)
        {
            if (_changeFarmBtnScaleCo != null) StopCoroutine(_changeFarmBtnScaleCo);
            var rt = changeFarmButton.transform as RectTransform;
            if (rt != null && _changeFarmBtnInitialScale != Vector3.zero)
                rt.localScale = _changeFarmBtnInitialScale;
        }
    }

    private void WireButtonOnClicks()
    {
        if (changeFarmButton != null)
        {
            changeFarmButton.onClick.RemoveAllListeners();
            changeFarmButton.onClick.AddListener(ChangeToFarmScene);
        }
    }

    private void SetupChangeFarmButtonHoverScale()
    {
        if (!enableHoverScaleForChangeFarmButton || changeFarmButton == null) return;
        var rt = changeFarmButton.transform as RectTransform;
        if (rt == null) return;
        _changeFarmBtnInitialScale = rt.localScale;

        var et = changeFarmButton.GetComponent<EventTrigger>();
        if (et == null) et = changeFarmButton.gameObject.AddComponent<EventTrigger>();
        AddOrReplaceTrigger(et, EventTriggerType.PointerEnter, OnChangeFarmButtonPointerEnter);
        AddOrReplaceTrigger(et, EventTriggerType.PointerExit, OnChangeFarmButtonPointerExit);
    }

    private void AddOrReplaceTrigger(EventTrigger et, EventTriggerType type, System.Action<BaseEventData> callback)
    {
        if (et == null) return;
        if (et.triggers == null) et.triggers = new System.Collections.Generic.List<EventTrigger.Entry>();
        et.triggers.RemoveAll(e => e != null && e.eventID == type);
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback = new EventTrigger.TriggerEvent();
        entry.callback.AddListener(new UnityEngine.Events.UnityAction<BaseEventData>(callback));
        et.triggers.Add(entry);
    }

    private void OnChangeFarmButtonPointerEnter(BaseEventData _)
    {
        if (!enableHoverScaleForChangeFarmButton || changeFarmButton == null) return;
        var rt = changeFarmButton.transform as RectTransform;
        if (rt == null) return;
        if (_changeFarmBtnScaleCo != null) StopCoroutine(_changeFarmBtnScaleCo);
        Vector3 target = _changeFarmBtnInitialScale * Mathf.Max(0.01f, changeFarmButtonHoverScale);
        _changeFarmBtnScaleCo = StartCoroutine(ScaleRectTransform(rt, target, Mathf.Max(0.01f, changeFarmButtonScaleInDuration)));
    }

    private void OnChangeFarmButtonPointerExit(BaseEventData _)
    {
        if (!enableHoverScaleForChangeFarmButton || changeFarmButton == null) return;
        var rt = changeFarmButton.transform as RectTransform;
        if (rt == null) return;
        if (_changeFarmBtnScaleCo != null) StopCoroutine(_changeFarmBtnScaleCo);
        _changeFarmBtnScaleCo = StartCoroutine(ScaleRectTransform(rt, _changeFarmBtnInitialScale, Mathf.Max(0.01f, changeFarmButtonScaleOutDuration)));
    }

    private IEnumerator ScaleRectTransform(RectTransform rt, Vector3 target, float duration)
    {
        Vector3 start = rt.localScale;
        if (duration <= 0.0001f) { rt.localScale = target; yield break; }
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / duration);
            rt.localScale = Vector3.Lerp(start, target, u);
            yield return null;
        }
        rt.localScale = target;
    }
}