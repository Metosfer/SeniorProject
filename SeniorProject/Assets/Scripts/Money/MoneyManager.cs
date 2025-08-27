using System;
using UnityEngine;

public class MoneyManager : MonoBehaviour
{
    public static MoneyManager Instance { get; private set; }

    [Tooltip("Default starting money if not set by save or code.")]
    public int startBalance = 1000;

    public int Balance { get; private set; }
    public event Action<int> OnMoneyChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (Balance <= 0)
        {
            Balance = startBalance;
        }
    }

    public void SetBalance(int amount)
    {
        int clamped = Mathf.Max(0, amount);
        if (clamped == Balance) return;
        Balance = clamped;
        OnMoneyChanged?.Invoke(Balance);
    }

    public void Add(int delta)
    {
        if (delta == 0) return;
        SetBalance(Balance + delta);
    }

    public bool CanAfford(int amount) => Balance >= amount;

    public bool TrySpend(int amount)
    {
        if (!CanAfford(amount)) return false;
        Add(-Mathf.Abs(amount));
        return true;
    }
}
