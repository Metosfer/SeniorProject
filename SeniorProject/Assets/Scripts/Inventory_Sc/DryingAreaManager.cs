using UnityEngine;
using UnityEngine.SceneManagement;

public class DryingAreaManager : MonoBehaviour
{
    [Header("Drying Settings")]
    public DryingSlot[] dryingSlots = new DryingSlot[3];
    
    [Header("Player Interaction")]
    public float interactionRange = 3f;
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
}
