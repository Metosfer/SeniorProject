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
    }

    private void Update()
    {
        // Player range'de ise pickup kontrolü
    if (!pickedUp && playerInRange && Input.GetKeyDown(pickupKey))
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