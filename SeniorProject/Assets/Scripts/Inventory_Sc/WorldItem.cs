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
        // Player inventory'sini bul
        playerInventory = FindObjectOfType<Inventory>();
        
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
                Debug.Log($"Picked up: {item.itemName} x{quantity}");
                
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
