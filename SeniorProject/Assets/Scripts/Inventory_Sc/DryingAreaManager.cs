using UnityEngine;
using UnityEngine.SceneManagement;

public class DryingAreaManager : MonoBehaviour, ISaveable
{
    [Header("Drying Settings")]
    public DryingSlot[] dryingSlots = new DryingSlot[3];
    
    [Header("Player Interaction")]
    public float interactionRange = 3f;
        [Header("Save Identity")]
        public string saveId = "DryingArea";
    public KeyCode interactionKey = KeyCode.T   ;
    public GameObject interactionUI; // "Press T to open drying area" UI
    [Tooltip("UI titremesini önlemek için çıkış eşiği (histerezis). İçeri giriş: interactionRange, dışarı çıkış: interactionRange + bu değer")]
    public float rangeHysteresis = 0.3f;

    [Header("UI References")]
    public DryingAreaUI dryingUI;
    
    private bool playerInRange = false;
    private Transform playerTransform;
    private Inventory playerInventory;
    
    [Header("Flicker Control")]
    [Tooltip("Sahne yüklendikten sonra UI değişikliklerini bastırma süresi (sn)")]
    public float initialUISuppressDuration = 0.3f;
    [Tooltip("UI görünürlük değişimini uygulamadan önce gereken stabil süre (sn)")]
    public float uiStateStabilityTime = 0.08f;

    private float _uiSuppressUntil;
    private bool _uiShown;
    private bool _uiDesiredLast;
    private float _uiDesiredSince;

    private void Start()
    {
        // Player referanslarını bul
        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
        playerInventory = FindObjectOfType<Inventory>();
    // Scene load sonrası kısa süre UI'ı bastır (flicker önlemek için)
    _uiSuppressUntil = Time.unscaledTime + Mathf.Max(0f, initialUISuppressDuration);
    UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        
        // Interaction UI'yı başlangıçta gizle
        if (interactionUI != null)
        {
            interactionUI.SetActive(false);
            _uiShown = false;
        }
        
        // Slot'ları sıfırla
        for (int i = 0; i < dryingSlots.Length; i++)
        {
            if (dryingSlots[i] != null)
            {
                dryingSlots[i].ResetSlot();
            }
        }
    }

    private void OnDestroy()
    {
    UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Yeni sahnede tekrar bastırma penceresi başlat
        _uiSuppressUntil = Time.unscaledTime + Mathf.Max(0f, initialUISuppressDuration);
        // UI'ı güvenli şekilde kapat
        if (interactionUI != null)
        {
            interactionUI.SetActive(false);
            _uiShown = false;
        }
    }

    private void Update()
    {
        // Player mesafe kontrolü (Market gibi modal UI'lar açıkken bastır)
        CheckPlayerDistance();

        // T tuşu kontrolü (input işlemleri Update'te olmalı)
        if (playerInRange && !MarketManager.IsAnyOpen && Input.GetKeyDown(interactionKey))
        {
            if (dryingUI != null)
            {
                dryingUI.TogglePanel();
            }
        }

        // Kurutma timer'ları güncelle
        foreach (DryingSlot slot in dryingSlots)
        {
            if (slot != null && slot.isOccupied && !slot.isReadyToCollect)
            {
                slot.timer -= Time.deltaTime;
                if (slot.timer <= 0f)
                {
                    CompleteDrying(slot);
                }
            }
        }
    }

    private void CheckPlayerDistance()
    {
        if (playerTransform == null)
        {
            if (interactionUI != null && interactionUI.activeSelf) interactionUI.SetActive(false);
            playerInRange = false;
            return;
        }
        // Modal UI açıkken (Market vb.) proximity UI gösterme ve range'i false say
        if (MarketManager.IsAnyOpen)
        {
            if (interactionUI != null && interactionUI.activeSelf)
            {
                interactionUI.SetActive(false);
                _uiShown = false;
            }
            playerInRange = false;
            return;
        }
        
        float distance = Vector3.Distance(transform.position, playerTransform.position);
        bool wasInRange = playerInRange;
        // Histerezis: içeri giriş interactionRange ile, dışarı çıkış interactionRange + rangeHysteresis ile
        if (playerInRange)
        {
            if (distance > interactionRange + Mathf.Max(0f, rangeHysteresis))
                playerInRange = false;
        }
        else
        {
            if (distance <= interactionRange)
                playerInRange = true;
        }
        
        // Player range'e girdi/çıktı: UI görünürlüğünü stabil/delay ile işle
        UpdateInteractionUI(playerInRange);
    }

    private void UpdateInteractionUI(bool desiredVisible)
    {
        if (interactionUI == null) return;
        // Sahne yüklendikten sonra kısa süre UI bastır
        if (Time.unscaledTime < _uiSuppressUntil)
        {
            if (_uiShown)
            {
                interactionUI.SetActive(false);
                _uiShown = false;
            }
            // Suppress süresi bitene kadar state izle ama uygulama
            _uiDesiredLast = desiredVisible;
            _uiDesiredSince = Time.unscaledTime;
            return;
        }

        if (desiredVisible != _uiDesiredLast)
        {
            _uiDesiredLast = desiredVisible;
            _uiDesiredSince = Time.unscaledTime;
        }

        // Stabil kaldıysa uygula
        if (Time.unscaledTime - _uiDesiredSince >= Mathf.Max(0f, uiStateStabilityTime))
        {
            if (_uiShown != desiredVisible)
            {
                interactionUI.SetActive(desiredVisible);
                _uiShown = desiredVisible;
            }
        }
    }

    public bool TryAddItemToSlot(int slotIndex, SCItem item)
    {
        if (slotIndex < 0 || slotIndex >= dryingSlots.Length)
            return false;
            
        DryingSlot slot = dryingSlots[slotIndex];
        if (slot == null) return false;
        
        // Slot boş mu ve item kurutulabilir mi kontrol et
        if (!slot.isOccupied && item.canBeDried && item.dryingTime > 0)
        {
            slot.currentItemData = item;
            slot.timer = item.dryingTime;
            slot.isOccupied = true;
            slot.isReadyToCollect = false;
            
            Debug.Log($"Item '{item.itemName}' kurutma slot {slotIndex}'a eklendi. Süre: {item.dryingTime}s");
            // Snapshot update
            if (GameSaveManager.Instance != null)
            {
                try { GameSaveManager.Instance.CaptureSceneObjectsSnapshotNow(); } catch {}
            }
            return true;
        }
        
        return false;
    }

    private void CompleteDrying(DryingSlot slot)
    {
        if (slot.currentItemData != null)
        {
            slot.isReadyToCollect = true;
            Debug.Log($"Kurutma tamamlandı: {slot.currentItemData.itemName}");
            if (GameSaveManager.Instance != null)
            {
                try { GameSaveManager.Instance.CaptureSceneObjectsSnapshotNow(); } catch {}
            }
        }
    }

    public void CollectSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= dryingSlots.Length)
            return;
            
        DryingSlot slot = dryingSlots[slotIndex];
        if (slot == null) return;
        
        if (slot.isReadyToCollect && slot.currentItemData != null)
        {
            Debug.Log($"CollectSlot: slotIndex={slotIndex}, item={slot.currentItemData.itemName}, driedVersion={(slot.currentItemData.driedVersion != null ? slot.currentItemData.driedVersion.itemName : "null")}");
            // Kurutulmuş item'ı inventory'e ekle
            if (playerInventory != null && slot.currentItemData.driedVersion != null)
            {
                bool added = playerInventory.playerInventory.AddItem(slot.currentItemData.driedVersion);
                Debug.Log($"AddItem(driedVersion): {slot.currentItemData.driedVersion.itemName}, result={added}");
                if (added)
                {
                    Debug.Log($"Kurutulmuş item toplandı: {slot.currentItemData.driedVersion.itemName}");
                    slot.ResetSlot();
                    if (GameSaveManager.Instance != null)
                    {
                        try { GameSaveManager.Instance.CaptureSceneObjectsSnapshotNow(); } catch {}
                    }
                }
                else
                {
                    Debug.LogWarning("Inventory dolu! Kurutulmuş item toplanamadı.");
                }
            }
            else if (slot.currentItemData.driedVersion == null)
            {
                // Eğer dried version yoksa orijinal item'ı geri ver
                bool added = playerInventory.playerInventory.AddItem(slot.currentItemData);
                Debug.Log($"AddItem(original): {slot.currentItemData.itemName}, result={added}");
                if (added)
                {
                    Debug.Log($"Item toplandı (dried version yok): {slot.currentItemData.itemName}");
                    slot.ResetSlot();
                    if (GameSaveManager.Instance != null)
                    {
                        try { GameSaveManager.Instance.CaptureSceneObjectsSnapshotNow(); } catch {}
                    }
                }
                else
                {
                    Debug.LogWarning("Inventory dolu! Item toplanamadı.");
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Interaction range'i göster
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }

    // ISaveable implementation
    public System.Collections.Generic.Dictionary<string, object> GetSaveData()
    {
        var data = new System.Collections.Generic.Dictionary<string, object>();
        // Basic transform
        data["position"] = transform.position;
        data["rotation"] = transform.eulerAngles;
        data["scale"] = transform.localScale;
        // Slots
        for (int i = 0; i < dryingSlots.Length; i++)
        {
            var s = dryingSlots[i]; if (s == null) continue;
            string p = $"slot{i}";
            data[$"{p}.occupied"] = s.isOccupied;
            data[$"{p}.ready"] = s.isReadyToCollect;
            data[$"{p}.timer"] = s.timer;
            data[$"{p}.itemName"] = s.currentItemData != null ? s.currentItemData.itemName : string.Empty;
        }
        return data;
    }

    public void LoadSaveData(System.Collections.Generic.Dictionary<string, object> data)
    {
        if (data == null) return;
        // Transform
        TryParseVector3(data, "position", out var pos); transform.position = pos;
        TryParseVector3(data, "rotation", out var rot); transform.eulerAngles = rot;
        TryParseVector3(data, "scale", out var scl); transform.localScale = scl;
        // Slots
        for (int i = 0; i < dryingSlots.Length; i++)
        {
            var s = dryingSlots[i]; if (s == null) continue;
            string p = $"slot{i}";
            bool occ = GetBool(data, $"{p}.occupied");
            bool ready = GetBool(data, $"{p}.ready");
            float timer = GetFloat(data, $"{p}.timer");
            string itemName = GetString(data, $"{p}.itemName");
            if (!string.IsNullOrEmpty(itemName))
            {
                s.currentItemData = FindItem(itemName);
            }
            else
            {
                s.currentItemData = null;
            }
            s.isOccupied = occ;
            s.isReadyToCollect = ready;
            s.timer = timer;
        }
    }

    private static bool TryParseVector3(System.Collections.Generic.Dictionary<string, object> d, string k, out Vector3 v)
    {
        v = Vector3.zero; if (!d.ContainsKey(k)) return false; var s = d[k] as string; if (string.IsNullOrEmpty(s)) return false;
        var parts = s.Split(','); if (parts.Length != 3) return false;
        if (float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) &&
            float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y) &&
            float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z))
        { v = new Vector3(x, y, z); return true; }
        return false;
    }
    private static bool GetBool(System.Collections.Generic.Dictionary<string, object> d, string k)
    { return d.ContainsKey(k) && (d[k]?.ToString() == "true" || d[k]?.ToString() == "True"); }
    private static float GetFloat(System.Collections.Generic.Dictionary<string, object> d, string k)
    { return d.ContainsKey(k) ? float.Parse(d[k]?.ToString() ?? "0", System.Globalization.CultureInfo.InvariantCulture) : 0f; }
    private static string GetString(System.Collections.Generic.Dictionary<string, object> d, string k)
    { return d.ContainsKey(k) ? (d[k]?.ToString() ?? string.Empty) : string.Empty; }
    private static SCItem FindItem(string name)
    {
        if (string.IsNullOrEmpty(name)) return null; var all = Resources.LoadAll<SCItem>("");
        for (int i = 0; i < all.Length; i++) if (all[i] != null && all[i].itemName == name) return all[i];
        return null;
    }
}
