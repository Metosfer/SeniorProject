using UnityEngine;

public class WorldItemSpawner : MonoBehaviour
{
    [Header("World Item Settings")]
    public GameObject defaultWorldItemPrefab; // Inspector'da atanacak varsayılan prefab
    public static GameObject worldItemPrefab; // Statik referans
    public static Transform itemContainer; // Spawn edilen itemları organize etmek için
    public static WorldItemSpawner instance; // Singleton referans

    [Header("Ground Clamp Settings")]
    [Tooltip("World item'ları sabit bir yer seviyesine (Y) sıkıştır.")]
    public bool clampToGroundY = true;
    [Tooltip("World item'ların spawn olacağı yer seviyesi (Y).")]
    public float defaultGroundY = 0.05f;

    // Statik cache (static method'larda erişim için)
    private static bool sClampToGroundY = true;
    private static float sDefaultGroundY = 0.05f;

    private void Awake()
    {
        // Singleton pattern
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        // Statik prefab referansını ayarla
        if (defaultWorldItemPrefab != null)
        {
            worldItemPrefab = defaultWorldItemPrefab;
        }

    // Statik ground clamp ayarlarını cache'le
    sClampToGroundY = clampToGroundY;
    sDefaultGroundY = defaultGroundY;
    }

    private void Start()
    {
        // Item container oluştur
        if (itemContainer == null)
        {
            GameObject container = new GameObject("World Items");
            itemContainer = container.transform;
        }
        
        // Eğer default prefab yoksa basit bir prefab oluştur
        if (worldItemPrefab == null && defaultWorldItemPrefab == null)
        {
            CreateDefaultWorldItemPrefab();
        }
    }
    
    private void CreateDefaultWorldItemPrefab()
    {
        // Basit bir cube prefab oluştur
        GameObject defaultPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        defaultPrefab.name = "DefaultWorldItem";
        defaultPrefab.transform.localScale = Vector3.one * 0.5f;
        
        // WorldItem component ekle
        defaultPrefab.AddComponent<WorldItem>();
        
        // Collider'ı trigger yap
        Collider col = defaultPrefab.GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
        
        // Prefab olarak ayarla
        worldItemPrefab = defaultPrefab;
        defaultWorldItemPrefab = defaultPrefab;
        
        // Geçici objeyi deaktif et (prefab olarak kullanılacak)
        defaultPrefab.SetActive(false);
    }

    public static void SpawnItem(SCItem item, Vector3 position, int quantity = 1)
    {
        if (item == null)
        {
            Debug.LogWarning("WorldItemSpawner: Item null!");
            return;
        }

        // Yer seviyesine sıkıştır (opsiyonel)
        if (sClampToGroundY)
        {
            position.y = sDefaultGroundY;
        }

        // World item prefab'ını oluştur
        GameObject worldItem = CreateWorldItemObject(item, position, quantity);
        
        if (worldItem != null)
        {
            Debug.Log($"World'e spawn edildi: {item.itemName} x{quantity} at {position}");
        }
    }    private static GameObject CreateWorldItemObject(SCItem item, Vector3 position, int quantity)
    {
        GameObject worldItem;
        
        // Önce item'ın kendi dropPrefab'ını kontrol et
        if (item.dropPrefab != null)
        {
            worldItem = Instantiate(item.dropPrefab, position, Quaternion.identity);
            worldItem.name = $"WorldItem_{item.itemName}";
        }
        // Eğer dropPrefab yoksa ama varsayılan prefab varsa onu kullan
        else if (worldItemPrefab != null)
        {
            worldItem = Instantiate(worldItemPrefab, position, Quaternion.identity);
            worldItem.name = $"WorldItem_{item.itemName}";
        }
        else
        {
            // Fallback: Basit bir cube oluştur
            worldItem = GameObject.CreatePrimitive(PrimitiveType.Cube);
            worldItem.name = $"WorldItem_{item.itemName}";
            worldItem.transform.position = position;
            worldItem.transform.localScale = Vector3.one * 0.5f; // Küçük boyut
            
            // Material ve renk ayarla (sadece primitive cube için)
            Renderer renderer = worldItem.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Standard"));
                
                // Item türüne göre renk ver
                if (item.itemName.ToLower().Contains("seed"))
                {
                    mat.color = Color.yellow;
                }
                else if (item.itemName.ToLower().Contains("fruit"))
                {
                    mat.color = Color.red;
                }
                else if (item.itemName.ToLower().Contains("vegetable"))
                {
                    mat.color = Color.green;
                }
                else if (item.itemName.ToLower().Contains("aloe"))
                {
                    mat.color = Color.green;
                }
                else
                {
                    mat.color = Color.gray;
                }
                
                renderer.material = mat;
            }
        }

        // Item container'a ekle
        if (itemContainer != null)
        {
            worldItem.transform.SetParent(itemContainer);
        }

        // WorldItem component'i ekle (eğer yoksa)
        WorldItem worldItemComponent = worldItem.GetComponent<WorldItem>();
        if (worldItemComponent == null)
        {
            worldItemComponent = worldItem.AddComponent<WorldItem>();
        }
        worldItemComponent.item = item;
        worldItemComponent.quantity = quantity;

        // Collider ekle (eğer yoksa)
        Collider collider = worldItem.GetComponent<Collider>();
        if (collider == null)
        {
            collider = worldItem.AddComponent<BoxCollider>();
        }
        
        // Pickup için trigger collider ekle
        BoxCollider triggerCollider = worldItem.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.size = Vector3.one * 2f; // Pickup range için büyük trigger

        // Rigidbody ekle (eğer yoksa)
        Rigidbody rb = worldItem.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = worldItem.AddComponent<Rigidbody>();
        }
        rb.mass = 0.1f; // Hafif eşya

        // Tag ekle
        worldItem.tag = "WorldItem";

        // Ek güvenlik: Spawn sonrası da ground Y'yi uygula (prefab içinde farklı pozisyon verilmişse)
        if (sClampToGroundY)
        {
            Vector3 p = worldItem.transform.position;
            p.y = sDefaultGroundY;
            worldItem.transform.position = p;
        }

        return worldItem;
    }

    public static void SetWorldItemPrefab(GameObject prefab)
    {
        worldItemPrefab = prefab;
    }
}
