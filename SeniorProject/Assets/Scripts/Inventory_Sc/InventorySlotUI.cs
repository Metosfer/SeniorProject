using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventorySlotUI : MonoBehaviour
{
    public Image itemIcon;
    public TextMeshProUGUI itemCountText;
    public Image slotBackground;

    public Color normalColor = Color.white;
    public Color occupiedColor = Color.gray;
    public Color readyColor = Color.green;

    private void Awake()
    {
        // Transform.Find ile bileşenleri bul ve null kontrolü yap
        Transform iconTransform = transform.Find("Icon");
        Transform countTransform = transform.Find("Count");
        Transform bgTransform = transform.Find("Background");

        if (iconTransform != null)
        {
            itemIcon = iconTransform.GetComponent<Image>();
            if (itemIcon != null)
            {
                Debug.Log($"InventorySlotUI: Icon component found for {gameObject.name}");
            }
        }
        else
        {
            Debug.LogWarning($"InventorySlotUI: 'Icon' child bulunamadı! GameObject: {gameObject.name}");
        }

        if (countTransform != null)
        {
            itemCountText = countTransform.GetComponent<TextMeshProUGUI>();
            if (itemCountText != null)
            {
                Debug.Log($"InventorySlotUI: Count component found for {gameObject.name}");
            }
        }
        else
        {
            Debug.LogWarning($"InventorySlotUI: 'Count' child bulunamadı! GameObject: {gameObject.name}");
        }

        if (bgTransform != null)
        {
            slotBackground = bgTransform.GetComponent<Image>();
            if (slotBackground != null)
            {
                Debug.Log($"InventorySlotUI: Background component found for {gameObject.name}");
            }
        }
        else
        {
            Debug.LogWarning($"InventorySlotUI: 'Background' child bulunamadı! GameObject: {gameObject.name}");
        }
    }
}