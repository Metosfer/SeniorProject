using System.Collections.Generic;
using UnityEngine;

public class WorldItemSpawner : MonoBehaviour
{
    [Header("World Item Settings")]
    public GameObject defaultWorldItemPrefab; // Inspector'da atanacak varsayılan prefab
    public static GameObject worldItemPrefab; // Statik referans
    [Tooltip("Varsayılan prefab yoksa kullanılacak ek fallback prefab'lar (ilk geçerli olan seçilir)")]
    public List<GameObject> extraDefaultPrefabs = new List<GameObject>();
    [Header("Lifetime")]
    [Tooltip("Bu spawner'ı sahne geçişlerinde koru.")]
    public bool dontDestroyOnLoad = false;
    [Tooltip("Aynı anda birden fazla instance varsa sahnedeki instance'ı tercih et ve eski kalıcı instance'ı kaldır.")]
    public bool preferSceneInstance = true;
    public static Transform itemContainer; // Spawn edilen itemları organize etmek için
    public static WorldItemSpawner instance; // Singleton referans

    [Header("Ground Clamp Settings")]
    [Tooltip("World item'ları zemine hizala (raycast ile). Kapatılırsa sadece defaultGroundY uygulanır.")]
    public bool clampToGroundY = true;
    [Tooltip("Raycast bulunamazsa kullanılacak varsayılan yer seviyesi (Y).")]
    public float defaultGroundY = 0.05f;
    [Header("Ground Raycast Settings")]
    [Tooltip("Zemin olarak kabul edilecek katmanlar.")]
    public LayerMask groundMask = ~0; // tüm katmanlar
    [Tooltip("Raycast başlangıç yüksekliği (groundCheck noktasının üstünden).")]
    public float rayStartHeight = 0.5f;
    [Tooltip("Raycast maksimum mesafesi.")]
    public float rayMaxDistance = 5f;
    [Tooltip("Zeminden çok hafif yukarıda dursun (0 = tam temas).")]
    public float groundClearance = 0.0f;

    // Statik cache (static method'larda erişim için)
    private static bool sClampToGroundY = true;
    private static float sDefaultGroundY = 0.05f;
    private static LayerMask sGroundMask = ~0;
    private static float sRayStartHeight = 0.5f;
    private static float sRayMaxDistance = 5f;
    private static float sGroundClearance = 0.0f;
    private static List<GameObject> sExtraDefaultPrefabs = new List<GameObject>();

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
            // Ek fallback listesi cache
            sExtraDefaultPrefabs = extraDefaultPrefabs != null ? new List<GameObject>(extraDefaultPrefabs) : new List<GameObject>();

        // Statik ground clamp ayarlarını cache'le
        sClampToGroundY = clampToGroundY;
        sDefaultGroundY = defaultGroundY;
    sGroundMask = groundMask;
    sRayStartHeight = rayStartHeight;
    sRayMaxDistance = rayMaxDistance;
    sGroundClearance = groundClearance;

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
            GameObject go = CreateWorldItemObject(autoSpawnItem, pos, Mathf.Max(1, autoSpawnQuantity), preferDropPrefab: false);
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

        // Envanterden yere atma senaryosu: dropPrefab tercih edilir
        GameObject worldItem = CreateWorldItemObject(item, position, quantity, preferDropPrefab: true);
        
        if (worldItem != null)
        {
            Debug.Log($"World'e spawn edildi: {item.itemName} x{quantity} at {position}");
        }
    }

    private static GameObject CreateWorldItemObject(SCItem item, Vector3 position, int quantity, bool preferDropPrefab)
    {
        GameObject worldItem;
        
        // Prefab seçimi modu
        if (preferDropPrefab)
        {
            // Envanterden atılan: dropPrefab -> itemPrefab -> fallback
            if (item.dropPrefab != null)
            {
                worldItem = Instantiate(item.dropPrefab, position, Quaternion.identity);
                worldItem.name = $"WorldItem_{item.itemName}";
            }
            else if (item.itemPrefab != null)
            {
                worldItem = Instantiate(item.itemPrefab, position, Quaternion.identity);
                worldItem.name = $"WorldItem_{item.itemName}";
            }
            else
            {
                worldItem = InstantiateFallback(item, position);
            }
        }
        else
        {
            // Spawner: itemPrefab -> dropPrefab -> fallback
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
            else
            {
                worldItem = InstantiateFallback(item, position);
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

    // Zemin hizalaması
    AlignToGround(worldItem);

        return worldItem;
    }

    public static void SetWorldItemPrefab(GameObject prefab)
    {
        worldItemPrefab = prefab;
    }

    // Default/extra fallback oluşturucu
    private static GameObject InstantiateFallback(SCItem item, Vector3 position)
    {
        GameObject basePrefab = worldItemPrefab;
        if (basePrefab == null)
        {
            for (int i = 0; i < sExtraDefaultPrefabs.Count; i++)
            {
                if (sExtraDefaultPrefabs[i] != null) { basePrefab = sExtraDefaultPrefabs[i]; break; }
            }
        }
        if (basePrefab != null)
        {
            var go = Instantiate(basePrefab, position, Quaternion.identity);
            go.name = $"WorldItem_{item.itemName}";
            return go;
        }

        // Fallback: primitive cube
        var worldItem = GameObject.CreatePrimitive(PrimitiveType.Cube);
        worldItem.name = $"WorldItem_{item.itemName}";
        worldItem.transform.position = position;
        worldItem.transform.localScale = Vector3.one * 0.5f;
        var renderer = worldItem.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Standard"));
            string lower = item.itemName != null ? item.itemName.ToLower() : string.Empty;
            if (lower.Contains("seed")) mat.color = Color.yellow;
            else if (lower.Contains("fruit")) mat.color = Color.red;
            else if (lower.Contains("vegetable") || lower.Contains("aloe")) mat.color = Color.green;
            else mat.color = Color.gray;
            renderer.material = mat;
        }
        return worldItem;
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
        string nameFilter = autoSpawnItem != null ? autoSpawnItem.itemName : null;
        int count = 0;

        // Kendi spawn ettiklerimiz
        for (int i = _autoSpawned.Count - 1; i >= 0; i--)
        {
            var tag = _autoSpawned[i];
            if (tag == null || tag.gameObject == null)
            {
                _autoSpawned.RemoveAt(i);
                continue;
            }
            if (tag.owner != this) { _autoSpawned.RemoveAt(i); continue; }
            var wi = tag.GetComponent<WorldItem>();
            if (wi == null) continue;
            if (!string.IsNullOrEmpty(nameFilter) && wi.item != null && wi.item.itemName != nameFilter) continue;
            if (b.Contains(tag.transform.position)) count++;
        }

        if (includeExistingWorldItemsInCount)
        {
            var all = GameObject.FindGameObjectsWithTag("WorldItem");
            for (int i = 0; i < all.Length; i++)
            {
                var go = all[i];
                if (go == null) continue;
                // Bizim etiketlediklerimizi zaten saydık
                var tag = go.GetComponent<AutoSpawnTag>();
                if (tag != null && tag.owner == this) continue;
                var wi = go.GetComponent<WorldItem>();
                if (wi == null) continue;
                if (!string.IsNullOrEmpty(nameFilter) && wi.item != null && wi.item.itemName != nameFilter) continue;
                if (b.Contains(go.transform.position)) count++;
            }
        }
        return count;
    }

    // --- Ground Helpers ---
    private static Transform FindChildByName(GameObject root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;
        var trs = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < trs.Length; i++)
        {
            if (trs[i] != null && trs[i].name == name) return trs[i];
        }
        return null;
    }

    private static bool TryGetGroundY(Vector3 pos, out float groundY)
    {
        Vector3 start = pos + Vector3.up * Mathf.Max(0.01f, sRayStartHeight);
        if (Physics.Raycast(start, Vector3.down, out var hit, sRayMaxDistance, sGroundMask))
        {
            groundY = hit.point.y;
            return true;
        }
        groundY = sDefaultGroundY;
        return false;
    }

    private static void AlignToGround(GameObject go)
    {
        if (!sClampToGroundY || go == null) return;
        var t = go.transform;
        var pos = t.position;

        // PlantManager.groundCheck öncelik, yoksa isimle arama
        Transform groundCheck = null;
        var pm = go.GetComponentInChildren<PlantManager>();
        if (pm != null && pm.groundCheck != null) groundCheck = pm.groundCheck;
        if (groundCheck == null) groundCheck = FindChildByName(go, "groundCheck");
        if (groundCheck != null)
        {
            Vector3 start = groundCheck.position + Vector3.up * Mathf.Max(0.01f, sRayStartHeight);
            // RaycastAll ile kendi collider'ını ayıkla ve en yakın geçerli zemini bul
            var hits = Physics.RaycastAll(start, Vector3.down, sRayMaxDistance, sGroundMask);
            float bestDist = float.MaxValue;
            Vector3 bestPoint = Vector3.negativeInfinity;
            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h.collider == null) continue;
                // Kendi hiyerarşindeki collider'ları atla
                if (h.collider.transform.IsChildOf(go.transform)) continue;
                float d = h.distance;
                if (d < bestDist)
                {
                    bestDist = d;
                    bestPoint = h.point;
                }
            }
            if (!float.IsNegativeInfinity(bestPoint.x))
            {
                Vector3 desired = bestPoint + Vector3.up * sGroundClearance;
                Vector3 delta = desired - groundCheck.position;
                t.position += delta;
                return;
            }
        }

        // Aksi halde collider bounds tabanına göre hizala
        float ground;
        TryGetGroundY(pos, out ground);
        var colliders = go.GetComponentsInChildren<Collider>();
        if (colliders != null && colliders.Length > 0)
        {
            bool hasNonTrigger = false;
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            for (int i = 0; i < colliders.Length; i++)
            {
                var c = colliders[i];
                if (c == null || c.isTrigger) continue;
                if (!hasNonTrigger) { b = c.bounds; hasNonTrigger = true; }
                else b.Encapsulate(c.bounds);
            }
            if (!hasNonTrigger)
            {
                b = colliders[0].bounds;
                for (int i = 1; i < colliders.Length; i++) b.Encapsulate(colliders[i].bounds);
            }
            float bottomOffset = pos.y - b.min.y;
            pos.y = ground + bottomOffset + sGroundClearance;
            t.position = pos;
        }
        else
        {
            pos.y = ground + sGroundClearance;
            t.position = pos;
        }
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
