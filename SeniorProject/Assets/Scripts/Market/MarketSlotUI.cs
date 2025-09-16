using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MarketSlotUI : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI priceText;
    public Image iconImage;
    public Button buyButton;
    public TextMeshProUGUI stockText;

    private object _market;
    private int _index;

    public void Bind(object market, int index, string displayName, int price, Sprite icon, int stock)
    {
        _market = market;
        _index = index;

        if (nameText != null) nameText.text = displayName;
        if (priceText != null) priceText.text = price.ToString();
        if (iconImage != null) iconImage.sprite = icon;
        if (stockText != null) stockText.text = stock.ToString();
        if (buyButton == null)
        {
            // Fallback: child hiyerarşideki ilk Button'u bul
            buyButton = GetComponentInChildren<Button>(true);
        }
        if (buyButton != null) buyButton.interactable = stock > 0;

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(() => AttemptPurchase());
        }
    }

    private void AttemptPurchase()
    {
        if (_market is MarketManager mm)
        {
            mm.AttemptPurchase(_index);
        }
        else if (_market is FishMarketManager fm)
        {
            fm.AttemptPurchase(_index);
        }
    }

    public void UpdateStock(int stock)
    {
        if (stockText != null) stockText.text = stock.ToString();
        if (buyButton != null) buyButton.interactable = stock > 0;
    }
}
