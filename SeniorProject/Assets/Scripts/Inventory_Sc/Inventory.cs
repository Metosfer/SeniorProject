using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    public SCInventory playerInventory;

    void OnTriggerEnter(Collider other) // Corrected from OggerEnter
    {
        if (other.gameObject.CompareTag("Plant"))
        {
            Plant plant = other.gameObject.GetComponent<Plant>(); // Assuming Plant component exists
            if (plant != null)
            {
                if (playerInventory.AddItem(plant.item)) // Corrected from GetComponent<SCItem>().item
                {
                    Destroy(other.gameObject); // Corrected from Destroy(gameObject)
                }
            }
        }
    }
}