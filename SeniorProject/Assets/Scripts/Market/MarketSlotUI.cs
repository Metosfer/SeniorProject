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

    private MarketManager _market;
    private int _index;

    public void Bind(MarketManager market, int index, string displayName, int price, Sprite icon, int stock)
    {
        _market = market;
        _index = index;

        if (nameText != null) nameText.text = displayName;
        if (priceText != null) priceText.text = price.ToString();
        if (iconImage != null) iconImage.sprite = icon;
        if (stockText != null) stockText.text = stock.ToString();
        if (buyButton != null) buyButton.interactable = stock > 0;

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(() => _market.AttemptPurchase(_index));
        }
    }

    public void UpdateStock(int stock)
    {
        if (stockText != null) stockText.text = stock.ToString();
        if (buyButton != null) buyButton.interactable = stock > 0;
    }
}
