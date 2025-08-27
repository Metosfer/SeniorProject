using TMPro;
using UnityEngine;

public class MoneyUIBinder : MonoBehaviour
{
    [Tooltip("Text to display the current money/balance.")]
    public TextMeshProUGUI moneyText;
    [Tooltip("Optional prefix, e.g., 'Para: ' or 'Money: '.")]
    public string prefix = "";

    private void OnEnable()
    {
        if (MoneyManager.Instance != null)
        {
            MoneyManager.Instance.OnMoneyChanged += HandleMoneyChanged;
            HandleMoneyChanged(MoneyManager.Instance.Balance);
        }
        else
        {
            // Try late hookup if MoneyManager is spawned later this frame
            Invoke(nameof(TryLateHook), 0f);
        }
    }

    private void OnDisable()
    {
        if (MoneyManager.Instance != null)
        {
            MoneyManager.Instance.OnMoneyChanged -= HandleMoneyChanged;
        }
    }

    private void TryLateHook()
    {
        if (MoneyManager.Instance != null)
        {
            MoneyManager.Instance.OnMoneyChanged += HandleMoneyChanged;
            HandleMoneyChanged(MoneyManager.Instance.Balance);
        }
    }

    private void HandleMoneyChanged(int balance)
    {
        if (moneyText != null)
        {
            moneyText.text = string.IsNullOrEmpty(prefix) ? balance.ToString() : prefix + balance.ToString();
        }
    }
}
