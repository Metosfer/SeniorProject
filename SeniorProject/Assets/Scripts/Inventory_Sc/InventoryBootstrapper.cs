using UnityEngine;

// Ensures there's always a persistent InventoryManager in the game
public static class InventoryBootstrapper
{
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	private static void EnsureInventoryManager()
	{
		if (InventoryManager.Instance != null) return;
		var go = new GameObject("InventoryManager", typeof(InventoryManager));
		Object.DontDestroyOnLoad(go);
	}
}
