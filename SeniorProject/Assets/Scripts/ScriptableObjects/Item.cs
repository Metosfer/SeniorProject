using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class Item : ScriptableObject
{
    public string itemName;
    public Sprite icon;
    public ItemCategory category;
    public GameObject prefab;
    public bool expires;
    public float expirationTime;
}

public enum ItemCategory
{
    Seed,
    Herb,
    DriedHerb
}