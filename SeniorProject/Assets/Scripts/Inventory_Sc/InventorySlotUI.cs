using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventorySlotUI : MonoBehaviour
{
    public Image itemIcon;
    public TextMeshProUGUI itemCountText;

    private void Awake()
    {
        itemIcon = transform.Find("Icon").GetComponent<Image>();
        itemCountText = transform.Find("Count").GetComponent<TextMeshProUGUI>();
    }
}