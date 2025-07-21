using UnityEngine;

public class Inventory : MonoBehaviour
{
    public SCInventory playerInventory;
    
    void Start()
    {
        // ScriptableObject zaten persistence sağlıyor
        // Sadece ilk sahne yüklemesinde sıfırla
        if (playerInventory != null && !playerInventory.name.Contains("Persistent"))
        {
            // Eğer bu "persistent" olmayan bir inventory ise, sıfırla
            playerInventory.ResetInventory();
            Debug.Log("Reset inventory for new session");
        }
    }

    // Artık OnTriggerEnter kullanılmıyor - Plant ve WorldItem kendi pickup sistemlerini kullanıyor
}