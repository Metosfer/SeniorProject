using UnityEngine;

public class FishDropItem : MonoBehaviour
{
    [Header("Fish Data")]
    public SCItem fishData; // Bu drop item'ın temsil ettiği balık
    
    [Header("Pickup Settings")]
    public float pickupRange = 2f; // Toplama mesafesi
    public LayerMask playerLayer = 1; // Player layer mask
    
    [Header("Visual Effects")]
    public bool bobInWater = true; // Suda yüzer mi?
    public float bobSpeed = 2f; // Yüzme hızı
    public float bobHeight = 0.5f; // Yüzme yüksekliği
    
    private Vector3 startPosition;
    private bool canBePickedUp = true;
    
    void Start()
    {
        startPosition = transform.position;
        
        // Eğer balık verisi yoksa, objeyi yok et
        if (fishData == null)
        {
            Debug.LogWarning("FishDropItem'da balık verisi yok! Obje yok ediliyor.");
            Destroy(gameObject, 1f);
        }
    }
    
    void Update()
    {
        if (bobInWater)
        {
            // Suda yüzme efekti
            float newY = startPosition.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }
        
        // Oyuncu yakınlık kontrolü
        CheckForPlayerPickup();
    }
    
    void CheckForPlayerPickup()
    {
        if (!canBePickedUp) return;
        
        Collider[] playersInRange = Physics.OverlapSphere(transform.position, pickupRange, playerLayer);
        
        foreach (Collider player in playersInRange)
        {
            if (player.CompareTag("Player"))
            {
                // Oyuncu yakınsa ve F tuşuna basılırsa topla
                if (InputHelper.GetKeyDown(KeyCode.F))
                {
                    PickupFish(player.gameObject);
                    break;
                }
            }
        }
    }
    
    void PickupFish(GameObject player)
    {
        if (fishData == null)
        {
            Debug.LogError("Balık verisi yok, toplanamaz!");
            return;
        }
        
        // Inventory Manager'ı bul ve balığı ekle
        MonoBehaviour[] components = player.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour component in components)
        {
            var addItemMethod = component.GetType().GetMethod("AddItem");
            if (addItemMethod != null)
            {
                addItemMethod.Invoke(component, new object[] { fishData, 1 });
                Debug.Log($"{fishData.itemName} toplandı!");
                
                canBePickedUp = false;
                Destroy(gameObject);
                return;
            }
        }
        
        Debug.LogWarning("Oyuncuda inventory manager bulunamadı!");
    }
    
    void OnDrawGizmosSelected()
    {
        // Pickup range'i görselleştir
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, pickupRange);
    }
}
