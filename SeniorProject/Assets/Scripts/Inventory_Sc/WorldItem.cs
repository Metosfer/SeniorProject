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
    }

    private void Update()
    {
        // Player range'de ise pickup kontrolü
        if (playerInRange && Input.GetKeyDown(pickupKey))
        {
            TryPickup();
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

    private void TryPickup()
    {
        if (playerInventory != null && item != null)
        {
            // Inventory'e eklemeyi dene
            bool added = playerInventory.playerInventory.AddItem(item);
            
            if (added)
            {
                // Trigger TakeItem for non-plant pickups
                var anim = FindObjectOfType<PlayerAnimationController>();
                if (anim != null) anim.TriggerTakeItem();
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
                    Destroy(gameObject);
                }
            }
            else
            {
                Debug.Log("Inventory full! Cannot pickup item.");
                // UI feedback verilebilir (inventory dolu mesajı vs.)
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
    }
}
