using UnityEngine;

// Ensures MoneyManager exists at boot so balance persists across scenes
public static class MoneyBootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureMoneyManager()
    {
        if (MoneyManager.Instance != null) return;
        var go = new GameObject("MoneyManager", typeof(MoneyManager));
        Object.DontDestroyOnLoad(go);
    }
}
