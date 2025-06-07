using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class SCItem : ScriptableObject
{
    public string itemName;
    public string itemDescription;
    public bool canStackable;
    public Sprite itemIcon;
    public GameObject itemPrefab;

    public bool isExpirable;
    public float expirationTime;
}