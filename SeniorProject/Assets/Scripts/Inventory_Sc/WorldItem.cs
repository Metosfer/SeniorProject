using System;
using System.Collections.Generic;
using UnityEngine;

public class WorldItem : MonoBehaviour
{
    [Header("Item Data")]
    public SCItem item;
    public int quantity = 1;
    
    [Header("Pickup Settings")]
    public float pickupRange = 2f;
    public KeyCode pickupKey = KeyCode.E;
    public GameObject pickupUI; // "Press E to pickup" UI
    
    private bool playerInRange = false;
    private Inventory playerInventory;
    private bool pendingPickup = false; // wait until TakeItem anim finishes
    private PlayerAnimationController playerAnim;
    private bool destroyedByPickup;

    public enum WorldItemOrigin
    {
        ScenePlaced,
        ManualDrop,
        AutoSpawn,
        Other
    }

    [SerializeField] private string persistentId;
    [SerializeField] private WorldItemOrigin origin = WorldItemOrigin.ScenePlaced;

    private static readonly Dictionary<string, WeakReference<WorldItem>> s_registry = new Dictionary<string, WeakReference<WorldItem>>();

    private void Awake()
    {
        EnsurePersistentId();
    }

    private void Start()
    {
        // Player inventory'sini bul veya persistent inventory kullan
        playerInventory = FindObjectOfType<Inventory>();
        if (playerInventory == null || playerInventory.playerInventory == null)
        {
            // Eğer Inventory component'i yoksa, persistent inventory'yi direkt kullan
            // Bu durumda bir dummy Inventory oluşturacağız
            GameObject inventoryGO = new GameObject("TempInventory");
            playerInventory = inventoryGO.AddComponent<Inventory>();
            playerInventory.playerInventory = SCInventory.GetPersistentInventory();
        }
        
        // Pickup UI'yı başlangıçta gizle
        if (pickupUI != null)
        {
            pickupUI.SetActive(false);
        }

    // Player anim controller (opsiyonel)
    playerAnim = FindObjectOfType<PlayerAnimationController>();
    }

    private void Update()
    {
        // Player range'de ise pickup kontrolü
        if (!pendingPickup && playerInRange && Input.GetKeyDown(pickupKey))
        {
            BeginPickupSequence();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            ShowPickupUI(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            ShowPickupUI(false);
        }
    }

    private void BeginPickupSequence()
    {
        if (pendingPickup) return;
        // Hide prompt during animation
        ShowPickupUI(false);

        // Hangi durumlarda TakeItem oynatılacak?
        bool shouldTakeItem = false;
        // 1) Item referansı varsa ve balık veya tohum ise
        if (item != null && (item.isFish || item.isSeed))
            shouldTakeItem = true;
        // 2) Bucket veya Harrow gibi özel objeler bu script ile temsilen yerdeyse isme göre ipucu (opsiyonel, güvenli)
        string n = gameObject.name.ToLower();
        if (n.Contains("bucket") || n.Contains("harrow"))
            shouldTakeItem = true;

        if (playerAnim != null)
        {
            pendingPickup = true;
            if (shouldTakeItem) playerAnim.TriggerTakeItem();
            StartCoroutine(WaitTakeItemThenPickup());
        }
        else
        {
            // Fallback: no animator found
            TryPickup();
        }
    }

    private System.Collections.IEnumerator WaitTakeItemThenPickup()
    {
        // small delay to register trigger
        yield return new WaitForEndOfFrame();
        // Wait while animation is reported active (with a safety timeout)
        float t = 0f;
        const float timeout = 2.0f;
        while (playerAnim != null && playerAnim.IsTakingItem() && t < timeout)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        pendingPickup = false;
        TryPickup();
    }

    private void TryPickup()
    {
        if (playerInventory != null && item != null)
        {
            // Inventory'e eklemeyi dene
            bool added = playerInventory.playerInventory.AddItem(item);
            
            if (added)
            {
                Debug.Log($"Picked up: {item.itemName} x{quantity}");
                // Notify save system immediately
                if (GameSaveManager.Instance != null)
                {
                    try { GameSaveManager.Instance.OnWorldItemPickedUp(this); } catch {}
                }
                
                // Eğer quantity 1'den fazlaysa, sadece 1 tane al ve quantity'yi azalt
                if (quantity > 1)
                {
                    quantity--;
                    UpdateWorldItemDisplay();
                }
                else
                {
                    // Quantity 1 ise eşyayı tamamen kaldır
                    ShowPickupUI(false);
                    destroyedByPickup = true;
                    Destroy(gameObject);
                }
            }
            else
            {
                Debug.Log("Inventory full! Cannot pickup item.");
                // UI feedback verilebilir (inventory dolu mesajı vs.)
                // Re-show prompt so player knows they can try again later
                ShowPickupUI(true);
            }
        }
    }

    private void UpdateWorldItemDisplay()
    {
        // Quantity'ye göre item boyutunu güncelle (opsiyonel)
        float scale = Mathf.Clamp(0.3f + (quantity * 0.1f), 0.3f, 1f);
        transform.localScale = Vector3.one * scale;
    }

    private void ShowPickupUI(bool show)
    {
        if (pickupUI != null)
        {
            pickupUI.SetActive(show);
        }
    }

    // Inspector'da item bilgilerini göstermek için
    private void OnValidate()
    {
        if (item != null)
        {
            gameObject.name = $"WorldItem_{item.itemName}";
        }
        if (!Application.isPlaying)
        {
            EnsurePersistentId();
        }
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying) return;
        if (destroyedByPickup) return;
        if (GameSaveManager.Instance == null) return;
        if (GameSaveManager.Instance.IsRestoringScene) return;

        try
        {
            GameSaveManager.Instance.CaptureWorldItemSnapshot(this);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"WorldItem OnDestroy snapshot failed: {ex.Message}");
        }

        if (!string.IsNullOrEmpty(persistentId))
        {
            s_registry.Remove(persistentId);
        }
    }

    public string PersistentId => EnsurePersistentId();
    public WorldItemOrigin Origin => origin;

    public void ApplyPersistentId(string id, WorldItemOrigin newOrigin)
    {
        persistentId = string.IsNullOrEmpty(id) ? GenerateNewId() : id;
        origin = newOrigin;
        RegisterPersistentId();
    }

    public void MarkRuntime(WorldItemOrigin newOrigin)
    {
        origin = newOrigin;
        EnsurePersistentId();
    }

    private string EnsurePersistentId()
    {
        if (string.IsNullOrEmpty(persistentId))
        {
            persistentId = GenerateNewId();
        }

        if (s_registry.TryGetValue(persistentId, out var existing) && existing.TryGetTarget(out var other) && other != null && other != this)
        {
            persistentId = GenerateNewId();
        }

        RegisterPersistentId();
        return persistentId;
    }

    private void RegisterPersistentId()
    {
        if (string.IsNullOrEmpty(persistentId)) return;
        s_registry[persistentId] = new WeakReference<WorldItem>(this);
    }

    private static string GenerateNewId()
    {
        return Guid.NewGuid().ToString("N");
    }
}
