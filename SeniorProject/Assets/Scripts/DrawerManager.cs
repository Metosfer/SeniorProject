using UnityEngine;

public class DrawerManager : MonoBehaviour
{
    public GameObject closedDrawer; // Kapalı çekmece objesi
    public GameObject openDrawer;   // Açık çekmece objesi
    private bool isOpen = false;    // Çekmecenin açık/kapalı durumu

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) // Sol tıklama
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit)) // Tıklama bir collider’a çarptı mı?
            {
                if (hit.collider != null && hit.collider.CompareTag("Drawer")) // Tag kontrolü
                {
                    isOpen = !isOpen;              // Durumu tersine çevir
                    closedDrawer.SetActive(!isOpen); // Kapalıyı gizle/göster
                    openDrawer.SetActive(isOpen);    // Açığı gizle/göster
                }
            }
        }
    }
}