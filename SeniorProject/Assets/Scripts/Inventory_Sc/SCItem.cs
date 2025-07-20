using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class SCItem : ScriptableObject
{
    [Header("Basic Info")]
    public string itemName;
    public string itemDescription;
    public bool canStackable;
    public Sprite itemIcon;

    [Header("3D Models")]
    public GameObject itemPrefab; // Dünyada bulunurken kullanılan prefab (örn: Aloe bitkisi)
    public GameObject dropPrefab; // Inventory'den atıldığında kullanılan prefab (örn: Aloe yaprağı)
    public GameObject dryPrefab; // Kurutma alanında kullanılan prefab (örn: kurutulmuş Aloe yaprağı)

    [Header("Drying Settings")]
    public bool canBeDried = false; // Bu item kurutulabilir mi?
    public float dryingTime = 10f; // Kurutma süresi (saniye cinsinden)
    public SCItem driedVersion; // Kurutulmuş hali (inventory'e eklenecek)
    
    [Header("Expiration")]
    public bool isExpirable;
    public float expirationTime;
}