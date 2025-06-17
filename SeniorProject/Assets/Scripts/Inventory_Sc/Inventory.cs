using UnityEngine;

public class Inventory : MonoBehaviour
{
    public SCInventory playerInventory;

    void Start()
    {
        // Oyunun başında envanteri sıfırla
        playerInventory.ResetInventory();
    }

    // Artık OnTriggerEnter kullanılmıyor - Plant ve WorldItem kendi pickup sistemlerini kullanıyor
}