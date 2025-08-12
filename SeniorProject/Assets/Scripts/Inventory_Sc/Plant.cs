using UnityEngine;

public class Plant : MonoBehaviour
{
    [Header("Item Data")]
    public SCItem item;
    
    [Header("Pickup Settings")]
    public float pickupRange = 2f;
    public KeyCode pickupKey = KeyCode.E;
    public GameObject pickupUI; // "Press E to pickup" UI
    
    private bool playerInRange = false;
    private Inventory playerInventory;
    private bool pickedUp = false; // guard against multiple awards
    private bool pendingPickup = false; // waiting for spuding to finish
    private PlayerAnimationController playerAnimation;

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
        
        // Eğer collider yoksa ekle ve trigger yap
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            col = gameObject.AddComponent<BoxCollider>();
        }
        col.isTrigger = true;

    // Find player animation controller (optional)
    playerAnimation = FindObjectOfType<PlayerAnimationController>();
    }

    private void Update()
    {
        // Player range'de ise pickup kontrolü
        if (!pickedUp && !pendingPickup && playerInRange && Input.GetKeyDown(pickupKey))
        {
            BeginHarvestSequence();
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

    private void BeginHarvestSequence()
    {
        if (pickedUp || pendingPickup) return;
        // Hide prompt before any action
        ShowPickupUI(false);

        // If this Plant represents a seed item, don't play Spuding; pickup immediately
        if (item != null && item.isSeed)
        {
            TryPickup();
            return;
        }
        if (playerAnimation != null)
        {
            pendingPickup = true;
            if (!playerAnimation.IsSpuding())
            {
                playerAnimation.TriggerSpuding();
            }
            StartCoroutine(WaitSpudingThenPickup());
        }
        else
        {
            // Fallback: no animation controller found, pickup immediately
            TryPickup();
        }
    }

    private System.Collections.IEnumerator WaitSpudingThenPickup()
    {
        // Small delay to allow trigger to register
        yield return new WaitForEndOfFrame();
        // Wait until spuding starts (with a timeout just in case)
        float t = 0f;
        while (playerAnimation != null && !playerAnimation.IsSpuding() && t < 1.0f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        // Wait while spuding is playing
        while (playerAnimation != null && playerAnimation.IsSpuding())
        {
            yield return null;
        }
        pendingPickup = false;
        TryPickup();
    }

    private void TryPickup()
    {
    if (pickedUp) return;
    if (playerInventory != null && item != null)
        {
            // Inventory'e eklemeyi dene
            bool added = playerInventory.playerInventory.AddItem(item);
            
            if (added)
            {
        pickedUp = true;
                Debug.Log($"Picked up: {item.itemName}");
                ShowPickupUI(false);
                
                // Save sistemine bitki toplandığını bildir
                if (GameSaveManager.Instance != null)
                {
                    GameSaveManager.Instance.OnPlantCollected(this);
                }
                // Plant'ı yok et
                Destroy(gameObject);
            }
            else
            {
                Debug.Log("Inventory full! Cannot pickup item.");
                // UI feedback verilebilir (inventory dolu mesajı vs.)
            }
        }
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
            gameObject.name = $"Plant_{item.itemName}";
        }
    }
}