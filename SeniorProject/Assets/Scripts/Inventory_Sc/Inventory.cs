using UnityEngine;

public class Inventory : MonoBehaviour
{
    public SCInventory playerInventory;

    void Start()
    {
        // Oyunun başında envanteri sıfırla
        playerInventory.ResetInventory();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Plant"))
        {
            Plant plant = other.gameObject.GetComponent<Plant>();
            if (plant != null)
            {
                if (playerInventory.AddItem(plant.item))
                {
                    Destroy(other.gameObject); // Sadece eşya eklenirse nesneyi yok et
                }
                // Eğer eşya eklenemezse, nesne yok edilmez ve sahnede kalır
            }
        }
    }
}