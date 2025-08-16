using System.Collections.Generic;
using UnityEngine;

public class WorldItemSpawner : MonoBehaviour
{
    [Header("World Item Settings")]
    public GameObject defaultWorldItemPrefab; // Inspector'da atanacak varsayılan prefab
    public static GameObject worldItemPrefab; // Statik referans
    [Header("Lifetime")]
    [Tooltip("Bu spawner'ı sahne geçişlerinde koru.")]
    public bool dontDestroyOnLoad = false;
    [Tooltip("Aynı anda birden fazla instance varsa sahnedeki instance'ı tercih et ve eski kalıcı instance'ı kaldır.")]
    public bool preferSceneInstance = true;
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

    [Header("Auto Spawn Settings")]
    [Tooltip("Belirli aralıklarla otomatik world item spawn et.")]
    public bool enableAutoSpawn = false;
    [Tooltip("Otomatik spawn edilecek SCItem (dropPrefab'ı varsa kullanılır).")]
    public SCItem autoSpawnItem;
    [Tooltip("Her spawn'da üretilecek adet.")]
    public int autoSpawnQuantity = 1;
    [Tooltip("Spawn deneme aralığı (saniye).")]
    public float spawnInterval = 8f;
    [Tooltip("Sahnede aynı anda bulunabilecek en fazla auto-spawn adet (alan içi). 0 veya daha küçük sınırsız demektir.")]
    public int maxAliveInArea = 10;
    [Tooltip("Kapsite hesabına bu spawner'a ait olmayan (etiketsiz) world item'ları da dahil et.")]
    public bool includeExistingWorldItemsInCount = true;

    [Header("Auto Spawn Area")] 
    [Tooltip("Merkez olarak bu objenin pozisyonunu kullan.")]
    public bool useTransformAsCenter = true;
    [Tooltip("Alan merkezini manuel belirtmek istersen.")]
    public Vector3 areaCenter = Vector3.zero;
    [Tooltip("Alan boyutu (X/Z genişlik/derinlik, Y yükseklik). Genelde yere yakın objeler için Y küçük tutulur.")]
    public Vector3 areaSize = new Vector3(10f, 0.1f, 10f);

    [Header("Gizmos")] 
    public bool drawAreaGizmos = true;
    public Color areaFillColor = new Color(0.2f, 1f, 0.2f, 0.15f);
    public Color areaWireColor = new Color(0.2f, 1f, 0.2f, 0.9f);

    private float _nextSpawnTime;
    private readonly List<AutoSpawnTag> _autoSpawned = new List<AutoSpawnTag>();

    private void Awake()
    {
        // Singleton pattern with scene-preference support
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            // If we prefer scene-local settings and this object lives in a regular scene,
            // replace the old instance (likely from DontDestroyOnLoad) with this one.
            if (preferSceneInstance && gameObject.scene.IsValid() && instance.gameObject.scene != gameObject.scene)
            {
                Destroy(instance.gameObject);
                instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }
        
        // Statik prefab referansını ayarla
        if (defaultWorldItemPrefab != null)
        {
            worldItemPrefab = defaultWorldItemPrefab;
        }

        // Statik ground clamp ayarlarını cache'le
        sClampToGroundY = clampToGroundY;
        sDefaultGroundY = defaultGroundY;

        // Apply optional persistence
        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }
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

        // Auto-spawn zamanlayıcıyı başlat
        _nextSpawnTime = Time.time + spawnInterval;
    }

    private void Update()
    {
        if (!enableAutoSpawn) return;
        if (autoSpawnItem == null) return;
        if (spawnInterval <= 0f) return;

        // Kapasite kontrolü (alan içerisindeki aynı item)
        if (maxAliveInArea > 0)
        {
            int alive = GetAliveCountInArea();
            if (alive >= maxAliveInArea) return;
        }

        if (Time.time >= _nextSpawnTime)
        {
            Vector3 pos = GetRandomPointInArea();
            // Y seviyesini (opsiyonel) clamp politikası zaten uygulayacak
            GameObject go = CreateWorldItemObject(autoSpawnItem, pos, Mathf.Max(1, autoSpawnQuantity));
            if (go != null)
            {
                var tag = go.AddComponent<AutoSpawnTag>();
                tag.owner = this;
                _autoSpawned.Add(tag);
            }
            _nextSpawnTime = Time.time + spawnInterval;
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
        
        // Önce itemPrefab'ı dene, yoksa dropPrefab'a düş
        if (item.itemPrefab != null)
        {
            worldItem = Instantiate(item.itemPrefab, position, Quaternion.identity);
            worldItem.name = $"WorldItem_{item.itemName}";
        }
        else if (item.dropPrefab != null)
        {
            worldItem = Instantiate(item.dropPrefab, position, Quaternion.identity);
            worldItem.name = $"WorldItem_{item.itemName}";
        }
        // Eğer ikisi de yoksa ama varsayılan prefab varsa onu kullan
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

    // --- Helpers for auto spawn ---
    private Bounds GetAreaBounds()
    {
        Vector3 center = useTransformAsCenter ? transform.position : areaCenter;
        Vector3 size = new Vector3(Mathf.Max(0.1f, areaSize.x), Mathf.Max(0.01f, areaSize.y), Mathf.Max(0.1f, areaSize.z));
        return new Bounds(center, size);
    }

    private Vector3 GetRandomPointInArea()
    {
        var b = GetAreaBounds();
        float x = Random.Range(b.min.x, b.max.x);
        float y = Mathf.Clamp(sDefaultGroundY, b.min.y, b.max.y);
        float z = Random.Range(b.min.z, b.max.z);
        return new Vector3(x, y, z);
    }

    private int GetAliveCountInArea()
    {
        var b = GetAreaBounds();
        // Clean list and count tagged
        int count = 0;
        for (int i = _autoSpawned.Count - 1; i >= 0; i--)
        {
            var t = _autoSpawned[i];
            if (t == null || t.gameObject == null)
            {
                _autoSpawned.RemoveAt(i);
                continue;
            }
            if (t.owner != this) { _autoSpawned.RemoveAt(i); continue; }
            if (b.Contains(t.transform.position)) count++;
        }
        if (includeExistingWorldItemsInCount)
        {
            // Count any matching WorldItem in bounds to avoid over-spawn after loads
            WorldItem[] all = FindObjectsOfType<WorldItem>();
            string name = autoSpawnItem != null ? autoSpawnItem.itemName : null;
            for (int i = 0; i < all.Length; i++)
            {
                var wi = all[i];
                if (wi == null || wi.item == null) continue;
                if (name != null && wi.item.itemName != name) continue;
                // Skip ones already tagged as ours (counted above)
                var tag = wi.GetComponent<AutoSpawnTag>();
                if (tag != null && tag.owner == this) continue;
                if (b.Contains(wi.transform.position)) count++;
            }
        }
        return count;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawAreaGizmos) return;
        var b = GetAreaBounds();
        Gizmos.color = areaFillColor;
        Gizmos.DrawCube(b.center, b.size);
        Gizmos.color = areaWireColor;
        Gizmos.DrawWireCube(b.center, b.size);
    }

    private class AutoSpawnTag : MonoBehaviour
    {
        public WorldItemSpawner owner;
    }
    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
