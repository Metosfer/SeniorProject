using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class SCItem : ScriptableObject
{
    [Header("Basic Info")]
    [Tooltip("Item'ın görünecek adı")]
    public string itemName;
    [Tooltip("Item'ın açıklama metni")]
    public string itemDescription;
    [Tooltip("Bu item yığılabilir mi? (aynı türden birden fazla slot'ta toplanabilir)")]
    public bool canStackable;
    [Tooltip("Envanterde görünecek ikon sprite'ı")]
    public Sprite itemIcon;

    [Header("3D Models")]
    [Tooltip("Dünyada bulunurken kullanılan prefab (örn: Aloe bitkisi, balık)")]
    public GameObject itemPrefab; // Dünyada bulunurken kullanılan prefab (örn: Aloe bitkisi)
    [Tooltip("Envanter'den atıldığında kullanılan prefab (örn: Aloe yaprağı, yakalanan balık)")]
    public GameObject dropPrefab; // Inventory'den atıldığında kullanılan prefab (örn: Aloe yaprağı)
    [Tooltip("Kurutma alanında kullanılan prefab (örn: kurutulmuş Aloe yaprağı)")]
    public GameObject dryPrefab; // Kurutma alanında kullanılan prefab (örn: kurutulmuş Aloe yaprağı)

    [Header("Farming Settings")]
    [Tooltip("Bu item bir tohum mu?")]
    public bool isSeed = false;
    [Tooltip("Tohumun olgunlaşma süresi (saniye)")]
    public float growthTime = 10f;
    [Tooltip("Büyüme tamamlandığında spawn edilecek olgun bitki prefab'ı (opsiyonel; yoksa itemPrefab kullanılır)")]
    public GameObject grownPrefab;
    [Tooltip("Olgun bitkiden toplanacak envanter item'i (opsiyonel; Plant script'i üzerinden atanır)")]
    public SCItem harvestItem;

    [Header("Drying Settings")]
    [Tooltip("Bu item kurutulabilir mi?")]
    public bool canBeDried = false; // Bu item kurutulabilir mi?
    [Tooltip("Kurutma süresi (saniye cinsinden)")]
    public float dryingTime = 10f; // Kurutma süresi (saniye cinsinden)
    [Tooltip("Kurutulmuş hali (envanter'e eklenecek item)")]
    public SCItem driedVersion; // Kurutulmuş hali (inventory'e eklenecek)
    
    [Header("Expiration")]
    [Tooltip("Bu item bozulabilir mi?")]
    public bool isExpirable;
    [Tooltip("Bozulma süresi (saniye cinsinden)")]
    public float expirationTime;
    
    [Header("Fish Settings")]
    [Tooltip("Bu item bir balık mı? (FishingManager için)")]
    public bool isFish = false; // Bu item bir balık mı?
    [Tooltip("Balığın değeri (puan, para vs.)")]
    public int fishValue = 1; // Balığın değeri (puan, para vs.)
    [Tooltip("Balığın ağırlığı (kilogram)")]
    public float fishWeight = 1f; // Balığın ağırlığı
    [Tooltip("Balık türü: Common, Rare, Epic, Legendary")]
    public string fishType = "Common"; // Balık türü (Common, Rare, Epic vs.)
    [Tooltip("Balık yakalandığında oyuncuya verilecek XP")]
    public int fishingXP = 10;
    [Tooltip("Balık yakalanma zorluğu (1=Çok Kolay, 2=Kolay, 3=Orta, 4=Zor, 5=Çok Zor)")]
    [Range(1, 5)]
    public int fishDifficulty = 3; // Balık yakalanma zorluğu
    [Tooltip("Bu balık efsanevi/özel bir balık mı? (Özel animasyonlar ve efektler için)")]
    public bool isLegendaryFish = false; // Efsanevi balık mı?
    
    [Header("Fish Feed Settings")]
    [Tooltip("Bu item balık yemi mi? (FishMarketManager için)")]
    public bool isFishFeed = false; // Bu item balık yemi mi?
    [Tooltip("Yem miktarı (kaç adet yem içeriyor)")]
    public int feedAmount = 1; // Yem miktarı
    [Tooltip("Yem kalitesi - iyi yem iyi balık yakalama şansını arttırır (1=Düşük, 2=Normal, 3=İyi, 4=Yüksek, 5=Premium)")]
    [Range(1, 5)]
    public int feedValue = 1; // Yem kalitesi (1-5 arası)
}